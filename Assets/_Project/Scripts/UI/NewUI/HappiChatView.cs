using System;
using System.Collections.Generic;
using System.Text;
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
    /// italics, speech voiced, the current line highlighted, and Auto or Step with Next and Skip.
    /// The conversation and the current line share one scrollable transcript (one text
    /// object per message, last 100 messages, 6000 characters each, as #64). Exists only while
    /// the new UI is on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HappiChatView : MonoBehaviour, IOverlayKeepAwake
    {
        RawImage _ribbonHeaderArt;
        RibbonClickAccent _ribbonSendAccent;
        RibbonGlyph _ribbonVoiceIcon;
        RibbonGlyph _ribbonPrivacyIcon;
        RectTransform _ribbonTitleMark;
        HappiButton _personaTitle;
        RibbonGlyph _personaArrow;
        RibbonPersonaMenu _personaMenu;
        RibbonPersonaSession _personaSession;
        RectTransform _ribbonHeaderRule;
        RectTransform _ribbonHeaderBacking;
        RectTransform _ribbonHeaderDivider;
        HappiButton _ribbonHide;
        RibbonGlyph _ribbonHideIcon;
        RectTransform _ribbonScrollThumb;
        HappiButton _ribbonLatest;
        readonly List<(Image Card, Image Accent, RibbonChatSpeaker Speaker)> _ribbonHistoryCards = new();
        RibbonChatSpeaker _ribbonLiveSpeaker;
        Texture2D _ribbonPortraitFallback;

        public const string ModePref = "happi.vn-mode";
        const float PlaneDistance = 0.48f;
        static readonly Color32 RibbonReplyCard = new Color32(41, 44, 53, 255);

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
        // JUH-177: the Private / Public switch (it took the place of "What do you see?", which
        // stays on the wheel: Screen > Look now).
        HappiButton _privacy;
        int _privacyShown = -1;
        HappiButton _close;
        GameObject _chatMenu;
        HappiButton _menuCopy;
        HappiButton _ribbonExtrasMenu;
        bool _ribbonExtras;
        bool _layoutPlayback;
        bool _layoutMic;
        bool _layoutNote;
        HappiButton _menuSettings;
        HappiButton _menuClose;
        HappiButton _menuProgram;
        bool _menuOpen;
        HappiButton _mode;
        HappiButton _next;
        HappiButton _skip;

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

        // JUH-154: her picture left of her name, reloaded only when she or her picture changes.
        Image _avatarFrame;
        RawImage _avatar;
        bool _avatarShown;
        string _avatarPersona;
        int _avatarPersonaRevision = -1;
        int _avatarPictureRevision = -1;
        // JUH-166: her picture at full size while the mouse rests on the small one.
        PictureHoverPreview _picturePreview;
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
        static readonly float[] ControlWeights = { 0.9f, 1f, 0.8f, 1.9f };

        // Spoken-text glow (JUH-108 follow-up).
        readonly SpokenGlowTracker _glow = new SpokenGlowTracker();
        ISpeechProgress _progress;
        int _glowLine = -1;
        int _glowSerial = -1;
        int _glowLineStart;
        bool _glowDirty;

        public VnPlayer Player => _player;
        internal bool PersonaMenuOpen => _personaMenu != null && _personaMenu.IsOpen;

        /// <summary>The chat box centre in Unity screen pixels.</summary>
        public Vector2 BoxCenter => _boxCenter;

        public bool KeepFrameRateActive
        {
            get
            {
                if (!_attached)
                    return false;
                return (_player != null && _player.Active)
                    || (_personaMenu != null && _personaMenu.IsOpen)
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
            _theme = RibbonMenu.Enabled ? new HappiTheme
            {
                ChatBoxWidth = 440f,
                PanelBackground = RibbonMenu.Ink,
                PanelInset = new Color32(28, 31, 38, 255),
                Border = new Color32(114, 238, 207, 255),
                HighlightBackground = new Color32(38, 230, 255, 38)
            } : HappiTheme.Current;
            _voice = new VnVoice(RealVoice, SpeechEnabled);
            _cues = new VnCueSink(PlayCue);
            _player = new VnPlayer(_voice, _cues, VnTiming.From(_theme));
            _player.Mode = PlayerPrefs.GetInt(ModePref, 0) == 1 ? VnMode.Step : VnMode.Auto;
            _transcript.SetFormatter(FormatHistoryText);
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
            _personaMenu?.Close();
            _personaMenu = null;
            _personaSession = null;
            if (_canvasObject != null)
                Destroy(_canvasObject);
            _canvasObject = null;
            _historyTexts.Clear();
            _historyHeights.Clear();
            _ribbonHistoryCards.Clear();
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

            SyncPersonaDraft();
            var settings = _settings != null ? _settings.CurrentSettings : null;
            _player.VoiceNarration = settings != null && settings.VoiceNarration;
            _player.Tick(Time.unscaledDeltaTime);

            UpdateSideRoom();
            var visible = _chat.ChatVisible;
            if (_canvasObject.activeSelf != visible)
                _canvasObject.SetActive(visible);
            if (!visible)
            {
                _personaMenu?.Close();
                _wasVisible = false;
                _dragging = false;
                _resizing = false;
                _resizePending = ResizeEdges.None;
                _middleScrolling = false;
                ClearPortraitHooks();
                if (_picturePreview != null)
                    _picturePreview.Hide();
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
            UpdatePersonaMenu();
            if (_menuOpen && Input.GetMouseButtonDown(0)
                && !Contains(_chatMenu != null ? (RectTransform)_chatMenu.transform : null)
                && !Contains(_close != null ? _close.Rect : null))
                CloseChatMenu();
            if (!_wasVisible)
            {
                _wasVisible = true;
                FocusInput();
            }

            HandleKeys();
            RefreshControls();
            if (_picturePreview != null)
            {
                var overPicture = _avatarShown && !_dragging && !_resizing && _avatarFrame != null && Contains(_avatarFrame.rectTransform);
                _picturePreview.Tick(overPicture, _avatar != null ? _avatar.texture : null, _avatarFrame != null ? _avatarFrame.rectTransform : null, Time.unscaledTime);
            }
            RefreshDialogue();
            ApplyGlow(settings);
            RefreshHistory();
            MiddleScroll();
            RefreshRibbonScrollControls();
            ShowResizeCursor();
            SyncFocus();
        }

        bool Contains(RectTransform rect)
        {
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, _canvas != null ? _canvas.worldCamera : null);
        }

        void HandleKeys()
        {
            if (_personaMenu != null && Application.isFocused && Input.GetKeyDown(KeyCode.P)
                && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            {
                TogglePersonaMenu();
                return;
            }
            if (_personaMenu != null && _personaMenu.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && _personaMenu.Close())
                    _personaTitle.Button.Select();
                return;
            }
            if (_menuOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseChatMenu();
                return;
            }

            var typing = (_input != null && _input.isFocused) || (_keyInput != null && _keyInput.isFocused);
            var control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!typing && control && Input.GetKeyDown(KeyCode.C) && Contains(_box))
                CopyTranscript();
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
            if (!classic && !wizardOpen && RibbonMenu.Enabled)
                side = Mathf.Max(side, RibbonMenu.PreferredSideRoom(scale));
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
            if (RibbonMenu.Enabled)
            {
                RibbonShape.Apply(boxImage, RibbonShape.Kind.ChatFrame);
                RibbonChatFrameEdge.Add(_box);
            }
            else
            {
                var outline = boxImage.gameObject.AddComponent<Outline>();
                outline.effectColor = HappiTheme.WithAlpha(_theme.Border, 0.9f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            if (RibbonMenu.Enabled)
            {
                var header = HappiUiKit.Panel(_box, "Ribbon header backing", new Color32(25, 30, 39, 255), false);
                RibbonShape.Apply(header, false);
                _ribbonHeaderBacking = header.rectTransform;
            }

            // Header bar: drag the box by it. Built first so the header buttons stay on top of it.
            var handle = HappiUiKit.Rect(_box, "DragHandle");
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0f, 0f, 0f, 0f);
            handleImage.raycastTarget = true;
            _dragHandle = handle.gameObject.AddComponent<HappiDragHandle>();
            _dragHandle.Rect = handle;
            _dragHandle.Pressed = () => _dragPending = true;

            _avatar = PersonaPictureCache.Frame(_box, "Avatar", _theme, out _avatarFrame);
            _avatarFrame.gameObject.SetActive(false);
            _picturePreview = new PictureHoverPreview((RectTransform)_canvasObject.transform, _theme);
            if (RibbonMenu.Enabled)
            {
                _personaTitle = HappiUiKit.Button(_box, "Choose persona", "Leia", _theme.FontTitle, TogglePersonaMenu);
                StyleRibbonControl(_personaTitle, angled: true);
                var titleColors = _personaTitle.Button.colors;
                titleColors.normalColor = new Color32(14, 17, 23, 255);
                titleColors.selectedColor = new Color32(34, 72, 83, 255);
                _personaTitle.Button.colors = titleColors;
                _name = _personaTitle.Label;
                _name.fontStyle |= FontStyles.Italic;
                _name.alignment = TextAlignmentOptions.MidlineLeft;
                _personaArrow = RibbonGlyph.Add(_personaTitle.Rect, "Opens upward", RibbonGlyph.Mark.ChevronUp, _theme.Text);
                _personaArrow.Label = _name;
                _ribbonTitleMark = HappiUiKit.Panel(_personaTitle.Rect, "Ribbon title stripe", new Color32(255, 206, 40, 255), false).rectTransform;
                RibbonShape.Apply(_ribbonTitleMark.GetComponent<Image>(), true);
                _ribbonHeaderRule = HappiUiKit.Panel(_box, "Ribbon header rule", new Color32(100, 118, 128, 130), false).rectTransform;
                _ribbonHeaderDivider = HappiUiKit.Panel(_box, "Ribbon header divider", new Color32(152, 165, 181, 150), false).rectTransform;
                var painting = Resources.Load<Texture2D>("RibbonArt/Talk");
                if (painting != null)
                {
                    _ribbonHeaderArt = HappiUiKit.Rect(_box, "Ribbon header art").gameObject.AddComponent<RawImage>();
                    _ribbonHeaderArt.texture = painting;
                    _ribbonHeaderArt.uvRect = new Rect(0.49f, 0.32f, 0.22f, 0.49f);
                    _ribbonHeaderArt.raycastTarget = false;
                    RibbonShape.Apply(_ribbonHeaderArt, RibbonShape.Kind.HeaderArt);
                }
            }
            else
            {
                _name = HappiUiKit.Text(_box, "Name", "Leia", _theme.FontTitle, _theme.Text, TextAlignmentOptions.MidlineLeft, true);
                _name.textWrappingMode = TextWrappingModes.NoWrap;
            }
            _badge = HappiUiKit.Text(_box, "Badge", string.Empty, _theme.FontSmall, _theme.BadgeCloud, TextAlignmentOptions.MidlineLeft, false);
            _mute = HappiUiKit.Button(_box, "Mute", ChatPresenter.MuteOffLabel, _theme.FontSmall, () => _chat.Presenter.ToggleMute());
            _privacy = HappiUiKit.Button(_box, "Privacy", "Public", _theme.FontSmall, OnPrivacy);
            _close = HappiUiKit.Button(_box, "Close", "⚙", _theme.FontBody, ToggleChatMenu);
            if (RibbonMenu.Enabled)
            {
                StyleRibbonControl(_mute);
                StyleRibbonControl(_privacy, clipped: false);
                var privacyBorder = _privacy.Image.gameObject.AddComponent<Outline>();
                privacyBorder.effectColor = new Color32(150, 163, 178, 180);
                privacyBorder.effectDistance = new Vector2(0.75f, -0.75f);
                StyleRibbonControl(_close);
                var gear = RibbonGlyph.Add(_close.Rect, "Chat options", RibbonGlyph.Mark.Settings, _theme.Text);
                gear.Label = _close.Label;
                HappiUiKit.Stretch(gear.rectTransform, 6f, 6f, 6f, 6f);
                _close.Label.gameObject.SetActive(false);
                _ribbonHide = HappiUiKit.Button(_box, "Hide chat", string.Empty, _theme.FontSmall, CloseChatFromMenu);
                StyleRibbonControl(_ribbonHide);
                _ribbonHideIcon = RibbonGlyph.Add(_ribbonHide.Rect, "Hide chat icon", RibbonGlyph.Mark.Minimize, _theme.Text);
                _ribbonHideIcon.Label = _ribbonHide.Label;
                _ribbonVoiceIcon = RibbonGlyph.Add(_mute.Rect, "Voice", RibbonGlyph.Mark.Voice, _theme.Text);
                _ribbonVoiceIcon.Label = _mute.Label;
                _mute.Label.gameObject.SetActive(false);
                _ribbonPrivacyIcon = RibbonGlyph.Add(_privacy.Rect, "Privacy state", RibbonGlyph.Mark.OpenLock, _theme.Text);
                _ribbonPrivacyIcon.Label = _privacy.Label;
            }

            var gearFont = RibbonMenu.Enabled ? null : HappiFonts.For("Segoe UI Symbol");
            if (gearFont != null)
            {
                _close.Label.font = gearFont;
                _close.Label.fontStyle = FontStyles.Normal;
            }
            else
            {
                _close.Label.text = "...";
            }

            var menuPanel = HappiUiKit.Panel(_box, "ChatMenu", _theme.PanelBackground, true);
            _chatMenu = menuPanel.gameObject;
            _menuCopy = HappiUiKit.Button(_chatMenu.transform, "MenuCopy", "Copy chat", _theme.FontSmall, CopyTranscriptFromMenu);
            _menuSettings = HappiUiKit.Button(_chatMenu.transform, "MenuSettings", "Settings", _theme.FontSmall, OpenSettingsFromMenu);
            _menuClose = HappiUiKit.Button(_chatMenu.transform, "MenuClose", "Close chat", _theme.FontSmall, CloseChatFromMenu);
            _menuProgram = HappiUiKit.Button(_chatMenu.transform, "MenuProgram", "Close program", _theme.FontSmall, CloseProgramFromMenu);
            if (RibbonMenu.Enabled)
            {
                RibbonShape.Apply(menuPanel, false);
                var border = menuPanel.gameObject.AddComponent<Outline>();
                border.effectColor = new Color32(114, 238, 207, 155);
                border.effectDistance = new Vector2(1f, -1f);
                StyleRibbonControl(_menuSettings, angled: true);
                StyleRibbonControl(_menuClose, angled: true);
                StyleRibbonControl(_menuProgram, angled: true);
                StyleRibbonControl(_menuCopy, angled: true);
                _ribbonExtrasMenu = HappiUiKit.Button(_chatMenu.transform, "Playback & mic",
                    "Show playback & mic", _theme.FontSmall, ToggleRibbonExtras);
                StyleRibbonControl(_ribbonExtrasMenu, angled: true);
            }
            _chatMenu.SetActive(false);

            var historyImage = HappiUiKit.Panel(_box, "HistoryArea", _theme.PanelInset, true);
            if (RibbonMenu.Enabled)
                historyImage.color = Color.clear;
            _historyRoot = historyImage.rectTransform;
            _historyScroll = historyImage.gameObject.AddComponent<ScrollRect>();
            _historyScroll.horizontal = false;
            _historyScroll.vertical = true;
            _historyScroll.movementType = ScrollRect.MovementType.Clamped;
            _historyScroll.scrollSensitivity = 24f;
            var viewport = HappiUiKit.Rect(_historyRoot, "Viewport");
            HappiUiKit.Stretch(viewport, 6f, 6f, RibbonMenu.Enabled ? 11f : 6f, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _historyScroll.viewport = viewport;
            _historyContent = HappiUiKit.Rect(viewport, "Content");
            _historyContent.anchorMin = new Vector2(0f, 1f);
            _historyContent.anchorMax = new Vector2(1f, 1f);
            _historyContent.pivot = new Vector2(0.5f, 1f);
            _historyContent.sizeDelta = new Vector2(0f, 8f);
            _historyScroll.content = _historyContent;
            if (RibbonMenu.Enabled)
            {
                _ribbonScrollThumb = HappiUiKit.Panel(_historyRoot, "History scroll position",
                    new Color32(38, 230, 255, 180), false).rectTransform;
                _ribbonScrollThumb.gameObject.SetActive(false);
                _ribbonLatest = HappiUiKit.Button(_historyRoot, "Jump to latest", "Latest ↓", _theme.FontSmall, JumpToLatest);
                StyleRibbonControl(_ribbonLatest, angled: true);
                _ribbonLatest.SetVisible(false);
            }

            _dialogueHit = HappiUiKit.Panel(_historyContent, "Dialogue", _theme.PanelInset, true);
            _dialogueHit.gameObject.AddComponent<RectMask2D>();
            if (RibbonMenu.Enabled)
            {
                _dialogueHit.color = RibbonReplyCard;
                AddReplyAccent(_dialogueHit.transform);
                _ribbonPortraitFallback = Resources.Load<Texture2D>("RibbonArt/ChatPortrait");
                _ribbonLiveSpeaker = new RibbonChatSpeaker(_historyContent);
            }
            var dialogueButton = _dialogueHit.gameObject.AddComponent<Button>();
            dialogueButton.transition = Selectable.Transition.None;
            var nav = dialogueButton.navigation;
            nav.mode = Navigation.Mode.None;
            dialogueButton.navigation = nav;
            dialogueButton.onClick.AddListener(OnDialogueClick);
            _dialogue = HappiUiKit.Text(_dialogueHit.transform, "Text", string.Empty, _theme.FontBody, _theme.Text, TextAlignmentOptions.BottomLeft, false);
            if (RibbonMenu.Enabled)
                _dialogue.alignment = TextAlignmentOptions.MidlineLeft;
            _dialogue.richText = true;
            _dialogue.overflowMode = TextOverflowModes.Overflow;
            HappiUiKit.Stretch(_dialogue.rectTransform, 8f, 6f, 8f, 6f);
            _note = HappiUiKit.Text(_box, "Note", string.Empty, _theme.FontSmall, _theme.TextMuted, TextAlignmentOptions.TopLeft, false);
            _note.fontStyle = FontStyles.Italic;

            _mode = HappiUiKit.Button(_box, "Mode", "Auto", _theme.FontSmall, ToggleMode);
            _next = HappiUiKit.Button(_box, "Next", "Next ›", _theme.FontSmall, () => _player.Next());
            _skip = HappiUiKit.Button(_box, "Skip", "Skip", _theme.FontSmall, () => _player.Skip());
            _narration = HappiUiKit.Button(_box, "Narration", "Narration voice: off", _theme.FontSmall, ToggleNarration);

            _mic = HappiUiKit.Button(_box, "Mic", ChatPresenter.MicIdleLabel, _theme.FontSmall, null);
            var trigger = _mic.Image.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            AddHold(trigger, UnityEngine.EventSystems.EventTriggerType.PointerDown, true);
            AddHold(trigger, UnityEngine.EventSystems.EventTriggerType.PointerUp, false);
            AddHold(trigger, UnityEngine.EventSystems.EventTriggerType.PointerExit, false);
            _input = HappiUiKit.Input(_box, "Message", RibbonMenu.Enabled ? "Write a message…" : "Message", _theme.FontBody, false);
            if (RibbonMenu.Enabled)
            {
                var composer = _input.GetComponent<Image>();
                composer.color = new Color32(35, 41, 53, 255);
                RibbonShape.Apply(composer, RibbonShape.Kind.Composer);
                RibbonChatFrameEdge.Add(composer.transform, RibbonShape.Kind.Composer);
                _input.selectionColor = new Color32(38, 230, 255, 100);
            }
            _input.onSubmit.AddListener(OnSubmit);
            _send = HappiUiKit.Button(_box, "Send", ChatPresenter.SendLabel, _theme.FontSmall, OnSend);
            if (RibbonMenu.Enabled)
            {
                RibbonShape.Apply(_send.Image, true);
                _ribbonSendAccent = RibbonClickAccent.Create(_box);
                var colors = _send.Button.colors;
                colors.normalColor = RibbonMenu.Cyan;
                colors.selectedColor = RibbonMenu.Cyan;
                _send.Button.colors = colors;
                var hover = _send.Button.GetComponent<HappiHoverText>();
                hover.Normal = RibbonMenu.Ink;
                _send.Label.color = RibbonMenu.Ink;
                _send.Label.fontStyle |= FontStyles.Italic;
            }

            _keyInput = HappiUiKit.Input(_box, "ApiKey", "API key", _theme.FontSmall, true);
            _keyInput.onSubmit.AddListener(_ => SaveKey());
            _saveKey = HappiUiKit.Button(_box, "SaveKey", "Save", _theme.FontSmall, SaveKey);

            _controlRow = new[] { _mode, _next, _skip, _narration };
            if (RibbonMenu.Enabled)
            {
                foreach (var control in _controlRow)
                    StyleRibbonControl(control);
                StyleRibbonControl(_saveKey);
            }
            _fontButtons = new[] { _mute, _privacy, _mode, _next, _skip, _narration, _mic, _send, _saveKey };
            if (RibbonMenu.Enabled && _settings != null)
            {
                _personaSession = new RibbonPersonaSession(_settings.CurrentSettings?.PersonaId);
                _personaMenu = new RibbonPersonaMenu(_canvasObject.transform, ChoosePersona, OpenPersonaStudio, OpenAllPersonas);
            }

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
            var ribbon = _ribbonVoiceIcon != null;
            var showPlayback = !ribbon || _ribbonExtras || _player.Mode == VnMode.Step;
            var showMic = !ribbon || _ribbonExtras || _micHeld || _chat.MicRecording || _chat.MicTranscribing;
            var showNote = !ribbon || !string.IsNullOrEmpty(_note.text);
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
                && _layoutKeyRow == keyRow && _layoutPlayback == showPlayback && _layoutNote == showNote && _layoutMic == showMic
                && Mathf.Approximately(_layoutBoxWidth, targetWidth) && Mathf.Approximately(_layoutBoxHeight, targetHeight))
                return;

            _layoutWidth = Screen.width;
            _layoutHeight = Screen.height;
            _layoutScale = scale;
            _layoutChatHeight = chatHeight;
            _layoutKeyRow = keyRow;
            _layoutPlayback = showPlayback;
            _layoutMic = showMic;
            _layoutNote = showNote;
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

            var minExpandedHeaderHeight = (keyRow ? 245f : 213f) * s
                - (showPlayback ? 0f : row + gap) - (showNote ? 0f : 16f * s + gap * 0.5f);
            var wideRibbon = ribbon && inner >= 380f * s && height >= minExpandedHeaderHeight;
            SetFonts(s, wideRibbon);

            // The wide chat gives the title, portrait and controls a taller shared header.
            var y = pad;
            var headerRow = wideRibbon ? 36f * s : row;
            var closeW = row;
            var lookW = (ribbon ? 64f : 112f) * s;
            var headerGap = ribbon ? 4f * s : gap;
            var muteW = ribbon ? row : 58f * s;
            var right = pad + inner;
            // Compact chat gives the title and portrait their own line so controls do not squeeze them out.
            var stackedHeader = ribbon && inner < 286f * s && height >= minExpandedHeaderHeight;
            var controlY = stackedHeader ? y + row + headerGap : y;
            var headerBottom = controlY + headerRow;
            var buttonY = controlY + (headerRow - row) * 0.5f;
            var artWidth = _ribbonHeaderArt != null && (stackedHeader || inner >= (_avatarShown ? 335f : 284f) * s)
                ? Mathf.Clamp(inner * 0.22f, 46f * s, (wideRibbon ? 100f : 82f) * s) : 0f;
            PlaceRibbonHeaderArt(right, y, artWidth, headerRow);
            if (artWidth > 0f && !stackedHeader)
                right -= artWidth + headerGap;
            if (ribbon)
            {
                HappiUiKit.Place(_ribbonHeaderBacking, pad * 0.5f, y - 4f * s, width - pad, headerBottom - y + 8f * s);
                HappiUiKit.Place(_ribbonHide.Rect, right - row, buttonY, row, row);
                HappiUiKit.Stretch(_ribbonHideIcon.rectTransform, 6f * s, 6f * s, 6f * s, 6f * s);
                right -= row + headerGap;
            }
            HappiUiKit.Place(_close.Rect, right - closeW, buttonY, closeW, row);
            PlaceChatMenu(s, pad, row, gap, headerBottom);
            right -= closeW + headerGap;
            if (ribbon)
            {
                _ribbonHeaderDivider.gameObject.SetActive(wideRibbon);
                if (wideRibbon)
                {
                    HappiUiKit.Place(_ribbonHeaderDivider, right - 2f * s, buttonY + 4f * s, s, row - 8f * s);
                    right -= 6f * s;
                }
            }
            var statusWidth = lookW + muteW + headerGap;
            var privacyX = right - (ribbon ? statusWidth : lookW);
            var muteX = right - (ribbon ? muteW : statusWidth);
            HappiUiKit.Place(_privacy.Rect, privacyX, buttonY, lookW, row);
            HappiUiKit.Place(_mute.Rect, muteX, buttonY, muteW, row);
            right -= statusWidth + headerGap;
            if (stackedHeader)
                right = pad + inner - (artWidth > 0f ? artWidth + headerGap : 0f);
            var nameX = pad;
            if (_avatarShown)
            {
                var portraitSize = ribbon && inner < 240f * s ? 19f * s : row;
                HappiUiKit.Place(_avatarFrame.rectTransform, pad, y + (headerRow - portraitSize) * 0.5f, portraitSize, portraitSize);
                nameX += portraitSize + headerGap;
            }

            if (ribbon)
            {
                var titleWidth = Mathf.Clamp(right - nameX, 1f, 164f * s);
                var titleHeight = headerRow - 4f * s;
                HappiUiKit.Place(_personaTitle.Rect, nameX, y + 2f * s, titleWidth, titleHeight);
                HappiUiKit.Place(_personaArrow.rectTransform, Mathf.Max(0f, titleWidth - 22f * s),
                    (titleHeight - 12f * s) * 0.5f, 12f * s, 12f * s);
                _personaArrow.enabled = titleWidth >= 24f * s;
                var icon = 15f * s;
                HappiUiKit.Place(_ribbonVoiceIcon.rectTransform, (muteW - icon) * 0.5f, (row - icon) * 0.5f, icon, icon);
                HappiUiKit.Place(_ribbonPrivacyIcon.rectTransform, 7f * s, (row - icon) * 0.5f, icon, icon);
                HappiUiKit.Stretch(_privacy.Label.rectTransform, 21f * s, 0f, 4f * s, 0f);
                var showMark = titleWidth >= 52f * s;
                _ribbonTitleMark.gameObject.SetActive(showMark);
                if (showMark)
                    HappiUiKit.Place(_ribbonTitleMark, 5f * s, 2f * s, 7f * s, titleHeight - 4f * s);
                HappiUiKit.Stretch(_name.rectTransform, (showMark ? 16f : 7f) * s, 0f, (_personaArrow.enabled ? 25f : 5f) * s, 0f);
                HappiUiKit.Place(_ribbonHeaderRule, pad, headerBottom + gap * 0.5f, inner, Mathf.Max(1f, s));
            }
            else
                HappiUiKit.Place(_name.rectTransform, nameX, y, Mathf.Max(40f, right - nameX), headerRow);
            HappiUiKit.Place(_dragHandle.Rect, 0f, 0f, width, headerBottom + gap * 0.5f);
            _badge.gameObject.SetActive(false);
            y = headerBottom + gap;

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

            var inputRow = row + (ribbon ? 9f : 4f) * s;
            var micW = 44f * s;
            var micSpace = showMic ? micW + gap : 0f;
            var sendW = (wideRibbon ? 84f : ribbon ? 76f : 64f) * s;
            _mic.SetVisible(showMic);
            if (showMic)
                HappiUiKit.Place(_mic.Rect, pad, bottom - inputRow, micW, inputRow);
            HappiUiKit.Place((RectTransform)_input.transform, pad + micSpace, bottom - inputRow,
                Mathf.Max(1f, inner - micSpace - sendW - gap), inputRow);
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
                controls[i].SetVisible(showPlayback);
                if (showPlayback)
                    HappiUiKit.Place(controls[i].Rect, x, bottom - row, w, row);
                x += w + gap;
            }

            if (showPlayback)
                bottom -= row + gap;

            _note.gameObject.SetActive(showNote);
            var noteH = showNote ? 16f * s : 0f;
            HappiUiKit.Place(_note.rectTransform, pad, bottom - noteH, inner, noteH);
            bottom -= noteH + (showNote ? gap * 0.5f : 0f);

            // JUH-127: one scrollable conversation window. There is no separate History area
            // or History button any more.
            var historyH = Mathf.Max(40f * s, bottom - y);
            HappiUiKit.Place(_historyRoot, pad, y, inner, historyH);
            _historyRoot.gameObject.SetActive(true);
            _historyWidth = -1f;
            PlaceResizeHandles(width, height, s);
        }

        void PlaceRibbonHeaderArt(float right, float y, float width, float height)
        {
            if (_ribbonHeaderArt == null)
                return;
            _ribbonHeaderArt.gameObject.SetActive(width > 0f);
            if (width <= 0f)
                return;

            const float cropHeight = 0.58f;
            var texture = _ribbonHeaderArt.texture;
            var aspect = (float)texture.width / texture.height;
            var cropWidth = Mathf.Min(1f, width / height * cropHeight / aspect);
            var cropX = Mathf.Clamp(0.62f - cropWidth * 0.5f, 0f, 1f - cropWidth);
            _ribbonHeaderArt.uvRect = new Rect(cropX, 0.28f, cropWidth, cropHeight);
            HappiUiKit.Place(_ribbonHeaderArt.rectTransform, right - width, y, width, height);
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

        void SetFonts(float s, bool wideRibbon)
        {
            _name.fontSize = (wideRibbon ? 25f : RibbonMenu.Enabled ? 21f : _theme.FontTitle) * s;
            _badge.fontSize = _theme.FontSmall * s * 0.9f;
            _dialogue.fontSize = ChatFontSize() * s;
            _note.fontSize = _theme.FontSmall * s;
            var buttons = _fontButtons;
            for (var i = 0; i < buttons.Length; i++)
                buttons[i].Label.fontSize = _theme.FontSmall * s;
            if (_menuCopy != null)
                _menuCopy.Label.fontSize = _theme.FontSmall * s;
            if (_menuSettings != null)
                _menuSettings.Label.fontSize = _theme.FontSmall * s;
            if (_menuClose != null)
                _menuClose.Label.fontSize = _theme.FontSmall * s;
            if (_menuProgram != null)
                _menuProgram.Label.fontSize = _theme.FontSmall * s;
            _close.Label.fontSize = _theme.FontBody * s;
            if (_ribbonExtrasMenu != null)
            {
                _ribbonExtrasMenu.Label.fontSize = _theme.FontSmall * s;
                _send.Label.fontSize = (wideRibbon ? 18f : 16f) * s;
            }
            SetInputFont(_input, ChatFontSize() * s);
            SetInputFont(_keyInput, _theme.FontSmall * s);
            ApplyChatFont();
        }

        /// <summary>Base font size for the chat, from the user's Chat settings.</summary>
        float ChatFontSize()
        {
            var settings = _settings != null ? _settings.CurrentSettings : null;
            return settings != null && settings.ChatFontSize >= 10f ? settings.ChatFontSize : _theme.FontBody;
        }

        /// <summary>
        /// History is the same paragraph, so it must use the live line's resolved size rather
        /// than applying its own scale or a smaller transcript multiplier.
        /// </summary>
        float HistoryFontSize()
        {
            if (_dialogue != null && _dialogue.fontSize > 0.1f)
                return _dialogue.fontSize;
            return Mathf.Max(9f, ChatFontSize()) * (_layoutScale > 0f ? _layoutScale : 1f);
        }

        /// <summary>Applies the chosen font to the dialogue, transcript and input.</summary>
        public void ApplyChatFont()
        {
            _historyWidth = -1f;
            _liveDirty = true;
            var settings = _settings != null ? _settings.CurrentSettings : null;
            var family = settings != null && !string.IsNullOrWhiteSpace(settings.ChatFontFamily)
                ? settings.ChatFontFamily
                : _theme.FontFamily;
            var font = HappiFonts.For(family);
            var scale = _layoutScale > 0f ? _layoutScale : 1f;
            var size = ChatFontSize() * scale;
            if (_dialogue != null)
            {
                if (font != null)
                    _dialogue.font = font;
                _dialogue.fontSize = size;
            }
            var historySize = HistoryFontSize();
            for (var i = 0; i < _historyTexts.Count; i++)
            {
                if (_historyTexts[i] == null)
                    continue;
                if (font != null)
                    _historyTexts[i].font = font;
                _historyTexts[i].fontSize = historySize;
            }
            if (_input != null && _input.textComponent != null)
            {
                if (font != null)
                    _input.textComponent.font = font;
                _input.textComponent.fontSize = size;
            }
            if (_input != null && _input.placeholder is TMP_Text hint)
            {
                if (font != null)
                    hint.font = font;
                hint.fontSize = size;
            }
            if (_keyInput != null && _keyInput.textComponent != null)
            {
                if (font != null)
                    _keyInput.textComponent.font = font;
                _keyInput.textComponent.fontSize = Mathf.Max(9f, size * 0.86f);
            }
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

        /// <summary>
        /// Her picture in the header. Compares a reference and two ints each frame (no allocation);
        /// loads only after a persona switch, an edit of her, or a picture save.
        /// </summary>
        void RefreshAvatar()
        {
            if (_avatar == null || _settings == null)
                return;
            var current = _settings.CurrentSettings;
            var id = current != null ? current.PersonaId ?? string.Empty : string.Empty;
            if (string.Equals(id, _avatarPersona, StringComparison.Ordinal) && _settings.PersonaRevision == _avatarPersonaRevision
                && PersonaPictureStore.Revision == _avatarPictureRevision)
                return;
            _avatarPersona = id;
            _avatarPersonaRevision = _settings.PersonaRevision;
            _avatarPictureRevision = PersonaPictureStore.Revision;

            var persona = id.Length == 0 ? null : _settings.PersonaCatalogNow().Find(id);
            var texture = PersonaPictureCache.Get(_settings.PersonasFolderPath, persona);
            var round = persona == null || PersonaPictureShapes.Normalize(persona.PictureShape) == PersonaPictureShapes.Round;
            if (_ribbonLiveSpeaker != null && _avatar.texture != texture)
                _liveDirty = true;
            PersonaPictureCache.Show(_avatarFrame, _avatar, texture, round);
            var shown = texture != null;
            if (shown != _avatarShown)
            {
                _avatarShown = shown;
                _layoutWidth = -1f;
            }
        }

        void RefreshControls()
        {
            RefreshAvatar();
            var presenter = _chat.Presenter;
            _send.Text = presenter.ActionLabel;
            _mute.Text = presenter.MuteLabel;
            if (_ribbonVoiceIcon != null)
                _ribbonVoiceIcon.Shape = presenter.Muted ? RibbonGlyph.Mark.Muted : RibbonGlyph.Mark.Voice;
            RefreshPrivacy(presenter.ReplyBusy);
            _input.interactable = !presenter.ReplyBusy;

            var recording = _chat.MicRecording;
            _mic.Text = ChatPresenter.MicLabelFor(recording, _chat.MicTranscribing);
            _mic.Image.color = recording ? _theme.Recording : Color.white;

            _mode.Text = _player.Mode == VnMode.Step ? "Step" : "Auto";
            _next.Interactable = _player.Active && (_player.HasNext || !_player.Finished);
            _skip.Interactable = _player.Active;
            _narration.Text = _player.VoiceNarration ? "Narration voice: on" : "Narration voice: off";

            // Her name, then the same model badge the old chat shows (provider, model, private), in its colour.
            var header = RibbonMenu.Enabled ? CompanionName() : CompanionName() + "|" + _chat.ModelBadgeText + "|" + HappiUiKit.ColorTag(_chat.ModelBadgeColor);
            if (header != _shownBadge)
            {
                _shownBadge = header;
                var badge = RibbonMenu.Enabled ? string.Empty : _chat.ModelBadgeText.Replace('\n', ' ').Trim();
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

        /// <summary>
        /// The live dialogue uses VnRender for italics and pink feedback. Finished replies
        /// must use the same renderer, otherwise the narration changes font as soon as it
        /// leaves the live box and enters the scrollback.
        /// </summary>
        string FormatHistoryText(ChatLine line)
        {
            if (line == null || string.IsNullOrEmpty(line.Text))
                return line != null ? line.Text : string.Empty;
            if (line.Kind != ChatLineKind.Assistant && line.Kind != ChatLineKind.User)
                return ChatLineColors.Format(line, HappiUiKit.Literal(line.Text));
            var segments = VnScriptParser.Parse(line.Text, true).Segments;
            return VnRender.Dialogue(segments, segments.Count, -1, _theme, false);
        }

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
                // JUH-174: change lines live in the scrollback; the note keeps real notes.
                if (line.Kind == ChatLineKind.System && note.Length == 0 && line.Tag != ChatLineTag.Change)
                    note = line.Text;
                if (line.Kind == ChatLineKind.User)
                    break;
            }

            _note.text = HappiUiKit.Literal(Shorten(note, 220));

            string text;
            if (_player.Segments.Count > 0 && _appendedSerial != serial)
            {
                text = VnRender.Dialogue(_player.Segments, _player.Revealed, _player.Finished ? -1 : _player.Current, _theme,
                    false);
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
                // JUH-174: an 18+ change line is not drawn while the gate is closed.
                if (!ChatConversation.Shows(lines[i]))
                    continue;
                _historyLines.Add(lines[i]);
            }

            var widthChanged = Mathf.Abs(width - _historyWidth) > 0.5f;
            if (!_transcript.Update(_historyLines) && !widthChanged && !_liveDirty
                && _historyTexts.Count >= _transcript.Count)
                return;

            var ribbon = _ribbonLatest != null;
            var oldOverflow = RibbonHistoryOverflow();
            var oldPosition = Mathf.Clamp01(_historyScroll.verticalNormalizedPosition);
            var atBottom = ribbon ? oldOverflow * oldPosition <= 8f * _layoutScale : oldPosition <= 0.01f;
            var scrollFromTop = oldOverflow * (1f - oldPosition);
            _historyWidth = width;
            _liveDirty = false;
            var count = _transcript.Count;
            var historySize = HistoryFontSize();
            var roomy = ribbon && width >= 360f * _layoutScale;
            var cardPadding = (roomy ? 13f : 8f) * _layoutScale;
            var messageInset = (roomy ? 9f : 6f) * _layoutScale;
            var messageGap = (roomy ? 14f : 8f) * _layoutScale;
            var showSpeakers = ribbon && width >= 158f * _layoutScale;
            var speakerSize = (roomy ? 38f : 31f) * _layoutScale;
            var gutter = showSpeakers ? speakerSize + (roomy ? 14f : 8f) * _layoutScale : 0f;
            var cardWidth = Mathf.Max(1f, width - 4f - gutter);
            var textWidth = Mathf.Max(1f, cardWidth - (ribbon ? 2f * cardPadding : 0f));
            var portrait = _avatarShown && _avatar != null ? _avatar.texture : _ribbonPortraitFallback;
            while (_historyTexts.Count < count)
            {
                Transform parent = _historyContent;
                if (ribbon)
                {
                    var card = HappiUiKit.Panel(parent, "Message card", _theme.PanelInset, false);
                    var accent = AddReplyAccent(card.transform);
                    var speaker = new RibbonChatSpeaker(_historyContent);
                    _ribbonHistoryCards.Add((card, accent, speaker));
                    parent = card.transform;
                }

                var text = HappiUiKit.Text(parent, "Message", string.Empty, historySize, _theme.Text, TextAlignmentOptions.TopLeft, false);
                text.font = _dialogue.font;
                text.richText = true;
                text.overflowMode = TextOverflowModes.Overflow;
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                _historyTexts.Add(text);
                _historyHeights.Add(-1f);
            }

            for (var i = 0; i < _historyTexts.Count; i++)
                if (_historyTexts[i] != null)
                {
                    _historyTexts[i].richText = true;
                    _historyTexts[i].fontSize = historySize;
                }

            var y = 0f;
            for (var i = 0; i < _historyTexts.Count; i++)
            {
                var text = _historyTexts[i];
                var rowObject = ribbon ? _ribbonHistoryCards[i].Card.gameObject : text.gameObject;
                if (i >= count)
                {
                    if (rowObject.activeSelf)
                    {
                        text.text = string.Empty;
                        rowObject.SetActive(false);
                        if (ribbon)
                            _ribbonHistoryCards[i].Speaker.Hide();
                    }

                    continue;
                }

                if (!rowObject.activeSelf)
                    rowObject.SetActive(true);
                if (_transcript.Changed(i) || widthChanged || _historyHeights[i] < 0f)
                {
                    // JUH-132: the transcript used the old History tab's small font after a
                    // reply finished, while the live reply was body size. Keep one size.
                    text.fontSize = HistoryFontSize();
                    var hasAuthor = showSpeakers && _transcript.KindAt(i) != ChatLineKind.System;
                    text.text = HistoryEntry(_transcript.Entry(i), hasAuthor);
                    _historyHeights[i] = Mathf.Max(8f, text.GetPreferredValues(text.text, textWidth, 0f).y);
                }

                var messageHeight = _historyHeights[i];
                if (ribbon)
                {
                    var (card, accent, speaker) = _ribbonHistoryCards[i];
                    var kind = _transcript.KindAt(i);
                    var isReply = kind == ChatLineKind.Assistant;
                    var showBadge = showSpeakers && kind != ChatLineKind.System;
                    var cardHeight = Mathf.Max(messageHeight + 2f * messageInset, showBadge ? speakerSize : 0f);
                    if (showBadge)
                        speaker.Show(isReply, portrait, 2f, y + (cardHeight - speakerSize) * 0.5f, speakerSize);
                    else
                        speaker.Hide();
                    card.enabled = kind != ChatLineKind.System;
                    card.color = isReply ? RibbonReplyCard : new Color32(49, 52, 63, 255);
                    accent.gameObject.SetActive(isReply);
                    HappiUiKit.Place(card.rectTransform, 2f + gutter, y, cardWidth, cardHeight);
                    HappiUiKit.Place(text.rectTransform, cardPadding, (cardHeight - messageHeight) * 0.5f, textWidth, messageHeight);
                    y += cardHeight + messageGap;
                }
                else
                {
                    var rect = text.rectTransform;
                    rect.anchoredPosition = new Vector2(2f, -y);
                    rect.sizeDelta = new Vector2(width - 4f, messageHeight);
                    y += messageHeight + 6f;
                }
            }

            _ribbonLiveSpeaker?.Hide();
            if (_dialogueHit != null && _dialogueHit.gameObject.activeSelf)
            {
                var liveRect = _dialogueHit.rectTransform;
                liveRect.anchorMin = new Vector2(0f, 1f);
                liveRect.anchorMax = new Vector2(0f, 1f);
                liveRect.pivot = new Vector2(0f, 1f);
                var liveHeight = ribbon
                    ? Mathf.Max(showSpeakers ? speakerSize : 24f * _layoutScale,
                        _dialogue.GetPreferredValues(_dialogue.text, textWidth, 0f).y + 2f * messageInset)
                    : Mathf.Max(24f * _layoutScale,
                        _dialogue.GetPreferredValues(_dialogue.text, Mathf.Max(1f, cardWidth - 16f), 0f).y + 12f * _layoutScale);
                liveRect.anchoredPosition = new Vector2(2f + gutter, -y);
                liveRect.sizeDelta = new Vector2(cardWidth, liveHeight);
                if (ribbon)
                    HappiUiKit.Stretch(_dialogue.rectTransform, cardPadding, messageInset, cardPadding, messageInset);
                if (showSpeakers)
                    _ribbonLiveSpeaker.Show(true, portrait, 2f, y + (liveHeight - speakerSize) * 0.5f, speakerSize);
                y += liveHeight + (ribbon ? messageGap : 6f);
            }

            _historyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(8f, y));
            if (atBottom || (!ribbon && widthChanged))
                _historyScroll.verticalNormalizedPosition = 0f;
            else if (ribbon)
            {
                var newOverflow = RibbonHistoryOverflow();
                if (newOverflow > 0f && !Mathf.Approximately(newOverflow, oldOverflow))
                    _historyScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(scrollFromTop / newOverflow);
            }
        }

        /// <summary>
        /// JUH-127: middle-mouse drag scrolls the one conversation window. The wheel still
        /// scrolls normally through the ScrollRect.
        /// </summary>
        float RibbonHistoryOverflow()
        {
            return Mathf.Max(0f, _historyContent.rect.height - _historyScroll.viewport.rect.height);
        }

        void RefreshRibbonScrollControls()
        {
            if (_ribbonLatest == null)
                return;

            var overflow = RibbonHistoryOverflow();
            var hasHistoryToScroll = overflow > 1f;
            _ribbonScrollThumb.gameObject.SetActive(hasHistoryToScroll);
            var position = Mathf.Clamp01(_historyScroll.verticalNormalizedPosition);
            _ribbonLatest.SetVisible(hasHistoryToScroll && overflow * position > 8f * _layoutScale);
            if (!hasHistoryToScroll)
                return;

            var viewport = _historyScroll.viewport.rect;
            var scale = _layoutScale;
            var thumbHeight = Mathf.Min(viewport.height, Mathf.Max(14f * scale,
                viewport.height * viewport.height / _historyContent.rect.height));
            var thumbY = 6f + (viewport.height - thumbHeight) * (1f - position);
            var thumbWidth = Mathf.Clamp(2f * scale, 2f, 4f);
            HappiUiKit.Place(_ribbonScrollThumb, _historyRoot.rect.width - 6f - thumbWidth,
                thumbY, thumbWidth, thumbHeight);
            if (_ribbonLatest.Button.gameObject.activeSelf)
            {
                var width = Mathf.Min(82f * scale, _historyRoot.rect.width - 22f);
                var height = Mathf.Min(22f * scale, viewport.height);
                _ribbonLatest.Label.fontSize = _theme.FontSmall * scale;
                HappiUiKit.Place(_ribbonLatest.Rect, _historyRoot.rect.width - width - 13f,
                    _historyRoot.rect.height - height - 5f, width, height);
            }
        }

        void JumpToLatest()
        {
            _historyScroll.StopMovement();
            _historyScroll.verticalNormalizedPosition = 0f;
            RefreshRibbonScrollControls();
        }

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

        string HistoryEntry(string entry, bool hideAuthor)
        {
            var split = entry.IndexOf('\n');
            if (split < 0)
                return HappiUiKit.Literal(entry);
            var label = entry.Substring(0, split);
            var body = entry.Substring(split + 1);
            // JUH-174: scene notes and change lines have no label, only their colour.
            if (hideAuthor || label.Length == 0)
                return body;
            if (label == "Companion")
                label = CompanionName();
            return "<b>" + HappiUiKit.Literal(label) + "</b>\n" + body;
        }

        static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text ?? string.Empty;
            return "…" + text.Substring(text.Length - max);
        }

        // ---- Actions ---------------------------------------------------------------------

        static Image AddReplyAccent(Transform parent)
        {
            var accent = HappiUiKit.Panel(parent, "Happi reply stripe", RibbonMenu.Cyan, false);
            var edge = accent.rectTransform;
            edge.anchorMin = Vector2.zero;
            edge.anchorMax = Vector2.up;
            edge.offsetMin = new Vector2(2f, 4f);
            edge.offsetMax = new Vector2(4f, -4f);
            return accent;
        }

        static void StyleRibbonControl(HappiButton button, bool angled = false, bool clipped = true)
        {
            if (clipped)
                RibbonShape.Apply(button.Image, angled);
            var colors = button.Button.colors;
            colors.normalColor = new Color32(34, 40, 52, 255);
            colors.selectedColor = colors.normalColor;
            colors.highlightedColor = RibbonMenu.Cyan;
            colors.pressedColor = RibbonMenu.Cyan;
            colors.disabledColor = new Color32(23, 29, 37, 235);
            button.Button.colors = colors;
            button.Button.GetComponent<HappiHoverText>().Hover = RibbonMenu.Ink;
        }

        void OnSend()
        {
            if (SyncPersonaDraft())
                return;
            var result = _chat.Presenter.PrimaryAction(_input != null ? _input.text : string.Empty);
            if (result == ChatActionResult.Sent && _ribbonSendAccent != null)
                _ribbonSendAccent.Play(_send);
            if (result == ChatActionResult.Sent && _input != null)
            {
                _input.text = string.Empty;
                _input.ActivateInputField();
            }
        }

        void OnSubmit(string text)
        {
            if (SyncPersonaDraft())
                return;
            if (_chat.Presenter.Send(text) != ChatActionResult.Sent)
                return;
            if (_ribbonSendAccent != null)
                _ribbonSendAccent.Play(_send);
            _input.text = string.Empty;
            _input.ActivateInputField();
        }

        /// <summary>
        /// JUH-177: switches private mode with the saved setting, so each side uses the model and
        /// voice set up for it. Not while she is still answering: that reply finishes first.
        /// </summary>
        void OnPrivacy()
        {
            if (_settings == null || _chat == null || _chat.Presenter.ReplyBusy)
                return;
            _settings.Presenter.SetPrivateMode(!_chat.PrivateNow);
            _privacyShown = -1;
        }

        void RefreshPrivacy(bool busy)
        {
            var on = _chat != null && _chat.PrivateNow;
            var shown = (on ? 1 : 0) + (busy ? 2 : 0);
            if (shown == _privacyShown)
                return;
            _privacyShown = shown;
            _privacy.Text = on ? "Private" : "Public";
            var statusColor = on ? _theme.Intimate : _theme.BadgeCloud;
            if (_ribbonPrivacyIcon != null)
            {
                _ribbonPrivacyIcon.Shape = on ? RibbonGlyph.Mark.Lock : RibbonGlyph.Mark.OpenLock;
                _privacy.Button.GetComponent<HappiHoverText>().Normal = statusColor;
            }
            _privacy.Label.color = statusColor;
            _privacy.Interactable = !busy;
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

        bool PersonaSwitchBusy() => _chat.TranscriptBusy || _player.Active || _sink.Streaming
            || _chat.MicRecording || _chat.MicTranscribing;

        bool SyncPersonaDraft()
        {
            if (_personaSession == null || _input == null || _settings.CurrentSettings == null
                || !_personaSession.TryChange(_settings.CurrentSettings.PersonaId, _input.text, out var draft))
                return false;

            _personaMenu?.Close();
            _input.SetTextWithoutNotify(draft);
            _input.caretPosition = draft.Length;
            _shownRevision = -1;
            return true;
        }

        void TogglePersonaMenu()
        {
            if (_personaMenu == null || GetComponent<HappiSettingsView>()?.IsOpen == true
                || GetComponent<PersonaStudioView>()?.IsOpen == true)
                return;
            if (_personaMenu.IsOpen)
            {
                _personaMenu.Close();
                return;
            }

            SyncPersonaDraft();
            CloseChatMenu();
            _input.DeactivateInputField();
            ReleaseFocus();
            GetComponent<ITypingFocus>()?.FocusForTyping();
            _personaMenu.Open(_personaSession.Choices(_settings.PersonaCards(), CompanionName()), PersonaSwitchBusy());
            PlacePersonaMenu();
        }

        void UpdatePersonaMenu()
        {
            if (_personaMenu == null || !_personaMenu.IsOpen)
                return;
            if (Input.GetMouseButtonDown(0) && !Contains(_personaMenu.Viewport) && !Contains(_personaTitle.Rect))
            {
                _personaMenu.Close();
                return;
            }
            _personaMenu.SetBusy(PersonaSwitchBusy());
            PlacePersonaMenu();
        }

        void PlacePersonaMenu()
        {
            var visible = _visibleArea.width > 0f ? _visibleArea : VisibleArea();
            var area = new Rect(visible.xMin, Screen.height - visible.yMax, visible.width, visible.height);
            var left = _boxCenter.x - _boxSize.x * 0.5f;
            var top = Screen.height - (_boxCenter.y + _boxSize.y * 0.5f);
            var title = _personaTitle.Rect.anchoredPosition;
            _personaMenu.Place(area, new Vector2(left + title.x, top - title.y), Mathf.Max(0.5f, _layoutScale));
        }

        void ChoosePersona(string id)
        {
            if (string.Equals(id, _settings.CurrentSettings?.PersonaId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                _personaMenu.Close();
                return;
            }
            if (PersonaSwitchBusy())
                return;

            _personaMenu.Close();
            _player.Begin();
            _settings.Personas.Choose(id);
            SyncPersonaDraft();
            FocusInput();
        }

        void OpenPersonaStudio()
        {
            _personaMenu.Close();
            var studio = GetComponent<PersonaStudioView>();
            if (studio != null)
                studio.OpenNew();
            else
                OpenAllPersonas();
        }

        void OpenAllPersonas()
        {
            _personaMenu.Close();
            var settingsView = GetComponent<HappiSettingsView>();
            if (settingsView == null || !settingsView.TryOpenPage(SettingsPage.HerPersona))
                _settings.Open();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                _personaMenu?.Close();
        }

        void ToggleChatMenu()
        {
            _personaMenu?.Close();
            _menuOpen = !_menuOpen;
            if (_chatMenu != null)
            {
                _chatMenu.SetActive(_menuOpen);
                if (_menuOpen)
                    _chatMenu.transform.SetAsLastSibling();
            }
            if (_menuOpen)
            {
                var scale = _layoutScale > 0f ? _layoutScale : 1f;
                PlaceChatMenu(scale, _theme.Padding * scale, 26f * scale, _theme.Spacing * scale, -_close.Rect.anchoredPosition.y + _close.Rect.rect.height);
            }
        }

        void CloseChatMenu()
        {
            _menuOpen = false;
            if (_chatMenu != null)
                _chatMenu.SetActive(false);
        }

        void OpenSettingsFromMenu()
        {
            CloseChatMenu();
            if (_settings != null)
                _settings.Open();
        }

        void CopyTranscriptFromMenu()
        {
            CloseChatMenu();
            CopyTranscript();
        }

        /// <summary>
        /// Copies the whole conversation as plain text, with the speaker label and without the
        /// VN rich-text tags. Ctrl+C over the box does the same unless the input field has focus.
        /// </summary>
        void CopyTranscript()
        {
            if (_chat == null)
                return;
            var lines = _chat.TranscriptLines;
            var sb = new StringBuilder();
            var her = _name != null && !string.IsNullOrWhiteSpace(_name.text) ? _name.text : "Happi";
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                    continue;
                if (line.Kind == ChatLineKind.Assistant && line.Streaming && line.Text.Length == 0)
                    continue;
                if (!ChatConversation.Shows(line))
                    continue;
                if (sb.Length > 0)
                    sb.Append("\n\n");
                sb.Append(line.Kind == ChatLineKind.User ? "You" : line.Kind == ChatLineKind.Assistant ? her : "Note");
                sb.Append('\n');
                sb.Append(line.Text.Trim());
            }

            GUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log("Chat: copied " + lines.Count + " messages to the clipboard");
        }

        void CloseChatFromMenu()
        {
            CloseChatMenu();
            if (_chat != null)
                _chat.ShowChat(false);
        }

        void CloseProgramFromMenu()
        {
            CloseChatMenu();
            Application.Quit();
        }

        void ToggleRibbonExtras()
        {
            _ribbonExtras = !_ribbonExtras;
            _ribbonExtrasMenu.Text = _ribbonExtras ? "Hide playback & mic" : "Show playback & mic";
            _layoutWidth = -1f;
            CloseChatMenu();
        }

        void PlaceChatMenu(float s, float pad, float row, float gap, float headerBottom)
        {
            if (_chatMenu == null)
                return;
            var rect = (RectTransform)_chatMenu.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            var width = 170f * s;
            var options = _ribbonExtrasMenu != null ? 5 : 4;
            var height = row * options + gap * (options + 1);
            rect.sizeDelta = new Vector2(width, height);
            var rightInset = pad;
            if (_ribbonHide != null)
            {
                var gearRight = _close.Rect.anchoredPosition.x + _close.Rect.rect.width;
                rightInset = Mathf.Clamp(_box.rect.width - gearRight, pad, Mathf.Max(pad, _box.rect.width - width - pad));
            }
            var top = _ribbonHide != null ? Mathf.Min(headerBottom + gap, Mathf.Max(pad, _box.rect.height - height - pad)) : headerBottom + gap;
            rect.anchoredPosition = new Vector2(-rightInset, -top);
            HappiUiKit.Place(_menuSettings.Rect, gap, gap, width - 2f * gap, row);
            HappiUiKit.Place(_menuClose.Rect, gap, gap * 2f + row, width - 2f * gap, row);
            HappiUiKit.Place(_menuProgram.Rect, gap, gap * 3f + row * 2f, width - 2f * gap, row);
            HappiUiKit.Place(_menuCopy.Rect, gap, gap * 4f + row * 3f, width - 2f * gap, row);
            if (_ribbonExtrasMenu != null)
                HappiUiKit.Place(_ribbonExtrasMenu.Rect, gap, gap * 5f + row * 4f, width - 2f * gap, row);
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
