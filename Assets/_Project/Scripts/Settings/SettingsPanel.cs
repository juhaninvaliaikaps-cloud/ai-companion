using System;
using System.Collections;
using System.IO;
using AICompanion.AI;
using AICompanion.Avatar;
using AICompanion.Core;
using AICompanion.Overlay;
using AICompanion.UI;
using AICompanion.Voice;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Settings
{
    /// <summary>
    /// Owns settings persistence, applies preferences to runtime services and routes requests to the Settings view.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed partial class SettingsPanel : MonoBehaviour, ICompanionStartup, IOverlayKeepAwake, IPrivacyMode
    {
        CompanionSettings _settings = CompanionSettings.CreateDefault();
        CompanionTray _tray;
        AvatarMount _mount;
        string _statusMessage = string.Empty;
        int _historyRevision = -1;
        int _probeSerial;
        bool _started;
        bool _applying;
        bool _probeBusy;
        bool _setupGate;
        bool _wizardClaimed;

        int _personaRevision;

        /// <summary>
        /// Goes up when the active persona is applied or edited, so views can refresh the name
        /// without loading the persona catalog from disk on a timer.
        /// </summary>
        public int PersonaRevision
        {
            get { return _personaRevision; }
        }

        public event Action SetupReplayRequested;

        public CompanionSettings CurrentSettings => _settings;
        public CompanionSettings EffectiveSettings { get; private set; }

        public bool PrivateMode => _settings != null && _settings.PrivateMode;

        public bool KeepFrameRateActive => IsOpen;

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            AdultClipWindow.InstallRouter();
            try
            {
                LocalDataActions.MigratePlaintext(Folder());
            }
            catch (Exception)
            {
                Debug.LogWarning("Local data migration did not finish.");
            }

            var path = SettingsLocations.SettingsFile(Folder());
            var exists = !string.IsNullOrEmpty(path) && File.Exists(path);
            _settings = CompanionSettingsStore.Load(path, exists ? SettingsLegacyPrefs.None : ReadLegacy());
            Subscribe();
            var tray = GetComponent<CompanionTray>();
            if (!exists && tray != null)
                _settings.StartWithWindows = tray.Menu.StartWithWindows;

            // A new install starts as Happi; existing settings keep their persona.
            _settings.PersonaId = PersonaDefaults.ForLaunch(exists, _settings.PersonaId);
            EnsureVideoAvatar();
            if (GetComponent<CompanionMoodKeeper>() == null)
                gameObject.AddComponent<CompanionMoodKeeper>();
            // JUH-192: short reaction wavs for the expression the cue, mood, or interaction just chose.
            if (GetComponent<ReactionVoicePlayer>() == null)
                gameObject.AddComponent<ReactionVoicePlayer>();
            if (GetComponent<RelationshipKeeper>() == null)
                gameObject.AddComponent<RelationshipKeeper>();
            // JUH-116: Innocent–Perverted, adult mode only; idle while the 18+ gate is closed.
            if (GetComponent<InnocenceKeeper>() == null)
                gameObject.AddComponent<InnocenceKeeper>();
            // JUH-170: her desire and libido, adult mode only; idle while the 18+ gate is closed.
            if (GetComponent<DesireKeeper>() == null)
                gameObject.AddComponent<DesireKeeper>();
            // JUH-125: her 18+ secrets, read on the local model only; idle while the gate is closed.
            if (GetComponent<IntimateSecretsKeeper>() == null)
                gameObject.AddComponent<IntimateSecretsKeeper>();

            // JUH-118: the reading tool (-pisara-read) runs alone; otherwise the keeper reads the active persona.
            if (PisaraReadingTool.Requested(Environment.GetCommandLineArgs()))
            {
                if (GetComponent<PisaraReadingTool>() == null)
                    gameObject.AddComponent<PisaraReadingTool>();
            }
            else if (GetComponent<PisaraReadingKeeper>() == null)
            {
                gameObject.AddComponent<PisaraReadingKeeper>();
            }

            // JUH-171: how she talks, the last Pisara reading (its 18+ part on the local model only).
            if (GetComponent<VoiceKeeper>() == null)
                gameObject.AddComponent<VoiceKeeper>();

            // JUH-122: scripted messages for checking the chat by hand (-pisara-chat <file>).
            if (PisaraChatTool.Requested(Environment.GetCommandLineArgs()) && GetComponent<PisaraChatTool>() == null)
                gameObject.AddComponent<PisaraChatTool>();

            _applying = true;
            try
            {
                ApplyEffectiveSettings();
                if (_settings.ChatHistory)
                    LoadHistory();
                GreetIfEmpty();
            }
            finally
            {
                _applying = false;
            }

            _setupGate = SetupWizardGate.ShouldPresentOnLaunch(
                exists,
                SetupWizardGate.HasSavedApiKey(),
                Environment.GetCommandLineArgs());
            if (!_setupGate)
                SaveSettings();
            var chat = GetComponent<ChatPanel>();
            _historyRevision = chat != null ? chat.TranscriptRevision : 0;
            Close();
        }

        void Start()
        {
            StartCompanion();
        }


        void OnDestroy()
        {
            if (_settings != null && _settings.ChatHistory)
                SaveHistory();
            if (_tray != null)
            {
                _tray.Menu.SettingsRequested -= Open;
                _tray.TrayStateChanged -= OnTrayState;
            }

            if (_mount != null)
                _mount.PreferenceChanged -= OnAvatar;
        }

        void Update()
        {
            if (!_started)
                return;

            AdultExternalPlayer.Tick();
            if (!IntimateExpressions.Active)
                AdultExternalPlayer.HideGateClosed();
            if (Input.GetKeyDown(HotkeyCatalog.SettingsKey) && !DebugInputGuard.IsBlocked(HotkeyCatalog.SettingsKey))
                Open();
            TrackLive();
            TrackHistory();
        }

        void LateUpdate()
        {
            if (!_setupGate || _wizardClaimed)
                return;
            _setupGate = false;
            SaveSettings();
        }

        public bool ClaimSetupGate()
        {
            if (!_setupGate || _wizardClaimed)
                return false;
            _wizardClaimed = true;
            return true;
        }

        public void HoldSetupSave()
        {
            _setupGate = true;
            _wizardClaimed = true;
        }

        public void PreviewSetup()
        {
            if (!_started)
                StartCompanion();
            ApplyLive();
        }

        public void CommitSetup()
        {
            if (!_started)
                StartCompanion();
            _setupGate = false;
            _wizardClaimed = true;
            ApplyLive();
        }

        public void Open()
        {
            if (WizardIsOpen())
                return;
            if (!_started)
                StartCompanion();
            OpenOverride?.Invoke();
        }

        public void Close()
        {
            CloseView?.Invoke();
            _probeSerial++;
            _probeBusy = false;
        }

        void Subscribe()
        {
            _tray = GetComponent<CompanionTray>();
            if (_tray != null)
            {
                _tray.Menu.SettingsRequested += Open;
                _tray.TrayStateChanged += OnTrayState;
            }

            _mount = GetComponent<AvatarMount>();
            if (_mount != null)
                _mount.PreferenceChanged += OnAvatar;
        }

        void OnTrayState()
        {
            if (_applying || _tray == null || _settings == null)
                return;

            var changed = false;
            if (_settings.Paused != _tray.IsPaused)
            {
                _settings.Paused = _tray.IsPaused;
                changed = true;
            }

            if (_settings.HideTaskbarButton != _tray.HideTaskbarButton)
            {
                _settings.HideTaskbarButton = _tray.HideTaskbarButton;
                changed = true;
            }

            if (_settings.StartWithWindows != _tray.Menu.StartWithWindows)
            {
                _settings.StartWithWindows = _tray.Menu.StartWithWindows;
                changed = true;
            }

            if (!changed)
                return;

            SaveSettings();
            SettingsRevision++;
        }

        void OnAvatar(AvatarPreference preference)
        {
            if (_applying || _settings == null)
                return;

            var name = AvatarSourceNames.From(preference);
            if (_settings.AvatarSource == name)
                return;

            _settings.AvatarSource = name;
            SaveSettings();
        }

        void TrackLive()
        {
            if (_applying || _settings == null)
                return;

            var changed = false;
            var window = GetComponent<OverlayWindow>();
            if (window != null)
            {
                if (_settings.AlwaysOnTop != window.IsTopmost)
                {
                    _settings.AlwaysOnTop = window.IsTopmost;
                    changed = true;
                }

                if (Mathf.Abs(_settings.Scale - window.WindowScale) > 0.001f)
                {
                    _settings.Scale = window.WindowScale;
                    changed = true;
                }
            }

            var speaker = GetComponent<VoiceSpeaker>();
            if (speaker != null && speaker.IsMuted != _settings.Muted)
            {
                _settings.Muted = speaker.IsMuted;
                changed = true;
            }

            if (!changed)
                return;

            SaveSettings();
        }

        void TrackHistory()
        {
            if (_settings == null || !_settings.ChatHistory)
                return;

            var chat = GetComponent<ChatPanel>();
            if (chat == null || chat.TranscriptBusy || chat.TranscriptRevision == _historyRevision)
                return;

            SaveHistory();
        }

        void ApplyLive()
        {
            _applying = true;
            try
            {
                ApplyEffectiveSettings();
            }
            finally
            {
                _applying = false;
            }

            _personaRevision++;
            SaveSettings();
        }

        void ApplyEffectiveSettings()
        {
            _settings.Normalize();
            EffectiveSettings = PersonaRuntime.ResolveSelected(_settings, Folder());
            SettingsApplicator.Apply(EffectiveSettings, this);
            _settings.StartWithWindows = EffectiveSettings.StartWithWindows;
        }

        void SaveSettings()
        {
            if (_setupGate || _settings == null)
                return;

            var path = SettingsLocations.SettingsFile(Folder());
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                CompanionSettingsStore.Save(path, _settings);
            }
            catch (Exception)
            {
                SetStatus("Could not save settings on this PC.");
            }
        }

        void LoadHistory()
        {
            var path = SettingsLocations.HistoryFile(Folder(), _settings != null ? _settings.PersonaId : string.Empty);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            var chat = GetComponent<ChatPanel>();
            if (chat == null)
                return;

            var read = ProtectedLocalFile.Read(path);
            if (read.Status == LocalReadStatus.Missing)
                return;
            if (read.Status != LocalReadStatus.Ready)
            {
                SetStatus("Saved chat history could not be read.");
                return;
            }

            try
            {
                chat.LoadTranscript(read.Text);
            }
            catch (Exception)
            {
                SetStatus("Saved chat history could not be read.");
            }
        }

        void SaveHistory()
        {
            if (_settings == null || !_settings.ChatHistory)
                return;

            var chat = GetComponent<ChatPanel>();
            if (chat == null)
                return;

            var path = SettingsLocations.HistoryFile(Folder(), _settings != null ? _settings.PersonaId : string.Empty);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (!ProtectedLocalFile.Write(path, chat.ExportTranscript()))
                {
                    SetStatus("Could not save chat history.");
                    return;
                }

                _historyRevision = chat.TranscriptRevision;
            }
            catch (Exception)
            {
                SetStatus("Could not save chat history.");
            }
        }

        static string Folder()
        {
            return SettingsLocations.Folder(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.persistentDataPath);
        }

        static SettingsLegacyPrefs ReadLegacy()
        {
            var legacy = new SettingsLegacyPrefs();
            if (PlayerPrefs.HasKey(TrayPreferences.HideTaskbarKey))
            {
                legacy.HasHideTaskbar = true;
                legacy.HideTaskbar = PlayerPrefs.GetInt(TrayPreferences.HideTaskbarKey, 1) != 0;
            }

            if (PlayerPrefs.HasKey(OverlayWindowSize.ScalePrefKey))
            {
                legacy.HasScale = true;
                legacy.Scale = PlayerPrefs.GetFloat(OverlayWindowSize.ScalePrefKey, 1f);
            }

            if (PlayerPrefs.HasKey(VoiceSpeaker.MutePref))
            {
                legacy.HasMuted = true;
                legacy.Muted = PlayerPrefs.GetInt(VoiceSpeaker.MutePref, 0) != 0;
            }

            if (PlayerPrefs.HasKey(TrayPreferences.PauseKey))
            {
                legacy.HasPaused = true;
                legacy.Paused = PlayerPrefs.GetInt(TrayPreferences.PauseKey, 0) != 0;
            }

            return legacy;
        }

        bool WizardIsOpen()
        {
            var wizard = GetComponent<SetupWizard>();
            return wizard != null && wizard.IsOpen;
        }

        void RemoveKey()
        {
            try
            {
                ApiKeyFile.Delete(ApiKeyFile.PreferredPath());
                SetStatus("Saved key removed. Restart without XAI_API_KEY if that variable is set.");
                SettingsRevision++;
            }
            catch (Exception)
            {
                SetStatus("Could not remove the saved key.");
            }
        }

        void ConfirmClear()
        {
            var path = SettingsLocations.HistoryFile(Folder(), _settings != null ? _settings.PersonaId : string.Empty);
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
                SetStatus("Could not delete the chat history file.");
                return;
            }

            var chat = GetComponent<ChatPanel>();
            if (chat != null)
                chat.ClearTranscript();
            GreetIfEmpty();
            _historyRevision = chat != null ? chat.TranscriptRevision : 0;
            SetStatus("Chat history cleared. The API key was kept.");
            SettingsRevision++;
        }


        public void TestConfiguredConnection(bool ollama)
        {
            if (_probeBusy)
                return;
            if (!ollama && !OutboundPrivacy.AllowsCloudChat(_settings.PrivateMode))
            {
                SetStatus("Switch to Public mode to test the cloud connection.");
                return;
            }
            BeginConnectionProbe(ollama ? ChatDefaults.LocalId : ChatDefaults.GrokId);
        }

        void BeginConnectionProbe(string providerId)
        {
            _probeSerial++;
            _probeBusy = true;
            SetStatus("Testing connection...");
            SettingsRevision++;
            StartCoroutine(Probe(_probeSerial, providerId));
        }

        IEnumerator Probe(int serial, string providerId)
        {
            var plan = ConnectionProbe.Plan(_settings.PrivateMode, providerId,
                string.IsNullOrWhiteSpace(_settings.BaseUrl) ? ChatDefaults.BaseUrl : _settings.BaseUrl,
                _settings.LocalBaseUrl);
            SetStatus(plan.Summary);
            if (!plan.Allowed)
            {
                FinishProbe();
                yield break;
            }

            string key = null;
            if (plan.SendAuthorization)
            {
                var source = new EnvironmentApiKeySource();
                string resolved;
                if (!source.TryGet(out resolved) || string.IsNullOrWhiteSpace(resolved))
                {
                    SetStatus(ApiKeyMask.MissingLabel + ". Save a key before testing the cloud AI API.");
                    FinishProbe();
                    yield break;
                }

                key = resolved;
            }

            using (var request = UnityWebRequest.Get(plan.Url))
            {
                request.timeout = 20;
                var headers = ConnectionProbe.AuthHeaders(plan, key);
                for (var h = 0; h < headers.Count; h++)
                    request.SetRequestHeader(headers[h].Key, headers[h].Value);
                key = null;
                yield return request.SendWebRequest();
                if (serial != _probeSerial)
                    yield break;

                var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                var status = ConnectionProbe.FormatResult((int)request.responseCode, request.error, body);
                SetStatus(status);
                Debug.Log(status);
            }

            FinishProbe();
        }

        void FinishProbe()
        {
            _probeBusy = false;
            SettingsRevision++;
        }

        static bool FileHasKey()
        {
            return SetupWizardGate.HasSavedApiKey();
        }

        static bool EnvOverrides()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyResolver.EnvName));
        }

        void SetStatus(string message)
        {
            _statusMessage = message ?? string.Empty;
        }

    }

    static class SettingsInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var hosts = UnityEngine.Object.FindObjectsByType<AppBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < hosts.Length; i++)
            {
                if (hosts[i] == null)
                    continue;
                var host = hosts[i].gameObject;
                if (host.GetComponent<SettingsPanel>() == null)
                    host.AddComponent<SettingsPanel>();
                if (host.GetComponent<ScreenLookController>() == null)
                    host.AddComponent<ScreenLookController>();
                // JUH-47: the game companion; idle until it is turned on in the Games section.
                if (host.GetComponent<GameCompanionController>() == null)
                    host.AddComponent<GameCompanionController>();
                if (host.GetComponent<SetupWizard>() == null)
                    host.AddComponent<SetupWizard>();
            }
        }
    }
}
