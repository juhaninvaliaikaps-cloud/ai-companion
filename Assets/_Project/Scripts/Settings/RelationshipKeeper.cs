using System;
using AICompanion.Core;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-119: keeps the selected persona's relationship state. Loads it at start and on a
    /// persona switch, adds real time while the chat is visible, saves at most every 30
    /// seconds and on quit, and exposes the readout line. Chat does not move affection or
    /// familiarity yet; that belongs to the later relationship leaf.
    /// </summary>
    public sealed class RelationshipKeeper : MonoBehaviour
    {
        const float SaveSeconds = 30f;

        SettingsPanel _settings;
        ChatPanel _chat;
        RelationshipState _state = new RelationshipState();
        string _personaId;
        string _path;
        bool _loaded;
        bool _dirty;
        float _saveIn = SaveSeconds;

        /// <summary>The relationship now.</summary>
        public RelationshipState State
        {
            get { return _state; }
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

            if (_chat == null)
                _chat = GetComponent<ChatPanel>();
            if (_chat != null && _chat.ChatVisible)
            {
                _state.AddSeconds(Time.unscaledDeltaTime);
                _dirty = true;
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
