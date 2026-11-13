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
    /// Keeps the active persona's character sheet (JUH-118, docs/PISARA.md). When a persona is
    /// selected or its prompt changes it loads the cached sheet; when there is none for this
    /// prompt it asks the model once, in the background, and saves the result encrypted next
    /// to her mood. Until the reading is back she runs on a neutral sheet.
    /// <para>
    /// The model is the one this persona's chat uses, picked by the same privacy rule
    /// (<see cref="PrivateChatRouting"/> with <see cref="IntimateExpressions.KeepsLocal"/>), so
    /// a private persona is never sent to a cloud model. The request does not go through the
    /// memory layer: remembered facts about the user are not part of a reading.
    /// </para>
    /// One call per event (persona created, prompt changed, Re-read), never per frame. Added
    /// by <see cref="SettingsPanel"/>.
    /// </summary>
    public sealed class PisaraReadingKeeper : MonoBehaviour
    {
        SettingsPanel _settings;
        ChatPanel _chat;
        ChatProviderRegistry _registry;
        ChatSettings _registryFor;

        string _personaId;
        string _prompt;
        bool _seen;
        CharacterSheet _sheet;
        bool _reading;
        string _failure = string.Empty;
        int _generation;
        CancellationTokenSource _cancel;
        readonly HashSet<string> _tried = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Goes up whenever the sheet or the reading state changes, so settings views can refresh.</summary>
        public int Revision { get; private set; }

        /// <summary>True while a model call is running.</summary>
        public bool IsReading
        {
            get { return _reading; }
        }

        /// <summary>The active persona's sheet. Null with the setup name (no persona), or before the first look.</summary>
        public CharacterSheet Sheet
        {
            get { return _sheet; }
        }

        /// <summary>The sheet for a persona if it is the active one (looked up now), else null.</summary>
        public CharacterSheet SheetFor(string personaId)
        {
            Check();
            return string.Equals(personaId ?? string.Empty, _personaId ?? string.Empty, StringComparison.Ordinal) ? _sheet : null;
        }

        /// <summary>The line under the reading in persona settings.</summary>
        public string StatusLine
        {
            get
            {
                if (_reading)
                    return "Reading her description with the model...";
                var line = PisaraReading.SourceLine(_sheet);
                if (_failure.Length == 0)
                    return line;
                return _sheet != null && _sheet.source == CharacterSheet.SourceModel
                    ? line + " The last Re-read did not work (" + _failure + ")."
                    : line + " (" + _failure + ")";
            }
        }

        void Update()
        {
            Check();
        }

        void OnDisable()
        {
            CancelReading();
        }

        /// <summary>
        /// Notices a persona switch or a changed prompt. Cheap while nothing changed: two string
        /// comparisons that hit the same references, and no allocation.
        /// </summary>
        void Check()
        {
            var settings = CurrentSettings();
            var chat = Chat();
            if (settings == null || chat == null)
                return;

            var persona = settings.PersonaId ?? string.Empty;
            var prompt = chat.systemPrompt ?? string.Empty;
            if (_seen && string.Equals(persona, _personaId, StringComparison.Ordinal) && string.Equals(prompt, _prompt, StringComparison.Ordinal))
                return;

            _seen = true;
            _personaId = persona;
            _prompt = prompt;
            Begin(false);
        }

        /// <summary>The "Re-read" button: asks the model again for the active persona.</summary>
        public void Reread()
        {
            Check();
            if (string.IsNullOrEmpty(_personaId))
                return;
            Begin(true);
        }

        void Begin(bool force)
        {
            CancelReading();
            _failure = string.Empty;
            if (string.IsNullOrEmpty(_personaId))
            {
                // The setup name is not a persona: no reading, and the temperament stays balanced.
                SetSheet(null);
                return;
            }

            var path = CharacterSheetStore.FileFor(Folder(), _personaId);
            var cached = force ? null : CharacterSheetStore.LoadFor(path, _prompt);
            var key = _personaId + "\n" + CharacterSheet.HashPrompt(_prompt);
            if (cached != null && (cached.source == CharacterSheet.SourceModel || _tried.Contains(key)))
            {
                SetSheet(cached);
                return;
            }

            // A fallback sheet from an earlier run is used meanwhile, and the model is tried once more.
            SetSheet(cached ?? CharacterSheet.Neutral(_prompt));
            _tried.Add(key);
            Read(_personaId, _prompt, path);
        }

        async void Read(string persona, string prompt, string path)
        {
            var generation = ++_generation;
            var cancel = new CancellationTokenSource();
            _cancel = cancel;
            _reading = true;
            Revision++;

            PisaraReadResult result;
            try
            {
                var privacy = GetComponent<IPrivacyMode>();
                var keepsLocal = IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
                result = await PisaraReader.ReadAsync(
                    Provider(keepsLocal),
                    PersonaName(persona),
                    prompt,
                    keepsLocal,
                    IntimateExpressions.Active,
                    PisaraKeywords.Temperament,
                    cancel.Token);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Pisara reading failed (" + e.GetType().Name + ")");
                result = new PisaraReadResult
                {
                    Sheet = PisaraReading.Fallback(prompt, null, DateTime.UtcNow),
                    Failure = "the call failed",
                };
            }

            if (this == null || generation != _generation || cancel.IsCancellationRequested)
                return;

            _reading = false;
            _cancel = null;
            cancel.Dispose();
            _failure = result.Failure ?? string.Empty;
            // A failed Re-read keeps the sheet the model wrote earlier for this prompt.
            var kept = PisaraReading.Keep(CharacterSheetStore.LoadFor(path, prompt), result.Sheet);
            if (ReferenceEquals(kept, result.Sheet) && !CharacterSheetStore.Save(path, kept))
                Debug.LogWarning("Pisara reading: the sheet could not be saved.");
            Debug.Log(PisaraReading.LogLine(persona, kept)
                + (_failure.Length == 0 ? string.Empty : " (" + _failure + (ReferenceEquals(kept, result.Sheet) ? ")" : "; kept the earlier reading)")));
            SetSheet(kept);
        }

        void SetSheet(CharacterSheet sheet)
        {
            _sheet = sheet;
            Revision++;
            var mood = GetComponent<CompanionMoodKeeper>();
            if (mood != null)
                mood.ReloadTemperament();
        }

        void CancelReading()
        {
            _generation++;
            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
                _cancel = null;
            }

            if (_reading)
            {
                _reading = false;
                Revision++;
            }
        }

        /// <summary>
        /// The provider this persona's chat would use now, without the memory layer. Null when
        /// it is unknown; the reader then falls back.
        /// </summary>
        IChatProvider Provider(bool keepsLocal)
        {
            var chat = Chat();
            if (chat == null)
                return null;
            if (_registry == null || !ReferenceEquals(_registryFor, chat))
            {
                _registry = ChatProviderFactory.Create(chat, new EnvironmentApiKeySource(), new UnityChatTransport(this), null);
                _registryFor = chat;
            }

            var id = PrivateChatRouting.ProviderId(keepsLocal, chat.providerId, chat.privateEndpointType);
            IChatProvider provider;
            return _registry.TryGet(id, out provider) ? provider : null;
        }

        string PersonaName(string personaId)
        {
            var settings = CurrentSettings();
            var persona = PersonaCatalog.LoadDefault(Folder()).Find(personaId);
            if (persona != null && !string.IsNullOrWhiteSpace(persona.DisplayName))
                return persona.DisplayName;
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
            return PersonaRuntime.DefaultSettingsFolder();
        }
    }
}
