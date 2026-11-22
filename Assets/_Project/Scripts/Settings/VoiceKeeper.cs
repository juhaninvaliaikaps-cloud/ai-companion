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
    /// JUH-171: the last Pisara reading, how she talks. It waits until her description reading
    /// is done (and, while the 18+ gate is open, her innocence reading), then starts the
    /// everyday reading and the 18+ one together, each on its own model
    /// (<see cref="VoiceReader"/>). Each is saved with the hash of what it was given, so it reads
    /// again only when her description, sliders, mood temperament, starting relationship or (18+)
    /// innocence band change. The block for chat goes out through <see cref="VoiceNow"/>, rebuilt
    /// when her mood, closeness or the gate change. The user may overwrite any field; their text
    /// wins (<see cref="VoiceFile"/>). Logs say what was read and by which model, never the text.
    /// </summary>
    public sealed class VoiceKeeper : MonoBehaviour, ICompanionStartOver
    {
        const float CheckSeconds = 2f;

        /// <summary>A changed input waits this long unchanged before it is read (slider drags, typing).</summary>
        public const float SettleSeconds = 4f;

        SettingsPanel _settings;
        ChatPanel _chat;
        string _personaId;
        string _path;
        VoiceFile _file = new VoiceFile();
        bool _loaded;
        float _checkIn;
        readonly HashSet<string> _tried = new HashSet<string>(StringComparer.Ordinal);
        string _everydayPending = string.Empty;
        float _everydaySince;
        string _intimatePending = string.Empty;
        float _intimateSince;
        CancellationTokenSource _everydayCancel;
        CancellationTokenSource _intimateCancel;
        int _generation;
        int _publishedKey = int.MinValue;
        int _contentRevision;
        int _publishedRevision = -1;
        int _mergedRevision = -1;
        VoiceSheet _everyday = new VoiceSheet();
        IntimateVoice _intimate = new IntimateVoice();

        // What the inputs were built from last time: compared by reference, so an idle check
        // builds nothing (no catalog read, no strings) until something changed.
        int _sigSheet = -1;
        string _sigPrompt;
        string _sigNotes;
        string _sigManner;
        bool _sigGate;
        int _sigBand = -1;
        bool _sigInnocenceReading;

        public int Revision { get; private set; }
        public bool ReadingEveryday { get; private set; }
        public bool ReadingIntimate { get; private set; }
        public string EverydayStatus { get; private set; } = string.Empty;
        public string IntimateStatus { get; private set; } = string.Empty;

        public bool IsLoaded
        {
            get { return _loaded; }
        }

        /// <summary>The model's reading only (the editor shows the merged one).</summary>
        public VoiceFile File
        {
            get { return _file; }
        }

        void OnDisable()
        {
            CancelAll();
            VoiceNow.Reset();
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
            if (string.IsNullOrEmpty(_personaId))
            {
                Publish(false);
                return;
            }

            MaybeRead(false);
            Publish(false);
        }

        // ---- Reading ---------------------------------------------------------------------

        /// <summary>"Read again": both parts now, whatever the hashes say.</summary>
        public void ReadAgain()
        {
            if (!_loaded || string.IsNullOrEmpty(_personaId))
                return;
            _tried.Clear();
            MaybeRead(true);
        }

        void MaybeRead(bool force)
        {
            var pisara = GetComponent<PisaraReadingKeeper>();
            var chat = Chat();
            if (pisara == null || chat == null)
                return;
            // The last reading: wait for her description to be read.
            if (pisara.IsReading)
                return;
            var innocenceKeeper = GetComponent<InnocenceKeeper>();
            var gateNow = IntimateExpressions.Active;
            var bandNow = innocenceKeeper != null && innocenceKeeper.IsLoaded ? InnocenceMath.Band(innocenceKeeper.State.Current) : -1;
            var innocenceReading = innocenceKeeper != null && innocenceKeeper.Reading;
            var prompt = chat.systemPrompt ?? string.Empty;
            var same = pisara.Revision == _sigSheet && ReferenceEquals(prompt, _sigPrompt) && ReferenceEquals(AdultNotesNow.Notes, _sigNotes)
                && ReferenceEquals(PersonaMannerNow.PromptLine, _sigManner) && gateNow == _sigGate && bandNow == _sigBand
                && innocenceReading == _sigInnocenceReading;
            if (!force && same && _everydayPending.Length == 0 && _intimatePending.Length == 0)
                return;
            _sigSheet = pisara.Revision;
            _sigPrompt = prompt;
            _sigNotes = AdultNotesNow.Notes;
            _sigManner = PersonaMannerNow.PromptLine;
            _sigGate = gateNow;
            _sigBand = bandNow;
            _sigInnocenceReading = innocenceReading;

            var sheet = pisara.SheetFor(_personaId);
            if (sheet == null)
                return;

            var axes = PersonaRuntime.TemperamentChoiceFor(PersonaCatalog.LoadDefault(Folder()), _personaId, sheet).Axes;
            var name = pisara.NameFor(_personaId);

            // Everyday: gate-closed input, the same model as her description (local in private mode).
            var everydayInput = VoiceInput.Everyday(prompt, axes, sheet);
            var everydayHash = VoiceInput.Hash(everydayInput);
            if (!ReadingEveryday && Due(force, everydayHash, _file.everydayHash, ref _everydayPending, ref _everydaySince, "e"))
                ReadEveryday(name, everydayInput, everydayHash);

            // 18+: only while the gate is open, after her innocence reading, on the local model.
            if (!IntimateExpressions.Active)
                return;
            var innocence = GetComponent<InnocenceKeeper>();
            if (innocence == null || !innocence.IsLoaded || innocence.Reading)
                return;
            var intimateInput = VoiceInput.Intimate(AdultNotesNow.ForReading(prompt), axes, sheet, innocence.State.Current);
            // The band, not the exact value, decides: her slow drift re-reads only in a new band.
            var intimateHash = VoiceInput.Hash(VoiceInput.Intimate(AdultNotesNow.ForReading(prompt), axes, sheet,
                InnocenceMath.Band(innocence.State.Current) * 25f));
            if (!ReadingIntimate && Due(force, intimateHash, _file.intimateHash, ref _intimatePending, ref _intimateSince, "i"))
                ReadIntimate(name, intimateInput, intimateHash);
        }

        /// <summary>True when this part should be read now: forced, or new and unchanged for a few seconds, once per launch.</summary>
        bool Due(bool force, string hash, string savedHash, ref string pending, ref float since, string part)
        {
            if (force)
            {
                pending = string.Empty;
                _tried.Add(part + _personaId + hash);
                return true;
            }

            if (string.Equals(hash, savedHash, StringComparison.Ordinal))
            {
                pending = string.Empty;
                return false;
            }

            if (!string.Equals(hash, pending, StringComparison.Ordinal))
            {
                pending = hash;
                since = Time.unscaledTime;
                return false;
            }

            if (Time.unscaledTime - since < SettleSeconds)
                return false;
            var key = part + _personaId + hash;
            if (_tried.Contains(key))
                return false;
            _tried.Add(key);
            pending = string.Empty;
            return true;
        }

        async void ReadEveryday(string name, string input, string hash)
        {
            var pisara = GetComponent<PisaraReadingKeeper>();
            bool keepsLocal;
            string readBy;
            var provider = pisara.VoiceProvider(out keepsLocal, out readBy);
            var persona = _personaId;
            var generation = _generation;
            var cancel = new CancellationTokenSource();
            _everydayCancel = cancel;
            ReadingEveryday = true;
            EverydayStatus = "Reading how she talks...";
            Revision++;
            var result = await VoiceReader.ReadEverydayAsync(provider, keepsLocal, name, input, cancel.Token);
            if (this == null || generation != _generation || !string.Equals(persona, _personaId, StringComparison.Ordinal))
            {
                cancel.Dispose();
                return;
            }

            ReadingEveryday = false;
            _everydayCancel = null;
            cancel.Dispose();
            if (result.Ok)
            {
                _file.everyday = result.Everyday;
                _file.everydayHash = hash;
                _file.everydayBy = readBy;
                Save();
                EverydayStatus = "Read by " + readBy + ".";
                Debug.Log("Voice: " + LogId() + " everyday read by " + readBy + " (" + result.Everyday.habits.Length + " habits, "
                    + result.Everyday.samples.Length + " samples)");
            }
            else
            {
                EverydayStatus = "Not read: " + result.Failure + (_file.everyday.IsEmpty ? "." : "; the earlier reading stays.");
                Debug.LogWarning("Voice: " + LogId() + " everyday reading failed (" + result.Failure + ")");
            }

            _contentRevision++;
            Revision++;
            Publish(true);
        }

        async void ReadIntimate(string name, string input, string hash)
        {
            var pisara = GetComponent<PisaraReadingKeeper>();
            string readBy;
            var provider = pisara.LocalVoiceProvider(out readBy);
            var persona = _personaId;
            var generation = _generation;
            var cancel = new CancellationTokenSource();
            _intimateCancel = cancel;
            ReadingIntimate = true;
            IntimateStatus = "Reading how she talks in intimate scenes...";
            Revision++;
            var result = await VoiceReader.ReadIntimateAsync(provider, IntimateExpressions.Active, name, input, cancel.Token);
            if (this == null || generation != _generation || !string.Equals(persona, _personaId, StringComparison.Ordinal))
            {
                cancel.Dispose();
                return;
            }

            ReadingIntimate = false;
            _intimateCancel = null;
            cancel.Dispose();
            if (result.Ok)
            {
                _file.intimate = result.Intimate;
                _file.intimateHash = hash;
                _file.intimateBy = readBy;
                Save();
                IntimateStatus = "Read by " + readBy + ".";
                Debug.Log("Voice: " + LogId() + " intimate read on the local model (" + result.Intimate.habits.Length + " habits)");
            }
            else
            {
                IntimateStatus = "Not read: " + result.Failure + (_file.intimate.IsEmpty ? "." : "; the earlier reading stays.");
                Debug.LogWarning("Voice: " + LogId() + " intimate reading failed (" + result.Failure + ")");
            }

            _contentRevision++;
            Revision++;
            Publish(true);
        }

        // ---- The user's text -------------------------------------------------------------

        /// <summary>Changes the user's own everyday text; an empty field falls back to the reading.</summary>
        public void EditEveryday(Action<VoiceSheet> change)
        {
            if (!_loaded || change == null)
                return;
            change(_file.UserEveryday);
            _file.UserEveryday.Normalize();
            Save();
            _contentRevision++;
            Publish(true);
        }

        /// <summary>Changes the user's own 18+ text. Only while the gate is open.</summary>
        public void EditIntimate(Action<IntimateVoice> change)
        {
            if (!_loaded || change == null || !IntimateExpressions.Active)
                return;
            change(_file.UserIntimate);
            _file.UserIntimate.Normalize();
            Save();
            _contentRevision++;
            Publish(true);
        }

        /// <summary>Drops the user's text: every field shows the reading again.</summary>
        public void UseReading()
        {
            if (!_loaded)
                return;
            _file.userEveryday = new VoiceSheet();
            if (IntimateExpressions.Active)
                _file.userIntimate = new IntimateVoice();
            Save();
            _contentRevision++;
            Revision++;
            Publish(true);
        }

        // ---- Prompt block ------------------------------------------------------------------

        void Publish(bool force)
        {
            if (!_loaded || string.IsNullOrEmpty(_personaId))
            {
                if (VoiceNow.PromptBlock.Length > 0)
                    VoiceNow.Reset();
                return;
            }

            var gate = IntimateExpressions.Active;
            var innocence = Innocence();
            var familiarity = Familiarity();
            if (_mergedRevision != _contentRevision)
            {
                // Merged only when the reading or the user's text changed, never on an idle check.
                _everyday = _file.Everyday;
                _intimate = _file.Intimate;
                _mergedRevision = _contentRevision;
            }

            var everyday = _everyday;
            var key = VoicePrompt.Key(everyday, CompanionMoodNow.Shown, CompanionMoodNow.Strength, familiarity, gate, innocence);
            if (!force && key == _publishedKey && _contentRevision == _publishedRevision)
                return;
            _publishedKey = key;
            _publishedRevision = _contentRevision;
            var intimate = gate ? _intimate : null;
            var axes = Axes();
            var pisara = GetComponent<PisaraReadingKeeper>();
            var block = VoicePrompt.Block(pisara != null ? pisara.NameFor(_personaId) : string.Empty, everyday, intimate,
                CompanionMoodNow.Shown, CompanionMoodNow.Strength, familiarity, gate, innocence, axes);
            VoiceNow.Set(block, everyday.samples, intimate != null ? intimate.samples : null);
        }

        // ---- Persona, Start over, wipe ---------------------------------------------------

        void Load(string persona)
        {
            CancelAll();
            _generation++;
            _personaId = persona;
            _path = VoiceFile.FileFor(Folder(), persona);
            _file = string.IsNullOrEmpty(_path) || string.IsNullOrEmpty(persona) ? new VoiceFile() : VoiceFile.Load(_path);
            _loaded = true;
            _sigSheet = -1;
            _everydayPending = string.Empty;
            _intimatePending = string.Empty;
            EverydayStatus = _file.everyday.IsEmpty ? "Not read yet." : "Read by " + (_file.everydayBy.Length > 0 ? _file.everydayBy : "a model") + ".";
            IntimateStatus = _file.intimate.IsEmpty ? "Not read yet." : "Read by " + (_file.intimateBy.Length > 0 ? _file.intimateBy : "the local model") + ".";
            _contentRevision++;
            Revision++;
            Publish(true);
        }

        /// <summary>"Start over" keeps how she talks: it is part of who she is, not of what happened.</summary>
        public void StartOver()
        {
        }

        /// <summary>After a full wipe: the files are gone, so forget everything without saving it back.</summary>
        public void Forget()
        {
            CancelAll();
            _generation++;
            _file = new VoiceFile();
            _loaded = false;
            _sigSheet = -1;
            _tried.Clear();
            Revision++;
            VoiceNow.Reset();
        }

        void Save()
        {
            if (!string.IsNullOrEmpty(_path) && !VoiceFile.Save(_path, _file))
                Debug.LogWarning("Voice: " + LogId() + " could not be saved.");
        }

        void CancelAll()
        {
            if (_everydayCancel != null)
                _everydayCancel.Cancel();
            if (_intimateCancel != null)
                _intimateCancel.Cancel();
            _everydayCancel = null;
            _intimateCancel = null;
            ReadingEveryday = false;
            ReadingIntimate = false;
        }

        // ---- Helpers ---------------------------------------------------------------------

        TemperamentAxes Axes()
        {
            var pisara = GetComponent<PisaraReadingKeeper>();
            var sheet = pisara != null ? pisara.SheetFor(_personaId) : null;
            return PersonaRuntime.TemperamentChoiceFor(PersonaCatalog.LoadDefault(Folder()), _personaId, sheet).Axes;
        }

        float Innocence()
        {
            var innocence = GetComponent<InnocenceKeeper>();
            return innocence != null && innocence.IsLoaded ? innocence.State.Current : InnocenceMath.Default;
        }

        float Familiarity()
        {
            var relationship = GetComponent<RelationshipKeeper>();
            return relationship != null && relationship.IsLoaded ? relationship.State.Familiarity : 0f;
        }

        string LogId()
        {
            var id = CompanionMoodStore.PersonaFileId(_personaId);
            return id.Length == 0 ? "(default)" : id;
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
