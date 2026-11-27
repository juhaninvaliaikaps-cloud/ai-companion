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

        // JUH-176: a reading that failed because the model was not answering yet is tried again later.
        readonly ReadRetry _retry = new ReadRetry();
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

        // The rotating pool: what chat showed and what she echoed, drained once per check.
        readonly List<string> _shownNow = new List<string>();
        readonly List<string> _echoedNow = new List<string>();
        CancellationTokenSource _refreshCancel;
        public bool Refreshing { get; private set; }
        int _refreshChecked = -1;

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
            CountUsage();
            MaybeRefresh();
            Publish(false);
        }

        // ---- The rotating pool: retire used lines, refresh thin moods --------------------

        /// <summary>The lines chat showed and she echoed: a line retires after a few replies, or at once when echoed.</summary>
        void CountUsage()
        {
            _shownNow.Clear();
            _echoedNow.Clear();
            if (!VoiceNow.TakeUsage(_shownNow, _echoedNow))
                return;
            var changed = false;
            for (var i = 0; i < _shownNow.Count; i++)
                changed |= Use(_shownNow[i], false);
            for (var i = 0; i < _echoedNow.Count; i++)
                changed |= Use(_echoedNow[i], true);
            Save();
            if (changed)
            {
                _contentRevision++;
                Revision++;
                Publish(true);
            }
        }

        /// <summary>Counts one showing (or an echo); true when the line just retired from a pool of the model's.</summary>
        bool Use(string line, bool echoed)
        {
            var uses = new List<LineUse>(_file.uses ?? new LineUse[0]);
            LineUse use = null;
            for (var i = 0; i < uses.Count; i++)
            {
                if (string.Equals(uses[i].text, line, StringComparison.Ordinal))
                {
                    use = uses[i];
                    break;
                }
            }

            if (use == null)
            {
                use = new LineUse { text = line };
                uses.Add(use);
            }

            if (!echoed)
                use.shown++;
            _file.uses = uses.ToArray();
            if (!VoicePool.ShouldRetire(use.shown, echoed))
                return false;

            // Only the model's lines retire; the user's are in their own fields and never move.
            var retired = false;
            for (var k = 0; k < VoiceLines.Keys.Length; k++)
            {
                var key = VoiceLines.Keys[k];
                var before = _file.everyday.lines.Get(key);
                var after = VoicePool.Without(before, line);
                if (after.Length != before.Length)
                {
                    _file.everyday.lines.Set(key, after);
                    retired = true;
                }
            }

            for (var k = 0; k < IntimateLines.Keys.Length; k++)
            {
                var key = IntimateLines.Keys[k];
                var before = _file.intimate.lines.Get(key);
                var after = VoicePool.Without(before, line);
                if (after.Length != before.Length)
                {
                    _file.intimate.lines.Set(key, after);
                    retired = true;
                }
            }

            if (retired)
            {
                _file.retired = VoicePool.Retire(_file.retired, line);
                _file.uses = Prune(_file.uses, line);
                Debug.Log("Voice: " + LogId() + " retired a line (" + (echoed ? "echoed" : use.shown + " replies") + ")");
            }

            return retired;
        }

        static LineUse[] Prune(LineUse[] uses, string line)
        {
            var kept = new List<LineUse>();
            for (var i = 0; uses != null && i < uses.Length; i++)
            {
                if (!string.Equals(uses[i].text, line, StringComparison.Ordinal))
                    kept.Add(uses[i]);
            }

            return kept.ToArray();
        }

        /// <summary>
        /// One refresh at a time: the first mood (or 18+ stage) down to its last line gets three
        /// new ones in the background. Everyday moods on the everyday model, the 18+ stages on
        /// the local model only. The user's own lines are never refreshed.
        /// </summary>
        void MaybeRefresh()
        {
            if (Refreshing || ReadingEveryday || ReadingIntimate)
                return;
            // Looked at again only after the pool changed (a retired line, a new reading, the gate).
            var check = _contentRevision * 2 + (IntimateExpressions.Active ? 1 : 0);
            if (check == _refreshChecked)
                return;
            var pisara = GetComponent<PisaraReadingKeeper>();
            if (pisara == null || pisara.IsReading)
                return;
            _refreshChecked = check;

            if (!_file.everyday.IsEmpty)
            {
                for (var k = 0; k < VoiceLines.Keys.Length; k++)
                {
                    var key = VoiceLines.Keys[k];
                    if (VoicePool.NeedsRefresh(_file.everyday.lines.Get(key), _file.UserEveryday.lines.Get(key)) && Untried("r" + key))
                    {
                        Refresh(key, false);
                        return;
                    }
                }
            }

            if (!IntimateExpressions.Active || _file.intimate.IsEmpty)
                return;
            for (var k = 0; k < IntimateLines.Keys.Length; k++)
            {
                var key = IntimateLines.Keys[k];
                if (VoicePool.NeedsRefresh(_file.intimate.lines.Get(key), _file.UserIntimate.lines.Get(key)) && Untried("r" + key))
                {
                    Refresh(key, true);
                    return;
                }
            }
        }

        /// <summary>Once per launch per mood and per retired count, so a failing model is not asked in a loop.</summary>
        bool Untried(string part)
        {
            return _tried.Add(part + _personaId + "#" + (_file.retired != null ? _file.retired.Length : 0));
        }

        async void Refresh(string key, bool intimate)
        {
            var pisara = GetComponent<PisaraReadingKeeper>();
            bool keepsLocal = true;
            string readBy;
            var provider = intimate ? pisara.LocalVoiceProvider(out readBy) : pisara.VoiceProvider(out keepsLocal, out readBy);
            var had = new List<string>(_file.retired ?? new string[0]);
            var left = intimate ? _file.intimate.lines.Get(key) : _file.everyday.lines.Get(key);
            had.AddRange(left);
            var input = VoiceReader.RefreshInput(_everyday, VoiceReader.Moment(key), Familiarity() >= VoicePrompt.CloseFrom, had,
                intimate ? _intimate : null);
            var persona = _personaId;
            var generation = _generation;
            var cancel = new CancellationTokenSource();
            _refreshCancel = cancel;
            Refreshing = true;
            var fresh = await VoiceReader.RefreshAsync(provider, keepsLocal, intimate, IntimateExpressions.Active, pisara.NameFor(persona), input, cancel.Token);
            if (this == null || generation != _generation || !string.Equals(persona, _personaId, StringComparison.Ordinal))
            {
                cancel.Dispose();
                return;
            }

            Refreshing = false;
            _refreshCancel = null;
            cancel.Dispose();
            if (fresh.Length == 0)
            {
                Debug.LogWarning("Voice: " + LogId() + " refresh for " + key + " gave no lines");
                // The other moods may still be refreshed; this one waits for the next launch.
                _refreshChecked = -1;
                return;
            }

            var now = intimate ? _file.intimate.lines.Get(key) : _file.everyday.lines.Get(key);
            var refilled = VoicePool.Refill(now, fresh, _file.retired);
            if (intimate)
                _file.intimate.lines.Set(key, refilled);
            else
                _file.everyday.lines.Set(key, refilled);
            Save();
            Debug.Log("Voice: " + LogId() + " refreshed " + key + " (" + refilled.Length + " lines, " + (intimate ? "local model" : readBy) + ")");
            _contentRevision++;
            Revision++;
            Publish(true);
        }

        // ---- Reading ---------------------------------------------------------------------

        /// <summary>"Read again": both parts now, whatever the hashes say.</summary>
        public void ReadAgain()
        {
            if (!_loaded || string.IsNullOrEmpty(_personaId))
                return;
            _tried.Clear();
            _retry.Clear();
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
            var desireKeeper = GetComponent<DesireKeeper>();
            var bandNow = (innocenceKeeper != null && innocenceKeeper.IsLoaded ? InnocenceMath.Band(innocenceKeeper.State.Current) : -1)
                + 10 * (desireKeeper != null && desireKeeper.IsLoaded ? DesireMath.LibidoBand(desireKeeper.Libido) : -1);
            var innocenceReading = innocenceKeeper != null && innocenceKeeper.Reading;
            var prompt = chat.systemPrompt ?? string.Empty;
            var same = pisara.Revision == _sigSheet && ReferenceEquals(prompt, _sigPrompt) && ReferenceEquals(AdultNotesNow.Notes, _sigNotes)
                && ReferenceEquals(PersonaMannerNow.PromptLine, _sigManner) && gateNow == _sigGate && bandNow == _sigBand
                && innocenceReading == _sigInnocenceReading;
            if (!force && same && _everydayPending.Length == 0 && _intimatePending.Length == 0 && !_retry.AnyWaiting)
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
                ReadEveryday(name, everydayInput, everydayHash, "e" + _personaId + everydayHash);

            // 18+: only while the gate is open, after her innocence reading, on the local model.
            if (!IntimateExpressions.Active)
                return;
            var innocence = GetComponent<InnocenceKeeper>();
            if (innocence == null || !innocence.IsLoaded || innocence.Reading)
                return;
            var desire = GetComponent<DesireKeeper>();
            var libido = desire != null && desire.IsLoaded ? desire.Libido : -1f;
            var withNotes = AdultNotesNow.ForReading(prompt);
            var intimateInput = VoiceInput.Intimate(withNotes, axes, sheet, innocence.State.Current, libido);
            // Bands, not exact values, decide: her slow drift re-reads only in a new band.
            var intimateHash = VoiceInput.IntimateHash(withNotes, axes, sheet, innocence.State.Current, libido);
            if (!ReadingIntimate && Due(force, intimateHash, _file.intimateHash, ref _intimatePending, ref _intimateSince, "i"))
                ReadIntimate(name, intimateInput, intimateHash, "i" + _personaId + intimateHash);
        }

        /// <summary>True when this part should be read now: forced, or new and unchanged for a few seconds, once per launch.</summary>
        bool Due(bool force, string hash, string savedHash, ref string pending, ref float since, string part)
        {
            var key = part + _personaId + hash;
            if (force)
            {
                pending = string.Empty;
                _retry.Started(key);
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
            if (!_retry.Ready(key, Time.unscaledTime))
                return false;
            _retry.Started(key);
            pending = string.Empty;
            return true;
        }

        async void ReadEveryday(string name, string input, string hash, string retryKey)
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
                // JUH-176: cancelled by a persona switch: it may run again later.
                if (this != null)
                    _retry.Failed(retryKey, Time.unscaledTime, true);
                return;
            }

            ReadingEveryday = false;
            _everydayCancel = null;
            cancel.Dispose();
            if (result.Ok)
            {
                _retry.Succeeded(retryKey);
                _file.everyday = result.Everyday;
                _file.everydayHash = hash;
                _file.everydayBy = readBy;
                Save();
                EverydayStatus = "Read by " + readBy + ".";
                var lines = new List<string>();
                result.Everyday.lines.AddAll(lines);
                Debug.Log("Voice: " + LogId() + " everyday read by " + readBy + " (" + result.Everyday.habits.Length + " habits, "
                    + lines.Count + " lines)");
            }
            else
            {
                EverydayStatus = "Not read: " + result.Failure + (_file.everyday.IsEmpty ? "." : "; the earlier reading stays.");
                var passing = ReadRetry.IsPassing(result.Failure);
                _retry.Failed(retryKey, Time.unscaledTime, passing);
                Debug.LogWarning("Voice: " + LogId() + " everyday reading failed (" + result.Failure + ")" + (passing ? "; trying again shortly" : string.Empty));
            }

            _contentRevision++;
            Revision++;
            Publish(true);
        }

        async void ReadIntimate(string name, string input, string hash, string retryKey)
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
                // JUH-176: cancelled by a persona switch: it may run again later.
                if (this != null)
                    _retry.Failed(retryKey, Time.unscaledTime, true);
                return;
            }

            ReadingIntimate = false;
            _intimateCancel = null;
            cancel.Dispose();
            if (result.Ok)
            {
                _retry.Succeeded(retryKey);
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
                var passing = ReadRetry.IsPassing(result.Failure);
                _retry.Failed(retryKey, Time.unscaledTime, passing);
                Debug.LogWarning("Voice: " + LogId() + " intimate reading failed (" + result.Failure + ")" + (passing ? "; trying again shortly" : string.Empty));
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
            var desire = Desire();
            var key = VoicePrompt.Key(everyday, CompanionMoodNow.Shown, CompanionMoodNow.Strength, familiarity, gate, innocence, desire);
            if (!force && key == _publishedKey && _contentRevision == _publishedRevision)
                return;
            _publishedKey = key;
            _publishedRevision = _contentRevision;
            var intimate = gate ? _intimate : null;
            var axes = Axes();
            var pisara = GetComponent<PisaraReadingKeeper>();
            var block = VoicePrompt.Block(pisara != null ? pisara.NameFor(_personaId) : string.Empty, everyday, intimate,
                CompanionMoodNow.Shown, CompanionMoodNow.Strength, familiarity, gate, innocence, axes, desire);
            var pool = VoicePrompt.Pool(everyday, intimate, CompanionMoodNow.Shown, CompanionMoodNow.Strength, gate, innocence, desire);
            var all = new List<string>();
            everyday.lines.AddAll(all);
            if (intimate != null)
                intimate.lines.AddAll(all);
            VoiceNow.Set(block, pool, all);
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
            _retry.Clear();
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
            if (_refreshCancel != null)
                _refreshCancel.Cancel();
            _everydayCancel = null;
            _intimateCancel = null;
            _refreshCancel = null;
            ReadingEveryday = false;
            ReadingIntimate = false;
            Refreshing = false;
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

        /// <summary>Her desire now (JUH-170), or -1 while the gate is closed or it is not loaded.</summary>
        float Desire()
        {
            var desire = GetComponent<DesireKeeper>();
            return IntimateExpressions.Active && desire != null && desire.IsLoaded ? desire.Desire : -1f;
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
            return _settings != null ? _settings.EffectiveSettings ?? _settings.CurrentSettings : null;
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
