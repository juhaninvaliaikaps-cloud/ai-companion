using System;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// Keeps her mood (JUH-103, phase 1): loads it for the selected persona, fades it once a
    /// second, feeds it her reply cues and the user's detected mood, saves it encrypted, and
    /// shows a small readout when "Show mood readout" is on. It does not change any
    /// expression or reaction yet; that is phase 2. Added by <see cref="SettingsPanel"/>.
    /// </summary>
    public sealed class CompanionMoodKeeper : MonoBehaviour
    {
        const float TickSeconds = 1f;
        const float SaveSeconds = 60f;

        SettingsPanel _settings;
        CompanionMoodState _state = new CompanionMoodState();
        string _personaId;
        string _path;
        bool _loaded;
        bool _dirty;
        float _tickIn;
        float _saveIn = SaveSeconds;
        string _readout = string.Empty;
        GUIStyle _style;

        /// <summary>The mood now. For phase 2 and for tests in a player.</summary>
        public CompanionMoodState State
        {
            get { return _state; }
        }

        void OnEnable()
        {
            CompanionMoodInputs.CueApplied += OnCue;
            CompanionMoodInputs.UserMoodDetected += OnUserMood;
            CompanionMoodInputs.Touched += OnTouch;
            CompanionMoodInputs.Ignored += OnIgnored;
        }

        void OnDisable()
        {
            CompanionMoodInputs.CueApplied -= OnCue;
            CompanionMoodInputs.UserMoodDetected -= OnUserMood;
            CompanionMoodInputs.Touched -= OnTouch;
            CompanionMoodInputs.Ignored -= OnIgnored;
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

            _tickIn -= Time.unscaledDeltaTime;
            if (_tickIn > 0f)
                return;
            var seconds = TickSeconds - _tickIn;
            _tickIn = TickSeconds;

            var changed = _state.SetIntimateAllowed(IntimateExpressions.Active);
            changed |= _state.Tick(seconds, LocalHour());
            _dirty = true;
            if (changed)
                LogShown("time");

            _saveIn -= seconds;
            if (_saveIn <= 0f)
            {
                _saveIn = SaveSeconds;
                Save();
            }

            _readout = settings.ShowMoodDebug ? Readout() : string.Empty;
        }

        void OnGUI()
        {
            if (_readout.Length == 0)
                return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
                _style.normal.textColor = Color.white;
            }

            // Below the overlay HUD (OverlayWindow draws it at 8, 8, 360 x 108).
            var rect = new Rect(8f, 122f, 360f, 36f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, rect.height - 4f), _readout, _style);
        }

        void OnCue(AnimationCue cue)
        {
            if (!_loaded)
                return;
            _state.SetIntimateAllowed(IntimateExpressions.Active);
            if (CompanionMoodTable.ApplyCue(_state, cue))
                LogShown("her cue");
            _dirty = true;
        }

        void OnUserMood(UserMood mood)
        {
            if (!_loaded || mood.IsNeutral)
                return;
            _state.SetIntimateAllowed(IntimateExpressions.Active);
            if (CompanionMoodTable.ApplyUser(_state, mood))
                LogShown("your mood");
            _dirty = true;
        }

        // Phase 1: hooks only. Touches (JUH-61) and being ignored do not move the mood yet.
        void OnTouch(CompanionTouch touch)
        {
        }

        void OnIgnored(float seconds)
        {
        }

        void Load(string persona)
        {
            if (_loaded)
                Save();

            _personaId = persona;
            _path = CompanionMoodStore.FileFor(SettingsPanelFolder(), persona);
            _state = string.IsNullOrEmpty(_path) ? new CompanionMoodState() : CompanionMoodStore.Load(_path, DateTime.UtcNow);
            _state.SetIntimateAllowed(IntimateExpressions.Active);
            _loaded = true;
            _dirty = false;
            Debug.Log("Mood: " + CompanionMoodNames.Name(_state.Shown) + " for persona "
                + (persona.Length == 0 ? "(default)" : CompanionMoodStore.PersonaFileId(persona))
                + " (" + _state.DescribeTop3() + ")");
        }

        void Save()
        {
            if (!_loaded || !_dirty || string.IsNullOrEmpty(_path))
                return;
            if (CompanionMoodStore.Save(_path, _state, DateTime.UtcNow))
                _dirty = false;
        }

        void LogShown(string cause)
        {
            Debug.Log("Mood: now " + CompanionMoodNames.Name(_state.Shown) + " after " + cause + " (" + _state.DescribeTop3() + ")");
        }

        string Readout()
        {
            return "Mood: " + CompanionMoodNames.Name(_state.Shown) + "\n" + _state.DescribeTop3();
        }

        CompanionSettings CurrentSettings()
        {
            if (_settings == null)
                _settings = GetComponent<SettingsPanel>();
            return _settings != null ? _settings.CurrentSettings : null;
        }

        static float LocalHour()
        {
            var now = DateTime.Now;
            return now.Hour + now.Minute / 60f;
        }

        static string SettingsPanelFolder()
        {
            return SettingsLocations.Folder(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.persistentDataPath);
        }
    }
}
