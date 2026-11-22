using System;
using AICompanion.Core;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-119: keeps the selected persona's relationship state. Loads it at start and on a
    /// persona switch, adds real time only within three minutes of the last message or
    /// keystroke, saves at most every 30 seconds and on quit, and exposes the readout line.
    /// <para>
    /// JUH-118 section 4: each finished reply lands through <see cref="RelationshipNow"/>
    /// (affection, familiarity, the Mood card reason, her mood); active chat time grows
    /// familiarity with <see cref="RelationshipMath.FamiliarityAfter"/>; a persona without a
    /// relationship file starts from the model's reading; and the prompt line is published
    /// for chat. While "Relationship changes" is off, nothing moves and the line is empty.
    /// </para>
    /// </summary>
    public sealed class RelationshipKeeper : MonoBehaviour, ICompanionStartOver
    {
        const float SaveSeconds = 30f;

        /// <summary>Time counts only this long after the user's last chat message or keystroke.</summary>
        public const float ActivityWindowSeconds = 180f;

        SettingsPanel _settings;
        ChatPanel _chat;
        bool _chatChecked;
        RelationshipState _state = new RelationshipState();
        string _personaId;
        string _path;
        bool _loaded;
        bool _dirty;
        float _saveIn = SaveSeconds;
        float _lastActivity = float.NegativeInfinity;
        int _lastRevision = -1;
        bool _revisionSeen;

        /// <summary>The relationship now.</summary>
        public RelationshipState State
        {
            get { return _state; }
        }

        public bool IsLoaded
        {
            get { return _loaded; }
        }

        readonly RelationshipHistory _history = new RelationshipHistory();

        /// <summary>The familiarity band last shown (JUH-122); -1 until the first landing of this persona.</summary>
        int _announcedBand = -1;

        // JUH-123: which of her secrets were found, for this persona, saved next to the relationship.
        SecretsState _secrets = new SecretsState();
        string _secretsPath;
        bool _secretsDirty;

        /// <summary>Her secrets found so far (JUH-123).</summary>
        public SecretsState Secrets
        {
            get { return _secrets; }
        }

        /// <summary>Profile → Personality → Her secrets, from this persona's sheet. Built when the panel opens, never per frame.</summary>
        public ProfileSecrets SecretsView()
        {
            var sheet = CurrentSheet();
            return ProfileSecrets.Build(HerSecrets.From(sheet != null ? sheet.landing : null), _loaded ? _secrets : null);
        }

        CharacterSheet CurrentSheet()
        {
            var reading = GetComponent<PisaraReadingKeeper>();
            return reading != null && _loaded ? reading.SheetFor(_personaId) : null;
        }

        /// <summary>The last few changes with a plain reason, newest first through <see cref="RelationshipHistory.Line"/> (JUH-117).</summary>
        public RelationshipHistory History
        {
            get { return _history; }
        }

        /// <summary>True while this persona had no relationship file and waits for a model reading to start from.</summary>
        bool _awaitingStart;
        readonly FamiliarityClock _familiarityClock = new FamiliarityClock();

        /// <summary>Applies the chat time gathered so far (before a save, a landed reply or a readout).</summary>
        void FlushFamiliarity()
        {
            if (_loaded && _familiarityClock.Pending > 0d)
            {
                _state.Familiarity = _familiarityClock.Apply(_state.Familiarity);
                _dirty = true;
            }
        }
        float _wiringIn;
        int _sheetRevision = -1;
        float _publishedAffection = float.NaN;
        float _publishedFamiliarity = float.NaN;
        bool _publishedSwitch;

        void OnEnable()
        {
            RelationshipNow.ReplyFinished += OnReply;
        }

        /// <summary>One finished reply to a user message: land it, record why, and nudge her mood.</summary>
        void OnReply(AnimationCue cue)
        {
            if (!_loaded)
                return;
            FlushFamiliarity();

            // JUH-123: a secret the reply names. The first find is a critical hit; one found
            // before lands like any other message. An id that is not one of hers is ignored.
            var hit = SecretHitKind.None;
            var secret = default(SecretItem);
            // JUH-125: an 18+ secret (k, s or y), only while the gate is open.
            var intimateHit = SecretHitKind.None;
            var intimate = default(IntimateSecretItem);
            var intimateKeeper = GetComponent<IntimateSecretsKeeper>();
            if (PisaraSwitches.Relationship && !string.IsNullOrEmpty(cue.Secret))
            {
                if (IntimateSecretsState.IsIntimateId(cue.Secret))
                {
                    if (intimateKeeper != null)
                        intimateHit = intimateKeeper.Check(cue.Secret, out intimate);
                }
                else
                {
                    var sheet = CurrentSheet();
                    hit = HerSecrets.Check(_secrets, HerSecrets.From(sheet != null ? sheet.landing : null), cue.Secret, out secret);
                }

                if (hit == SecretHitKind.None && intimateHit == SecretHitKind.None)
                    Debug.Log("Secrets: the reply named \"" + cue.Secret + "\", which is not one of hers; ignored");
            }

            // The first find of any secret is a critical hit. After that, her kinks and sensitive
            // areas always reward a little more (x1.25); everything else lands as usual.
            var first = hit == SecretHitKind.First || intimateHit == SecretHitKind.First;
            var bonus = intimateHit == SecretHitKind.Again && intimate.Group != IntimateSecretGroup.Shy ? RelationshipMath.FavouriteMultiplier : 1f;
            // JUH-169: a secret she likes always lands as a gain, one that stings as a loss.
            var lean = SecretLean.None;
            // Her other secrets: only the first find (found before, they land like any message).
            if (hit == SecretHitKind.First)
                lean = secret.Group == SecretGroup.WinsHerOver ? SecretLean.Likes : SecretLean.Stings;
            else if (intimateHit != SecretHitKind.None && intimate.Group != IntimateSecretGroup.Shy)
                lean = SecretLean.Likes;
            var change = RelationshipNow.Land(cue, _state.Affection, _state.Familiarity, first, bonus, lean);
            var foundText = hit == SecretHitKind.First ? secret.Text : intimateHit == SecretHitKind.First ? intimate.Text : null;
            if (!Apply(change, foundText))
                return;
            if (intimateHit == SecretHitKind.First && intimateKeeper != null && intimateKeeper.Reveal(intimate))
                Debug.Log("Intimate secrets: found " + intimate.Id + " for persona " + PersonaLogId() + " (" + intimateKeeper.FoundCount + " found)");
            else if (intimateHit == SecretHitKind.Again)
                Debug.Log("Intimate secrets: " + intimate.Id + " again" + (bonus > 1f ? ", x" + bonus.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : string.Empty));
            if (first && hit == SecretHitKind.First && _secrets.Reveal(secret.Key))
            {
                _secretsDirty = true;
                SaveSecrets();
                Debug.Log("Secrets: found " + secret.Id + " for persona " + PersonaLogId() + " (" + _secrets.FoundCount + " found)");
            }
            else if (hit == SecretHitKind.Again)
            {
                Debug.Log("Secrets: " + secret.Id + " was found before; an ordinary landing");
            }

            var mood = GetComponent<CompanionMoodKeeper>();
            if (mood != null)
                mood.ApplyRelationship(change, cue.Intensity);
            Publish(true);
        }

        /// <summary>
        /// About once a second: a new persona starts from the model's reading, and the prompt
        /// line follows the sheet, the switch and the numbers. No allocation unless it changed.
        /// </summary>
        void TickWiring()
        {
            _wiringIn -= Time.unscaledDeltaTime;
            if (_wiringIn > 0f)
                return;
            _wiringIn = 1f;

            var reading = GetComponent<PisaraReadingKeeper>();
            var revision = reading != null ? reading.Revision : 0;
            var sheetChanged = revision != _sheetRevision;
            _sheetRevision = revision;

            if (_awaitingStart && reading != null)
            {
                var sheet = reading.SheetFor(_personaId);
                if (sheet != null && sheet.source == CharacterSheet.SourceModel)
                    StartFromReading(sheet);
            }

            Publish(sheetChanged);
        }

        /// <summary>
        /// The first model reading for a persona whose relationship file did not exist: start from
        /// <see cref="RelationshipMath.Start"/> and save. An existing relationship is never
        /// overwritten, so a Re-read later changes nothing here.
        /// </summary>
        void StartFromReading(CharacterSheet sheet)
        {
            _awaitingStart = false;
            float affection;
            float familiarity;
            RelationshipMath.Start(sheet.relationship, out affection, out familiarity);
            _familiarityClock.Clear();
            _state.Affection = affection;
            _state.Familiarity = familiarity;
            _history.Clear();
            _announcedBand = -1;
            _dirty = true;
            Save();
            Debug.Log("Relationship: started from the reading for persona " + PersonaLogId() + " (" + _state.Readout() + ")");
            Publish(true);
        }

        /// <summary>
        /// The prompt line for chat. Rebuilt only when the sheet, the switch or a rounded number
        /// changed (or when <paramref name="force"/>), so time spent chatting does not allocate.
        /// </summary>
        void Publish(bool force)
        {
            var on = PisaraSwitches.Relationship;
            var affection = (float)Math.Round(_state.Affection);
            var familiarity = (float)Math.Round(_state.Familiarity);
            if (!force && on == _publishedSwitch && affection == _publishedAffection && familiarity == _publishedFamiliarity)
                return;
            _publishedSwitch = on;
            _publishedAffection = affection;
            _publishedFamiliarity = familiarity;

            var reading = GetComponent<PisaraReadingKeeper>();
            var sheet = reading != null && _loaded ? reading.SheetFor(_personaId) : null;
            // The setup name has no sheet: its own relationship text stays the only one.
            RelationshipNow.SetPromptLine(on && sheet != null && !string.IsNullOrEmpty(_personaId)
                ? RelationshipPrompt.Line(sheet.relationship, sheet.landing, _state.Affection, _state.Familiarity)
                : string.Empty);
        }

        string PersonaLogId()
        {
            var id = CompanionMoodStore.PersonaFileId(_personaId);
            return id.Length == 0 ? "(default)" : id;
        }

        /// <summary>
        /// Applies one landed message (JUH-117): the new numbers, a history line and the log
        /// line. Does nothing while the Relationship switch is off. True when it was applied.
        /// The caller passes the change's mood nudges on with <see cref="RelationshipMath.PushMoods"/>.
        /// </summary>
        public bool Apply(RelationshipChange change, string secretFound = null)
        {
            if (!_loaded || !PisaraSwitches.Relationship)
                return false;
            var familiarityBefore = _state.Familiarity;
            _state.Affection = change.Affection;
            _state.Familiarity = change.Familiarity;
            _history.Record(change);
            _dirty = true;
            Debug.Log(change.LogLine());
            // JUH-122: the floating pieces. The band is compared with the last one announced, so a
            // band reached through chat time shows with the next landing.
            if (_announcedBand < 0)
                _announcedBand = RelationshipCard.KnowingBand(familiarityBefore);
            var notice = RelationshipNotice.From(change, _announcedBand, secretFound);
            if (notice.NewBand)
                _announcedBand = notice.Band;
            RelationshipNow.ReportChanged(notice);
            return true;
        }

        /// <summary>The user set the numbers by hand (a correction in persona settings). Saved now.</summary>
        public void SetByUser(float affection, float familiarity)
        {
            if (!_loaded)
                return;
            _state.Affection = RelationshipMath.ClampAffection(affection);
            _state.Familiarity = RelationshipMath.ClampFamiliarity(familiarity);
            _history.Clear();
            _announcedBand = -1;
            _awaitingStart = false;
            _familiarityClock.Clear();
            _dirty = true;
            Save();
            Debug.Log("Relationship: set by the user (" + _state.Readout() + ")");
            Publish(true);
        }

        /// <summary>
        /// Drops the in-memory numbers after a wipe. The next Update reloads the default from
        /// the now-missing file, even when the persona id did not change.
        /// </summary>
        public void Forget()
        {
            _history.Clear();
            _announcedBand = -1;
            _state = new RelationshipState();
            _personaId = null;
            _path = null;
            _loaded = false;
            _dirty = false;
            _saveIn = SaveSeconds;
            _lastActivity = float.NegativeInfinity;
            _lastRevision = -1;
            _revisionSeen = false;
            _awaitingStart = false;
            _familiarityClock.Clear();
            _secrets = new SecretsState();
            _secretsPath = null;
            _secretsDirty = false;
            RelationshipNow.Reset();
        }

        /// <summary>True while a message or keystroke is recent enough to count as chat time.</summary>
        public static bool IsWithinActivityWindow(float now, float lastActivity)
        {
            return !float.IsNaN(now) && !float.IsNaN(lastActivity)
                && now >= lastActivity && now - lastActivity <= ActivityWindowSeconds;
        }

        /// <summary>
        /// JUH-117 "Start over with this persona": put the relationship back where this
        /// persona's character sheet starts it and save at once. Without a sheet, 0 and 0.
        /// </summary>
        public void StartOver()
        {
            if (!_loaded)
                return;

            float affection = 0f;
            float familiarity = 0f;
            var reading = GetComponent<PisaraReadingKeeper>();
            var sheet = reading != null ? reading.Sheet : null;
            if (sheet != null)
                RelationshipMath.Start(sheet.relationship, out affection, out familiarity);

            _state = new RelationshipState { Affection = affection, Familiarity = familiarity, SecondsTogether = 0d };
            // JUH-123: start over hides her secrets again.
            _secrets.Clear();
            _secretsDirty = true;
            SaveSecrets();
            _history.Clear();
            _announcedBand = -1;
            _awaitingStart = false;
            _familiarityClock.Clear();
            _dirty = true;
            Save();
            Publish(true);
            Debug.Log("Relationship: start over for persona "
                + (_personaId.Length == 0 ? "(default)" : CompanionMoodStore.PersonaFileId(_personaId))
                + " (" + _state.Readout() + ")");
        }

        /// <summary>"Relationship: +35, familiarity 12" for the mood readout.</summary>
        public string Readout
        {
            get { return _state.Readout(); }
        }

        void OnDisable()
        {
            RelationshipNow.ReplyFinished -= OnReply;
            Save();
            RelationshipNow.Reset();
        }

        void OnApplicationQuit()
        {
            Save();
        }

        void Update()
        {
            var settings = CurrentSettings();
            if (settings == null)
                return;

            var persona = settings.PersonaId ?? string.Empty;
            if (!_loaded || !string.Equals(persona, _personaId, StringComparison.Ordinal))
                Load(persona);

            if (!_chatChecked)
            {
                _chat = GetComponent<ChatPanel>();
                _chatChecked = true;
            }

            if (_chat != null)
            {
                var revision = _chat.TranscriptRevision;
                if (!_revisionSeen)
                {
                    _revisionSeen = true;
                    _lastRevision = revision;
                }
                else if (revision != _lastRevision)
                {
                    _lastRevision = revision;
                    if (_chat.ChatVisible)
                        _lastActivity = Time.unscaledTime;
                }

                if (_chat.ChatVisible && Input.anyKeyDown)
                    _lastActivity = Time.unscaledTime;

                // "Relationship changes" off (JUH-117) freezes familiarity from time as well.
                if (PisaraSwitches.Relationship && _chat.ChatVisible && IsWithinActivityWindow(Time.unscaledTime, _lastActivity))
                {
                    _state.AddSeconds(Time.unscaledDeltaTime);
                    // JUH-118 section 4: real time spent chatting is how they get to know each other.
                    // Gathered in a double and applied every 5 s: per frame the gain rounds to nothing.
                    if (_familiarityClock.Add(Time.unscaledDeltaTime))
                        FlushFamiliarity();
                    _dirty = true;
                }
            }

            TickWiring();

            _saveIn -= Time.unscaledDeltaTime;
            if (_saveIn <= 0f)
            {
                _saveIn = SaveSeconds;
                Save();
            }
        }

        void Load(string persona)
        {
            if (_loaded)
                Save();

            _personaId = persona;
            _history.Clear();
            _announcedBand = -1;
            _path = RelationshipStore.FileFor(SettingsPanelFolder(), persona);
            _state = string.IsNullOrEmpty(_path)
                ? new RelationshipState()
                : RelationshipStore.Load(_path);
            _secretsPath = string.IsNullOrEmpty(_path) ? null : SecretsStore.FileFor(SettingsPanelFolder(), persona);
            _secrets = SecretsStore.Load(_secretsPath);
            _secretsDirty = false;
            // No file yet: start from this persona's model reading once it is there (never for the setup name).
            _awaitingStart = !string.IsNullOrEmpty(persona) && !string.IsNullOrEmpty(_path) && !System.IO.File.Exists(_path);
            // The previous persona's time was applied by the Save above; none carries over.
            _familiarityClock.Clear();
            _sheetRevision = -1;
            _wiringIn = 0f;
            _loaded = true;
            _dirty = false;
            _saveIn = SaveSeconds;
            Debug.Log(RelationshipLog.Line(persona, _state));
        }

        void SaveSecrets()
        {
            if (!_secretsDirty || string.IsNullOrEmpty(_secretsPath))
                return;
            if (SecretsStore.Save(_secretsPath, _secrets))
                _secretsDirty = false;
        }

        void Save()
        {
            SaveSecrets();
            FlushFamiliarity();
            if (!_loaded || !_dirty || string.IsNullOrEmpty(_path))
                return;
            if (RelationshipStore.Save(_path, _state, DateTime.UtcNow))
                _dirty = false;
        }

        CompanionSettings CurrentSettings()
        {
            if (_settings == null)
                _settings = GetComponent<SettingsPanel>();
            return _settings != null ? _settings.CurrentSettings : null;
        }

        static string SettingsPanelFolder()
        {
            return SettingsLocations.Folder(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.persistentDataPath);
        }
    }

    /// <summary>The relationship log line: <c>Relationship: friend = +35, familiarity 12</c>.</summary>
    public static class RelationshipLog
    {
        public static string Line(string persona, RelationshipState state)
        {
            var id = CompanionMoodStore.PersonaFileId(persona);
            var value = state ?? new RelationshipState();
            return "Relationship: " + (id.Length == 0 ? "(default)" : id)
                + " = " + value.Affection.ToString("+0.#;-0.#;0", System.Globalization.CultureInfo.InvariantCulture)
                + ", familiarity " + value.Familiarity.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
