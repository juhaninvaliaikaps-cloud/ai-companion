using System;
using System.Collections.Generic;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-125: keeps the active persona's 18+ secrets (kinks, sensitive areas, shy to do).
    /// While the 18+ gate is open it reads them from her description on the local model once
    /// per description, publishes the prompt line through <see cref="IntimateSecretsNow"/>
    /// (the shy part follows her innocence), and holds which were found. While the gate is
    /// closed it does nothing and the line is empty. <see cref="RelationshipKeeper"/> asks it
    /// about a cue's <c>secret=</c> with <see cref="Check"/> and <see cref="Reveal"/>.
    /// </summary>
    public sealed class IntimateSecretsKeeper : MonoBehaviour, ICompanionStartOver
    {
        const float CheckSeconds = 0.5f;

        SettingsPanel _settings;
        ChatPanel _chat;
        ChatProviderRegistry _registry;
        ChatSettings _registryFor;

        string _personaId;
        string _path;
        IntimateSecretsState _state = new IntimateSecretsState();
        bool _loaded;
        float _checkIn;

        bool _reading;
        bool _forceRead;
        int _generation;
        CancellationTokenSource _cancel;
        readonly HashSet<string> _triedThisLaunch = new HashSet<string>(StringComparer.Ordinal);
        string _failure = string.Empty;

        bool _publishedOn;
        int _publishedBand = -1;
        int _publishedRevision = -1;

        /// <summary>Goes up when the reading or the finds changed.</summary>
        public int Revision { get; private set; }

        public bool IsLoaded
        {
            get { return _loaded; }
        }

        public bool Reading
        {
            get { return _reading; }
        }

        /// <summary>For the Profile panel: a reading in progress, the last failure, or nothing read yet. Empty once read.</summary>
        public string StatusLine()
        {
            if (_reading)
                return "Reading her 18+ secrets with the local model...";
            if (_state.HasReading)
                return string.Empty;
            return _failure.Length > 0 ? "Not read yet. Last try: " + _failure + "." : "Not read from her description yet.";
        }

        /// <summary>Profile → Personality → Her 18+ secrets. Null while the gate is closed.</summary>
        public ProfileIntimateSecrets View(float innocence)
        {
            if (!_loaded || !IntimateExpressions.Active)
                return null;
            return ProfileIntimateSecrets.Build(_state, innocence, StatusLine());
        }

        // ---- Finding them -------------------------------------------------------------------

        /// <summary>What a cue's 18+ secret id does. Always None while the gate is closed.</summary>
        public SecretHitKind Check(string id, out IntimateSecretItem item)
        {
            item = default(IntimateSecretItem);
            if (!_loaded || !IntimateExpressions.Active)
                return SecretHitKind.None;
            return _state.Check(id, out item);
        }

        /// <summary>Marks one found and saves at once. True when it was not found before.</summary>
        public bool Reveal(IntimateSecretItem item)
        {
            if (!_loaded || !IntimateExpressions.Active || !_state.Found.Reveal(item.Key))
                return false;
            Save();
            Revision++;
            return true;
        }

        public int FoundCount
        {
            get { return _state.Found.FoundCount; }
        }

        /// <summary>The "Read again" button. Only while the gate is open.</summary>
        public void ReadAgain()
        {
            if (!_loaded || !IntimateExpressions.Active)
                return;
            _forceRead = true;
            MaybeRead();
        }

        // ---- Unity ------------------------------------------------------------------------

        void OnDisable()
        {
            CancelReading();
            IntimateSecretsNow.Reset();
        }

        void Update()
        {
            _checkIn -= Time.unscaledDeltaTime;
            if (_checkIn > 0f)
                return;
            _checkIn = CheckSeconds;

            var settings = CurrentSettings();
            if (settings == null)
                return;
            var persona = settings.PersonaId ?? string.Empty;
            if (!_loaded || !string.Equals(persona, _personaId, StringComparison.Ordinal))
                Load(persona);

            if (IntimateExpressions.Active)
                MaybeRead();
            Publish(false);
        }

        // ---- Reading ----------------------------------------------------------------------

        string _hashedPrompt;
        string _hashedNotes;
        string _hashedFor;
        string _readInput = string.Empty;
        string _hash = string.Empty;
        string _key = string.Empty;

        void MaybeRead()
        {
            if (_reading || string.IsNullOrEmpty(_personaId))
                return;
            var chat = Chat();
            if (chat == null)
                return;
            var prompt = chat.systemPrompt ?? string.Empty;
            if (prompt.Trim().Length == 0)
                return;
            // JUH-157: her 18+ notes are read too, so what the user wrote comes first.
            var notes = AdultNotesNow.Notes;
            // The hash changes only with the prompt, the notes or the persona, so idle checks do not allocate.
            if (!ReferenceEquals(prompt, _hashedPrompt) || !ReferenceEquals(notes, _hashedNotes) || !string.Equals(_hashedFor, _personaId, StringComparison.Ordinal))
            {
                _hashedPrompt = prompt;
                _hashedNotes = notes;
                _hashedFor = _personaId;
                _readInput = AdultNotesNow.ForReading(prompt);
                _hash = CharacterSheet.HashPrompt(_readInput);
                _key = _personaId + "\n" + _hash;
            }

            if (!_forceRead && (string.Equals(_hash, _state.ReadHash, StringComparison.Ordinal) || _triedThisLaunch.Contains(_key)))
                return;
            _forceRead = false;
            _triedThisLaunch.Add(_key);
            Read(_personaId, _readInput, _hash);
        }

        async void Read(string persona, string prompt, string hash)
        {
            var generation = ++_generation;
            var cancel = new CancellationTokenSource();
            _cancel = cancel;
            _reading = true;
            _failure = string.Empty;
            Revision++;

            int kinks;
            int sensitive;
            int shy;
            IntimateSecretCounts.For(persona, hash, out kinks, out sensitive, out shy);

            IntimateSecretsReadResult result;
            try
            {
                result = await IntimateSecretsReader.ReadAsync(LocalProvider(Chat()), PersonaName(persona), prompt,
                    kinks, sensitive, shy, IntimateExpressions.Active, cancel.Token);
            }
            catch (Exception e)
            {
                result = new IntimateSecretsReadResult { Ok = false, Failure = "the call failed (" + e.GetType().Name + ")" };
            }

            if (this == null || generation != _generation || cancel.IsCancellationRequested)
            {
                cancel.Dispose();
                return;
            }

            _reading = false;
            _cancel = null;
            cancel.Dispose();
            Revision++;

            if (!result.Ok || !string.Equals(persona, _personaId, StringComparison.Ordinal))
            {
                _failure = result.Ok ? string.Empty : result.Failure ?? string.Empty;
                if (!result.Ok)
                    Debug.Log("Intimate secrets: reading for " + PersonaLogId() + " gave nothing (" + _failure + ")");
                return;
            }

            _state.SetReading(hash, result.Kinks, result.Sensitive, result.Shy, kinks, sensitive, shy);
            Save();
            // Counts only: the secrets themselves stay out of the log.
            Debug.Log("Intimate secrets: " + PersonaLogId() + " read on the local model ("
                + _state.Kinks.Count + " kinks, " + _state.Sensitive.Count + " sensitive areas, " + _state.Shy.Count + " shy to do)");
            Publish(true);
        }

        void CancelReading()
        {
            _generation++;
            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel = null;
            }

            _reading = false;
        }

        /// <summary>The local model or the private server this chat would use, without the memory layer. Never a cloud model.</summary>
        IChatProvider LocalProvider(ChatSettings chat)
        {
            if (chat == null)
                return null;
            if (_registry == null || !ReferenceEquals(_registryFor, chat))
            {
                _registry = ChatProviderFactory.Create(chat, new EnvironmentApiKeySource(), new UnityChatTransport(this), null);
                _registryFor = chat;
            }

            var id = PrivateChatRouting.ProviderId(true, chat.providerId, chat.privateEndpointType);
            IChatProvider provider;
            return _registry.TryGet(id, out provider) ? provider : null;
        }

        // ---- Prompt line ------------------------------------------------------------------

        /// <summary>Rebuilt only when the gate, her innocence band or the secrets changed, so it does not allocate while idle.</summary>
        void Publish(bool force)
        {
            var on = _loaded && IntimateExpressions.Active && !string.IsNullOrEmpty(_personaId) && _state.HasReading;
            var innocence = Innocence();
            var band = InnocenceMath.Band(innocence);
            if (!force && on == _publishedOn && band == _publishedBand && Revision == _publishedRevision)
                return;
            _publishedOn = on;
            _publishedBand = band;
            _publishedRevision = Revision;
            IntimateSecretsNow.SetPromptLine(on ? IntimateSecretsPrompt.Line(_state.Items(), innocence, true) : string.Empty);
        }

        float Innocence()
        {
            var keeper = GetComponent<InnocenceKeeper>();
            return keeper != null && keeper.IsLoaded ? keeper.State.Current : InnocenceMath.Default;
        }

        // ---- Persona, Start over, wipe ----------------------------------------------------

        void Load(string persona)
        {
            CancelReading();
            _personaId = persona;
            _path = IntimateSecretsStore.FileFor(Folder(), persona);
            _state = IntimateSecretsStore.Load(_path);
            _loaded = true;
            _failure = string.Empty;
            Revision++;
            Publish(true);
        }

        /// <summary>"Start over" (JUH-117): her 18+ secrets are hidden again; the reading stays.</summary>
        public void StartOver()
        {
            if (!_loaded)
                return;
            _state.Found.Clear();
            Save();
            Debug.Log("Intimate secrets: start over for " + PersonaLogId());
            Revision++;
            Publish(true);
        }

        /// <summary>After a full wipe: the files are gone, so forget the state without saving it back.</summary>
        public void Forget()
        {
            CancelReading();
            _state = new IntimateSecretsState();
            _loaded = false;
            _triedThisLaunch.Clear();
            _failure = string.Empty;
            Revision++;
            IntimateSecretsNow.Reset();
        }

        void Save()
        {
            if (!_loaded || string.IsNullOrEmpty(_path))
                return;
            IntimateSecretsStore.Save(_path, _state);
        }

        // ---- Helpers ----------------------------------------------------------------------

        string PersonaLogId()
        {
            var id = CompanionMoodStore.PersonaFileId(_personaId);
            return id.Length == 0 ? "(default)" : id;
        }

        string PersonaName(string personaId)
        {
            var persona = PersonaCatalog.LoadDefault(PersonaRuntime.DefaultSettingsFolder()).Find(personaId);
            if (persona != null && !string.IsNullOrWhiteSpace(persona.DisplayName))
                return persona.DisplayName;
            var settings = CurrentSettings();
            return settings != null ? settings.CompanionName : string.Empty;
        }

        CompanionSettings CurrentSettings()
        {
            if (_settings == null)
                _settings = GetComponent<SettingsPanel>();
            return _settings != null ? _settings.CurrentSettings : null;
        }

        ChatSettings Chat()
        {
            if (_chat == null)
                _chat = GetComponent<ChatPanel>();
            return _chat != null ? _chat.RuntimeChat : null;
        }

        static string Folder()
        {
            return SettingsLocations.Folder(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.persistentDataPath);
        }
    }
}
