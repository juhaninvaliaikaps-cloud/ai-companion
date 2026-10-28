using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AICompanion.AI;
using AICompanion.Avatar;
using AICompanion.Core;
using AICompanion.Overlay;
using AICompanion.UI;
using AICompanion.Voice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    /// <summary>
    /// In-window settings. A second OS window fights the layered transparent
    /// overlay. This panel is ordinary uGUI, so the existing hit test keeps it
    /// clickable while the rest of the window still clicks through. Opening it
    /// does not restyle the HWND: transparency, topmost, and the click-through
    /// mode stay as they were.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed partial class SettingsPanel : MonoBehaviour, ICompanionStartup, IDebugPanel, IOverlayKeepAwake, IPrivacyMode
    {
        enum Tab
        {
            Ai = 0,
            Avatar = 1,
            Voice = 2,
            Controls = 3,
            Overlay = 4,
            Privacy = 5
        }

        static readonly string[] TabLabels = { "AI", "Avatar", "Voice", "Controls", "Overlay", "Privacy" };

        readonly List<InputField> _fields = new List<InputField>();
        readonly List<Image> _tabImages = new List<Image>();

        CompanionSettings _settings = CompanionSettings.CreateDefault();
        CompanionTray _tray;
        AvatarMount _mount;
        Canvas _canvas;
        GameObject _canvasObject;
        RectTransform _panelRect;
        RectTransform _body;
        Text _status;
        Font _font;
        Sprite _white;
        Texture2D _whiteTexture;
        InputField _keyInput;
        Tab _tab = Tab.Ai;
        Rect _guiRect;
        string _statusMessage = string.Empty;
        int _historyRevision = -1;
        int _focusClaims;
        int _probeSerial;
        int _fieldGeneration;
        bool _open;
        bool _started;
        bool _applying;
        bool _building;
        bool _rebuild;
        bool _confirmClear;
        bool _probeBusy;
        bool _setupGate;
        bool _wizardClaimed;

        public event Action SetupReplayRequested;

        public CompanionSettings CurrentSettings => _settings;

        public bool PrivateMode => _settings != null && _settings.PrivateMode;

        public Rect InteractiveRect => _open ? _guiRect : new Rect(0f, 0f, 0f, 0f);

        public bool KeepFrameRateActive => _open || _focusClaims > 0;

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            var path = SettingsLocations.SettingsFile(Folder());
            var exists = !string.IsNullOrEmpty(path) && File.Exists(path);
            _settings = CompanionSettingsStore.Load(path, exists ? SettingsLegacyPrefs.None : ReadLegacy());
            Subscribe();
            var tray = GetComponent<CompanionTray>();
            if (!exists && tray != null)
                _settings.StartWithWindows = tray.Menu.StartWithWindows;

            _applying = true;
            try
            {
                SettingsApplicator.Apply(_settings, this);
                if (_settings.ChatHistory)
                    LoadHistory();
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
            BuildChrome();
            Close();
        }

        void Start()
        {
            StartCompanion();
        }

        void OnDisable()
        {
            ReleaseFocus();
        }

        void OnDestroy()
        {
            ReleaseFocus();
            if (_settings != null && _settings.ChatHistory)
                SaveHistory();
            if (_tray != null)
            {
                _tray.Menu.SettingsRequested -= Open;
                _tray.TrayStateChanged -= OnTrayState;
            }

            if (_mount != null)
                _mount.PreferenceChanged -= OnAvatar;
            if (_white != null)
                Destroy(_white);
            if (_whiteTexture != null)
                Destroy(_whiteTexture);
        }

        void Update()
        {
            if (!_started)
                return;

            if (Input.GetKeyDown(HotkeyCatalog.SettingsKey) && !DebugInputGuard.IsBlocked(HotkeyCatalog.SettingsKey))
            {
                if (_open)
                    Close();
                else
                    Open();
            }

            if (!_open)
            {
                TrackLive();
                TrackHistory();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_confirmClear)
                {
                    _confirmClear = false;
                    _rebuild = true;
                }
                else if (AnyFieldFocused())
                {
                    BlurFields();
                }
                else
                {
                    Close();
                }
            }

            SyncFocus(AnyFieldFocused());
            TrackLive();
            TrackHistory();
            Place();
        }

        void LateUpdate()
        {
            if (_setupGate && !_wizardClaimed)
            {
                _setupGate = false;
                SaveSettings();
            }

            if (!_rebuild || !_open)
                return;
            _rebuild = false;
            RebuildBody();
            RefreshTabs();
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

            if (_tray != null)
                _tray.ShowCompanion();
            _open = true;
            if (_canvasObject != null)
                _canvasObject.SetActive(true);
            var window = GetComponent<OverlayWindow>();
            if (window != null)
                window.FocusForTyping();
            _rebuild = true;
            Place();
        }

        public void Close()
        {
            _open = false;
            _confirmClear = false;
            _probeSerial++;
            _probeBusy = false;
            if (_keyInput != null)
                _keyInput.text = string.Empty;
            BlurFields();
            ReleaseFocus();
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
            _guiRect = new Rect(0f, 0f, 0f, 0f);
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
            if (_open && _tab == Tab.Overlay)
                _rebuild = true;
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
            if (_open && _tab == Tab.Avatar)
                _rebuild = true;
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
            if (_open && !AnyFieldFocused() && (_tab == Tab.Overlay || _tab == Tab.Voice))
                _rebuild = true;
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
            if (_building)
                return;

            _applying = true;
            try
            {
                SettingsApplicator.Apply(_settings, this);
            }
            finally
            {
                _applying = false;
            }

            SaveSettings();
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
            var path = SettingsLocations.HistoryFile(Folder());
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            var chat = GetComponent<ChatPanel>();
            if (chat == null)
                return;

            try
            {
                chat.LoadTranscript(File.ReadAllText(path));
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

            var path = SettingsLocations.HistoryFile(Folder());
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, chat.ExportTranscript(), new UTF8Encoding(false));
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

        void BuildChrome()
        {
            _font = BuiltinFont();
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTexture.hideFlags = HideFlags.HideAndDontSave;
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
            _white = Sprite.Create(_whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _white.hideFlags = HideFlags.HideAndDontSave;

            _canvasObject = new GameObject("SettingsCanvas");
            _canvasObject.transform.SetParent(transform, false);
            _canvasObject.hideFlags = HideFlags.DontSave;
            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.sortingOrder = 80;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var panel = new GameObject("SettingsPanel");
            panel.transform.SetParent(_canvasObject.transform, false);
            _panelRect = panel.AddComponent<RectTransform>();
            var image = panel.AddComponent<Image>();
            image.sprite = _white;
            image.color = new Color(0.10f, 0.09f, 0.14f, 0.96f);
            image.raycastTarget = true;

            var title = CreateText(panel.transform, _font, 16, TextAnchor.MiddleLeft, AppName.Display);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(10f, -4f);
            title.rectTransform.sizeDelta = new Vector2(-80f, 24f);
            title.raycastTarget = false;

            var close = CreateButton(panel.transform, "Close", Close);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-8f, -6f);
            closeRect.sizeDelta = new Vector2(64f, 22f);

            for (var i = 0; i < TabLabels.Length; i++)
            {
                var index = i;
                var tab = CreateButton(panel.transform, TabLabels[i], () =>
                {
                    _tab = (Tab)index;
                    _confirmClear = false;
                    _rebuild = true;
                });
                var rect = tab.GetComponent<RectTransform>();
                var col = i % 3;
                var row = i / 3;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(8f + col * 146f, -34f - row * 26f);
                rect.sizeDelta = new Vector2(140f, 24f);
                _tabImages.Add(tab.GetComponent<Image>());
            }

            var body = new GameObject("Body");
            body.transform.SetParent(panel.transform, false);
            var bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(8f, 28f);
            bodyRect.offsetMax = new Vector2(-8f, -90f);
            var scroll = body.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(body.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            Stretch(viewportRect);
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.sprite = _white;
            viewportImage.color = new Color(0.08f, 0.07f, 0.11f, 0.2f);
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = viewportRect;
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _body = content.AddComponent<RectTransform>();
            _body.anchorMin = new Vector2(0f, 1f);
            _body.anchorMax = new Vector2(1f, 1f);
            _body.pivot = new Vector2(0.5f, 1f);
            _body.anchoredPosition = Vector2.zero;
            scroll.content = _body;

            _status = CreateText(panel.transform, _font, 12, TextAnchor.MiddleLeft, string.Empty);
            _status.rectTransform.anchorMin = new Vector2(0f, 0f);
            _status.rectTransform.anchorMax = new Vector2(1f, 0f);
            _status.rectTransform.pivot = new Vector2(0.5f, 0f);
            _status.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            _status.rectTransform.sizeDelta = new Vector2(-16f, 22f);
            _status.raycastTarget = false;
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void RebuildBody()
        {
            if (_body == null)
                return;

            _building = true;
            _fieldGeneration++;
            for (var i = _body.childCount - 1; i >= 0; i--)
                Destroy(_body.GetChild(i).gameObject);
            _fields.Clear();
            _keyInput = null;
            _y = 4f;
            switch (_tab)
            {
                case Tab.Avatar:
                    BuildAvatar();
                    break;
                case Tab.Voice:
                    BuildVoice();
                    break;
                case Tab.Controls:
                    BuildControls();
                    break;
                case Tab.Overlay:
                    BuildOverlay();
                    break;
                case Tab.Privacy:
                    BuildPrivacy();
                    break;
                default:
                    BuildAi();
                    break;
            }

            _body.sizeDelta = new Vector2(0f, _y + 8f);
            if (_status != null)
                _status.text = _statusMessage ?? string.Empty;
            _building = false;
        }

        float _y;

        void BuildAi()
        {
            Note("Text changes apply when you leave the field. The API key is stored in the secret store, never in settings.json.");
            var grok = _settings.ProviderId == ChatDefaults.GrokId;
            AddButton(grok ? "Provider: Grok (selected)" : "Use Grok", () =>
            {
                _settings.ProviderId = ChatDefaults.GrokId;
                ApplyLive();
                _rebuild = true;
            });
            AddButton(!grok ? "Provider: local Ollama (selected)" : "Use local Ollama", () =>
            {
                _settings.ProviderId = ChatDefaults.LocalId;
                ApplyLive();
                _rebuild = true;
            });
            if (_settings.PrivateMode)
                Note("Private mode is on. Chat goes to the local model only. This provider is remembered for when private mode is off.");
            AddField("Grok model", _settings.Model, false, value =>
            {
                _settings.Model = value;
                ApplyLive();
            });
            AddField("Local model", _settings.LocalModel, false, value =>
            {
                _settings.LocalModel = value;
                ApplyLive();
            });
            AddField("Local address", _settings.LocalBaseUrl, false, value =>
            {
                _settings.LocalBaseUrl = value;
                ApplyLive();
                _rebuild = true;
            });
            Note("Default local address is Ollama at http://127.0.0.1:11434/v1.");
            AddField("Timeout seconds", _settings.TimeoutSeconds.ToString("0.###"), false, value =>
            {
                float parsed;
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    _settings.TimeoutSeconds = parsed;
                ApplyLive();
                _rebuild = true;
            });
            Note(ApiKeyMask.Label(FileHasKey()) + ".");
            _keyInput = AddField("Paste a new API key", string.Empty, true, _ => { });
            AddButton("Save key", SaveKey);
            AddButton("Remove saved key", RemoveKey);
            if (EnvOverrides())
                Note("XAI_API_KEY is set and overrides the saved key. Restart the app without that variable to use the saved key.");
            AddButton(_probeBusy ? "Testing..." : "Test connection", () =>
            {
                if (!_probeBusy)
                    TestConnection();
            });
            Note("Test connection does not write the key or the Authorization header to the log.");
        }

        void BuildAvatar()
        {
            Note("The source applies immediately. Asset Store, Mixamo, and Daz models stay out of this project.");
            AddSourceButton("Auto", AvatarSourceNames.Auto);
            AddSourceButton("Local prefab", AvatarSourceNames.Local);
            AddSourceButton("VRM sample", AvatarSourceNames.Vrm);
            AddSourceButton("FBX sample", AvatarSourceNames.Fbx);
            BuildPortraitToggle();
            AddButton("Reset rotation", () =>
            {
                var mount = GetComponent<AvatarMount>();
                if (mount != null)
                    mount.ResetRotation();
                SetStatus("Rotation reset.");
            });
            Note("Reset faces the current avatar toward the camera. It applies immediately.");
            AddToggle("Look at cursor", _settings.CursorLookAt, value => _settings.CursorLookAt = value);
            Note("Eyes and head follow the desktop cursor. They ease back when it is still or far away.");
        }

        void BuildVoice()
        {
            AddToggle("Speak replies", _settings.TtsEnabled, value => _settings.TtsEnabled = value);
            AddField("Voice", _settings.VoiceId, false, value =>
            {
                _settings.VoiceId = value;
                ApplyLive();
            });
            AddField("TTS language", _settings.TtsLanguage, false, value =>
            {
                _settings.TtsLanguage = value;
                ApplyLive();
            });
            var volumeLabel = AddLabel("Volume " + Mathf.RoundToInt(_settings.Volume * 100f) + "%");
            AddSlider(_settings.Volume, 0f, 1f, false, value =>
            {
                _settings.Volume = value;
                volumeLabel.text = "Volume " + Mathf.RoundToInt(value * 100f) + "%";
                ApplyLive();
            });
            AddToggle("Muted", _settings.Muted, value => _settings.Muted = value);
            Note("Mute is the same switch as F6. It applies immediately.");
            AddToggle("Push-to-talk", _settings.SttEnabled, value => _settings.SttEnabled = value);
            AddField("Push-to-talk key", _settings.PushToTalkKey, false, value =>
            {
                _settings.PushToTalkKey = value;
                ApplyLive();
                _rebuild = true;
            });
            Note("Taken keys (F6, F7, F8, F10, F12, T, R, Esc) stay on V. The new key applies immediately.");
            AddField("Whisper model", _settings.WhisperModel, false, value =>
            {
                _settings.WhisperModel = value;
                ApplyLive();
                SetStatus("The Whisper model applies on the next recording. The .bin file is not part of the app.");
            });
            AddField("Speech language", _settings.SttLanguage, false, value =>
            {
                _settings.SttLanguage = value;
                ApplyLive();
            });
            Note("Placeholder. Wake word is not in this build.");
            Note("Placeholder. There is no local TTS voice yet. Private mode keeps replies off cloud TTS.");
            Note("Placeholder. The voice agent is not in this build. Private mode blocks it.");
        }

        void BuildControls()
        {
            Note(HotkeyCatalog.RebindNote);
            var entries = HotkeyCatalog.Current(_settings.PushToTalkKey);
            for (var i = 0; i < entries.Length; i++)
                AddLabel(entries[i].Keys + "   " + entries[i].Action);
        }

        void BuildOverlay()
        {
            AddToggle("Always on top", _settings.AlwaysOnTop, value => _settings.AlwaysOnTop = value);
            Note("F8 still toggles this. It applies immediately and does not change transparency.");
            var sizeLabel = AddLabel("Size " + Mathf.RoundToInt(_settings.Scale * 100f) + "%");
            AddSlider(_settings.Scale, OverlayWindowSize.MinScale, OverlayWindowSize.MaxScale, false, value =>
            {
                _settings.Scale = value;
                sizeLabel.text = "Size " + Mathf.RoundToInt(value * 100f) + "%";
                ApplyLive();
            });
            Note("Applies immediately. The Editor Game view does not grow. The Windows player resizes the transparent window.");
            AddButton("Click-through: " + ClickThroughLabel(_settings.ClickThrough), () =>
            {
                var mode = OverlayClickThroughPolicy.Parse(_settings.ClickThrough);
                var next = mode == OverlayClickThrough.Silhouette
                    ? OverlayClickThrough.PanelsOnly
                    : mode == OverlayClickThrough.PanelsOnly
                        ? OverlayClickThrough.Capture
                        : OverlayClickThrough.Silhouette;
                _settings.ClickThrough = OverlayClickThroughPolicy.Name(next);
                ApplyLive();
                _rebuild = true;
            });
            Note(ClickThroughHelp(_settings.ClickThrough));
            var idleLabel = AddLabel("Idle frame rate " + _settings.IdleFrameRate);
            AddSlider(_settings.IdleFrameRate, 1f, OverlayFrameRate.Active, true, value =>
            {
                _settings.IdleFrameRate = Mathf.RoundToInt(value);
                idleLabel.text = "Idle frame rate " + _settings.IdleFrameRate;
                ApplyLive();
            });
            Note("Applies immediately, after the pointer has been outside the window for a few seconds.");
            AddToggle("Hide taskbar button", _settings.HideTaskbarButton, value => _settings.HideTaskbarButton = value);
            Note("The Windows player hides the button after the window handle locks. The Editor has no tray icon.");
            AddToggle("Start with Windows", _settings.StartWithWindows, value => _settings.StartWithWindows = value);
            Note("Saved now. The Windows player writes the Run key. It takes effect the next time you sign in. The Editor cannot write that key.");
            AddToggle("Pause", _settings.Paused, value => _settings.Paused = value);
            Note("Pause matches the tray checkmark. Speech, push-to-talk, and animation cues stay off while it is on.");
        }

        void BuildPrivacy()
        {
            if (_confirmClear)
            {
                Note("Clear saved chat history on this PC? The API key and settings stay.");
                AddButton("Confirm clear", ConfirmClear);
                AddButton("Cancel", () =>
                {
                    _confirmClear = false;
                    _rebuild = true;
                });
                return;
            }

            AddToggle("Save chat history", _settings.ChatHistory, value =>
            {
                _settings.ChatHistory = value;
                if (value)
                    SaveHistory();
            });
            Note("Off keeps the transcript for this session only. On writes it under ProjectLeia, outside the repo.");
            AddToggle("Private mode", _settings.PrivateMode, value => _settings.PrivateMode = value);
            Note("Private mode sends chat only to the local Ollama model. Cloud TTS, the voice agent, and Uthana do not receive that content. The default is off.");
            AddButton("Clear chat history", () =>
            {
                _confirmClear = true;
                _rebuild = true;
            });
            AddButton("Run setup again", () =>
            {
                if (SetupReplayRequested != null)
                    SetupReplayRequested();
            });
            Note("Opens the first-run setup again in this window. Skip on a step uses that step's default. Finish saves. Transparency, topmost, and click-through stay as they are.");
            BuildMemorySection();
        }

        bool WizardIsOpen()
        {
            var wizard = GetComponent<SetupWizard>();
            return wizard != null && wizard.IsOpen;
        }

        void AddSourceButton(string label, string source)
        {
            var selected = _settings.AvatarSource == source;
            AddButton(selected ? label + " (selected)" : label, () =>
            {
                _settings.AvatarSource = source;
                ApplyLive();
                _rebuild = true;
            });
        }

        void AddToggle(string label, bool value, Action<bool> changed)
        {
            AddButton(label + ": " + (value ? "on" : "off"), () =>
            {
                changed(!value);
                ApplyLive();
                _rebuild = true;
            });
        }

        void SaveKey()
        {
            var key = _keyInput != null ? (_keyInput.text ?? string.Empty).Trim() : string.Empty;
            if (key.Length == 0)
            {
                SetStatus("Enter a key to save. A saved key stays masked.");
                return;
            }

            try
            {
                ApiKeyFile.SavePreferred(key);
                if (_keyInput != null)
                    _keyInput.text = string.Empty;
                SetStatus(ApiKeyMask.SavedLabel + ". It is in the secret store, not in settings.");
                _rebuild = true;
            }
            catch (Exception)
            {
                SetStatus("Could not save the API key.");
            }
        }

        void RemoveKey()
        {
            try
            {
                ApiKeyFile.Delete(ApiKeyFile.PreferredPath());
                if (_keyInput != null)
                    _keyInput.text = string.Empty;
                SetStatus("Saved key removed. Restart without XAI_API_KEY if that variable is set.");
                _rebuild = true;
            }
            catch (Exception)
            {
                SetStatus("Could not remove the saved key.");
            }
        }

        void ConfirmClear()
        {
            var path = SettingsLocations.HistoryFile(Folder());
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
            _historyRevision = chat != null ? chat.TranscriptRevision : 0;
            _confirmClear = false;
            SetStatus("Chat history cleared. The API key was kept.");
            _rebuild = true;
        }

        void TestConnection()
        {
            _probeSerial++;
            _probeBusy = true;
            SetStatus("Testing connection...");
            _rebuild = true;
            StartCoroutine(Probe(_probeSerial));
        }

        IEnumerator Probe(int serial)
        {
            var plan = ConnectionProbe.Plan(_settings.PrivateMode, _settings.ProviderId, ChatDefaults.BaseUrl, _settings.LocalBaseUrl);
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
                    SetStatus(ApiKeyMask.MissingLabel + ". Save a key before testing Grok.");
                    FinishProbe();
                    yield break;
                }

                key = resolved;
            }

            using (var request = UnityWebRequest.Get(plan.Url))
            {
                request.timeout = 20;
                if (plan.SendAuthorization && !string.IsNullOrEmpty(key))
                    request.SetRequestHeader("Authorization", "Bearer " + key);
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
            if (_open)
                _rebuild = true;
        }

        static bool FileHasKey()
        {
            return SetupWizardGate.HasSavedApiKey();
        }

        static bool EnvOverrides()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyResolver.EnvName));
        }

        static string ClickThroughLabel(string value)
        {
            var mode = OverlayClickThroughPolicy.Parse(value);
            switch (mode)
            {
                case OverlayClickThrough.PanelsOnly:
                    return "panels only";
                case OverlayClickThrough.Capture:
                    return "capture the window";
                default:
                    return "silhouette";
            }
        }

        static string ClickThroughHelp(string value)
        {
            var mode = OverlayClickThroughPolicy.Parse(value);
            switch (mode)
            {
                case OverlayClickThrough.PanelsOnly:
                    return "Only panels catch clicks. The avatar clicks through. Applies immediately.";
                case OverlayClickThrough.Capture:
                    return "The whole window catches clicks. Applies immediately. Transparency stays.";
                default:
                    return "Empty space clicks through. The avatar and panels stay clickable. This is the default.";
            }
        }

        void SetStatus(string message)
        {
            _statusMessage = message ?? string.Empty;
            if (_status != null)
                _status.text = _statusMessage;
        }

        void Place()
        {
            var width = Mathf.Min(448f, Mathf.Max(280f, Screen.width - 16f));
            var height = Mathf.Min(560f, Mathf.Max(240f, Screen.height - 128f));
            _guiRect = new Rect(8f, 116f, width, height);
            if (_panelRect == null)
                return;

            _panelRect.anchorMin = new Vector2(0f, 1f);
            _panelRect.anchorMax = new Vector2(0f, 1f);
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.anchoredPosition = new Vector2(_guiRect.x, -_guiRect.y);
            _panelRect.sizeDelta = new Vector2(_guiRect.width, _guiRect.height);
            var camera = Camera.main;
            if (_canvas == null)
                return;
            if (camera == null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = camera;
            _canvas.planeDistance = 0.35f;
        }

        bool AnyFieldFocused()
        {
            for (var i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != null && _fields[i].isFocused)
                    return true;
            }

            return false;
        }

        void BlurFields()
        {
            for (var i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != null)
                    _fields[i].DeactivateInputField();
            }

            ReleaseFocus();
        }

        void SyncFocus(bool focused)
        {
            if (focused && _focusClaims == 0)
            {
                DebugInputGuard.ClaimTextField();
                _focusClaims = 1;
            }
            else if (!focused && _focusClaims > 0)
            {
                ReleaseFocus();
            }
        }

        void ReleaseFocus()
        {
            if (_focusClaims <= 0)
                return;
            DebugInputGuard.ReleaseTextField();
            _focusClaims = 0;
        }

        void RefreshTabs()
        {
            for (var i = 0; i < _tabImages.Count; i++)
            {
                if (_tabImages[i] == null)
                    continue;
                _tabImages[i].color = i == (int)_tab
                    ? new Color(0.55f, 0.32f, 0.62f, 1f)
                    : new Color(0.28f, 0.22f, 0.36f, 1f);
            }
        }

        Text AddLabel(string text)
        {
            var label = CreateText(_body, _font, 14, TextAnchor.MiddleLeft, text);
            label.raycastTarget = false;
            Place(_labelRect(label), 22f);
            return label;
        }

        RectTransform _labelRect(Text label)
        {
            return label.rectTransform;
        }

        void Note(string text)
        {
            var label = CreateText(_body, _font, 12, TextAnchor.UpperLeft, text);
            label.fontStyle = FontStyle.Italic;
            label.color = new Color(0.80f, 0.76f, 0.86f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var height = text != null && text.Length > 78 ? 34f : 20f;
            Place(label.rectTransform, height);
        }

        void AddButton(string label, Action action)
        {
            var button = CreateButton(_body, label, action);
            Place(button.GetComponent<RectTransform>(), 26f);
        }

        InputField AddField(string placeholder, string value, bool password, Action<string> onEnd)
        {
            var field = new GameObject(password ? "Secret" : "Field");
            field.transform.SetParent(_body, false);
            var rect = field.AddComponent<RectTransform>();
            Place(rect, 26f);
            var image = field.AddComponent<Image>();
            image.sprite = _white;
            image.color = new Color(0.18f, 0.16f, 0.22f, 1f);
            var input = field.AddComponent<InputField>();
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.targetGraphic = image;
            var text = CreateText(field.transform, _font, 14, TextAnchor.MiddleLeft, string.Empty);
            Stretch(text.rectTransform, 6f, 2f, 6f, 2f);
            text.supportRichText = false;
            var hint = CreateText(field.transform, _font, 13, TextAnchor.MiddleLeft, placeholder);
            Stretch(hint.rectTransform, 6f, 2f, 6f, 2f);
            hint.fontStyle = FontStyle.Italic;
            hint.color = new Color(0.75f, 0.70f, 0.78f, 0.9f);
            hint.supportRichText = false;
            input.textComponent = text;
            input.placeholder = hint;
            input.text = password ? string.Empty : (value ?? string.Empty);
            var generation = _fieldGeneration;
            input.onEndEdit.AddListener(textValue =>
            {
                if (_building || generation != _fieldGeneration || onEnd == null)
                    return;
                onEnd(textValue ?? string.Empty);
            });
            _fields.Add(input);
            return input;
        }

        void AddSlider(float value, float min, float max, bool whole, Action<float> changed)
        {
            var sliderObject = new GameObject("Slider");
            sliderObject.transform.SetParent(_body, false);
            var rect = sliderObject.AddComponent<RectTransform>();
            Place(rect, 18f);
            var background = sliderObject.AddComponent<Image>();
            background.sprite = _white;
            background.color = new Color(0.20f, 0.18f, 0.24f, 1f);
            var slider = sliderObject.AddComponent<Slider>();
            var handleObject = new GameObject("Handle");
            handleObject.transform.SetParent(sliderObject.transform, false);
            var handleRect = handleObject.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(16f, 18f);
            var handleImage = handleObject.AddComponent<Image>();
            handleImage.sprite = _white;
            handleImage.color = new Color(0.92f, 0.88f, 0.96f, 1f);
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.value = Mathf.Clamp(value, min, max);
            var generation = _fieldGeneration;
            slider.onValueChanged.AddListener(next =>
            {
                if (_building || generation != _fieldGeneration || changed == null)
                    return;
                changed(next);
            });
        }

        void Place(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -_y);
            rect.sizeDelta = new Vector2(0f, height);
            _y += height + 6f;
        }

        GameObject CreateButton(Transform parent, string label, Action action)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            buttonObject.AddComponent<RectTransform>();
            var image = buttonObject.AddComponent<Image>();
            image.sprite = _white;
            image.color = new Color(0.36f, 0.28f, 0.46f, 1f);
            image.raycastTarget = true;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                if (action != null)
                    action();
            });
            var text = CreateText(buttonObject.transform, _font, 13, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform, 4f, 1f, 4f, 1f);
            text.raycastTarget = false;
            return buttonObject;
        }

        static Text CreateText(Transform parent, Font font, int size, TextAnchor anchor, string value)
        {
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            textObject.AddComponent<RectTransform>();
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = value ?? string.Empty;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static void Stretch(RectTransform rect)
        {
            Stretch(rect, 0f, 0f, 0f, 0f);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var system = new GameObject("EventSystem");
            system.hideFlags = HideFlags.DontSave;
            system.AddComponent<EventSystem>();
            system.AddComponent<StandaloneInputModule>();
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
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
                if (host.GetComponent<SetupWizard>() == null)
                    host.AddComponent<SetupWizard>();
            }
        }
    }
}
