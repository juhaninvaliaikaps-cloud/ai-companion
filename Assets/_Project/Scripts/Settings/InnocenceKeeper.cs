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
    /// JUH-116: keeps the active persona's place between Innocent and Perverted (adult mode
    /// only). While the 18+ gate is open it reads her start from the persona on the local
    /// model, moves her toward experienced after each intimate moment, lets her drift back
    /// over time, nudges her mood, and publishes the prompt line through
    /// <see cref="InnocenceNow"/>. While the gate is closed it does nothing and the line is empty.
    /// The numbers live in <see cref="InnocenceMath"/>; this component only wires them.
    /// </summary>
    public sealed class InnocenceKeeper : MonoBehaviour, ICompanionStartOver
    {
        const float CheckSeconds = 0.25f;
        const float SaveSeconds = 2f;

        SettingsPanel _settings;
        ChatPanel _chat;
        ChatProviderRegistry _registry;
        ChatSettings _registryFor;

        string _personaId;
        string _path;
        InnocenceState _state = new InnocenceState();
        bool _loaded;
        bool _dirty;
        float _checkIn;
        float _saveIn = SaveSeconds;
        bool _wasUndressed;

        bool _reading;
        bool _forceRead;
        int _generation;
        CancellationTokenSource _cancel;
        // JUH-176: once per launch, unless the model was not answering yet: then again later.
        readonly ReadRetry _retry = new ReadRetry();
        string _failure = string.Empty;

        int _publishedBand = -1;
        bool _publishedUndressed;
        bool _publishedOn;

        /// <summary>Goes up when anything the settings view shows changed.</summary>
        public int Revision { get; private set; }

        public bool IsLoaded
        {
            get { return _loaded; }
        }

        public InnocenceState State
        {
            get { return _state; }
        }

        public bool Reading
        {
            get { return _reading; }
        }

        /// <summary>The line under the slider: the reading and its reason, a failure, or the user's setting.</summary>
        public string ReadingLine()
        {
            if (_reading)
                return "Reading her description with the local model...";
            var line = _state.HasReading ? InnocenceReader.Describe(_state.ReadValue, _state.ReadReason) : "Not read from her description yet.";
            if (_failure.Length > 0)
                line += " Last try: " + _failure + ".";
            return line;
        }

        void OnEnable()
        {
            RelationshipNow.ReplyFinished += OnReply;
        }

        void OnDisable()
        {
            RelationshipNow.ReplyFinished -= OnReply;
            CancelReading();
            Save(true);
            InnocenceNow.Reset();
        }

        void OnApplicationQuit()
        {
            Save(true);
        }

        void Update()
        {
            _saveIn -= Time.unscaledDeltaTime;
            if (_dirty && _saveIn <= 0f)
                Save(true);

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

            var gate = IntimateExpressions.Active;
            var undressed = gate && IntimateExpressions.Undressed;
            if (undressed && !_wasUndressed)
                PushMoods(InnocenceMath.UndressedPushes(_state.Current), "being undressed");
            _wasUndressed = undressed;

            if (gate)
            {
                ApplyDrift(DateTime.UtcNow);
                MaybeRead();
            }

            Publish(false);
        }

        /// <summary>One finished reply. An intimate moment moves her toward experienced and nudges her mood.</summary>
        void OnReply(AnimationCue cue)
        {
            if (!_loaded || string.IsNullOrEmpty(_personaId) || !InnocenceMath.IsMoment(cue, IntimateExpressions.Active))
                return;
            var now = DateTime.UtcNow;
            ApplyDrift(now);
            var boost = KinkBoost(cue);
            var after = InnocenceMath.AfterMoment(_state.Current, cue.Intensity, boost);
            _state.Current = RelationshipCeiling.ClampInnocence(_state.Current, after, InnocenceCeiling());
            _state.AtLastMoment = _state.Current;
            _state.LastMomentTicks = now.Ticks;
            _dirty = true;
            PushMoods(InnocenceMath.MomentPushes(_state.Current), "an intimate moment");
            Debug.Log("Innocence: " + PersonaLogId() + " = " + _state.Current.ToString("0.0")
                + " after an intimate moment" + (boost > 1f ? " (favourite x" + boost.ToString("0.0") + ")" : string.Empty));
            Revision++;
            Publish(false);
        }

        void ApplyDrift(DateTime nowUtc)
        {
            if (_state.LastMomentTicks <= 0L)
                return;
            var drifted = InnocenceMath.DriftBack(_state.AtLastMoment, _state.Baseline, _state.DaysSinceMoment(nowUtc));
            if (Math.Abs(drifted - _state.Current) < 0.01f)
                return;
            _state.Current = RelationshipCeiling.ClampInnocence(_state.Current, drifted, InnocenceCeiling());
            _dirty = true;
        }

        /// <summary>JUH-190: kinks and sensitive spots raise this from 70 toward 100.</summary>
        public float InnocenceCeiling()
        {
            var secrets = GetComponent<IntimateSecretsKeeper>();
            return secrets == null
                ? RelationshipCeiling.InnocenceTop
                : RelationshipCeiling.Innocence(secrets.KinkSensitiveFound, secrets.KinkSensitiveTotal);
        }

        /// <summary>
        /// Acting out one of her kinks moves her much faster; a sensitive spot is a smaller
        /// boost. Only a 18+ secret named in the cue counts, so an ordinary intimate moment is
        /// still the baseline.
        /// </summary>
        float KinkBoost(AnimationCue cue)
        {
            if (string.IsNullOrEmpty(cue.Secret))
                return 1f;
            var secrets = GetComponent<IntimateSecretsKeeper>();
            IntimateSecretItem item;
            if (secrets == null || secrets.Check(cue.Secret, out item) == SecretHitKind.None)
                return 1f;
            if (item.Group == IntimateSecretGroup.Kink)
                return InnocenceMath.KinkMomentBoost;
            if (item.Group == IntimateSecretGroup.Sensitive)
                return InnocenceMath.SensitiveMomentBoost;
            return 1f;
        }

        void PushMoods(CompanionMoodPush[] pushes, string cause)
        {
            var mood = GetComponent<CompanionMoodKeeper>();
            if (mood != null)
                mood.TryApplyPushes(_personaId, pushes, cause);
        }

        // ---- The slider -----------------------------------------------------------------

        /// <summary>The user moves the slider: she is set there, it becomes her baseline, and a new reading no longer changes it.</summary>
        public void SetByUser(float value)
        {
            if (!_loaded)
                return;
            _state.UserSet = true;
            value = Math.Min(value, InnocenceCeiling());
            _state.SetAt(value, value);
            _dirty = true;
            _saveIn = Math.Min(_saveIn, SaveSeconds);
            Revision++;
            Publish(true);
        }

        /// <summary>Back to what the reading said (or the middle when there is no reading).</summary>
        public void UseReading()
        {
            if (!_loaded)
                return;
            _state.UserSet = false;
            var value = _state.HasReading ? _state.ReadValue : InnocenceMath.Default;
            value = Math.Min(value, InnocenceCeiling());
            _state.SetAt(value, value);
            _dirty = true;
            Save(true);
            Debug.Log("Innocence: " + PersonaLogId() + " back to the reading (" + _state.Readout() + ")");
            Revision++;
            Publish(true);
        }

        /// <summary>The "Read again" button. Only while the gate is open.</summary>
        public void ReadAgain()
        {
            if (!_loaded || !IntimateExpressions.Active)
                return;
            _forceRead = true;
            MaybeRead();
        }

        // ---- Reading ---------------------------------------------------------------------

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
            // The hash and the key change only with the prompt, the notes or the persona, so idle checks do not allocate.
            if (!ReferenceEquals(prompt, _hashedPrompt) || !ReferenceEquals(notes, _hashedNotes) || !string.Equals(_hashedFor, _personaId, StringComparison.Ordinal))
            {
                _hashedPrompt = prompt;
                _hashedNotes = notes;
                _hashedFor = _personaId;
                _readInput = AdultNotesNow.ForReading(prompt);
                _hash = CharacterSheet.HashPrompt(_readInput);
                _key = _personaId + "\n" + _hash;
            }

            var hash = _hash;
            var key = _key;
            // JUH-170: a reading from before libido was read is asked once more, for libido only.
            var current = string.Equals(hash, _state.ReadHash, StringComparison.Ordinal) && _state.ReadLibido >= 0f;
            if (!_forceRead && (current || !_retry.Ready(key, Time.unscaledTime)))
                return;
            var full = _forceRead || !string.Equals(hash, _state.ReadHash, StringComparison.Ordinal);
            _forceRead = false;
            _retry.Started(key);
            Read(_personaId, _readInput, hash, full);
        }

        /// <summary><paramref name="full"/> is false when only her libido is missing from a current reading (JUH-170).</summary>
        async void Read(string persona, string prompt, string hash, bool full)
        {
            var generation = ++_generation;
            var cancel = new CancellationTokenSource();
            _cancel = cancel;
            _reading = true;
            _failure = string.Empty;
            Revision++;

            InnocenceReadResult result;
            try
            {
                var chat = Chat();
                result = await InnocenceReader.ReadAsync(LocalProvider(chat), PersonaName(persona), prompt, IntimateExpressions.Active, cancel.Token);
            }
            catch (Exception e)
            {
                result = new InnocenceReadResult { Ok = false, Failure = "the call failed (" + e.GetType().Name + ")" };
            }

            if (this == null || generation != _generation || cancel.IsCancellationRequested)
            {
                cancel.Dispose();
                // JUH-176: a cancelled reading (a persona switch, Read again) may run again later.
                if (this != null)
                    _retry.Failed(persona + "\n" + hash, Time.unscaledTime, true);
                return;
            }

            _reading = false;
            _cancel = null;
            cancel.Dispose();
            Revision++;

            if (!result.Ok || !string.Equals(persona, _personaId, StringComparison.Ordinal))
            {
                _failure = result.Ok ? string.Empty : result.Failure ?? string.Empty;
                if (result.Ok)
                    _retry.Failed(persona + "\n" + hash, Time.unscaledTime, true);
                else
                {
                    var passing = ReadRetry.IsPassing(_failure);
                    _retry.Failed(persona + "\n" + hash, Time.unscaledTime, passing);
                    Debug.Log("Innocence: reading for " + PersonaLogId() + " gave nothing (" + _failure + ")" + (passing ? "; trying again shortly" : string.Empty));
                }

                return;
            }

            _retry.Succeeded(persona + "\n" + hash);

            var hadReading = _state.HasReading;
            if (hadReading && !full && string.Equals(hash, _state.ReadHash, StringComparison.Ordinal))
            {
                // JUH-170: the same description, read again only for her libido. Her innocence stays as it was.
                _state.ReadLibido = result.Libido;
                _dirty = true;
                Save(true);
                Debug.Log("Innocence: " + PersonaLogId() + " libido read as "
                    + (result.Libido >= 0f ? result.Libido.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "nothing"));
                Publish(true);
                return;
            }

            _state.ReadHash = hash;
            _state.ReadLibido = result.Libido;
            _state.ReadValue = result.Value;
            _state.ReadReason = result.Reason ?? string.Empty;
            if (!_state.UserSet)
            {
                if (!hadReading && _state.LastMomentTicks <= 0L)
                {
                    var readValue = Math.Min(result.Value, InnocenceCeiling());
                    _state.SetAt(readValue, readValue);
                }
                else
                {
                    var oldBaseline = _state.Baseline;
                    _state.Current = RelationshipCeiling.ClampInnocence(_state.Current,
                        InnocenceMath.Rebase(_state.Current, oldBaseline, result.Value), InnocenceCeiling());
                    _state.AtLastMoment = RelationshipCeiling.ClampInnocence(_state.AtLastMoment,
                        InnocenceMath.Rebase(_state.AtLastMoment, oldBaseline, result.Value), InnocenceCeiling());
                    _state.Baseline = result.Value;
                }
            }

            _dirty = true;
            Save(true);
            // JUH-184: the number and band only. The reason is 18+ reading text and stays out of Player.log.
            Debug.Log(InnocenceLog.ReadLine(PersonaLogId(), result.Value, _state.UserSet));
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

        /// <summary>The local model or the private server this chat would use, without the memory layer.</summary>
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

        // ---- Prompt line -----------------------------------------------------------------

        /// <summary>Rebuilt only when the band, the naked state or the gate changed, so it does not allocate while idle.</summary>
        void Publish(bool force)
        {
            var on = _loaded && IntimateExpressions.Active && !string.IsNullOrEmpty(_personaId);
            var band = InnocenceMath.Band(_state.Current);
            var undressed = IntimateExpressions.Undressed;
            if (!force && on == _publishedOn && band == _publishedBand && undressed == _publishedUndressed)
                return;
            _publishedOn = on;
            _publishedBand = band;
            _publishedUndressed = undressed;
            InnocenceNow.SetPromptLine(InnocencePrompt.Line(_state.Current, undressed, on));
        }

        // ---- Persona, Start over, wipe ---------------------------------------------------

        void Load(string persona)
        {
            if (_loaded)
                Save(true);
            CancelReading();
            _personaId = persona;
            _path = InnocenceStore.FileFor(Folder(), persona);
            _state = string.IsNullOrEmpty(_path) ? new InnocenceState() : InnocenceStore.Load(_path);
            _loaded = true;
            _dirty = false;
            _failure = string.Empty;
            _wasUndressed = IntimateExpressions.Active && IntimateExpressions.Undressed;
            Revision++;
            Publish(true);
        }

        /// <summary>"Start over" (JUH-117): she goes back to her baseline; the reading and the user's setting stay.</summary>
        public void StartOver()
        {
            if (!_loaded)
                return;
            _state.SetAt(Math.Min(_state.Baseline, InnocenceCeiling()), _state.Baseline);
            _dirty = true;
            Save(true);
            Debug.Log("Innocence: start over for " + PersonaLogId() + " (" + _state.Readout() + ")");
            Revision++;
            Publish(true);
        }

        /// <summary>After a full wipe: the files are gone, so forget the state without saving it back.</summary>
        public void Forget()
        {
            CancelReading();
            _state = new InnocenceState();
            _dirty = false;
            _loaded = false;
            _retry.Clear();
            _failure = string.Empty;
            Revision++;
            InnocenceNow.Reset();
        }

        void Save(bool now)
        {
            if (!_loaded || !_dirty || string.IsNullOrEmpty(_path))
                return;
            if (!now && _saveIn > 0f)
                return;
            _saveIn = SaveSeconds;
            if (InnocenceStore.Save(_path, _state))
                _dirty = false;
        }

        // ---- Helpers ---------------------------------------------------------------------

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
