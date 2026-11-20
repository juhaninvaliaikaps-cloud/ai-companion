using System;
using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.NewUI.VisualNovel;
using AICompanion.Overlay;
using AICompanion.Settings;
using AICompanion.UI;
using AICompanion.UI.Presenters;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Director = AICompanion.AnimationDirector.AnimationDirector;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-104 PR C / JUH-108: the new chat, a visual-novel dialogue box along the bottom of the
    /// overlay window. The old <see cref="ChatPanel"/> stays the chat engine; this view only
    /// draws and calls <see cref="ChatPresenter"/>. Each reply plays line by line: narration in
    /// italics, speech voiced, the current line highlighted, Auto or Step, Next / Skip / Show
    /// all. The conversation and the current line share one scrollable transcript (one text
    /// object per message, last 100 messages, 6000 characters each, as #64). Exists only while
    /// the new UI is on.
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
        TextMeshProUGUI _dialogue;
        TextMeshProUGUI _note;
        Image _dialogueHit;
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
        bool _focusClaimed;
        bool _wasVisible;
        bool _micHeld;
        int _shownRevision = -1;
        int _shownSerial = -1;
        int _shownCurrent = -2;
        int _shownRevealed = -1;
        bool _shownFinished;
        int _appendedSerial = -1;
        int _shownLineCount = -1;
        float _layoutWidth = -1f;
        float _layoutHeight = -1f;
        float _layoutScale = -1f;
        float _layoutChatHeight = -1f;
        bool _layoutKeyRow;
        float _historyWidth = -1f;
        bool _liveDirty;
        readonly List<ChatLine> _historyLines = new List<ChatLine>();
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
        readonly List<Image> _portraitMarkers = new List<Image>();
        HappiDragHandle _portraitHandle;
        bool _portraitDragPending;
        bool _portraitDragging;
        Vector2 _portraitDragOffset;
        bool _portraitResizing;
        ResizeEdges _portraitResizeEdges;
        Rect _portraitResizeStart;
        Vector2 _portraitResizePointerStart;
        Vector2 _portraitResizeSize;
        int _placedSnap = ChatSnapSpots.Default;
        float _roomBoxHeight;

        // JUH-127: resizing by the edges and corners, and the mouse wheel over the box.
        readonly ChatResizeHandle[] _resizeHandles = new ChatResizeHandle[8];
        ResizeEdges _resizePending;
        ResizeEdges _resizeEdges;
        bool _resizing;
        Rect _resizeStart;
        Vector2 _resizePointerStart;
        Vector2 _resizeSize;
        float _roomBoxWidth;
        float _layoutBoxWidth = -1f;
        float _layoutBoxHeight = -1f;
        bool _middleScrolling;
        float _middleLastY;
        HappiButton[] _controlRow;
        HappiButton[] _fontButtons;
        static readonly float[] ControlWeights = { 0.9f, 1f, 0.8f, 1.1f, 1.9f };

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
            {
                _window.SetSideRoom(0);
                _window.SetBottomRoom(0);
            }

            _snapMarkers.Clear();
            _portraitMarkers.Clear();
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
                _resizing = false;
                _resizePending = ResizeEdges.None;
                _middleScrolling = false;
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
            RefreshHistory();
            MiddleScroll();
            ShowResizeCursor();
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

            // Where she is: the box lives beside (or under) her and follows her when she walks or is moved.
            HerBody body;
            if (!_bodyLocator.TryGetBody(Time.unscaledDeltaTime, out body))
            {
                body.Head = new Vector2(Screen.width * 0.5f, Screen.height * 0.82f);
                body.FeetY = 0f;
            }

            var scale = Mathf.Max(0.5f, _layoutScale);
            var gap = _theme.ChatSideGap * scale;
            var under = UnderAllowed();
            var pointer = Pointer();
            if (_resizePending != ResizeEdges.None)
            {
                // JUH-127: an edge or corner was pressed: resize from where the box is now.
                _resizeEdges = _resizePending;
                _resizePending = ResizeEdges.None;
                _resizing = true;
                _dragging = false;
                _dragPending = false;
                _resizeStart = new Rect(_boxCenter - _boxSize * 0.5f, _boxSize);
                _resizePointerStart = pointer;
                _resizeSize = _boxSize;
            }

            if (_resizing)
            {
                if (!LeftHeld())
                {
                    // Let go: save the size (100% units) and where it is, the same way a header drag does.
                    _resizing = false;
                    var near = ChatSnapSpots.Nearest(_boxCenter, body, _boxSize, gap, _theme.ChatSnapRadius * scale, under, _visibleArea, _boxMargin);
                    var placed = ChatSnapSpots.OffsetFor(_boxCenter, body, scale);
                    var widthUnits = ChatBoxResize.ToUnits(_resizeSize.x, scale);
                    var heightUnits = ChatBoxResize.ToUnits(_resizeSize.y, scale);
                    if (_settings != null)
                        _settings.SaveLayoutSetting(s =>
                        {
                            s.ChatBoxSnap = near;
                            s.ChatBoxOffsetX = placed.x;
                            s.ChatBoxOffsetY = placed.y;
                            s.ChatBoxWidthPx = widthUnits;
                            s.ChatBoxHeightPx = heightUnits;
                        });
                    _layoutWidth = -1f;
                    Debug.Log("New UI: chat box resized to " + Mathf.RoundToInt(widthUnits) + " x " + Mathf.RoundToInt(heightUnits));
                }
                else
                {
                    var bounds = new Rect(
                        _visibleArea.xMin + _boxMargin,
                        _visibleArea.yMin + _boxMargin,
                        Mathf.Max(0f, _visibleArea.width - 2f * _boxMargin),
                        Mathf.Max(0f, _visibleArea.height - 2f * _boxMargin));
                    var min = new Vector2(ChatBoxResize.MinWidth * scale, MinBoxHeight(scale));
                    var rect = ChatBoxResize.Apply(_resizeStart, pointer - _resizePointerStart, _resizeEdges, min, bounds);
                    _resizeSize = rect.size;
                    _boxCenter = rect.center;
                }
            }

            if (_dragPending && !_resizing)
            {
                _dragPending = false;
                _dragging = true;
                _dragOffset = _boxCenter - pointer;
            }

            var snap = settings != null ? settings.ChatBoxSnap : ChatSnapSpots.Default;
            if (_dragging)
            {
                var near = ChatSnapSpots.Nearest(_boxCenter, body, _boxSize, gap, _theme.ChatSnapRadius * scale, under, _visibleArea, _boxMargin);
                if (!LeftHeld())
                {
                    _dragging = false;
                    ShowSnapMarkers(false, body, gap, 0, under);
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
                    ShowSnapMarkers(true, body, gap, near, under);
                }
            }

            if (!_dragging && !_resizing)
            {
                // A spot without room (she stands at a screen edge) flips to her other side; spot 5
                // needs the 2D avatar and becomes spot 4 without it.
                snap = ChatSnapSpots.Fit(snap, body, _boxSize, gap, _visibleArea, _boxMargin, under);
                _placedSnap = snap;
                var offset = settings != null ? new Vector2(settings.ChatBoxOffsetX, settings.ChatBoxOffsetY) : Vector2.zero;
                _boxCenter = ChatBoxPlacement.Clamp(ChatSnapSpots.Resolve(snap, offset, scale, body, _boxSize, gap, under), _boxSize, _visibleArea, _boxMargin);
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
            PlacePortrait(settings, body, scale, gap, pointer);
        }

        /// <summary>JUH-127: the smallest box height now: every row, and the API key row when it shows.</summary>
        float MinBoxHeight(float scale)
        {
            return (ChatBoxResize.MinHeight + (_layoutKeyRow ? 32f : 0f)) * scale;
        }

        /// <summary>Spot 5 (under her) needs the 2D video avatar: its layout can leave room below her.</summary>
        bool UnderAllowed()
        {
            var video = GetComponent<VideoAvatar>();
            return video != null && video.State == VideoAvatarState.Playing;
        }

        /// <summary>While dragging: ghost boxes at the snap spots; the one the box would snap to is brighter.</summary>
        void ShowSnapMarkers(bool show, HerBody body, float gap, int active, bool under)
        {
            for (var i = 0; i < _snapMarkers.Count; i++)
            {
                var marker = _snapMarkers[i];
                var spot = i + 1;
                var visible = show && (spot != ChatSnapSpots.Under || under);
                if (marker.gameObject.activeSelf != visible)
                    marker.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var rect = marker.rectTransform;
                rect.sizeDelta = _boxSize;
                rect.anchoredPosition = ChatBoxPlacement.Clamp(ChatSnapSpots.Spot(spot, body, _boxSize, gap), _boxSize, _visibleArea, _boxMargin);
                marker.color = spot == active ? _theme.SnapMarkerActive : _theme.SnapMarker;
            }
        }

        // ---- Moving the 2D portrait -------------------------------------------------------

        /// <summary>
        /// The 2D portrait can be dragged (by an invisible handle over it). Dropped near the chat
        /// box's left or right side it docks there, scaled so its top and bottom line up with the
        /// box; dropped elsewhere it stays there (relative to her head). Not dragged: automatic
        /// beside her head as before.
        /// </summary>
        void PlacePortrait(CompanionSettings settings, HerBody body, float scale, float gap, Vector2 pointer)
        {
            var shown = ExpressionPortraitPanel.ShownRect;
            var guiVisible = new Rect(_visibleArea.x, Screen.height - _visibleArea.yMax, _visibleArea.width, _visibleArea.height);
            var pointerGui = new Vector2(pointer.x, Screen.height - pointer.y);
            var headGui = new Vector2(body.Head.x, Screen.height - body.Head.y);
            var savedWidth = settings != null ? settings.PortraitWidthPx : 0f;
            var savedHeight = settings != null ? settings.PortraitHeightPx : 0f;
            var normal = PortraitResize.Resolve(
                savedWidth,
                savedHeight,
                new Vector2(PortraitPlacement.Width, PortraitPlacement.Height),
                scale,
                ExpressionPortraitPanel.NativeSize);

            var left = PortraitDocking.DockRect(_boxCenter, _boxSize, PortraitDocking.LeftOfBox, gap, Screen.height);
            var right = PortraitDocking.DockRect(_boxCenter, _boxSize, PortraitDocking.RightOfBox, gap, Screen.height);
            // JUH-139: keep the edge next to the box when the user resized the portrait.
            left = PortraitDocking.Sized(left, normal, PortraitDocking.LeftOfBox);
            right = PortraitDocking.Sized(right, normal, PortraitDocking.RightOfBox);

            if (!_portraitResizing && shown.width > 0f && Input.GetMouseButtonDown(0))
            {
                var edge = PortraitResize.EdgeAt(shown, pointerGui, scale);
                if (edge != ResizeEdges.None)
                {
                    _portraitResizing = true;
                    _portraitResizeEdges = edge;
                    _portraitResizeStart = shown;
                    _portraitResizePointerStart = pointerGui;
                    _portraitResizeSize = shown.size;
                    _portraitDragPending = false;
                    _portraitDragging = false;
                }
            }

            if (_portraitResizing)
            {
                if (LeftHeld())
                {
                    var min = new Vector2(PortraitResize.MinWidth * scale, PortraitResize.MinHeight * scale);
                    var max = ExpressionPortraitPanel.NativeSize;
                    var aspect = max.y > 0f ? max.x / max.y : 0f;
                    var rect = PortraitResize.Apply(
                        _portraitResizeStart,
                        pointerGui - _portraitResizePointerStart,
                        _portraitResizeEdges,
                        min,
                        max,
                        guiVisible,
                        aspect);
                    _portraitResizeSize = rect.size;
                    ExpressionPortraitPanel.NewUiTarget = rect;
                    PlacePortraitHandle(rect);
                    return;
                }

                _portraitResizing = false;
                _portraitDragPending = false;
                if (_settings != null)
                {
                    _settings.SaveLayoutSetting(s =>
                    {
                        s.PortraitWidthPx = PortraitResize.ToUnits(_portraitResizeSize.x, scale);
                        s.PortraitHeightPx = PortraitResize.ToUnits(_portraitResizeSize.y, scale);
                    });
                }
                settings = _settings != null ? _settings.CurrentSettings : settings;
            }

            if (_portraitDragPending)
            {
                _portraitDragPending = false;
                if (shown.width > 0f)
                {
                    _portraitDragging = true;
                    // Keep the grab point at the same place on the picture.
                    _portraitDragOffset = shown.position - pointerGui;
                }
            }

            if (_portraitDragging)
            {
                var dragged = new Rect(pointerGui + _portraitDragOffset, normal);
                var near = PortraitDocking.Nearest(dragged, left, right, _theme.ChatSnapRadius * scale);
                if (LeftHeld())
                {
                    ExpressionPortraitPanel.NewUiTarget = PortraitDocking.Clamp(dragged, guiVisible);
                    ShowPortraitMarkers(true, left, right, near);
                    PlacePortraitHandle(shown);
                    return;
                }

                _portraitDragging = false;
                ShowPortraitMarkers(false, left, right, 0);
                var dock = near != PortraitDocking.Auto ? near : PortraitDocking.Free;
                var offset = (dragged.position - headGui) / Mathf.Max(0.01f, scale);
                if (_settings != null)
                    _settings.SaveLayoutSetting(s =>
                    {
                        s.PortraitDock = dock;
                        s.PortraitOffsetX = offset.x;
                        s.PortraitOffsetY = offset.y;
                    });
                settings = _settings != null ? _settings.CurrentSettings : settings;
            }

            var saved = settings != null ? settings.PortraitDock : PortraitDocking.Auto;
            var target = new Rect(0f, 0f, 0f, 0f);
            if (saved == PortraitDocking.LeftOfBox || saved == PortraitDocking.RightOfBox)
            {
                var side = PortraitDocking.FitSide(saved, left, right, guiVisible.xMin, guiVisible.xMax);
                target = side == PortraitDocking.LeftOfBox ? left : right;
            }
            else if (saved == PortraitDocking.Free && settings != null)
            {
                target = new Rect(headGui + new Vector2(settings.PortraitOffsetX, settings.PortraitOffsetY) * scale, normal);
            }
            else
            {
                target = PortraitPlacement.Beside(headGui, Screen.width, Screen.height,
                    ExpressionPortraitPanel.NewUiAvoid, PortraitPlacement.DebugLabel, ExpressionPortraitPanel.NewUiPreferLeft);
            }

            if (target.width > 0f)
            {
                target.size = normal;
                target = PortraitDocking.Clamp(target, guiVisible);
            }

            ExpressionPortraitPanel.NewUiTarget = target.width > 0f ? target : new Rect(0f, 0f, 0f, 0f);
            PlacePortraitHandle(shown);
        }

        /// <summary>An invisible handle over the portrait (in this canvas), so it can be pressed and dragged.</summary>
        void PlacePortraitHandle(Rect shown)
        {
            var show = shown.width > 0f && shown.height > 0f;
            if (_portraitHandle.gameObject.activeSelf != show)
                _portraitHandle.gameObject.SetActive(show);
            if (!show)
                return;
            _portraitHandle.Rect.sizeDelta = shown.size;
            _portraitHandle.Rect.anchoredPosition = new Vector2(shown.x + shown.width * 0.5f, Screen.height - (shown.y + shown.height * 0.5f));
        }

        void ShowPortraitMarkers(bool show, Rect left, Rect right, int active)
        {
            for (var i = 0; i < _portraitMarkers.Count; i++)
            {
                var marker = _portraitMarkers[i];
                if (marker.gameObject.activeSelf != show)
                    marker.gameObject.SetActive(show);
                if (!show)
                    continue;
                var rect = i == 0 ? left : right;
                marker.rectTransform.sizeDelta = rect.size;
                marker.rectTransform.anchoredPosition = new Vector2(rect.x + rect.width * 0.5f, Screen.height - (rect.y + rect.height * 0.5f));
                marker.color = active == i + 1 ? _theme.SnapMarkerActive : _theme.SnapMarker;
            }
        }

        void ClearPortraitHooks()
        {
            ExpressionPortraitPanel.NewUiAvoid = new Rect(0f, 0f, 0f, 0f);
            ExpressionPortraitPanel.NewUiPreferLeft = null;
            ExpressionPortraitPanel.NewUiTarget = new Rect(0f, 0f, 0f, 0f);
            _portraitDragging = false;
            _portraitDragPending = false;
            _portraitResizing = false;
        }

        /// <summary>
        /// Room for the box: on each side of her always, and below her while the box is under her
        /// (spot 5, she stands on it). None while the classic settings or the setup wizard is open
        /// (they sit at the window's left edge).
        /// </summary>
        void UpdateSideRoom()
        {
            if (_window == null)
                return;
            var classic = _settings != null && _settings.IsOpen;
            var wizard = GetComponent<SetupWizard>();
            var wizardOpen = wizard != null && wizard.IsOpen;
            var scale = _theme.UiScale(Screen.dpi);
            var gap = _theme.ChatSideGap * scale;
            var visible = _chat != null && _chat.ChatVisible;
            // JUH-127: the room follows the box's real width; it is set from the layout, not while resizing.
            var roomWidth = _roomBoxWidth > 0f ? _roomBoxWidth : _theme.ChatBoxWidth * scale;
            var side = classic || wizardOpen ? 0 : ChatSnapSpots.SideRoomFor(roomWidth, gap);
            var below = !classic && !wizardOpen && visible && _placedSnap == ChatSnapSpots.Under && _roomBoxHeight > 0f
                ? ChatSnapSpots.BottomRoomFor(_roomBoxHeight, gap)
                : 0;
            _window.SetSideRoom(side);
            // Small changes (rounding, DPI) are ignored, so the window is not resized over and over.
            if (ChatSnapSpots.BottomRoomChanged(_window.BottomRoom, below))
                _window.SetBottomRoom(below);
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

        /// <summary>Settings > Happi > "Reset chat position": back to the default spot (5 under her in 2D, 4 beside her on the right in 3D).</summary>
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
            for (var i = 1; i <= ChatSnapSpots.Last; i++)
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

            // Portrait dock spots (left and right of the box) shown while the portrait is dragged.
            for (var i = 0; i < 2; i++)
            {
                var marker = HappiUiKit.Panel(_canvasObject.transform, i == 0 ? "PortraitDockLeft" : "PortraitDockRight", _theme.SnapMarker, false);
                marker.rectTransform.anchorMin = Vector2.zero;
                marker.rectTransform.anchorMax = Vector2.zero;
                marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var ring = marker.gameObject.AddComponent<Outline>();
                ring.effectColor = HappiTheme.WithAlpha(_theme.Hover, 0.8f);
                ring.effectDistance = new Vector2(1.5f, -1.5f);
                marker.gameObject.SetActive(false);
                _portraitMarkers.Add(marker);
            }

            // Invisible handle over the 2D portrait (it lives in its own canvas), to drag it.
            var portraitHandle = HappiUiKit.Rect(_canvasObject.transform, "PortraitHandle");
            portraitHandle.anchorMin = Vector2.zero;
            portraitHandle.anchorMax = Vector2.zero;
            portraitHandle.pivot = new Vector2(0.5f, 0.5f);
            var portraitHit = portraitHandle.gameObject.AddComponent<Image>();
            portraitHit.color = new Color(0f, 0f, 0f, 0f);
            portraitHit.raycastTarget = true;
            _portraitHandle = portraitHandle.gameObject.AddComponent<HappiDragHandle>();
            _portraitHandle.Rect = portraitHandle;
            _portraitHandle.Pressed = () => _portraitDragPending = true;
            portraitHandle.gameObject.SetActive(false);

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

            _dialogueHit = HappiUiKit.Panel(_historyContent, "Dialogue", _theme.PanelInset, true);
            _dialogueHit.gameObject.AddComponent<RectMask2D>();
            var dialogueButton = _dialogueHit.gameObject.AddComponent<Button>();
            dialogueButton.transition = Selectable.Transition.None;
            var nav = dialogueButton.navigation;
            nav.mode = Navigation.Mode.None;
            dialogueButton.navigation = nav;
            dialogueButton.onClick.AddListener(OnDialogueClick);
            _dialogue = HappiUiKit.Text(_dialogueHit.transform, "Text", string.Empty, _theme.FontBody, _theme.Text, TextAlignmentOptions.BottomLeft, false);
            _dialogue.overflowMode = TextOverflowModes.Overflow;
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

            _controlRow = new[] { _mode, _next, _skip, _showAll, _narration };
            _fontButtons = new[] { _mute, _look, _mode, _next, _skip, _showAll, _narration, _mic, _send, _saveKey };

            // JUH-127: the edges and corners, last so they sit above everything in the box.
            var edges = new[]
            {
                ResizeEdges.Left, ResizeEdges.Right, ResizeEdges.Bottom, ResizeEdges.Top,
                ResizeEdges.TopLeft, ResizeEdges.TopRight, ResizeEdges.BottomLeft, ResizeEdges.BottomRight,
            };
            for (var i = 0; i < edges.Length; i++)
            {
                var image = HappiUiKit.Panel(_box, "Resize " + edges[i], new Color(0f, 0f, 0f, 0f), true);
                var resizeHandle = image.gameObject.AddComponent<ChatResizeHandle>();
                resizeHandle.Edges = edges[i];
                resizeHandle.Rect = image.rectTransform;
                resizeHandle.Pressed = e => _resizePending = e;
                _resizeHandles[i] = resizeHandle;
            }
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

            var keyRow = !_keySaved;
            var settings = _settings != null ? _settings.CurrentSettings : null;
            var chatHeight = settings != null ? Mathf.Clamp(settings.ChatBoxHeight, 0.6f, 2f) : 1f;
            // Her own height (the window without the room for the box): the box size must not depend
            // on that room, or making room would resize the box and change the room again.
            var bodyHeight = _window != null ? (float)_window.BodyHeight : Screen.height;
            // DPI scale, reduced only when the window is too low for the box.
            var scale = Mathf.Min(_theme.UiScale(Screen.dpi), Mathf.Max(1f, bodyHeight / 480f));
            // JUH-127: the size the user dragged to (100% units, 0 for automatic), or the size being dragged now.
            var savedWidth = settings != null ? settings.ChatBoxWidthPx : 0f;
            var savedHeight = settings != null ? settings.ChatBoxHeightPx : 0f;
            var targetWidth = _resizing ? _resizeSize.x : savedWidth;
            var targetHeight = _resizing ? _resizeSize.y : savedHeight;
            if (Mathf.Approximately(_layoutWidth, Screen.width) && Mathf.Approximately(_layoutHeight, Screen.height)
                && Mathf.Approximately(_layoutScale, scale) && Mathf.Approximately(_layoutChatHeight, chatHeight)
                && _layoutKeyRow == keyRow
                && Mathf.Approximately(_layoutBoxWidth, targetWidth) && Mathf.Approximately(_layoutBoxHeight, targetHeight))
                return;

            _layoutWidth = Screen.width;
            _layoutHeight = Screen.height;
            _layoutScale = scale;
            _layoutChatHeight = chatHeight;
            _layoutKeyRow = keyRow;
            _layoutBoxWidth = targetWidth;
            _layoutBoxHeight = targetHeight;
            _shownRevision = -1;

            var s = scale;
            var margin = 8f * s;
            var pad = _theme.Padding * s;
            var gap = _theme.Spacing * s;
            var row = 26f * s;
            // Automatic: a fixed-width box (not the whole window), so it fits beside her.
            var maxWidth = Screen.width - 2f * margin;
            var maxHeight = Screen.height - 2f * margin;
            var autoWidth = Mathf.Max(220f, Mathf.Min(_theme.ChatBoxWidth * s, maxWidth));
            var minHeight = 196f * s * chatHeight;
            var collapsed = Mathf.Clamp(bodyHeight * 0.40f * chatHeight, minHeight, 320f * s * chatHeight);
            float width;
            float height;
            if (_resizing)
            {
                // JUH-127: being dragged; ChatBoxResize.Apply already kept it inside the limits.
                width = _resizeSize.x;
                height = _resizeSize.y;
            }
            else
            {
                width = ChatBoxResize.Resolve(savedWidth, autoWidth, s, Mathf.Min(ChatBoxResize.MinWidth * s, maxWidth), maxWidth);
                if (savedHeight > 0f)
                {
                    // A size the user chose: opening History shares it instead of making the box taller.
                    height = ChatBoxResize.Resolve(savedHeight, collapsed, s, Mathf.Min(MinBoxHeight(s), maxHeight), maxHeight);
                    _roomBoxHeight = height;
                }
                else
                {
                    height = collapsed;
                    if (keyRow)
                        height += row + gap;
                    // The room under her (spot 5) follows the closed box.
                    _roomBoxHeight = collapsed + (!_keySaved ? row + gap : 0f);
                }

                _roomBoxWidth = width;
            }

            var inner = width - 2f * pad;
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
            var right = pad + inner;
            HappiUiKit.Place(_close.Rect, right - closeW, y, closeW, row);
            right -= closeW + gap;
            HappiUiKit.Place(_look.Rect, right - lookW, y, lookW, row);
            right -= lookW + gap;
            HappiUiKit.Place(_mute.Rect, right - muteW, y, muteW, row);
            right -= muteW + gap;
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

            // Cached arrays: the layout runs every frame while the box is being resized.
            var controls = _controlRow;
            var weights = ControlWeights;
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

            // JUH-127: one scrollable conversation window. There is no separate History area
            // or History button any more.
            var historyH = Mathf.Max(40f * s, bottom - y);
            HappiUiKit.Place(_historyRoot, pad, y, inner, historyH);
            _historyRoot.gameObject.SetActive(true);
            _historyWidth = -1f;
            PlaceResizeHandles(width, height, s);
        }

        /// <summary>JUH-127: the 8 handles along the edges and in the corners (in box coordinates, top-left, y down).</summary>
        void PlaceResizeHandles(float width, float height, float s)
        {
            var edge = ChatBoxResize.EdgeThickness * s;
            var corner = ChatBoxResize.CornerSize * s;
            for (var i = 0; i < _resizeHandles.Length; i++)
            {
                var handle = _resizeHandles[i];
                if (handle == null)
                    continue;
                switch (handle.Edges)
                {
                    case ResizeEdges.Left: HappiUiKit.Place(handle.Rect, 0f, corner, edge, height - 2f * corner); break;
                    case ResizeEdges.Right: HappiUiKit.Place(handle.Rect, width - edge, corner, edge, height - 2f * corner); break;
                    case ResizeEdges.Top: HappiUiKit.Place(handle.Rect, corner, 0f, width - 2f * corner, edge); break;
                    case ResizeEdges.Bottom: HappiUiKit.Place(handle.Rect, corner, height - edge, width - 2f * corner, edge); break;
                    case ResizeEdges.TopLeft: HappiUiKit.Place(handle.Rect, 0f, 0f, corner, corner); break;
                    case ResizeEdges.TopRight: HappiUiKit.Place(handle.Rect, width - corner, 0f, corner, corner); break;
                    case ResizeEdges.BottomLeft: HappiUiKit.Place(handle.Rect, 0f, height - corner, corner, corner); break;
                    case ResizeEdges.BottomRight: HappiUiKit.Place(handle.Rect, width - corner, height - corner, corner, corner); break;
                }
            }
        }

        void SetFonts(float s)
        {
            _name.fontSize = _theme.FontTitle * s;
            _badge.fontSize = _theme.FontSmall * s * 0.9f;
            _dialogue.fontSize = _theme.FontBody * s;
            _note.fontSize = _theme.FontSmall * s;
            var buttons = _fontButtons;
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

        /// <summary>Her name, cached until the active persona is applied or edited.</summary>
        string CompanionName()
        {
            var current = _settings != null ? _settings.CurrentSettings : null;
            var key = (current != null ? current.PersonaId : string.Empty)
                + "|" + (_settings != null ? _settings.PersonaRevision : 0);
            if (_companionName == null || !string.Equals(_companionNameKey, key, StringComparison.Ordinal))
            {
                _companionNameKey = key;
                _companionName = Wheel.HappiNames.Companion(_settings);
            }

            return _companionName;
        }

        string _companionName;
        string _companionNameKey;

        void RefreshDialogue()
        {
            var revision = _chat.TranscriptRevision;
            var serial = _sink.ReplySerial;
            var current = _player.Current;
            var revealed = _player.Revealed;
            var finished = _player.Finished;
            var lineCount = _chat.TranscriptLines.Count;
            if (revision < _shownRevision || (_shownLineCount >= 0 && lineCount < _shownLineCount))
                ClearFinishedReplies();
            _shownLineCount = lineCount;
            if (revision == _shownRevision && serial == _shownSerial && current == _shownCurrent && revealed == _shownRevealed
                && finished == _shownFinished && !_sink.Streaming)
                return;

            _shownRevision = revision;
            _shownSerial = serial;
            _shownCurrent = current;
            _shownRevealed = revealed;
            _shownFinished = finished;

            if (finished)
                AppendFinishedReply(serial);

            var lines = _chat.TranscriptLines;
            string note = string.Empty;
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line == null)
                    continue;
                if (line.Kind == ChatLineKind.System && note.Length == 0)
                    note = line.Text;
                if (line.Kind == ChatLineKind.User)
                    break;
            }

            _note.text = HappiUiKit.Literal(Shorten(note, 220));

            string text;
            if (_player.Segments.Count > 0 && _appendedSerial != serial)
            {
                text = VnRender.Dialogue(_player.Segments, _player.Revealed, _player.Finished ? -1 : _player.Current, _theme);
                _glowLineStart = VnRender.LineStart(_player.Segments, _player.Current);
            }
            else if (_sink.Streaming)
                text = "<i>...</i>";
            else if (lines.Count == 0)
                text = "<i>" + HappiUiKit.Literal("Say hi to " + CompanionName() + ".") + "</i>";
            else
                text = string.Empty;

            if (_dialogue.text != text)
                _dialogue.text = text;
            if (_dialogueHit != null)
                _dialogueHit.gameObject.SetActive(!string.IsNullOrEmpty(text));
            _liveDirty = true;
        }

        void AppendFinishedReply(int serial)
        {
            _appendedSerial = serial;
        }

        void ClearFinishedReplies()
        {
            _appendedSerial = -1;
            _glowLineStart = 0;
        }

        void RefreshHistory()
        {
            var lines = _chat.TranscriptLines;
            var width = _historyContent.rect.width;
            if (width < 8f)
                return;

            // The live, still-revealing reply is drawn as the last entry, so it is not
            // also duplicated from the transcript list.
            _historyLines.Clear();
            var live = _dialogueHit != null && _dialogueHit.gameObject.activeSelf;
            var skipLast = live && lines.Count > 0 && lines[lines.Count - 1].Kind == ChatLineKind.Assistant;
            for (var i = 0; i < lines.Count; i++)
            {
                if (skipLast && i == lines.Count - 1)
                    continue;
                _historyLines.Add(lines[i]);
            }

            var widthChanged = Mathf.Abs(width - _historyWidth) > 0.5f;
            if (!_transcript.Update(_historyLines) && !widthChanged && !_liveDirty
                && _historyTexts.Count >= _transcript.Count)
                return;

            var atBottom = _historyScroll.verticalNormalizedPosition <= 0.01f;
            _historyWidth = width;
            _liveDirty = false;
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
                    // JUH-132: the transcript used the old History tab's small font after a
                    // reply finished, while the live reply was body size. Keep one size.
                    text.fontSize = _theme.FontBody * _layoutScale;
                    text.text = HistoryEntry(_transcript.Entry(i));
                    _historyHeights[i] = Mathf.Max(8f, text.GetPreferredValues(text.text, width - 4f, 0f).y);
                }

                var rect = text.rectTransform;
                rect.anchoredPosition = new Vector2(2f, -y);
                rect.sizeDelta = new Vector2(width - 4f, _historyHeights[i]);
                y += _historyHeights[i] + 6f;
            }

            if (_dialogueHit != null && _dialogueHit.gameObject.activeSelf)
            {
                var liveRect = _dialogueHit.rectTransform;
                liveRect.anchorMin = new Vector2(0f, 1f);
                liveRect.anchorMax = new Vector2(0f, 1f);
                liveRect.pivot = new Vector2(0f, 1f);
                var liveHeight = Mathf.Max(24f * _layoutScale,
                    _dialogue.GetPreferredValues(_dialogue.text, width - 16f, 0f).y + 12f * _layoutScale);
                liveRect.anchoredPosition = new Vector2(2f, -y);
                liveRect.sizeDelta = new Vector2(width - 4f, liveHeight);
                y += liveHeight + 6f;
            }

            _historyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(8f, y));
            if (atBottom || widthChanged)
                _historyScroll.verticalNormalizedPosition = 0f;
        }

        /// <summary>
        /// JUH-127: middle-mouse drag scrolls the one conversation window. The wheel still
        /// scrolls normally through the ScrollRect.
        /// </summary>
        void MiddleScroll()
        {
            if (_historyRoot == null || _historyContent == null || _historyScroll == null)
                return;

            var over = RectTransformUtility.RectangleContainsScreenPoint(_historyRoot, Input.mousePosition, null);
            if (Input.GetMouseButtonDown(2) && over)
            {
                _middleScrolling = true;
                _middleLastY = Input.mousePosition.y;
            }

            if (!_middleScrolling)
                return;
            if (!Input.GetMouseButton(2))
            {
                _middleScrolling = false;
                return;
            }

            var viewport = _historyScroll.viewport;
            var scrollable = _historyContent.rect.height - (viewport != null ? viewport.rect.height : 0f);
            if (scrollable > 1f)
            {
                var dy = Input.mousePosition.y - _middleLastY;
                _historyScroll.verticalNormalizedPosition = Mathf.Clamp01(
                    _historyScroll.verticalNormalizedPosition + dy / scrollable);
            }

            _middleLastY = Input.mousePosition.y;
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

        /// <summary>The Windows resize cursor while over an edge or corner, or while resizing.</summary>
        void ShowResizeCursor()
        {
            if (_resizing)
            {
                ResizeCursor.Show(_resizeEdges);
                return;
            }
            if (_portraitResizing)
            {
                ResizeCursor.Show(_portraitResizeEdges);
                return;
            }

            if (_dragging || _portraitDragging)
                return;
            for (var i = 0; i < _resizeHandles.Length; i++)
            {
                var handle = _resizeHandles[i];
                if (handle != null && handle.Hovered)
                {
                    ResizeCursor.Show(handle.Edges);
                    return;
                }
            }

            var portrait = ExpressionPortraitPanel.ShownRect;
            if (portrait.width > 0f)
            {
                var cursor = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                var edge = PortraitResize.EdgeAt(portrait, cursor, _layoutScale);
                if (edge != ResizeEdges.None)
                    ResizeCursor.Show(edge);
            }
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
