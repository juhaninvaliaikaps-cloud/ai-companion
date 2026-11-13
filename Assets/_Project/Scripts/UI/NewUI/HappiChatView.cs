using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.NewUI.VisualNovel;
using AICompanion.Overlay;
using AICompanion.Settings;
using AICompanion.UI;
using AICompanion.UI.Presenters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Director = AICompanion.AnimationDirector.AnimationDirector;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-104 PR C / JUH-108: the new chat, a visual-novel dialogue box along the bottom of the
    /// overlay window. The old <see cref="ChatPanel"/> stays the chat engine; this view only
    /// draws and calls <see cref="ChatPresenter"/>. Each reply plays line by line: narration in
    /// italics, speech voiced, the current line highlighted, Auto or Step, Next / Skip / Show
    /// all. "History" opens the full transcript (one text object per message, last 100
    /// messages, 6000 characters each, as #64). Exists only while the new UI is on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HappiChatView : MonoBehaviour, IOverlayKeepAwake
    {
        public const string ModePref = "happi.vn-mode";
        const float PlaneDistance = 0.48f;

        ChatPanel _chat;
        SettingsPanel _settings;
        HappiTheme _theme;
        VnPlayer _player;
        VnVoice _voice;
        VnSpeechSink _sink;
        VnCueSink _cues;
        readonly ChatTranscriptView _transcript = new ChatTranscriptView();
        readonly List<TextMeshProUGUI> _historyTexts = new List<TextMeshProUGUI>();
        readonly List<float> _historyHeights = new List<float>();

        GameObject _canvasObject;
        Canvas _canvas;
        RectTransform _box;
        TextMeshProUGUI _name;
        TextMeshProUGUI _badge;
        TextMeshProUGUI _userLine;
        TextMeshProUGUI _dialogue;
        TextMeshProUGUI _note;
        Image _dialogueHit;
        HappiButton _history;
        HappiButton _mute;
        HappiButton _look;
        HappiButton _close;
        HappiButton _mode;
        HappiButton _next;
        HappiButton _skip;
        HappiButton _showAll;
        HappiButton _narration;
        HappiButton _mic;
        HappiButton _send;
        HappiButton _saveKey;
        TMP_InputField _input;
        TMP_InputField _keyInput;
        RectTransform _historyRoot;
        ScrollRect _historyScroll;
        RectTransform _historyContent;

        bool _attached;
        bool _historyOpen;
        bool _focusClaimed;
        bool _wasVisible;
        bool _micHeld;
        int _shownRevision = -1;
        int _shownSerial = -1;
        int _shownCurrent = -2;
        int _shownRevealed = -1;
        string _shownLastAssistant;
        float _layoutWidth = -1f;
        float _layoutHeight = -1f;
        float _layoutScale = -1f;
        bool _layoutHistory;
        bool _layoutKeyRow;
        float _historyWidth = -1f;
        string _shownBadge;
        bool _keySaved;
        float _keyCheckAt = -1f;

        // Movable box (JUH-104 follow-up).
        HappiDragHandle _dragHandle;
        bool _dragPending;
        bool _dragging;
        Vector2 _dragOffset;
        Vector2 _boxSize;
        Vector2 _boxCenter;
        float _boxMargin = 8f;
        Rect _visibleArea;
        float _visibleCheckAt = -1f;
        OverlayWindow _window;
        Wheel.HeadLocator _bodyLocator;
        readonly List<Image> _snapMarkers = new List<Image>();

        // Spoken-text glow (JUH-108 follow-up).
        readonly SpokenGlowTracker _glow = new SpokenGlowTracker();
        ISpeechProgress _progress;
        int _glowLine = -1;
        int _glowSerial = -1;
        int _glowLineStart;
        bool _glowDirty;

        public VnPlayer Player => _player;

        /// <summary>The chat box centre in Unity screen pixels.</summary>
        public Vector2 BoxCenter => _boxCenter;

        public bool KeepFrameRateActive
        {
            get
            {
                if (!_attached)
                    return false;
                return (_player != null && _player.Active)
                    || (_input != null && _input.isFocused)
                    || (_keyInput != null && _keyInput.isFocused);
            }
        }

        /// <summary>Takes over the chat's speech and drawing. Called when the new UI turns on.</summary>
        public void Attach(ChatPanel chat, SettingsPanel settings)
        {
            if (_attached || chat == null)
                return;
            _chat = chat;
            _settings = settings;
            _theme = HappiTheme.Current;
            _voice = new VnVoice(RealVoice, SpeechEnabled);
            _cues = new VnCueSink(PlayCue);
            _player = new VnPlayer(_voice, _cues, VnTiming.From(_theme));
            _player.Mode = PlayerPrefs.GetInt(ModePref, 0) == 1 ? VnMode.Step : VnMode.Auto;
            _sink = new VnSpeechSink(_player, _voice, RealVoice, () => _chat != null ? _chat.LastAssistantText : string.Empty);

            _window = GetComponent<OverlayWindow>();
            _bodyLocator = new Wheel.HeadLocator(gameObject);
            _progress = GetComponent<AICompanion.Voice.VoiceSpeaker>();
            _glow.FadeSeconds = _theme.SpokenGlowFadeSeconds;
            _glow.CommaPause = _theme.SpokenGlowCommaPause;
            _glow.SentencePause = _theme.SpokenGlowSentencePause;
            _glow.SecondsPerLetter = _theme.SpokenGlowSecondsPerLetter;
            BuildUi();
            _chat.UseSpeechOverride(_sink, true);
            _chat.SetRawReplyObserver(_sink.OnRawPiece);
            _chat.SetLegacyHidden(true);
            _attached = true;
            _wasVisible = false;
            Debug.Log("New UI: chat view on");
        }

        /// <summary>Gives the chat back to the old panel exactly as on main. Called when the new UI turns off.</summary>
        public void Detach()
        {
            if (!_attached)
                return;
            _attached = false;
            if (_player != null)
                _player.Stop();
            ReleaseFocus();
            if (_micHeld && _chat != null)
                _chat.SetMicButtonHeld(false);
            _micHeld = false;
            if (_chat != null)
            {
                _chat.SetRawReplyObserver(null);
                _chat.UseSpeechOverride(null, false);
                _chat.SetLegacyHidden(false);
            }

            ClearPortraitHooks();
            if (_window != null)
                _window.SetSideRoom(0);
            _snapMarkers.Clear();
            if (_canvasObject != null)
                Destroy(_canvasObject);
            _canvasObject = null;
            _historyTexts.Clear();
            _historyHeights.Clear();
            Debug.Log("New UI: chat view off");
        }

        void OnDestroy()
        {
            Detach();
        }

        ISpeechOutput RealVoice()
        {
            return GetComponent<AICompanion.Voice.VoiceSpeaker>();
        }

        bool SpeechEnabled()
        {
            var settings = _settings != null ? _settings.CurrentSettings : null;
            return settings == null || (settings.TtsEnabled && !settings.Paused);
        }

        void PlayCue(AnimationCue cue)
        {
            var director = GetComponent<Director>();
            if (director != null)
                director.Play(cue);
        }

        void Update()
        {
            if (!_attached || _chat == null || _canvasObject == null)
                return;

            var settings = _settings != null ? _settings.CurrentSettings : null;
            _player.VoiceNarration = settings != null && settings.VoiceNarration;
            _player.Tick(Time.unscaledDeltaTime);

            UpdateSideRoom();
            var visible = _chat.ChatVisible;
            if (_canvasObject.activeSelf != visible)
                _canvasObject.SetActive(visible);
            if (!visible)
            {
                _wasVisible = false;
                _dragging = false;
                ClearPortraitHooks();
                ReleaseFocus();
                if (_micHeld)
                {
                    _micHeld = false;
                    _chat.SetMicButtonHeld(false);
                }

                return;
            }

            HappiUiKit.FollowCamera(_canvas, PlaneDistance);
            Layout();
            PlaceBox(settings);
            if (!_wasVisible)
            {
                _wasVisible = true;
                FocusInput();
            }

            HandleKeys();
            RefreshControls();
            RefreshDialogue();
            ApplyGlow(settings);
            if (_historyOpen)
                RefreshHistory();
            SyncFocus();
        }

        void HandleKeys()
        {
            var typing = (_input != null && _input.isFocused) || (_keyInput != null && _keyInput.isFocused);
            if (!typing && _player.Mode == VnMode.Step && Input.GetKeyDown(KeyCode.Space))
                _player.Next();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_input != null && _input.isFocused)
                    _input.DeactivateInputField();
                if (_keyInput != null && _keyInput.isFocused)
                    _keyInput.DeactivateInputField();
            }
        }

        // ---- Moving the box ---------------------------------------------------------------

        /// <summary>
        /// Drag by the header bar: the box follows the cursor while the left button is held (read
        /// globally, so a fast drag that leaves the box keeps working), clamped to the part of
        /// the window that is on its monitor. The position is saved when the button is released.
        /// Without a drag, the saved (or default) position is clamped the same way, so a spot
        /// saved on another monitor or screen size snaps back on screen.
        /// </summary>
        void PlaceBox(CompanionSettings settings)
        {
            if (_boxSize.x <= 0f)
                return;
            if (Time.unscaledTime >= _visibleCheckAt || _visibleArea.width <= 0f)
            {
                _visibleCheckAt = Time.unscaledTime + _theme.MonitorCheckSeconds;
                _visibleArea = VisibleArea();
            }

            // Where she is: the box lives beside her and follows her when she walks or is moved.
            HerBody body;
            if (!_bodyLocator.TryGetBody(Time.unscaledDeltaTime, out body))
            {
                body.Head = new Vector2(Screen.width * 0.5f, Screen.height * 0.82f);
                body.FeetY = 0f;
            }

            var scale = Mathf.Max(0.5f, _layoutScale);
            var gap = _theme.ChatSideGap * scale;
            var pointer = Pointer();
            if (_dragPending)
            {
                _dragPending = false;
                _dragging = true;
                _dragOffset = _boxCenter - pointer;
            }

            var snap = settings != null ? settings.ChatBoxSnap : ChatSnapSpots.Default;
            if (_dragging)
            {
                var near = ChatSnapSpots.Nearest(_boxCenter, body, _boxSize, gap, _theme.ChatSnapRadius * scale);
                if (!LeftHeld())
                {
                    _dragging = false;
                    ShowSnapMarkers(false, body, gap, 0);
                    var offset = ChatSnapSpots.OffsetFor(_boxCenter, body, scale);
                    snap = near;
                    if (_settings != null)
                        _settings.SaveLayoutSetting(s =>
                        {
                            s.ChatBoxSnap = near;
                            s.ChatBoxOffsetX = offset.x;
                            s.ChatBoxOffsetY = offset.y;
                        });
                }
                else
                {
                    _boxCenter = ChatBoxPlacement.Clamp(pointer + _dragOffset, _boxSize, _visibleArea, _boxMargin);
                    ShowSnapMarkers(true, body, gap, near);
                }
            }

            if (!_dragging)
            {
                // A spot without room (she stands at a screen edge) flips to her other side.
                snap = ChatSnapSpots.Fit(snap, body, _boxSize, gap, _visibleArea, _boxMargin);
                var offset = settings != null ? new Vector2(settings.ChatBoxOffsetX, settings.ChatBoxOffsetY) : Vector2.zero;
                _boxCenter = ChatBoxPlacement.Clamp(ChatSnapSpots.Resolve(snap, offset, scale, body, _boxSize, gap), _boxSize, _visibleArea, _boxMargin);
            }

            if ((_box.anchoredPosition - _boxCenter).sqrMagnitude > 0.01f)
                _box.anchoredPosition = _boxCenter;

            // The 2D portrait keeps clear of the box; spot 2 sends it to her left.
            ExpressionPortraitPanel.NewUiAvoid = new Rect(
                _boxCenter.x - _boxSize.x * 0.5f,
                Screen.height - (_boxCenter.y + _boxSize.y * 0.5f),
                _boxSize.x,
                _boxSize.y);
            ExpressionPortraitPanel.NewUiPreferLeft = ChatSnapSpots.PortraitGoesLeft(snap) ? true : (bool?)null;
        }

        /// <summary>While dragging: four ghost boxes at the snap spots; the one the box would snap to is brighter.</summary>
        void ShowSnapMarkers(bool show, HerBody body, float gap, int active)
        {
            for (var i = 0; i < _snapMarkers.Count; i++)
            {
                var marker = _snapMarkers[i];
                if (marker.gameObject.activeSelf != show)
                    marker.gameObject.SetActive(show);
                if (!show)
                    continue;
                var spot = i + 1;
                var rect = marker.rectTransform;
                rect.sizeDelta = _boxSize;
                rect.anchoredPosition = ChatBoxPlacement.Clamp(ChatSnapSpots.Spot(spot, body, _boxSize, gap), _boxSize, _visibleArea, _boxMargin);
                marker.color = spot == active ? _theme.SnapMarkerActive : _theme.SnapMarker;
            }
        }

        void ClearPortraitHooks()
        {
            ExpressionPortraitPanel.NewUiAvoid = new Rect(0f, 0f, 0f, 0f);
            ExpressionPortraitPanel.NewUiPreferLeft = null;
        }

        /// <summary>Room on each side of her for the box; none while the classic settings or the setup wizard is open (they sit at the window's left).</summary>
        void UpdateSideRoom()
        {
            if (_window == null)
                return;
            var classic = _settings != null && _settings.IsOpen;
            var wizard = GetComponent<SetupWizard>();
            var wizardOpen = wizard != null && wizard.IsOpen;
            var scale = _theme.UiScale(Screen.dpi);
            var room = classic || wizardOpen ? 0 : ChatSnapSpots.SideRoomFor(_theme.ChatBoxWidth * scale, _theme.ChatSideGap * scale);
            _window.SetSideRoom(room);
        }

        Rect VisibleArea()
        {
            return OverlayVisibleArea.Get(_window);
        }

        Vector2 Pointer()
        {
            var cursor = GetComponent<IScreenCursor>();
            float x;
            float y;
            if (cursor != null && cursor.TryGetUnityScreenCursor(out x, out y))
                return new Vector2(x, y);
            var mouse = Input.mousePosition;
            return new Vector2(mouse.x, mouse.y);
        }

        bool LeftHeld()
        {
            var chrome = _window != null ? _window.Chrome : null;
            if (chrome != null && chrome.IsSupported)
                return chrome.IsLeftButtonHeld();
            return Input.GetMouseButton(0);
        }

        /// <summary>Settings > Happi > "Reset chat position": back to the default spot (4, beside her on the right).</summary>
        public void ResetPosition()
        {
            _dragging = false;
            _dragPending = false;
            if (_settings != null)
                _settings.SaveLayoutSetting(s =>
                {
                    s.ChatBoxSnap = ChatSnapSpots.Default;
                    s.ChatBoxOffsetX = 0f;
                    s.ChatBoxOffsetY = 0f;
                    s.ChatBoxX = -1f;
                    s.ChatBoxY = -1f;
                });
        }

        // ---- Spoken-text glow ----------------------------------------------------------

        /// <summary>
        /// Lights up the words of the current spoken line as she says them (JUH-108). Only the
        /// current line's characters are touched, by writing TMP vertex colours; nothing is
        /// allocated per frame. Narration glows only with Voice narration on (it is voiced then).
        /// Skip, Show all, Stop and a new line change the text, which resets the colours.
        /// </summary>
        void ApplyGlow(CompanionSettings settings)
        {
            var current = _player.Current;
            var on = settings != null && settings.SpokenGlow && current >= 0 && current < _player.Segments.Count
                && !_player.Finished && _player.IsVoiced(current) && current == _shownCurrent;
            if (!on)
            {
                ClearGlow();
                return;
            }

            if (current != _glowLine || _sink.ReplySerial != _glowSerial)
            {
                ClearGlow();
                _glowLine = current;
                _glowSerial = _sink.ReplySerial;
                _glow.BeginLine(_player.Segments[current].Text);
            }

            var now = Time.unscaledTime;
            var speaking = _player.CurrentVoicing && _voice.IsSpeaking;
            var clipSerial = 0;
            string clipText = null;
            var time = 0f;
            var length = 0f;
            var hasClip = _progress != null && _progress.TryGetSpokenClip(out clipSerial, out clipText, out time, out length);
            _glow.Update(now, speaking, hasClip, clipSerial, clipText, time, length);
            if (!_glowDirty && !_glow.AnyGlowing(now))
                return;
            PaintGlow(now, SpokenGlowTracker.Strength(settings.SpokenGlowIntensity, _theme.SpokenGlowMinStrength));
        }

        void PaintGlow(float now, float strength)
        {
            var info = _dialogue.textInfo;
            if (info == null || info.characterInfo == null)
                return;
            Color32 glow = _theme.SpokenGlowColor;
            var any = false;
            var count = _glow.WordCount;
            for (var w = 0; w < count; w++)
            {
                var g = _glow.Glow(w, now) * strength;
                if (g > 0f)
                    any = true;
                var end = _glowLineStart + _glow.WordEnd(w);
                for (var c = _glowLineStart + _glow.WordStart(w); c < end && c < info.characterCount; c++)
                {
                    var character = info.characterInfo[c];
                    if (!character.isVisible)
                        continue;
                    var mesh = info.meshInfo[character.materialReferenceIndex];
                    var colors = mesh.colors32;
                    var vertices = mesh.vertices;
                    var v = character.vertexIndex;
                    if (colors == null || vertices == null || v < 0 || v + 3 >= colors.Length || v + 3 >= vertices.Length)
                        continue;
                    var color = g > 0f ? Color32.Lerp(character.color, glow, g) : character.color;
                    colors[v] = color;
                    colors[v + 1] = color;
                    colors[v + 2] = color;
                    colors[v + 3] = color;

                    // The word being said also grows a little, so the glow reads at a glance.
                    var grow = 1f + _theme.SpokenGlowPop * g;
                    var bl = character.vertex_BL.position;
                    var tl = character.vertex_TL.position;
                    var tr = character.vertex_TR.position;
                    var br = character.vertex_BR.position;
                    var middle = (bl + tr) * 0.5f;
                    vertices[v] = middle + (bl - middle) * grow;
                    vertices[v + 1] = middle + (tl - middle) * grow;
                    vertices[v + 2] = middle + (tr - middle) * grow;
                    vertices[v + 3] = middle + (br - middle) * grow;
                }
            }

            _dialogue.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32 | TMP_VertexDataUpdateFlags.Vertices);
            _glowDirty = any;
        }

        /// <summary>Back to normal text at once (Skip, Show all, Stop, pause, a new line, or the setting off).</summary>
        void ClearGlow()
        {
            if (_glowLine < 0 && !_glowDirty)
                return;
            _glowLine = -1;
            _glow.Clear();
            if (_glowDirty)
            {
                _glowDirty = false;
                _dialogue.ForceMeshUpdate();
            }
        }

        // ---- Build -----------------------------------------------------------------------

        void BuildUi()
        {
            _canvasObject = HappiUiKit.CreateCanvas(transform, "HappiChatCanvas", 55, out _canvas);

            // Snap spots shown while dragging (under the box, never catching clicks).
            for (var i = 1; i <= 4; i++)
            {
                var marker = HappiUiKit.Panel(_canvasObject.transform, "SnapSpot " + i, _theme.SnapMarker, false);
                marker.rectTransform.anchorMin = Vector2.zero;
                marker.rectTransform.anchorMax = Vector2.zero;
                marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var ring = marker.gameObject.AddComponent<Outline>();
                ring.effectColor = HappiTheme.WithAlpha(_theme.Hover, 0.8f);
                ring.effectDistance = new Vector2(1.5f, -1.5f);
                var number = HappiUiKit.Text(marker.transform, "Number", i.ToString(), _theme.FontTitle * 1.6f, HappiTheme.WithAlpha(_theme.Text, 0.85f), TextAlignmentOptions.Center, true);
                HappiUiKit.Stretch(number.rectTransform, 0f, 0f, 0f, 0f);
                marker.gameObject.SetActive(false);
                _snapMarkers.Add(marker);
            }

            var boxImage = HappiUiKit.Panel(_canvasObject.transform, "HappiChat", _theme.PanelBackground, true);
            _box = boxImage.rectTransform;
            var outline = boxImage.gameObject.AddComponent<Outline>();
            outline.effectColor = HappiTheme.WithAlpha(_theme.Border, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Header bar: drag the box by it. Built first so the header buttons stay on top of it.
            var handle = HappiUiKit.Rect(_box, "DragHandle");
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0f, 0f, 0f, 0f);
            handleImage.raycastTarget = true;
            _dragHandle = handle.gameObject.AddComponent<HappiDragHandle>();
            _dragHandle.Rect = handle;
            _dragHandle.Pressed = () => _dragPending = true;

            _name = HappiUiKit.Text(_box, "Name", "Leia", _theme.FontTitle, _theme.Text, TextAlignmentOptions.MidlineLeft, true);
            _name.textWrappingMode = TextWrappingModes.NoWrap;
            _badge = HappiUiKit.Text(_box, "Badge", string.Empty, _theme.FontSmall, _theme.BadgeCloud, TextAlignmentOptions.MidlineLeft, false);
            _history = HappiUiKit.Button(_box, "History", "History", _theme.FontSmall, ToggleHistory);
            _mute = HappiUiKit.Button(_box, "Mute", ChatPresenter.MuteOffLabel, _theme.FontSmall, () => _chat.Presenter.ToggleMute());
            _look = HappiUiKit.Button(_box, "Look", ChatPresenter.LookLabelIdle, _theme.FontSmall, OnLook);
            _close = HappiUiKit.Button(_box, "Close", "×", _theme.FontBody, () => _chat.ShowChat(false));

            var historyImage = HappiUiKit.Panel(_box, "HistoryArea", _theme.PanelInset, true);
            _historyRoot = historyImage.rectTransform;
            _historyScroll = historyImage.gameObject.AddComponent<ScrollRect>();
            _historyScroll.horizontal = false;
            _historyScroll.vertical = true;
            _historyScroll.movementType = ScrollRect.MovementType.Clamped;
            _historyScroll.scrollSensitivity = 24f;
            var viewport = HappiUiKit.Rect(_historyRoot, "Viewport");
            HappiUiKit.Stretch(viewport, 6f, 6f, 6f, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _historyScroll.viewport = viewport;
            _historyContent = HappiUiKit.Rect(viewport, "Content");
            _historyContent.anchorMin = new Vector2(0f, 1f);
            _historyContent.anchorMax = new Vector2(1f, 1f);
            _historyContent.pivot = new Vector2(0.5f, 1f);
            _historyContent.sizeDelta = new Vector2(0f, 8f);
            _historyScroll.content = _historyContent;
            _historyRoot.gameObject.SetActive(false);

            _userLine = HappiUiKit.Text(_box, "UserLine", string.Empty, _theme.FontSmall, _theme.TextMuted, TextAlignmentOptions.TopLeft, false);
            _userLine.textWrappingMode = TextWrappingModes.NoWrap;

            _dialogueHit = HappiUiKit.Panel(_box, "Dialogue", _theme.PanelInset, true);
            var dialogueButton = _dialogueHit.gameObject.AddComponent<Button>();
            dialogueButton.transition = Selectable.Transition.None;
            var nav = dialogueButton.navigation;
            nav.mode = Navigation.Mode.None;
            dialogueButton.navigation = nav;
            dialogueButton.onClick.AddListener(OnDialogueClick);
            _dialogue = HappiUiKit.Text(_dialogueHit.transform, "Text", string.Empty, _theme.FontBody, _theme.Text, TextAlignmentOptions.TopLeft, false);
            _dialogue.overflowMode = TextOverflowModes.Page;
            HappiUiKit.Stretch(_dialogue.rectTransform, 8f, 6f, 8f, 6f);
            _note = HappiUiKit.Text(_box, "Note", string.Empty, _theme.FontSmall, _theme.TextMuted, TextAlignmentOptions.TopLeft, false);
            _note.fontStyle = FontStyles.Italic;

            _mode = HappiUiKit.Button(_box, "Mode", "Auto", _theme.FontSmall, ToggleMode);
            _next = HappiUiKit.Button(_box, "Next", "Next ›", _theme.FontSmall, () => _player.Next());
            _skip = HappiUiKit.Button(_box, "Skip", "Skip", _theme.FontSmall, () => _player.Skip());
            _showAll = HappiUiKit.Button(_box, "ShowAll", "Show all", _theme.FontSmall, () => _player.ShowAll());
            _narration = HappiUiKit.Button(_box, "Narration", "Narration voice: off", _theme.FontSmall, ToggleNarration);

            _mic = HappiUiKit.Button(_box, "Mic", ChatPresenter.MicIdleLabel, _theme.FontSmall, null);
            var trigger = _mic.Image.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            AddHold(trigger, UnityEngine.EventSystems.EventTriggerType.PointerDown, true);
            AddHold(trigger, UnityEngine.EventSystems.EventTriggerType.PointerUp, false);
            AddHold(trigger, UnityEngine.EventSystems.EventTriggerType.PointerExit, false);
            _input = HappiUiKit.Input(_box, "Message", "Message", _theme.FontBody, false);
            _input.onSubmit.AddListener(OnSubmit);
            _send = HappiUiKit.Button(_box, "Send", ChatPresenter.SendLabel, _theme.FontSmall, OnSend);

            _keyInput = HappiUiKit.Input(_box, "ApiKey", "API key", _theme.FontSmall, true);
            _keyInput.onSubmit.AddListener(_ => SaveKey());
            _saveKey = HappiUiKit.Button(_box, "SaveKey", "Save", _theme.FontSmall, SaveKey);
        }

        void AddHold(UnityEngine.EventSystems.EventTrigger trigger, UnityEngine.EventSystems.EventTriggerType type, bool held)
        {
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ =>
            {
                _micHeld = held;
                _chat.SetMicButtonHeld(held);
            });
            trigger.triggers.Add(entry);
        }

        // ---- Layout ----------------------------------------------------------------------

        void Layout()
        {
            if (Time.unscaledTime >= _keyCheckAt)
            {
                _keyCheckAt = Time.unscaledTime + 2f;
                _keySaved = SetupWizardGate.HasSavedApiKey();
            }

            var keyRow = _historyOpen || !_keySaved;
            // DPI scale, reduced only when the window is too low for the box.
            var scale = Mathf.Min(_theme.UiScale(Screen.dpi), Mathf.Max(1f, Screen.height / 480f));
            if (Mathf.Approximately(_layoutWidth, Screen.width) && Mathf.Approximately(_layoutHeight, Screen.height)
                && Mathf.Approximately(_layoutScale, scale) && _layoutHistory == _historyOpen && _layoutKeyRow == keyRow)
                return;

            _layoutWidth = Screen.width;
            _layoutHeight = Screen.height;
            _layoutScale = scale;
            _layoutHistory = _historyOpen;
            _layoutKeyRow = keyRow;
            _shownRevision = -1;

            var s = scale;
            var margin = 8f * s;
            var pad = _theme.Padding * s;
            var gap = _theme.Spacing * s;
            var row = 26f * s;
            // A fixed-width box (not the whole window), so it fits beside her.
            var width = Mathf.Max(220f, Mathf.Min(_theme.ChatBoxWidth * s, Screen.width - 2f * margin));
            var inner = width - 2f * pad;
            var minHeight = 196f * s;
            var height = _historyOpen
                ? Mathf.Max(minHeight, Screen.height * 0.82f)
                : Mathf.Clamp(Screen.height * 0.40f, minHeight, 320f * s);
            if (keyRow)
                height += row + gap;
            height = Mathf.Min(height, Screen.height - 2f * margin);

            _box.anchorMin = Vector2.zero;
            _box.anchorMax = Vector2.zero;
            _box.pivot = new Vector2(0.5f, 0.5f);
            _box.sizeDelta = new Vector2(width, height);
            _boxSize = new Vector2(width, height);
            _boxMargin = margin;
            _visibleCheckAt = -1f;

            SetFonts(s);

            // Header: name + badge on the left, History / Mute / Look / × on the right.
            var y = pad;
            var closeW = row;
            var lookW = 112f * s;
            var muteW = 58f * s;
            var historyW = 62f * s;
            var right = pad + inner;
            HappiUiKit.Place(_close.Rect, right - closeW, y, closeW, row);
            right -= closeW + gap;
            HappiUiKit.Place(_look.Rect, right - lookW, y, lookW, row);
            right -= lookW + gap;
            HappiUiKit.Place(_mute.Rect, right - muteW, y, muteW, row);
            right -= muteW + gap;
            HappiUiKit.Place(_history.Rect, right - historyW, y, historyW, row);
            right -= historyW + gap;
            var nameW = Mathf.Max(40f, right - pad);
            HappiUiKit.Place(_name.rectTransform, pad, y, nameW, row);
            HappiUiKit.Place(_dragHandle.Rect, 0f, 0f, width, pad + row + gap * 0.5f);
            _badge.gameObject.SetActive(false);
            y += row + gap;

            // Bottom rows, from the bottom up.
            var bottom = height - pad;
            if (keyRow)
            {
                var saveW = 56f * s;
                HappiUiKit.Place((RectTransform)_keyInput.transform, pad, bottom - row, inner - saveW - gap, row);
                HappiUiKit.Place(_saveKey.Rect, pad + inner - saveW, bottom - row, saveW, row);
                bottom -= row + gap;
            }

            _keyInput.gameObject.SetActive(keyRow);
            _saveKey.SetVisible(keyRow);

            var inputRow = row + 4f * s;
            var micW = 44f * s;
            var sendW = 64f * s;
            HappiUiKit.Place(_mic.Rect, pad, bottom - inputRow, micW, inputRow);
            HappiUiKit.Place((RectTransform)_input.transform, pad + micW + gap, bottom - inputRow, inner - micW - sendW - 2f * gap, inputRow);
            HappiUiKit.Place(_send.Rect, pad + inner - sendW, bottom - inputRow, sendW, inputRow);
            bottom -= inputRow + gap;

            var controls = new[] { _mode, _next, _skip, _showAll, _narration };
            var weights = new[] { 0.9f, 1f, 0.8f, 1.1f, 1.9f };
            var total = 0f;
            for (var i = 0; i < weights.Length; i++)
                total += weights[i];
            var x = pad;
            var avail = inner - gap * (controls.Length - 1);
            for (var i = 0; i < controls.Length; i++)
            {
                var w = avail * weights[i] / total;
                HappiUiKit.Place(controls[i].Rect, x, bottom - row, w, row);
                x += w + gap;
            }

            bottom -= row + gap;

            var noteH = 16f * s;
            HappiUiKit.Place(_note.rectTransform, pad, bottom - noteH, inner, noteH);
            bottom -= noteH + gap * 0.5f;

            if (_historyOpen)
            {
                var historyH = Mathf.Max(40f * s, (bottom - y) * 0.55f);
                HappiUiKit.Place(_historyRoot, pad, y, inner, historyH);
                y += historyH + gap;
            }

            _historyRoot.gameObject.SetActive(_historyOpen);

            var userH = 16f * s;
            HappiUiKit.Place(_userLine.rectTransform, pad, y, inner, userH);
            y += userH + gap * 0.5f;
            HappiUiKit.Place(_dialogueHit.rectTransform, pad, y, inner, Mathf.Max(24f * s, bottom - y));
            _historyWidth = -1f;
            if (_dialogue.text.Length > 0)
                KeepCurrentLineVisible();
        }

        void SetFonts(float s)
        {
            _name.fontSize = _theme.FontTitle * s;
            _badge.fontSize = _theme.FontSmall * s * 0.9f;
            _userLine.fontSize = _theme.FontSmall * s;
            _dialogue.fontSize = _theme.FontBody * s;
            _note.fontSize = _theme.FontSmall * s;
            var buttons = new[] { _history, _mute, _look, _mode, _next, _skip, _showAll, _narration, _mic, _send, _saveKey };
            for (var i = 0; i < buttons.Length; i++)
                buttons[i].Label.fontSize = _theme.FontSmall * s;
            _close.Label.fontSize = _theme.FontBody * s;
            SetInputFont(_input, _theme.FontBody * s);
            SetInputFont(_keyInput, _theme.FontSmall * s);
        }

        static void SetInputFont(TMP_InputField field, float size)
        {
            if (field == null)
                return;
            if (field.textComponent != null)
                field.textComponent.fontSize = size;
            var hint = field.placeholder as TMP_Text;
            if (hint != null)
                hint.fontSize = size;
            field.pointSize = size;
            field.caretWidth = Mathf.Max(2, Mathf.RoundToInt(size / 7f));
        }

        // ---- Refresh -----------------------------------------------------------------------

        void RefreshControls()
        {
            var presenter = _chat.Presenter;
            _send.Text = presenter.ActionLabel;
            _mute.Text = presenter.MuteLabel;
            _look.Text = presenter.LookLabel;
            _look.Interactable = presenter.LookInteractable;
            _input.interactable = !presenter.ReplyBusy;

            var recording = _chat.MicRecording;
            _mic.Text = ChatPresenter.MicLabelFor(recording, _chat.MicTranscribing);
            _mic.Image.color = recording ? _theme.Recording : Color.white;

            _mode.Text = _player.Mode == VnMode.Step ? "Step" : "Auto";
            _next.Interactable = _player.Active && (_player.HasNext || !_player.Finished);
            _skip.Interactable = _player.Active;
            _showAll.Interactable = _player.Active;
            _narration.Text = _player.VoiceNarration ? "Narration voice: on" : "Narration voice: off";

            // Her name, then the same model badge the old chat shows (provider, model, private), in its colour.
            var header = CompanionName() + "|" + _chat.ModelBadgeText + "|" + HappiUiKit.ColorTag(_chat.ModelBadgeColor);
            if (header != _shownBadge)
            {
                _shownBadge = header;
                var badge = _chat.ModelBadgeText.Replace('\n', ' ').Trim();
                _name.text = HappiUiKit.Literal(CompanionName())
                    + (badge.Length > 0 ? "  <size=72%><color=" + HappiUiKit.ColorTag(_chat.ModelBadgeColor) + ">" + HappiUiKit.Literal(badge) + "</color></size>" : string.Empty);
            }
        }

        /// <summary>Her name, refreshed every two seconds (the persona catalog is read from disk).</summary>
        string CompanionName()
        {
            if (_companionName == null || Time.unscaledTime >= _companionNameAt)
            {
                _companionNameAt = Time.unscaledTime + 2f;
                _companionName = Wheel.HappiNames.Companion(_settings);
            }

            return _companionName;
        }

        string _companionName;
        float _companionNameAt;

        void RefreshDialogue()
        {
            var revision = _chat.TranscriptRevision;
            var serial = _sink.ReplySerial;
            var current = _player.Current;
            var revealed = _player.Revealed;
            var lastAssistant = _chat.LastAssistantText;
            if (revision == _shownRevision && serial == _shownSerial && current == _shownCurrent && revealed == _shownRevealed
                && ReferenceEquals(lastAssistant, _shownLastAssistant) && !_sink.Streaming)
                return;

            _shownRevision = revision;
            _shownSerial = serial;
            _shownCurrent = current;
            _shownRevealed = revealed;
            _shownLastAssistant = lastAssistant;

            var lines = _chat.TranscriptLines;
            string user = string.Empty;
            string note = string.Empty;
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line == null)
                    continue;
                if (line.Kind == ChatLineKind.System && note.Length == 0 && user.Length == 0)
                    note = line.Text;
                if (line.Kind == ChatLineKind.User)
                {
                    user = line.Text;
                    break;
                }
            }

            _userLine.text = user.Length > 0 ? "You: " + HappiUiKit.Literal(Shorten(user, 160)) : string.Empty;
            _note.text = HappiUiKit.Literal(Shorten(note, 220));

            string text;
            if (_player.Segments.Count > 0)
            {
                text = VnRender.Dialogue(_player.Segments, _player.Revealed, _player.Finished ? -1 : _player.Current, _theme);
                _glowLineStart = VnRender.LineStart(_player.Segments, _player.Current);
            }
            else if (_sink.Streaming)
                text = "<i>…</i>";
            else if (!string.IsNullOrEmpty(lastAssistant))
                text = HappiUiKit.Literal(Shorten(lastAssistant, ChatTranscriptView.MaxShownCharacters));
            else
                text = "<i>" + HappiUiKit.Literal("Say hi to " + CompanionName() + ".") + "</i>";

            if (_dialogue.text != text)
            {
                _dialogue.text = text;
                KeepCurrentLineVisible();
            }
        }

        /// <summary>Long replies: drop the oldest revealed lines from view so the current one stays inside the box.</summary>
        void KeepCurrentLineVisible()
        {
            _dialogue.overflowMode = TextOverflowModes.Page;
            _dialogue.pageToDisplay = 1;
            _dialogue.ForceMeshUpdate();
            if (_dialogue.textInfo == null)
                return;
            _dialogue.pageToDisplay = Mathf.Max(1, _dialogue.textInfo.pageCount);
        }

        void RefreshHistory()
        {
            var lines = _chat.TranscriptLines;
            var width = _historyContent.rect.width;
            if (width < 8f)
                return;
            var widthChanged = Mathf.Abs(width - _historyWidth) > 0.5f;
            if (!_transcript.Update(lines) && !widthChanged && _historyTexts.Count >= _transcript.Count)
                return;

            var atBottom = _historyScroll.verticalNormalizedPosition <= 0.01f;
            _historyWidth = width;
            var count = _transcript.Count;
            while (_historyTexts.Count < count)
            {
                var text = HappiUiKit.Text(_historyContent, "Message", string.Empty, _theme.FontSmall * _layoutScale, _theme.Text, TextAlignmentOptions.TopLeft, false);
                text.overflowMode = TextOverflowModes.Overflow;
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                _historyTexts.Add(text);
                _historyHeights.Add(-1f);
            }

            var y = 0f;
            for (var i = 0; i < _historyTexts.Count; i++)
            {
                var text = _historyTexts[i];
                if (i >= count)
                {
                    if (text.gameObject.activeSelf)
                    {
                        text.text = string.Empty;
                        text.gameObject.SetActive(false);
                    }

                    continue;
                }

                if (!text.gameObject.activeSelf)
                    text.gameObject.SetActive(true);
                if (_transcript.Changed(i) || widthChanged || _historyHeights[i] < 0f)
                {
                    text.fontSize = _theme.FontSmall * _layoutScale;
                    text.text = HistoryEntry(_transcript.Entry(i));
                    _historyHeights[i] = Mathf.Max(8f, text.GetPreferredValues(text.text, width - 4f, 0f).y);
                }

                var rect = text.rectTransform;
                rect.anchoredPosition = new Vector2(2f, -y);
                rect.sizeDelta = new Vector2(width - 4f, _historyHeights[i]);
                y += _historyHeights[i] + 6f;
            }

            _historyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(8f, y));
            if (atBottom || widthChanged)
                _historyScroll.verticalNormalizedPosition = 0f;
        }

        string HistoryEntry(string entry)
        {
            var split = entry.IndexOf('\n');
            if (split < 0)
                return HappiUiKit.Literal(entry);
            var label = entry.Substring(0, split);
            var body = entry.Substring(split + 1);
            if (label == "Companion")
                label = CompanionName();
            return "<b>" + HappiUiKit.Literal(label) + "</b>\n" + HappiUiKit.Literal(body);
        }

        static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text ?? string.Empty;
            return "…" + text.Substring(text.Length - max);
        }

        // ---- Actions ---------------------------------------------------------------------

        void OnSend()
        {
            var result = _chat.Presenter.PrimaryAction(_input != null ? _input.text : string.Empty);
            if (result == ChatActionResult.Sent && _input != null)
            {
                _input.text = string.Empty;
                _input.ActivateInputField();
            }
        }

        void OnSubmit(string text)
        {
            if (_chat.Presenter.Send(text) != ChatActionResult.Sent)
                return;
            _input.text = string.Empty;
            _input.ActivateInputField();
        }

        void OnLook()
        {
            _chat.Presenter.Look();
        }

        void OnDialogueClick()
        {
            if (_player.Mode == VnMode.Step)
                _player.Next();
        }

        void ToggleMode()
        {
            _player.Mode = _player.Mode == VnMode.Step ? VnMode.Auto : VnMode.Step;
            PlayerPrefs.SetInt(ModePref, _player.Mode == VnMode.Step ? 1 : 0);
        }

        void ToggleNarration()
        {
            if (_settings == null)
                return;
            var settings = _settings.CurrentSettings;
            _settings.Presenter.SetVoiceNarration(settings == null || !settings.VoiceNarration);
        }

        void ToggleHistory()
        {
            _historyOpen = !_historyOpen;
            _history.Text = _historyOpen ? "Hide" : "History";
            _historyWidth = -1f;
        }

        void SaveKey()
        {
            if (_keyInput == null)
                return;
            _chat.SaveApiKeyFromNewUi(_keyInput.text);
            _keyInput.text = string.Empty;
        }

        void FocusInput()
        {
            var typing = GetComponent<ITypingFocus>();
            if (typing != null)
                typing.FocusForTyping();
            if (_input != null)
            {
                _input.ActivateInputField();
                _input.Select();
            }
        }

        void SyncFocus()
        {
            var focused = (_input != null && _input.isFocused) || (_keyInput != null && _keyInput.isFocused);
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
