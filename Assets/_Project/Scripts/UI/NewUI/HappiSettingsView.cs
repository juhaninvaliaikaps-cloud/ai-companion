using System;
using System.Collections.Generic;
using System.Globalization;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Overlay;
using AICompanion.ScreenLook;
using AICompanion.Settings;
using AICompanion.Settings.Presenters;
using AICompanion.UI;
using AICompanion.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-104 PR E: the new settings view in the Happi theme. Every item of
    /// docs/ui-overhaul/INVENTORY.md section 4 is here, on the same tabs as the old panel plus a
    /// "Happi" tab for the new UI's own options. Every change goes through the PR B presenters
    /// and the old panel as host, so settings are applied and saved exactly as before.
    /// "Classic settings" opens the old panel at any time. 18+ items appear under the same
    /// conditions as today. Exists only while the new UI is on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HappiSettingsView : MonoBehaviour, IOverlayKeepAwake
    {
        enum Tab
        {
            Happi,
            Ai,
            Avatar,
            Voice,
            Controls,
            Overlay,
            Privacy,
            Persona,
            Screen,
        }

        static readonly string[] TabLabels = { "Happi", "AI", "Avatar", "Voice", "Controls", "Overlay", "Privacy", "Persona", "Screen" };
        const float PlaneDistance = 0.36f;

        SettingsPanel _settings;
        ChatPanel _chat;
        HappiTheme _theme;
        GameObject _canvasObject;
        Canvas _canvas;
        RectTransform _panel;
        TextMeshProUGUI _title;
        TextMeshProUGUI _status;
        HappiButton _close;
        HappiButton _classic;
        readonly List<HappiButton> _tabs = new List<HappiButton>();
        ScrollRect _scroll;
        RectTransform _viewport;
        RectTransform _body;
        RectTransform _frameRoot;
        readonly List<TMP_InputField> _fields = new List<TMP_InputField>();
        readonly PrivacyConfirm _confirm = new PrivacyConfirm();
        Tab _tab = Tab.Happi;
        bool _attached;
        bool _open;
        bool _rebuild;
        bool _building;
        bool _focusClaimed;
        bool _framePreview;
        bool _frameFit = true;
        float _y;
        float _s = 1f;
        float _width;
        string _exportFolder = string.Empty;
        string _signature;
        TextMeshProUGUI _videoStatus;
        float _layoutW = -1f;
        float _layoutH = -1f;

        public bool IsOpen => _open;

        public bool KeepFrameRateActive => _attached && (_open || _focusClaimed);

        public void Attach(SettingsPanel settings, ChatPanel chat)
        {
            if (_attached || settings == null)
                return;
            _settings = settings;
            _chat = chat;
            _theme = HappiTheme.Current;
            BuildChrome();
            _settings.OpenOverride = OnOpenRequested;
            _attached = true;
            Close();
            Debug.Log("New UI: settings view on");
        }

        public void Detach()
        {
            if (!_attached)
                return;
            _attached = false;
            if (_settings != null && _settings.OpenOverride == (Func<bool>)OnOpenRequested)
                _settings.OpenOverride = null;
            ReleaseFocus();
            if (_canvasObject != null)
                Destroy(_canvasObject);
            _canvasObject = null;
            _open = false;
            Debug.Log("New UI: settings view off");
        }

        void OnDestroy()
        {
            Detach();
        }

        /// <summary>F12 toggles; the tray, the wheel and "Settings" buttons open.</summary>
        bool OnOpenRequested()
        {
            if (!_attached)
                return false;
            if (_open && Input.GetKeyDown(HotkeyCatalog.SettingsKey))
                Close();
            else
                Open();
            return true;
        }

        public void Open()
        {
            var tray = GetComponent<CompanionTray>();
            if (tray != null)
                tray.ShowCompanion();
            _open = true;
            _canvasObject.SetActive(true);
            var typing = GetComponent<ITypingFocus>();
            if (typing != null)
                typing.FocusForTyping();
            _rebuild = true;
        }

        public void Close()
        {
            _open = false;
            _confirm.Cancel();
            _framePreview = false;
            BlurFields();
            ReleaseFocus();
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
        }

        void Update()
        {
            if (!_attached || !_open || _canvasObject == null)
                return;

            HappiUiKit.FollowCamera(_canvas, PlaneDistance);
            Layout();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_framePreview)
                {
                    _framePreview = false;
                    _rebuild = true;
                }
                else if (_confirm.Escape())
                    _rebuild = true;
                else if (AnyFieldFocused())
                    BlurFields();
                else
                {
                    Close();
                    return;
                }
            }

            SyncFocus();
            var signature = Signature();
            // Not while a field has focus or a slider is being dragged (the Size slider changes the window).
            if (signature != _signature && !AnyFieldFocused() && !Input.GetMouseButton(0))
            {
                _signature = signature;
                _rebuild = true;
            }

            if (_videoStatus != null)
            {
                var video = GetComponent<VideoAvatar>();
                var text = HappiUiKit.Literal(video != null ? video.StatusText : "The video avatar is not running.");
                if (_videoStatus.text != text)
                    _videoStatus.text = text;
            }

            var status = _settings.StatusMessage;
            var shown = HappiUiKit.Literal(status);
            if (_status.text != shown)
                _status.text = shown;
        }

        void LateUpdate()
        {
            if (!_attached || !_open || !_rebuild)
                return;
            _rebuild = false;
            RebuildBody();
        }

        /// <summary>Live values that other places can change (F8, F6, the tray, Ctrl+scroll, the wheel, a screen look).</summary>
        string Signature()
        {
            var s = _settings.CurrentSettings;
            if (s == null)
                return string.Empty;
            var look = _settings.ScreenController;
            return s.AlwaysOnTop + "|" + Mathf.RoundToInt(s.Scale * 100f) + "|" + s.Muted + "|" + s.Paused + "|" + s.HideTaskbarButton + "|" + s.StartWithWindows
                + "|" + s.AvatarSource + "|" + s.AvatarMode + "|" + s.PersonaId + "|" + s.PrivateMode + "|" + s.ProviderId + "|" + IntimateExpressions.Active
                + "|" + (look != null ? look.Revision : 0) + "|" + _settings.ConnectionTestRunning + "|" + s.ShowMoodDebug + "|" + s.VoiceNarration;
        }

        // ---- Chrome ------------------------------------------------------------------------

        void BuildChrome()
        {
            _canvasObject = HappiUiKit.CreateCanvas(transform, "HappiSettingsCanvas", 80, out _canvas);
            var panel = HappiUiKit.Panel(_canvasObject.transform, "HappiSettings", _theme.PanelBackground, true);
            _panel = panel.rectTransform;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = _theme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            _title = HappiUiKit.Text(_panel, "Title", "Happi · Settings", _theme.FontTitle, _theme.Text, TextAlignmentOptions.MidlineLeft, true);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _close = HappiUiKit.Button(_panel, "Close", "×", _theme.FontBody, Close);
            _classic = HappiUiKit.Button(_panel, "Classic", "Classic settings", _theme.FontSmall, () => { Close(); _settings.OpenClassic(); });
            for (var i = 0; i < TabLabels.Length; i++)
            {
                var index = i;
                _tabs.Add(HappiUiKit.Button(_panel, "Tab " + TabLabels[i], TabLabels[i], _theme.FontSmall, () =>
                {
                    _tab = (Tab)index;
                    _confirm.Cancel();
                    _framePreview = false;
                    _rebuild = true;
                }));
            }

            var scrollImage = HappiUiKit.Panel(_panel, "Body", _theme.PanelInset, true);
            _scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;
            _viewport = HappiUiKit.Rect(scrollImage.transform, "Viewport");
            HappiUiKit.Stretch(_viewport, 6f, 6f, 6f, 6f);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = _viewport;
            _body = HappiUiKit.Rect(_viewport, "Content");
            _body.anchorMin = new Vector2(0f, 1f);
            _body.anchorMax = new Vector2(1f, 1f);
            _body.pivot = new Vector2(0.5f, 1f);
            _body.sizeDelta = new Vector2(0f, 8f);
            _body.anchoredPosition = Vector2.zero;
            _scroll.content = _body;
            _status = HappiUiKit.Text(_panel, "Status", string.Empty, _theme.FontSmall, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, false);
            _status.fontStyle = FontStyles.Italic;
        }

        void Layout()
        {
            if (Mathf.Approximately(_layoutW, Screen.width) && Mathf.Approximately(_layoutH, Screen.height))
                return;
            _layoutW = Screen.width;
            _layoutH = Screen.height;
            _s = Mathf.Min(_theme.UiScale(Screen.dpi), Mathf.Max(1f, Screen.width / 470f));
            var s = _s;
            var margin = 8f;
            var width = Mathf.Min(Screen.width - 2f * margin, _theme.SettingsWidth * s);
            var height = Mathf.Min(Screen.height - 2f * margin, _theme.SettingsMaxHeight * s);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(width, height);
            var pad = _theme.Padding * s;
            var gap = _theme.Spacing * s;
            var row = 26f * s;
            var inner = width - 2f * pad;
            _title.fontSize = _theme.FontTitle * s;
            HappiUiKit.Place(_close.Rect, width - pad - row, pad, row, row);
            var classicW = 118f * s;
            HappiUiKit.Place(_classic.Rect, width - pad - row - gap - classicW, pad, classicW, row);
            _classic.Label.fontSize = _theme.FontSmall * s;
            _close.Label.fontSize = _theme.FontBody * s;
            HappiUiKit.Place(_title.rectTransform, pad, pad, inner - classicW - row - 2f * gap, row);
            var y = pad + row + gap;
            const int columns = 3;
            var tabW = (inner - gap * (columns - 1)) / columns;
            for (var i = 0; i < _tabs.Count; i++)
            {
                var col = i % columns;
                var r = i / columns;
                HappiUiKit.Place(_tabs[i].Rect, pad + col * (tabW + gap), y + r * (row + gap * 0.5f), tabW, row);
                _tabs[i].Label.fontSize = _theme.FontSmall * s;
            }

            y += ((_tabs.Count + columns - 1) / columns) * (row + gap * 0.5f) + gap * 0.5f;
            var statusH = 20f * s;
            HappiUiKit.Place((RectTransform)_scroll.transform, pad, y, inner, height - y - pad - statusH - gap);
            HappiUiKit.Place(_status.rectTransform, pad, height - pad - statusH, inner, statusH);
            _status.fontSize = _theme.FontSmall * s;
            _width = inner - 12f;
            _rebuild = true;
        }

        // ---- Body --------------------------------------------------------------------------

        void RebuildBody()
        {
            if (_body == null)
                return;
            _building = true;
            BlurFields();
            for (var i = _body.childCount - 1; i >= 0; i--)
                Destroy(_body.GetChild(i).gameObject);
            if (_frameRoot != null)
                Destroy(_frameRoot.gameObject);
            _frameRoot = null;
            _fields.Clear();
            _videoStatus = null;
            _y = 4f;
            for (var i = 0; i < _tabs.Count; i++)
            {
                var selected = i == (int)_tab;
                var colors = _tabs[i].Button.colors;
                colors.normalColor = selected ? _theme.Selected : _theme.Button;
                _tabs[i].Button.colors = colors;
            }

            try
            {
                switch (_tab)
                {
                    case Tab.Ai: BuildAi(); break;
                    case Tab.Avatar: BuildAvatar(); break;
                    case Tab.Voice: BuildVoice(); break;
                    case Tab.Controls: BuildControls(); break;
                    case Tab.Overlay: BuildOverlay(); break;
                    case Tab.Privacy: BuildPrivacy(); break;
                    case Tab.Persona: BuildPersona(); break;
                    case Tab.Screen: BuildScreen(); break;
                    default: BuildHappi(); break;
                }
            }
            catch (Exception ex)
            {
                Note("This tab could not be drawn (" + ex.GetType().Name + "). Use Classic settings.");
                Debug.LogWarning("New UI: settings tab " + _tab + " failed: " + ex.Message);
            }

            _body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _y + 8f);
            _signature = Signature();
            _building = false;
        }

        SettingsPresenter P => _settings.Presenter;

        CompanionSettings S => _settings.CurrentSettings;

        void Change(Action<CompanionSettings> change)
        {
            P.Change(change);
            _rebuild = true;
        }

        void BuildHappi()
        {
            Section("Happi (new UI)");
            Toggle("New UI (preview)", S.NewUiPreview, v => Change(s => s.NewUiPreview = v));
            Note(NewUiSwitch.ArgumentPresent(Environment.GetCommandLineArgs())
                ? "This launch has -newui, so the new UI stays on until you restart without it."
                : "Off returns to the classic look right away. Your settings stay.");
            Toggle("Voice narration", S.VoiceNarration, v => Change(s => s.VoiceNarration = v));
            Note("Off: lines between *asterisks* are shown in the chat but not spoken. Only her own words use the voice.");
            var chat = GetComponent<HappiChatView>();
            if (chat != null && chat.Player != null)
            {
                var step = chat.Player.Mode == VisualNovel.VnMode.Step;
                Toggle("Step through replies", step, v =>
                {
                    chat.Player.Mode = v ? VisualNovel.VnMode.Step : VisualNovel.VnMode.Auto;
                    PlayerPrefs.SetInt(HappiChatView.ModePref, v ? 1 : 0);
                    _rebuild = true;
                });
                Note("Step: Next, a click on the dialogue or Space shows one line at a time. Auto plays the whole reply.");
            }

            Toggle("Spoken text glow", S.SpokenGlow, v =>
            {
                _settings.SaveLayoutSetting(s => s.SpokenGlow = v);
                _rebuild = true;
            });
            Note("The words light up warmly as she says them, then fade back. Narration glows only when Voice narration is on.");
            if (S.SpokenGlow)
                Slider(v => "Glow intensity: " + (v < 0.34f ? "subtle" : v < 0.67f ? "medium" : "strong"), S.SpokenGlowIntensity, 0f, 1f, false,
                    v => _settings.SaveLayoutSetting(s => s.SpokenGlowIntensity = v));

            Section("Chat box");
            Note("Drag the chat box by its top bar to move it. It stays inside the window and on the screen.");
            Button("Reset chat position", () =>
            {
                var chat = GetComponent<HappiChatView>();
                if (chat != null)
                    chat.ResetPosition();
                else
                    _settings.SaveLayoutSetting(s =>
                    {
                        s.ChatBoxX = -1f;
                        s.ChatBoxY = -1f;
                    });
                _settings.SetStatusMessage("The chat box is back at the bottom.");
            });

            Section("Right-click wheel");
            Note("Right-click her for the wheel: Talk, Watch my screen, Mood, Persona, Do something, Leave me alone, Memories, Settings. Shift+right-click still rotates her.");
        }

        void BuildAi()
        {
            Note("Text changes apply when you leave the field. The API key is stored in the secret store, never in settings.json.");
            Label("Active now: " + P.ActiveModelText);
            var grok = S.ProviderId == ChatDefaults.GrokId;
            var localActive = !VoiceRoute.UsesCloudTts(S.PrivateMode, S.ProviderId);
            Choice(ProviderLabel("Grok", grok, localActive), grok, () => { P.SetProvider(ChatDefaults.GrokId); _rebuild = true; });
            Choice(ProviderLabel("Local Ollama", !grok, !localActive), !grok, () => { P.SetProvider(ChatDefaults.LocalId); _rebuild = true; });
            if (S.PrivateMode)
                Note("Private mode is on. Chat and voice stay on the local model. The choice above is remembered for when private mode is off.");
            Field("Grok model", S.Model, false, v => Change(s => s.Model = v));
            Field("Base URL", S.BaseUrl, false, v => Change(s => s.BaseUrl = v));
            Note("OpenAI-compatible endpoint for the cloud provider. Default is xAI; use DeepSeek, OpenRouter, Groq or another compatible API.");
            Field("Local model", S.LocalModel, false, v => Change(s => s.LocalModel = v));
            Field("Local address", S.LocalBaseUrl, false, v => Change(s => s.LocalBaseUrl = v));
            Note("Default local address is Ollama at http://127.0.0.1:11434/v1.");
            Field("Timeout seconds", S.TimeoutSeconds.ToString("0.###", CultureInfo.InvariantCulture), false, v =>
            {
                float parsed;
                if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    Change(s => s.TimeoutSeconds = parsed);
                else
                    _rebuild = true;
            });
            Section("API key");
            Note(ApiKeyMask.Label(_settings.ApiKeySaved) + ".");
            var key = Field("Paste a new API key", string.Empty, true, null);
            Button("Save key", () => { _settings.SaveApiKey(key != null ? key.text : string.Empty); if (key != null) key.text = string.Empty; _rebuild = true; });
            Button("Remove saved key", () => { _settings.RemoveApiKey(); _rebuild = true; });
            if (_settings.ApiKeyFromEnvironment)
                Note("XAI_API_KEY is set and overrides the saved key. Restart the app without that variable to use the saved key.");
            Button(_settings.ConnectionTestRunning ? "Testing..." : "Test connection", () => { _settings.TestConnectionNow(); _rebuild = true; });
            Note("Test connection does not write the key or the Authorization header to the log.");
        }

        static string ProviderLabel(string name, bool remembered, bool overridden)
        {
            if (!remembered)
                return name;
            return overridden ? name + " (remembered)" : name + " (active)";
        }

        void BuildAvatar()
        {
            Section("Avatar");
            var video = AvatarModeNames.IsVideo(S.AvatarMode);
            Choice("3D model", !video, () => { P.SetAvatarMode(AvatarModeNames.ThreeD); _rebuild = true; });
            Choice("2D video", video, () => { P.SetAvatarMode(AvatarModeNames.Video); _rebuild = true; });
            if (!video)
                Note("2D video loops idle.mp4 or idle.webm from %APPDATA%\\ProjectLeia\\video-avatar. The 3D avatar stays loaded.");
            else
                BuildVideo();

            Section("3D avatar");
            Note("The source applies immediately. Asset Store, Mixamo, and Daz models stay out of this project.");
            Source("Auto", AvatarSourceNames.Auto);
            Source("Local prefab", AvatarSourceNames.Local);
            Source("VRM sample", AvatarSourceNames.Vrm);
            Source("FBX sample", AvatarSourceNames.Fbx);
            Toggle("Show expression portrait", S.ShowExpressionPortrait, v => Change(s => s.ShowExpressionPortrait = v));
            Note("Images load from the persona folder, or %APPDATA%\\ProjectLeia\\portraits. Default is off.");
            // Same condition as the classic Avatar tab: always listed, works only with Private mode and the adult folder.
            Toggle("Intimate expressions (18+)", S.IntimateExpressions, v => Change(s => s.IntimateExpressions = v));
            Note("Adult drawn expressions from %APPDATA%\\ProjectLeia\\portraits-adult. Works only in Private mode (local model). Off by default; they are not shown or offered to the model.");
            Button("Reset rotation", () => { _settings.ResetAvatarRotation(); _rebuild = true; });
            Note("Reset faces the current avatar toward the camera. It applies immediately.");
            Toggle("Look at cursor", S.CursorLookAt, v => Change(s => s.CursorLookAt = v));
            Note("Eyes and head follow the desktop cursor. They ease back when it is still or far away.");
        }

        void BuildVideo()
        {
            var player = GetComponent<VideoAvatar>();
            _videoStatus = Note(player != null ? player.StatusText : "The video avatar is not running.");
            Field("Video folder (empty uses the default)", S.VideoAvatarFolder, false, v => Change(s => s.VideoAvatarFolder = v));
            Button("Reload video", () => { var v = GetComponent<VideoAvatar>(); if (v != null) v.Reload(); _settings.SetStatusMessage("Looking for the clip again."); });
            Note("Put idle.mp4 with idle.mask.mp4 in the folder. Optional: idle variations, walk and walk_stop clips with their masks, and video-avatar.json to list them. See docs/VIDEO_AVATAR.md. No clip's sound is ever played.");
            Slider(v => "Video size " + Mathf.RoundToInt(v * 100f) + "%", S.VideoAvatarSize, VideoAvatarLayout.MinSize, VideoAvatarLayout.MaxSize, false, v => P.Change(s => s.VideoAvatarSize = v));
            Slider(v => "Video left or right " + Mathf.RoundToInt(v * 100f) + "%", S.VideoAvatarOffsetX, -VideoAvatarLayout.MaxOffsetX, VideoAvatarLayout.MaxOffsetX, false, v => P.Change(s => s.VideoAvatarOffsetX = v));
            Slider(v => "Video up or down " + Mathf.RoundToInt(v * 100f) + "%", S.VideoAvatarOffsetY, -VideoAvatarLayout.MaxOffsetY, VideoAvatarLayout.MaxOffsetY, false, v => P.Change(s => s.VideoAvatarOffsetY = v));
            Button("Reset video size and position", () => Change(s =>
            {
                s.VideoAvatarSize = VideoAvatarLayout.DefaultSize;
                s.VideoAvatarOffsetX = 0f;
                s.VideoAvatarOffsetY = 0f;
            }));
            Note("Size and position apply immediately. The window size is on the Overlay tab.");
            Button("Test: walk left", () => VideoTest(a => a.TestWalk(-1f)));
            Button("Test: walk right (mirrored)", () => VideoTest(a => a.TestWalk(1f)));
            Button("Test: stop", () => VideoTest(a => a.TestStop()));
            Button("Test: back to the front idle", () => VideoTest(a => a.TestFront()));
            var reactions = VideoAvatarPresenter.TestReactions;
            for (var i = 0; i < reactions.Count; i++)
            {
                var name = reactions[i].Value;
                Button(reactions[i].Key, () => VideoTest(a =>
                {
                    var status = VideoAvatarPresenter.ReactionStatus(a.RequestReaction(name), name, VideoAvatarDirector.PendingSeconds);
                    if (status.Length > 0)
                        _settings.SetStatusMessage(status);
                }));
            }

            Slider(v => v < 0.02f ? "Walk speed: from the walk clip" : "Walk speed " + v.ToString("0.00", CultureInfo.InvariantCulture) + " clip heights per second",
                S.VideoAvatarWalkSpeed, 0f, 1f, false, v => P.Change(s => s.VideoAvatarWalkSpeed = v < 0.02f ? 0f : v));
            Toggle("Play clip sounds", S.PlayClipSounds, v => Change(s => s.PlayClipSounds = v));
            Slider(v => "Clip sound volume " + Mathf.RoundToInt(v * 100f) + "%", S.ClipSoundVolume, 0f, 1f, false, v => P.Change(s => s.ClipSoundVolume = v));
            Note("Clip sounds are her own sounds in clips marked \"sound\": true in video-avatar.json. They follow the Mute button and stay silent while she speaks.");
            Note("With settings closed and the window focused, the arrow keys do the same: left and right walk, down stops, up returns to the front idle.");
        }

        void VideoTest(Action<VideoAvatar> action)
        {
            var player = GetComponent<VideoAvatar>();
            if (player == null || player.State != VideoAvatarState.Playing)
            {
                _settings.SetStatusMessage(VideoAvatarPresenter.NotPlaying);
                return;
            }

            action(player);
        }

        void Source(string label, string source)
        {
            Choice(label, S.AvatarSource == source, () => Change(s => s.AvatarSource = source));
        }

        void BuildVoice()
        {
            Label("Active now: " + P.ActiveModelText);
            Note(VoiceRoute.UsesCloudTts(S.PrivateMode, S.ProviderId)
                ? "Voice follows chat. Grok replies use the cloud voice."
                : "Voice follows chat. Local and private replies use Piper and are not sent to cloud TTS.");
            Toggle("Speak replies", S.TtsEnabled, v => Change(s => s.TtsEnabled = v));
            Toggle("Voice narration", S.VoiceNarration, v => Change(s => s.VoiceNarration = v));
            Note("New chat only. Off: narration lines are shown but not voiced.");
            Field("Cloud voice (Grok)", S.VoiceId, false, v => Change(s => s.VoiceId = v));
            var piper = string.Equals(S.TtsProvider, "piper", StringComparison.OrdinalIgnoreCase);
            var kokoro = string.Equals(S.TtsProvider, "kokoro", StringComparison.OrdinalIgnoreCase);
            Toggle(piper ? "Local voice: Piper (selected)" : "Use Piper locally", piper, v => Change(s => s.TtsProvider = v ? "piper" : "xai"));
            Toggle(kokoro ? "Local voice: Kokoro (selected)" : "Use Kokoro locally", kokoro, v => Change(s => s.TtsProvider = v ? "kokoro" : "xai"));
            Field("Piper exe", S.PiperExe, false, v => Change(s => s.PiperExe = v));
            Field("Piper voice model", S.PiperVoice, false, v => Change(s => s.PiperVoice = v));
            Field("Kokoro python", S.KokoroPython, false, v => Change(s => s.KokoroPython = v));
            Field("Kokoro script", S.KokoroScript, false, v => Change(s => s.KokoroScript = v));
            Field("Kokoro model", S.KokoroModel, false, v => Change(s => s.KokoroModel = v));
            Field("Kokoro voices", S.KokoroVoices, false, v => Change(s => s.KokoroVoices = v));
            Field("Kokoro voice", S.KokoroVoice, false, v => Change(s => s.KokoroVoice = v));
            Note("Piper and Kokoro speak offline with no API key. Kokoro sounds better; its first load takes about 15 seconds. A blank persona voice keeps these.");
            Field("TTS language", S.TtsLanguage, false, v => Change(s => s.TtsLanguage = v));
            Slider(v => "Volume " + Mathf.RoundToInt(v * 100f) + "%", S.Volume, 0f, 1f, false, v => P.Change(s => s.Volume = v));
            Toggle("Muted", S.Muted, v => Change(s => s.Muted = v));
            Note("Mute is the same switch as F6. It applies immediately.");
            Toggle("Push-to-talk", S.SttEnabled, v => Change(s => s.SttEnabled = v));
            Field("Push-to-talk key", S.PushToTalkKey, false, v => Change(s => s.PushToTalkKey = v));
            Note("Taken keys (F6, F7, F8, F10, F12, T, R, Esc) stay on V. The new key applies immediately.");
            Field("Whisper model", S.WhisperModel, false, v =>
            {
                Change(s => s.WhisperModel = v);
                _settings.SetStatusMessage("The Whisper model applies on the next recording. The .bin file is not part of the app.");
            });
            Field("Speech language", S.SttLanguage, false, v => Change(s => s.SttLanguage = v));
            Note("Placeholder. Wake word is not in this build.");
            Note("Local and private replies use Piper only. Cloud TTS does not receive them.");
            Note("Placeholder. The voice agent is not in this build. Private mode blocks it.");
        }

        void BuildControls()
        {
            Note(HotkeyCatalog.RebindNote);
            var entries = HotkeyCatalog.Current(S.PushToTalkKey);
            for (var i = 0; i < entries.Length; i++)
                Label(entries[i].Keys + "   " + entries[i].Action);
            Section("New UI");
            Label("Right-click her   Radial wheel");
            Label("Shift + right-click   Rotate mode");
            Label("Space / click (Step mode)   Next chat line");
        }

        void BuildOverlay()
        {
            Toggle("Always on top", S.AlwaysOnTop, v => Change(s => s.AlwaysOnTop = v));
            Note("F8 still toggles this. It applies immediately and does not change transparency.");
            Slider(v => "Size " + Mathf.RoundToInt(v * 100f) + "%", S.Scale, OverlayWindowSize.MinScale, OverlayWindowSize.MaxScale, false, v => P.Change(s => s.Scale = v));
            Note("Applies immediately. The Windows player resizes the transparent window.");
            Button("Click-through: " + SettingsPresenter.ClickThroughLabel(S.ClickThrough), () => { P.CycleClickThrough(); _rebuild = true; });
            Note(SettingsPresenter.ClickThroughHelp(S.ClickThrough));
            Slider(v => "Idle frame rate " + Mathf.RoundToInt(v), S.IdleFrameRate, 1f, OverlayFrameRate.Active, true, v => P.Change(s => s.IdleFrameRate = Mathf.RoundToInt(v)));
            Note("Applies immediately, after the pointer has been outside the window for a few seconds.");
            Toggle("Hide taskbar button", S.HideTaskbarButton, v => Change(s => s.HideTaskbarButton = v));
            Toggle("Start with Windows", S.StartWithWindows, v => Change(s => s.StartWithWindows = v));
            Note("Saved now. The Windows player writes the Run key. It takes effect the next time you sign in.");
            Toggle("Pause", S.Paused, v => Change(s => s.Paused = v));
            Note("Pause matches the tray checkmark. Speech, push-to-talk, and animation cues stay off while it is on.");
            Toggle("Show mood readout (debug)", S.ShowMoodDebug, v => Change(s => s.ShowMoodDebug = v));
            Note("Her current mood and the three strongest moods, top left. Mood changes are also in Player.log.");
            Toggle("Show debug info", S.ShowDebugInfo, v => Change(s => s.ShowDebugInfo = v));
            Note("Debug text top-left: topmost, frame rate, emotion, key hints, rotate, size and zoom. Off by default. The hotkeys work either way.");
            Toggle("New UI (preview)", S.NewUiPreview, v => Change(s => s.NewUiPreview = v));
        }

        void BuildPrivacy()
        {
            if (_confirm.Pending == PrivacyConfirmKind.WipeAll || _confirm.Pending == PrivacyConfirmKind.ClearHistory)
            {
                Note(PrivacyConfirm.Prompt(_confirm.Pending, 0, null));
                var kind = _confirm.Pending;
                Button(kind == PrivacyConfirmKind.WipeAll ? "Confirm wipe" : "Confirm clear", () =>
                {
                    _confirm.Cancel();
                    if (kind == PrivacyConfirmKind.WipeAll)
                        _settings.WipeAllConfirmed();
                    else
                        _settings.ClearChatHistoryConfirmed();
                    _rebuild = true;
                }, true);
                Button("Cancel", () => { _confirm.Cancel(); _rebuild = true; });
                return;
            }

            Toggle("Save chat history", S.ChatHistory, v => { _settings.SetChatHistory(v); _rebuild = true; });
            Note("Off keeps the transcript for this session only. On writes it under ProjectLeia, outside the repo.");
            Toggle("Private mode", S.PrivateMode, v => Change(s => s.PrivateMode = v));
            Note("Private mode sends chat only to the local Ollama model. Cloud TTS, the voice agent, and Uthana do not receive that content. The default is off.");
            Button("Clear chat history", () => { _confirm.Request(PrivacyConfirmKind.ClearHistory); _rebuild = true; });
            Section("Export");
            Note("Export writes a decrypted copy, including chat, memories, and the API key if one is saved. Keep that folder private.");
            Field("Export folder", _exportFolder, false, v => _exportFolder = v ?? string.Empty);
            Button("Choose export folder", () =>
            {
                string picked;
                if (ExportFolderDialog.TryPick(out picked) && !string.IsNullOrWhiteSpace(picked))
                    _exportFolder = picked.Trim();
                else
                    _settings.SetStatusMessage("Type an export folder. The folder window is unavailable here.");
                _rebuild = true;
            });
            Button("Export decrypted folder", () => _settings.ExportLocalData(_exportFolder, false));
            Button("Export decrypted zip", () => _settings.ExportLocalData(_exportFolder, true));
            Button("Wipe all local data", () => { _confirm.Request(PrivacyConfirmKind.WipeAll); _rebuild = true; }, true);
            Button("Run setup again", () => { Close(); _settings.RequestSetupReplay(); });
            Note("Opens the first-run setup again. Skip on a step uses that step's default. Finish saves.");
            BuildMemories();
        }

        void BuildMemories()
        {
            var memories = _settings.Memories;
            Section("Memories on this PC");
            Note(memories.Summary);
            Note("Stored under ProjectLeia, outside the repository. Memories made in private mode are never sent to a cloud model.");
            if (memories.Count == 0)
                return;
            if (memories.Confirm.HasPending)
            {
                Note(memories.PendingPrompt());
                Button(memories.Confirm.Pending == PrivacyConfirmKind.DeleteAllMemories ? "Confirm delete all" : "Confirm delete", () => { memories.ConfirmPending(); _rebuild = true; }, true);
                Button("Cancel", () => { memories.Confirm.Cancel(); _rebuild = true; });
                return;
            }

            var page = memories.Page();
            for (var i = 0; i < page.Count; i++)
            {
                var id = page[i].Id;
                Button(page[i].Label, () => { memories.AskForget(id); _rebuild = true; });
            }

            if (memories.Count > page.Count)
                Note("Showing " + page.Count + " of " + memories.Count + ". Delete one to reveal more.");
            Button("Delete all memories", () => { memories.AskForgetAll(); _rebuild = true; }, true);
        }

        void BuildPersona()
        {
            var catalog = _settings.PersonaCatalogNow();
            Note("Built-ins ship with the app. Edits save outside the repo.");
            Label("Active now: " + P.ActiveModelText);
            Note(VoiceRoute.UsesCloudTts(S.PrivateMode, S.ProviderId)
                ? "Cloud voice is used while chat is Grok."
                : "A local voice is used while chat is local or private: Piper, or Kokoro when you picked it. That audio stays on this PC.");
            Note("Each persona keeps its own chat history, so switching characters starts a different conversation.");
            var choices = _settings.Personas.Choices();
            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                Choice(choice.Label, choice.Active, () => { _settings.Personas.Choose(choice.Id); _rebuild = true; });
            }

            Button("New persona", () => { _settings.NewPersona(); _rebuild = true; });
            var active = S.PersonaId ?? string.Empty;
            if (active.Length == 0)
            {
                Note("Pick a persona, or keep the name from setup. A blank voice, model, or avatar keeps the current one.");
                return;
            }

            var current = catalog.FindEntry(active);
            if (current == null || current.Persona == null)
            {
                Note("The selected persona file is missing. Voice and model stay as they are.");
                return;
            }

            var persona = current.Persona;
            var id = persona.Id;
            Section("Edit " + (string.IsNullOrEmpty(persona.DisplayName) ? id : persona.DisplayName));
            if (current.BuiltIn)
                Note(current.UserFile ? "Editing your saved copy." : "Editing saves a copy. The built-in file stays.");
            Note("Id " + id);
            Field("Display name", persona.DisplayName, false, v => Save(id, p => p.DisplayName = v));
            Area("Personality", persona.Personality, 80f, PersonaDefinition.MaxPersonalityLength, v => Save(id, p => p.Personality = v));
            Area("Speaking style", persona.SpeakingStyle, 54f, PersonaDefinition.MaxSpeakingStyleLength, v => Save(id, p => p.SpeakingStyle = v));
            Field("Piper voice id (local)", persona.PiperVoiceId, false, v => Save(id, p => p.PiperVoiceId = v));
            Field("Kokoro voice id (local)", persona.KokoroVoiceId, false, v => Save(id, p => p.KokoroVoiceId = v));
            Field("Cloud voice id (Grok)", persona.CloudVoiceId, false, v => Save(id, p => p.CloudVoiceId = v));
            Note("Blank voice, model, or avatar keeps the current one.");
            Choice("Grok", Same(persona.ProviderId, ChatDefaults.GrokId), () => Save(id, p => p.ProviderId = ChatDefaults.GrokId));
            Choice("Local Ollama", Same(persona.ProviderId, ChatDefaults.LocalId), () => Save(id, p => p.ProviderId = ChatDefaults.LocalId));
            Choice("Leave provider unchanged", string.IsNullOrEmpty(persona.ProviderId), () => Save(id, p => p.ProviderId = string.Empty));
            Field("Model name", persona.Model, false, v => Save(id, p => p.Model = v));
            Note("That name is the Grok model, or the local Ollama tag, for the provider above.");
            Choice("Avatar auto", Same(persona.AvatarSource, AvatarSourceNames.Auto), () => Save(id, p => p.AvatarSource = AvatarSourceNames.Auto));
            Choice("Avatar local", Same(persona.AvatarSource, AvatarSourceNames.Local), () => Save(id, p => p.AvatarSource = AvatarSourceNames.Local));
            Choice("Avatar VRM", Same(persona.AvatarSource, AvatarSourceNames.Vrm), () => Save(id, p => p.AvatarSource = AvatarSourceNames.Vrm));
            Choice("Avatar FBX", Same(persona.AvatarSource, AvatarSourceNames.Fbx), () => Save(id, p => p.AvatarSource = AvatarSourceNames.Fbx));
            Choice("Leave avatar unchanged", string.IsNullOrEmpty(persona.AvatarSource), () => Save(id, p => p.AvatarSource = string.Empty));
            Field("Portrait folder", persona.PortraitFolder, false, v => Save(id, p => p.PortraitFolder = v));
            Note("A missing portrait folder uses the standard portraits folder.");
            if (current.UserFile)
                Button(current.BuiltIn ? "Delete saved copy" : "Delete persona", () => { _settings.DeletePersona(); _rebuild = true; }, true);
        }

        void Save(string id, Action<PersonaDefinition> change)
        {
            _settings.SavePersona(id, change);
            _rebuild = true;
        }

        static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        void BuildScreen()
        {
            var look = _settings.ScreenController;
            var screen = _settings.ScreenLook;
            Note("Off until you turn it on. One window, one frame, then you ask what she sees. This is not continuous game commentary.");
            Toggle("Read a window", S.ScreenLookEnabled, v => { screen.SetEnabled(v); _rebuild = true; });
            if (!S.ScreenLookEnabled)
            {
                Note("Nothing is captured while this is off.");
                return;
            }

            Note("Preview stays on this PC. Allowing cloud screen analysis sends the selected window image to the configured cloud vision provider (Grok, at the Base URL on the AI tab). The image is not saved. Private mode never sends it and does not switch to a cloud model.");
            Toggle("Allow cloud screen analysis", S.ScreenCloudConsent, v => Change(s => s.ScreenCloudConsent = v));
            if (look != null)
                look.RefreshMonitors();
            Toggle("Whole screen instead of a window", S.ScreenCaptureWholeMonitor, v => Change(s => s.ScreenCaptureWholeMonitor = v));
            if (S.ScreenCaptureWholeMonitor)
            {
                Toggle("Allow whole-screen capture", S.ScreenWholeMonitorConsent, v => Change(s => s.ScreenWholeMonitorConsent = v));
                Note("Whole-screen capture is a separate consent from window capture. It includes every window on that display. Private mode keeps it local.");
                if (S.ScreenWholeMonitorConsent)
                {
                    string note;
                    var monitors = screen.Monitors(out note);
                    if (monitors.Count == 0)
                        Note(note);
                    for (var i = 0; i < monitors.Count; i++)
                    {
                        var index = monitors[i].Index;
                        Choice(monitors[i].Label, monitors[i].Selected, () => { screen.SelectMonitor(index); _rebuild = true; });
                    }
                }
            }

            Toggle("Let her look during chat", S.ScreenAutoLook, v => Change(s => s.ScreenAutoLook = v));
            if (S.ScreenAutoLook)
            {
                Toggle("Pause automatic looks", S.ScreenAutoLookPaused, v => Change(s => s.ScreenAutoLookPaused = v));
                Note(S.ScreenAutoLookPaused
                    ? "Paused. The permission is still granted, but she cannot look until you unpause."
                    : "She may ask for one look while replying, at most once per reply and no more than once every " + Mathf.RoundToInt(ScreenLookController.AutoLookCooldownSeconds) + " seconds. It stays on this PC.");
            }
            else
            {
                Note("Automatic looks are off. She never captures the screen on her own; the Look button still works.");
            }

            Toggle("Fast screen replies", S.ScreenFastReply, v => Change(s => s.ScreenFastReply = v));
            Note(S.ScreenFastReply
                ? "The vision model writes the screen remark, so the chat model is not reloaded. Faster, with a simpler voice."
                : "The chat model writes the screen remark after the vision model. Slower, but it keeps the full persona.");
            Field("Local vision model", S.LocalVisionModel, false, v => Change(s => s.LocalVisionModel = v));
            Button("Use " + OllamaVision.RecommendedTag, () => Change(s => s.LocalVisionModel = OllamaVision.RecommendedTag));
            Note("The vision model and the chat model are different. Install it yourself with " + OllamaVision.PullCommand + ". A smaller tag is " + OllamaVision.SmallerTag + ".");
            if (look == null)
            {
                Note("Window capture is not available in this session.");
                return;
            }

            var decision = look.CurrentDecision();
            if (decision.Route == ScreenLookRoute.Local)
                Note("What do you see? uses the local vision model with cloud screen analysis off. The image, the observation, the reply, and speech stay on this PC.");
            else if (decision.Route == ScreenLookRoute.Cloud)
                Note("Cloud screen analysis is allowed for the window you select. It does not include the rest of the desktop.");
            else if (!string.IsNullOrEmpty(decision.Message))
                Note(decision.Message);
            Note(string.IsNullOrEmpty(look.StatusText) ? "Ready." : look.StatusText);
            Button("Refresh window list", () => { look.RefreshWindows(); _rebuild = true; });
            if (!S.ScreenCaptureWholeMonitor && look.Windows != null)
            {
                for (var i = 0; i < look.Windows.Count; i++)
                {
                    var target = look.Windows[i];
                    Choice(WindowLabel(target), look.Selected == target, () => { look.Select(target); _rebuild = true; });
                }
            }

            if (S.ScreenCaptureWholeMonitor)
                Note(look.SelectedMonitor != null ? "Selected display: " + look.SelectedMonitor.Label + "." : "Choose a display. Nothing is captured until you do.");
            else
                Note(look.Selected == null ? "No window selected." : "Selected: " + WindowLabel(look.Selected) + ". That name is the window title from Windows.");

            if (look.PreviewTexture != null)
            {
                Preview(look.PreviewTexture);
                if (!string.IsNullOrEmpty(look.DeliveryNote))
                    Note(look.DeliveryNote);
                Button("Inspect exact frame", () => { _framePreview = true; _frameFit = true; _rebuild = true; });
            }
            else
            {
                Note("The preview appears here after you pick a window.");
            }

            var lookButton = Button(look.IsBusy ? "Stop look" : "Look at screen", () => { if (look.IsBusy) look.Stop(); else look.RequestLook(); _rebuild = true; });
            lookButton.Interactable = ScreenLookPolicy.LookButtonInteractable(decision.SendButtonEnabled, look.IsBusy);
            Button(look.IsBusy ? "Stop" : "Stop look", () => { look.Stop(); _rebuild = true; });
            Button("Clear window", () => { look.ClearSelection(); if (S.ScreenCaptureWholeMonitor) look.RefreshMonitors(); _rebuild = true; });
            Note("Stop cancels the current frame. Clear window drops the selection and the preview.");
            if (_framePreview && look.PreviewTexture != null)
                ExactFrame(look);
        }

        static string WindowLabel(WindowTarget target)
        {
            if (target == null)
                return "(none)";
            var title = string.IsNullOrEmpty(target.Title) ? "(untitled window)" : target.Title;
            if (title.Length > 42)
                title = title.Substring(0, 42);
            if (target.Minimized)
                title += " (minimised)";
            return title;
        }

        void Preview(Texture texture)
        {
            var rect = HappiUiKit.Rect(_body, "Preview");
            var aspect = texture.height > 0 ? texture.width / (float)texture.height : 1f;
            var height = Mathf.Clamp(_width / Mathf.Max(0.2f, aspect), 120f * _s, 320f * _s);
            Place(rect, height);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }

        /// <summary>The exact decoded frame that was sent (S-SC-15): Fit or 1:1, never written to disk.</summary>
        void ExactFrame(ScreenLookController look)
        {
            var texture = look.PreviewTexture;
            var root = HappiUiKit.Panel(_panel, "ExactFrame", new Color(0.02f, 0.03f, 0.06f, 0.98f), true);
            _frameRoot = root.rectTransform;
            HappiUiKit.Stretch(_frameRoot, 0f, 0f, 0f, 0f);
            var details = string.IsNullOrEmpty(look.PreviewDetails) ? look.DeliveryNote : look.PreviewDetails;
            var title = HappiUiKit.Text(_frameRoot, "Title", HappiUiKit.Literal("Exact frame sent to the vision model. " + details), _theme.FontSmall * _s, _theme.Text, TextAlignmentOptions.TopLeft, false);
            HappiUiKit.Place(title.rectTransform, 10f, 8f, _panel.rect.width - 180f * _s, 52f * _s);
            var close = HappiUiKit.Button(_frameRoot, "Close", "Close", _theme.FontSmall * _s, () => { _framePreview = false; _rebuild = true; });
            HappiUiKit.Place(close.Rect, _panel.rect.width - 80f * _s, 8f, 70f * _s, 26f * _s);
            var zoom = HappiUiKit.Button(_frameRoot, "Zoom", _frameFit ? "1:1" : "Fit", _theme.FontSmall * _s, () => { _frameFit = !_frameFit; _rebuild = true; });
            HappiUiKit.Place(zoom.Rect, _panel.rect.width - 160f * _s, 8f, 70f * _s, 26f * _s);
            var viewport = HappiUiKit.Rect(_frameRoot, "Viewport");
            HappiUiKit.Stretch(viewport, 10f, 66f * _s, 10f, 10f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = HappiUiKit.Rect(viewport, "Image");
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            var raw = content.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewW = Mathf.Max(60f, _panel.rect.width - 20f);
            var viewH = Mathf.Max(60f, _panel.rect.height - 76f * _s);
            var scale = _frameFit ? Mathf.Min(1f, Mathf.Min(viewW / texture.width, viewH / texture.height)) : 1f;
            content.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
            content.anchoredPosition = _frameFit ? new Vector2(Mathf.Max(0f, (viewW - texture.width * scale) * 0.5f), 0f) : Vector2.zero;
        }

        // ---- Row builders --------------------------------------------------------------------

        void Place(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -_y);
            rect.sizeDelta = new Vector2(-8f, height);
            _y += height + _theme.Spacing * _s;
        }

        void Section(string title)
        {
            _y += 4f * _s;
            var text = HappiUiKit.Text(_body, "Section", HappiUiKit.Literal(title), _theme.FontBody * _s, _theme.Hover, TextAlignmentOptions.MidlineLeft, true);
            Place(text.rectTransform, 22f * _s);
        }

        TextMeshProUGUI Label(string value)
        {
            var text = HappiUiKit.Text(_body, "Label", HappiUiKit.Literal(value), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            var height = Mathf.Max(20f * _s, text.GetPreferredValues(text.text, _width, 0f).y);
            Place(text.rectTransform, height);
            return text;
        }

        TextMeshProUGUI Note(string value)
        {
            var text = HappiUiKit.Text(_body, "Note", HappiUiKit.Literal(value), _theme.FontSmall * _s, _theme.TextMuted, TextAlignmentOptions.TopLeft, false);
            text.fontStyle = FontStyles.Italic;
            text.overflowMode = TextOverflowModes.Overflow;
            var height = Mathf.Max(16f * _s, text.GetPreferredValues(text.text, _width, 0f).y + 2f);
            Place(text.rectTransform, height);
            return text;
        }

        HappiButton Button(string label, Action action, bool danger = false)
        {
            var button = HappiUiKit.Button(_body, "Button", HappiUiKit.Literal(label), _theme.FontSmall * _s, () =>
            {
                if (!_building && action != null)
                    action();
            });
            button.Label.overflowMode = TextOverflowModes.Ellipsis;
            if (danger)
            {
                var colors = button.Button.colors;
                colors.normalColor = HappiTheme.Darken(_theme.Intimate, 0.35f);
                button.Button.colors = colors;
            }

            Place(button.Rect, _theme.ButtonHeight * _s);
            return button;
        }

        /// <summary>A choice among several: the selected one is marked and highlighted.</summary>
        void Choice(string label, bool selected, Action action)
        {
            var button = Button(selected ? "✓ " + StripMarks(label) : StripMarks(label), action);
            if (selected)
            {
                var colors = button.Button.colors;
                colors.normalColor = _theme.Selected;
                button.Button.colors = colors;
            }
        }

        static string StripMarks(string label)
        {
            return (label ?? string.Empty).Replace(" (selected)", string.Empty);
        }

        /// <summary>A labelled on/off switch.</summary>
        void Toggle(string label, bool value, Action<bool> changed)
        {
            var row = HappiUiKit.Rect(_body, "Toggle");
            Place(row, _theme.ButtonHeight * _s);
            var text = HappiUiKit.Text(row, "Label", HappiUiKit.Literal(label), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            HappiUiKit.Stretch(text.rectTransform, 0f, 0f, 70f * _s, 0f);
            var pill = HappiUiKit.Button(row, "Switch", value ? "On" : "Off", _theme.FontSmall * _s, () =>
            {
                if (!_building && changed != null)
                    changed(!value);
            });
            pill.Rect.anchorMin = new Vector2(1f, 0f);
            pill.Rect.anchorMax = new Vector2(1f, 1f);
            pill.Rect.pivot = new Vector2(1f, 0.5f);
            pill.Rect.anchoredPosition = Vector2.zero;
            pill.Rect.sizeDelta = new Vector2(62f * _s, 0f);
            if (value)
            {
                var colors = pill.Button.colors;
                colors.normalColor = _theme.Selected;
                pill.Button.colors = colors;
            }
        }

        TMP_InputField Field(string caption, string value, bool password, Action<string> onEnd)
        {
            var label = HappiUiKit.Text(_body, "Caption", HappiUiKit.Literal(caption), _theme.FontSmall * _s, _theme.TextMuted, TextAlignmentOptions.BottomLeft, false);
            Place(label.rectTransform, 15f * _s);
            _y -= _theme.Spacing * _s * 0.6f;
            var field = HappiUiKit.Input(_body, "Field", caption, _theme.FontBody * _s, password);
            Place((RectTransform)field.transform, 28f * _s);
            field.text = password ? string.Empty : value ?? string.Empty;
            field.onEndEdit.AddListener(text =>
            {
                if (_building || onEnd == null)
                    return;
                onEnd(text ?? string.Empty);
            });
            _fields.Add(field);
            return field;
        }

        void Area(string caption, string value, float height, int limit, Action<string> onEnd)
        {
            var field = Field(caption, value, false, onEnd);
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            field.characterLimit = limit;
            if (field.textComponent != null)
            {
                field.textComponent.textWrappingMode = TextWrappingModes.Normal;
                field.textComponent.alignment = TextAlignmentOptions.TopLeft;
            }

            var rect = (RectTransform)field.transform;
            _y -= rect.sizeDelta.y + _theme.Spacing * _s;
            Place(rect, height * _s);
        }

        void Slider(Func<float, string> label, float value, float min, float max, bool whole, Action<float> changed)
        {
            var caption = HappiUiKit.Text(_body, "SliderLabel", HappiUiKit.Literal(label(value)), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            Place(caption.rectTransform, 18f * _s);
            var rect = HappiUiKit.Rect(_body, "Slider");
            Place(rect, 18f * _s);
            var background = rect.gameObject.AddComponent<Image>();
            background.sprite = HappiUiKit.Rounded();
            background.type = Image.Type.Sliced;
            background.color = _theme.PanelInset;
            var fillArea = HappiUiKit.Rect(rect, "Fill Area");
            HappiUiKit.Stretch(fillArea, 8f * _s, 6f * _s, 8f * _s, 6f * _s);
            var fill = HappiUiKit.Rect(fillArea, "Fill");
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = HappiUiKit.Rounded();
            fillImage.type = Image.Type.Sliced;
            fillImage.color = _theme.Border;
            fill.sizeDelta = Vector2.zero;
            var handleArea = HappiUiKit.Rect(rect, "Handle Slide Area");
            HappiUiKit.Stretch(handleArea, 8f * _s, 0f, 8f * _s, 0f);
            var handle = HappiUiKit.Rect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(18f * _s, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = HappiUiKit.Circle();
            handleImage.color = _theme.Text;
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            var colors = slider.colors;
            colors.highlightedColor = _theme.Hover;
            slider.colors = colors;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.value = Mathf.Clamp(value, min, max);
            slider.onValueChanged.AddListener(next =>
            {
                if (_building)
                    return;
                caption.text = HappiUiKit.Literal(label(next));
                if (changed != null)
                    changed(next);
            });
        }

        // ---- Focus -------------------------------------------------------------------------

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
                if (_fields[i] != null && _fields[i].isFocused)
                    _fields[i].DeactivateInputField();
            }
        }

        void SyncFocus()
        {
            var focused = AnyFieldFocused();
            if (focused && !_focusClaimed)
            {
                DebugInputGuard.ClaimTextField();
                _focusClaimed = true;
            }
            else if (!focused)
            {
                ReleaseFocus();
            }
        }

        void ReleaseFocus()
        {
            if (!_focusClaimed)
                return;
            DebugInputGuard.ReleaseTextField();
            _focusClaimed = false;
        }
    }
}
