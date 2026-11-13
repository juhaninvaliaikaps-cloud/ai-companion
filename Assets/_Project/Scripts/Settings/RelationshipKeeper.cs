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
    /// Chat does not move affection or familiarity yet; that belongs to the later relationship leaf.
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

        /// <summary>
        /// Drops the in-memory numbers after a wipe. The next Update reloads the default from
        /// the now-missing file, even when the persona id did not change.
        /// </summary>
        public void Forget()
        {
            _state = new RelationshipState();
            _personaId = null;
            _path = null;
            _loaded = false;
            _dirty = false;
            _saveIn = SaveSeconds;
            _lastActivity = float.NegativeInfinity;
            _lastRevision = -1;
            _revisionSeen = false;
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
            _dirty = true;
            Save();
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
            Save();
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

                if (_chat.ChatVisible && IsWithinActivityWindow(Time.unscaledTime, _lastActivity))
                {
                    _state.AddSeconds(Time.unscaledDeltaTime);
                    _dirty = true;
                }
            }

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
            _path = RelationshipStore.FileFor(SettingsPanelFolder(), persona);
            _state = string.IsNullOrEmpty(_path)
                ? new RelationshipState()
                : RelationshipStore.Load(_path);
            _loaded = true;
            _dirty = false;
            _saveIn = SaveSeconds;
            Debug.Log(RelationshipLog.Line(persona, _state));
        }

        void Save()
        {
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
