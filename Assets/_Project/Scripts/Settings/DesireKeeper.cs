using System;
using System.Globalization;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-170: keeps the active persona's desire (adult mode only). While the 18+ gate is open,
    /// time moves it toward her resting level (from her libido), each reply's heat and the 18+
    /// secrets it touched raise it, a release spends it, her mood follows a rising band, and the
    /// prompt line goes out through <see cref="DesireNow"/>. While the gate is closed it does
    /// nothing, the line is empty and no piece floats. The numbers live in
    /// <see cref="DesireMath"/>; this component only wires them. Libido comes from the
    /// innocence reading (the same local-only request) unless the user set it.
    /// </summary>
    public sealed class DesireKeeper : MonoBehaviour, ICompanionStartOver
    {
        const float CheckSeconds = 1f;
        // Time alone saves at most this often; a moment saves within a second.
        const float SaveSeconds = 30f;

        /// <summary>Time alone moves a floating piece once it has moved her this far.</summary>
        public const float TimeShown = 3f;

        /// <summary>A moment shows a piece when it moved her at least this far.</summary>
        public const float MomentShown = 0.5f;

        SettingsPanel _settings;
        string _personaId;
        string _path;
        DesireState _state = new DesireState();
        bool _loaded;
        bool _dirty;
        float _checkIn;
        float _saveIn = SaveSeconds;
        float _timeUnshown;
        int _band = -1;
        int _publishedKey = int.MinValue;

        /// <summary>Goes up when anything the settings view or the profile shows changed.</summary>
        public int Revision { get; private set; }

        public bool IsLoaded
        {
            get { return _loaded; }
        }

        public DesireState State
        {
            get { return _state; }
        }

        /// <summary>Her desire now (0 to 100).</summary>
        public float Desire
        {
            get { return _state.Desire; }
        }

        /// <summary>The libido she runs on: the user's setting, else the reading, else 50.</summary>
        public float Libido
        {
            get { return _state.Libido(ReadLibido()); }
        }

        /// <summary>The reading's libido, or -1 when there is none yet.</summary>
        public float ReadLibido()
        {
            var innocence = GetComponent<InnocenceKeeper>();
            return innocence != null && innocence.IsLoaded ? innocence.State.ReadLibido : -1f;
        }

        public bool Spent
        {
            get { return _state.InRefractory(DateTime.UtcNow); }
        }

        void OnEnable()
        {
            RelationshipNow.ReplyFinished += OnReply;
        }

        void OnDisable()
        {
            RelationshipNow.ReplyFinished -= OnReply;
            Save(true);
            DesireNow.Reset();
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

            if (IntimateExpressions.Active)
                Step(DateTime.UtcNow);
            Publish(false);
        }

        /// <summary>Time since the last step: she builds toward her level or fades back to it.</summary>
        void Step(DateTime nowUtc)
        {
            var libido = Libido;
            if (_state.IsNew)
            {
                // A new persona (or one from before JUH-170) starts a little under her level.
                _state.Desire = DesireMath.Start(libido);
                _state.LastTicks = nowUtc.Ticks;
                _dirty = true;
                Revision++;
                Debug.Log("Desire: " + PersonaLogId() + " starts at " + _state.Readout(libido));
                return;
            }

            var minutes = (nowUtc - new DateTime(_state.LastTicks, DateTimeKind.Utc)).TotalMinutes;
            _state.LastTicks = nowUtc.Ticks;
            if (minutes <= 0d)
                return;
            var pace = DesireMath.MoodPace(CompanionMoodNow.Shown, CompanionMoodNow.Strength);
            var before = _state.Desire;
            _state.Desire = DesireMath.AfterTime(before, libido, minutes, pace, IntimateExpressions.Undressed, _state.InRefractory(nowUtc));
            var delta = _state.Desire - before;
            _dirty = true;
            if (delta == 0f)
                return;

            _timeUnshown += delta;
            if (Math.Abs(_timeUnshown) >= TimeShown)
            {
                DesireNow.Report(new DesireNotice { Delta = _timeUnshown, Desire = _state.Desire, ByTime = true });
                _timeUnshown = 0f;
                Revision++;
            }

            // A long time away is logged once, as numbers.
            if (minutes > 10d)
                Debug.Log("Desire: " + PersonaLogId() + " after " + minutes.ToString("0", CultureInfo.InvariantCulture) + " min away = " + _state.Readout(libido));
            BandChanged();
        }

        /// <summary>One finished reply: its heat and any 18+ secret it touched.</summary>
        void OnReply(AnimationCue cue)
        {
            if (!_loaded || string.IsNullOrEmpty(_personaId) || !IntimateExpressions.Active)
                return;
            var now = DateTime.UtcNow;
            Step(now);

            IntimateSecretGroup? group = null;
            var secrets = GetComponent<IntimateSecretsKeeper>();
            if (secrets != null && !string.IsNullOrEmpty(cue.Secret) && IntimateSecretsState.IsIntimateId(cue.Secret))
            {
                // First find or found before: either way the moment touched one of hers. An id
                // that is not one of hers is None and adds nothing.
                IntimateSecretItem item;
                if (secrets.Check(cue.Secret, out item) != SecretHitKind.None)
                    group = item.Group;
            }

            if (cue.Heat == DesireHeat.None && !group.HasValue)
                return;

            var libido = Libido;
            var before = _state.Desire;
            var release = cue.Heat == DesireHeat.Release;
            if (release)
            {
                _state.Desire = DesireMath.AfterRelease(before);
                _state.RefractoryUntilTicks = now.AddMinutes(DesireMath.RefractoryMinutes(libido)).Ticks;
                PushMoods(DesireMath.ReleasePushes(), "a release");
            }
            else
            {
                _state.Desire = DesireMath.AfterMoment(before, cue.Heat, cue.Intensity, libido, Affection(), _state.InRefractory(now), group);
            }

            _dirty = true;
            _saveIn = Math.Min(_saveIn, 1f);
            var delta = _state.Desire - before;
            Debug.Log("Desire: " + PersonaLogId() + " " + DesireHeats.Name(cue.Heat) + (group.HasValue ? " +secret" : string.Empty)
                + " " + delta.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " = " + _state.Readout(libido));
            if (Math.Abs(delta) >= MomentShown || release)
                DesireNow.Report(new DesireNotice { Delta = delta, Desire = _state.Desire, Release = release });
            Revision++;
            BandChanged();
            Publish(true);
        }

        /// <summary>When she climbs into a higher band, her mood follows. Falling moves no mood.</summary>
        void BandChanged()
        {
            var band = DesireMath.Band(_state.Desire);
            if (band == _band)
                return;
            var rose = _band >= 0 && band > _band;
            _band = band;
            Revision++;
            if (!rose)
                return;
            PushMoods(DesireMath.BandPushes(band, Innocence()), "desire");
            Debug.Log("Desire: " + PersonaLogId() + " is now " + DesireMath.BandName(_state.Desire));
        }

        void PushMoods(CompanionMoodPush[] pushes, string cause)
        {
            if (pushes == null || pushes.Length == 0)
                return;
            var mood = GetComponent<CompanionMoodKeeper>();
            if (mood != null)
                mood.TryApplyPushes(_personaId, pushes, cause);
        }

        // ---- Libido slider ---------------------------------------------------------------

        /// <summary>The user sets her libido; a new reading no longer changes it.</summary>
        public void SetLibidoByUser(float value)
        {
            if (!_loaded)
                return;
            _state.LibidoUserSet = true;
            _state.UserLibido = value;
            _dirty = true;
            _saveIn = Math.Min(_saveIn, 2f);
            Revision++;
            Publish(true);
        }

        /// <summary>Back to what the reading said (50 when there is none).</summary>
        public void UseLibidoReading()
        {
            if (!_loaded)
                return;
            _state.LibidoUserSet = false;
            _dirty = true;
            Save(true);
            Debug.Log("Desire: " + PersonaLogId() + " libido back to the reading (" + Libido.ToString("0", CultureInfo.InvariantCulture) + ")");
            Revision++;
            Publish(true);
        }

        // ---- Prompt line -----------------------------------------------------------------

        /// <summary>Rebuilt only when what the line says changed, so it does not allocate while idle.</summary>
        void Publish(bool force)
        {
            var on = _loaded && IntimateExpressions.Active && !string.IsNullOrEmpty(_personaId);
            var libido = Libido;
            var innocence = Innocence();
            var affection = Affection();
            var spent = on && _state.InRefractory(DateTime.UtcNow);
            var key = DesirePrompt.Key(_state.Desire, libido, innocence, affection, spent, on);
            if (!force && key == _publishedKey)
                return;
            _publishedKey = key;
            DesireNow.SetPromptLine(DesirePrompt.Line(_state.Desire, libido, innocence, affection, spent, on));
        }

        // ---- Persona, Start over, wipe ---------------------------------------------------

        void Load(string persona)
        {
            if (_loaded)
                Save(true);
            _personaId = persona;
            _path = DesireStore.FileFor(Folder(), persona);
            _state = string.IsNullOrEmpty(_path) ? new DesireState() : DesireStore.Load(_path);
            _loaded = true;
            _dirty = false;
            _timeUnshown = 0f;
            _band = DesireMath.Band(_state.Desire);
            Revision++;
            Publish(true);
        }

        /// <summary>"Start over" (JUH-117): she starts again a little under her level; the user's libido setting stays.</summary>
        public void StartOver()
        {
            if (!_loaded)
                return;
            _state.Desire = DesireMath.Start(Libido);
            _state.RefractoryUntilTicks = 0L;
            _state.LastTicks = DateTime.UtcNow.Ticks;
            _band = DesireMath.Band(_state.Desire);
            _dirty = true;
            Save(true);
            Debug.Log("Desire: start over for " + PersonaLogId() + " (" + _state.Readout(Libido) + ")");
            Revision++;
            Publish(true);
        }

        /// <summary>After a full wipe: the files are gone, so forget the state without saving it back.</summary>
        public void Forget()
        {
            _state = new DesireState();
            _dirty = false;
            _loaded = false;
            _band = -1;
            Revision++;
            DesireNow.Reset();
        }

        void Save(bool now)
        {
            if (!_loaded || !_dirty || string.IsNullOrEmpty(_path))
                return;
            if (!now && _saveIn > 0f)
                return;
            _saveIn = SaveSeconds;
            if (DesireStore.Save(_path, _state))
                _dirty = false;
        }

        // ---- Helpers ---------------------------------------------------------------------

        float Innocence()
        {
            var innocence = GetComponent<InnocenceKeeper>();
            return innocence != null && innocence.IsLoaded ? innocence.State.Current : InnocenceMath.Default;
        }

        float Affection()
        {
            var relationship = GetComponent<RelationshipKeeper>();
            return relationship != null && relationship.IsLoaded && PisaraSwitches.Relationship ? relationship.State.Affection : 0f;
        }

        string PersonaLogId()
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

        static string Folder()
        {
            return SettingsLocations.Folder(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.persistentDataPath);
        }
    }
}
