using System;
using System.Globalization;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using Director = AICompanion.AnimationDirector.AnimationDirector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.UI
{
    /// <summary>
    /// Session chat panel. It talks to <see cref="IChatProvider"/> only through the
    /// registry and <see cref="ChatSettings"/>. F10 shows and hides it.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class ChatPanel : MonoBehaviour, ICompanionStartup, IDebugPanel, IOverlayKeepAwake, IAvatarDoubleClick, ISpeechNotes, IChatSubmitter, IChatWindow
    {
        const string OpenPref = "ai-companion.chat-open";
        const string HelloPrompt = "Say hello in one short sentence.";

        [SerializeField] ChatSettings settings;
        [SerializeField] float sideGap = ChatPanelPlacement.DefaultGap;

        ChatConversation _conversation;
        ChatExchange _exchange;
        ChatProviderRegistry _registry;
        CancellationTokenSource _cancel;
        Canvas _canvas;
        GameObject _canvasObject;
        GameObject _panel;
        RectTransform _panelRect;
        ScrollRect _scroll;
        RectTransform _content;
        Text _transcript;
        Text _actionLabel;
        Text _muteLabel;
        Text _micLabel;
        Image _micImage;
        ISpeechOutput _speech;
        IPushToTalk _talk;
        bool _wasRecording;
        InputField _input;
        InputField _keyInput;
        Sprite _white;
        Texture2D _whiteTexture;
        Rect _guiRect;
        Transform _placeholder;
        IAvatarAnchor _anchor;
        IDesktopWindow _desktop;
        float _anchorX;
        bool _anchorReady;
        int _anchorSource = -1;
        bool _placementLogged;
        float _placementLogAt;
        int _logX;
        int _logY;
        int _logW;
        int _logH;
        int _logScreenW;
        int _logScreenH;
        int _logVisibleX;
        int _logVisibleY;
        int _logVisibleW;
        int _logVisibleH;
        int _shownRevision = -1;
        bool _visible = true;
        bool _started;
        bool _alive = true;

        public Rect InteractiveRect => _visible ? _guiRect : new Rect(0f, 0f, 0f, 0f);

        public bool KeepFrameRateActive
        {
            get
            {
                if (_conversation != null && _conversation.IsBusy)
                    return true;
                BindTalk();
                if (_talk != null && (_talk.IsRecording || _talk.IsTranscribing))
                    return true;
                BindSpeech();
                if (_speech != null && _speech.IsSpeaking)
                    return true;
                if (_input != null && _input.isFocused)
                    return true;
                return _keyInput != null && _keyInput.isFocused;
            }
        }

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            if (settings == null)
                settings = ScriptableObject.CreateInstance<ChatSettings>();

            _conversation = new ChatConversation();
            _exchange = new ChatExchange(_conversation);
            _exchange.SpokenDelta = ForwardSpeech;
            _registry = ChatProviderFactory.Create(settings, new EnvironmentApiKeySource(), new UnityChatTransport(this));
            _visible = PlayerPrefs.GetInt(OpenPref, 1) != 0;
            BuildUi();
            SetVisible(_visible, false);
        }

        void Start()
        {
            StartCompanion();
        }

        void OnDisable()
        {
            DebugInputGuard.SetTextFieldFocused(false);
            SetMicHeld(false);
        }

        void OnDestroy()
        {
            _alive = false;
            DebugInputGuard.SetTextFieldFocused(false);
            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
                _cancel = null;
            }

            BindSpeech();
            if (_speech != null)
                _speech.Cancel();
            SetTalking(false);
            if (_white != null)
                Destroy(_white);
            if (_whiteTexture != null)
                Destroy(_whiteTexture);
            if (_canvasObject != null)
                Destroy(_canvasObject);
        }

        void Update()
        {
            var fieldFocused = (_input != null && _input.isFocused) || (_keyInput != null && _keyInput.isFocused);
            DebugInputGuard.SetTextFieldFocused(fieldFocused);
            if (!_started)
                return;

            EnsureCamera();
            if (Input.GetKeyDown(KeyCode.F10))
                SetVisible(!_visible, !_visible);

            if (Input.GetKeyDown(KeyCode.F9) && !DebugInputGuard.IsBlocked(KeyCode.F9))
                QuickSend();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_conversation != null && _conversation.IsBusy)
                    CancelReply();
                else
                {
                    BindSpeech();
                    if (_speech != null)
                        _speech.Cancel();
                }

                if (_input != null && _input.isFocused)
                    _input.DeactivateInputField();
                if (_keyInput != null && _keyInput.isFocused)
                    _keyInput.DeactivateInputField();
            }

            RefreshPlacement();
            RefreshTranscript(false);
            BindSpeech();
            var busy = _conversation != null && _conversation.IsBusy;
            var speaking = _speech != null && _speech.IsSpeaking;
            if (_actionLabel != null)
                _actionLabel.text = busy || speaking ? "Stop" : "Send";
            RefreshMuteLabel();
            RefreshMic();
            if (_input != null && _conversation != null)
                _input.interactable = !busy;

            fieldFocused = (_input != null && _input.isFocused) || (_keyInput != null && _keyInput.isFocused);
            DebugInputGuard.SetTextFieldFocused(fieldFocused);
        }

        public void OnAvatarDoubleClick()
        {
            OpenChat();
        }

        public void OpenChat()
        {
            if (!_started)
                StartCompanion();
            SetVisible(true, true);
        }

        void QuickSend()
        {
            if (!_visible)
                SetVisible(true, false);

            var draft = _input != null ? _input.text : string.Empty;
            var text = string.IsNullOrWhiteSpace(draft) ? HelloPrompt : draft;
            if (!TrySend(text))
                return;
            if (_input != null && !string.IsNullOrWhiteSpace(draft))
                _input.text = string.Empty;
        }

        void SubmitDraft(string text)
        {
            if (!TrySend(text))
                return;
            if (_input != null)
            {
                _input.text = string.Empty;
                _input.ActivateInputField();
            }
        }

        public bool SubmitUserText(string text)
        {
            if (!_alive)
                return false;
            if (!_started)
                StartCompanion();
            if (!_visible)
                SetVisible(true, false);
            return TrySend(text);
        }

        bool TrySend(string text)
        {
            if (_conversation == null || _conversation.IsBusy || string.IsNullOrWhiteSpace(text))
                return false;

            if (_registry == null || settings == null || !_registry.TryGet(settings.providerId, out var provider))
            {
                var message = ChatErrors.UnknownProvider(settings != null ? settings.providerId : string.Empty);
                _conversation.AddSystem(message);
                Debug.Log("Chat error: " + message);
                RefreshTranscript(true);
                return false;
            }

            if (_cancel != null)
                _cancel.Dispose();
            _cancel = new CancellationTokenSource();
            BindSpeech();
            if (_speech != null)
            {
                _speech.Cancel();
                _speech.BeginUtterance();
            }

            var prompt = settings.systemPrompt;
            var director = GetComponent<Director>();
            if (director != null)
                prompt += director.SystemPromptSuffix;
            var temperature = settings.temperature;
            var maxTokens = settings.maxTokens;
            Dispatch(provider, text, prompt, temperature, maxTokens, _cancel.Token);
            return true;
        }

        async void Dispatch(IChatProvider provider, string text, string prompt, float temperature, int maxTokens, CancellationToken token)
        {
            var result = await _exchange.Run(provider, text, prompt, temperature, maxTokens, token);
            if (!_alive)
                return;

            if (result.Ok)
                ApplyCue();

            BindSpeech();
            if (_speech != null && _cancel != null && _cancel.Token.Equals(token))
            {
                if (result.Ok)
                    _speech.CompleteUtterance();
                else
                    _speech.Cancel();
            }

            if (result.Ok)
                Debug.Log("Chat reply: " + result.Text);
            else if (!string.IsNullOrEmpty(result.LogMessage))
                Debug.LogWarning(result.LogMessage);
            else if (!string.IsNullOrEmpty(result.UserMessage))
                Debug.Log("Chat error: " + result.UserMessage);

            RefreshTranscript(true);
        }

        void CancelReply()
        {
            BindSpeech();
            if (_speech != null)
                _speech.Cancel();
            if (_cancel != null)
                _cancel.Cancel();
        }

        void OnActionClick()
        {
            if (_conversation != null && _conversation.IsBusy)
            {
                CancelReply();
                return;
            }

            BindSpeech();
            if (_speech != null && _speech.IsSpeaking)
            {
                _speech.Cancel();
                return;
            }

            SubmitDraft(_input != null ? _input.text : string.Empty);
        }

        void SaveKey()
        {
            if (_conversation == null)
                return;

            var key = _keyInput != null ? (_keyInput.text ?? string.Empty).Trim() : string.Empty;
            if (key.Length == 0)
            {
                _conversation.AddSystem("Enter an API key first.");
                RefreshTranscript(true);
                return;
            }

            try
            {
                ApiKeyFile.SavePreferred(key);
                if (_keyInput != null)
                    _keyInput.text = string.Empty;
                _conversation.AddSystem("API key saved on this PC.");
            }
            catch (Exception)
            {
                _conversation.AddSystem("Could not save the API key on this PC.");
            }

            RefreshTranscript(true);
        }

        void SetTalking(bool talking)
        {
            if (!_alive)
                return;

            var animator = GetComponent<IAvatarAnimator>();
            if (animator != null)
                animator.SetTalking(talking);
        }

        /// <summary>
        /// Hands the cue parsed from the finished reply to the director. The tag itself
        /// never reached the transcript or speech, so the avatar reacts to what was said
        /// rather than to markup.
        /// </summary>
        void ApplyCue()
        {
            var director = GetComponent<Director>();
            if (director != null)
                director.Play(_exchange.LastCue);
        }

        public void ShowSpeechNote(string message)
        {
            if (!_alive || _conversation == null || string.IsNullOrWhiteSpace(message))
                return;

            _conversation.AddSystem(message);
            RefreshTranscript(true);
        }

        void ForwardSpeech(string piece)
        {
            BindSpeech();
            if (_speech != null)
                _speech.Append(piece);
        }

        void BindSpeech()
        {
            if (_speech == null)
                _speech = GetComponent<ISpeechOutput>();
        }

        void OnMuteClick()
        {
            BindSpeech();
            if (_speech == null)
                return;

            _speech.SetMuted(!_speech.IsMuted);
            RefreshMuteLabel();
        }

        void RefreshMuteLabel()
        {
            if (_muteLabel == null)
                return;

            BindSpeech();
            _muteLabel.text = _speech != null && _speech.IsMuted ? "Muted" : "Mute";
        }

        void RefreshMic()
        {
            BindTalk();
            var recording = _talk != null && _talk.IsRecording;
            var waiting = _talk != null && _talk.IsTranscribing;
            if (recording && !_wasRecording && !_visible)
                SetVisible(true, false);
            _wasRecording = recording;
            if (_micLabel != null)
                _micLabel.text = recording ? "Rec" : waiting ? "..." : "Mic";
            if (_micImage != null)
                _micImage.color = recording
                    ? new Color(0.72f, 0.16f, 0.18f, 1f)
                    : new Color(0.36f, 0.28f, 0.46f, 1f);
        }

        void BindTalk()
        {
            if (_talk == null)
                _talk = GetComponent<IPushToTalk>();
        }

        void SetMicHeld(bool held)
        {
            BindTalk();
            if (_talk != null)
                _talk.SetButtonHeld(held);
        }

        void SetVisible(bool visible, bool focusInput)
        {
            _visible = visible;
            if (_panel != null)
                _panel.SetActive(visible);
            PlayerPrefs.SetInt(OpenPref, visible ? 1 : 0);
            if (!visible)
            {
                if (_input != null)
                    _input.DeactivateInputField();
                if (_keyInput != null)
                    _keyInput.DeactivateInputField();
                DebugInputGuard.SetTextFieldFocused(false);
                _guiRect = new Rect(0f, 0f, 0f, 0f);
                return;
            }

            RefreshPlacement();
            if (focusInput)
                FocusInput();
        }

        void FocusInput()
        {
            var typing = GetComponent<ITypingFocus>();
            if (typing != null)
                typing.FocusForTyping();
            if (_input == null)
                return;

            _input.ActivateInputField();
            _input.Select();
        }

        void EnsureCamera()
        {
            if (_canvas == null)
                return;

            var camera = Camera.main;
            if (camera == null)
            {
                if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            if (_canvas.renderMode != RenderMode.ScreenSpaceCamera || _canvas.worldCamera != camera)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = camera;
                _canvas.planeDistance = 0.5f;
            }
        }

        void RefreshPlacement()
        {
            if (!_visible)
                return;

            var gap = sideGap;
            if (float.IsNaN(gap) || gap < 0f)
                gap = 0f;

            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var visible = VisibleRect(screenWidth, screenHeight);
            var anchorX = ResolveAnchorX(screenWidth);
            var column = ChatPanelPlacement.Column(anchorX, ChatPanelPlacement.BodyWidth, screenWidth, screenHeight);
            var placed = ChatPanelPlacement.Place(screenWidth, screenHeight, column, gap);
            _guiRect = ChatPanelPlacement.ClampInside(placed, visible);
            LogPlacement(screenWidth, screenHeight, visible);
            if (_panelRect == null)
                return;

            _panelRect.anchorMin = new Vector2(0f, 1f);
            _panelRect.anchorMax = new Vector2(0f, 1f);
            _panelRect.pivot = new Vector2(0f, 1f);
            var canvasOrigin = Vector2.zero;
            if (_canvas != null)
            {
                var pixel = _canvas.pixelRect;
                if (pixel.width > 1f && pixel.height > 1f)
                {
                    var gui = ChatPanelPlacement.FromBottomLeft(pixel, screenHeight);
                    canvasOrigin = new Vector2(gui.x, gui.y);
                }
            }

            _panelRect.anchoredPosition = new Vector2(_guiRect.x - canvasOrigin.x, -(_guiRect.y - canvasOrigin.y));
            _panelRect.sizeDelta = new Vector2(_guiRect.width, _guiRect.height);
        }

        Rect VisibleRect(float screenWidth, float screenHeight)
        {
            var screen = new Rect(0f, 0f, screenWidth, screenHeight);
            var visible = screen;
            if (_canvas != null)
            {
                var pixel = _canvas.pixelRect;
                if (pixel.width > 1f && pixel.height > 1f)
                    visible = ChatPanelPlacement.Intersect(visible, ChatPanelPlacement.FromBottomLeft(pixel, screenHeight));
            }

            var safe = Screen.safeArea;
            if (safe.width > 1f && safe.height > 1f)
                visible = ChatPanelPlacement.Intersect(visible, ChatPanelPlacement.FromBottomLeft(safe, screenHeight));

            if (_desktop == null)
                _desktop = GetComponent<IDesktopWindow>();
            if (_desktop != null && _desktop.TryGetDesktopWindow(out var window))
            {
                var gui = ChatPanelPlacement.VisibleGui(
                    screenWidth,
                    screenHeight,
                    window.Left,
                    window.Top,
                    window.Right,
                    window.Bottom,
                    window.WorkLeft,
                    window.WorkTop,
                    window.WorkRight,
                    window.WorkBottom);
                visible = ChatPanelPlacement.Intersect(visible, gui);
            }

            if (visible.width < 1f || visible.height < 1f)
                return screen;
            return visible;
        }

        float ResolveAnchorX(float screenWidth)
        {
            if (!TryReadAnchor(out var target, out var source))
            {
                if (_anchorReady)
                    return _anchorX;

                target = screenWidth * 0.5f;
                source = 0;
            }

            if (source != _anchorSource)
            {
                _anchorReady = false;
                _anchorSource = source;
            }

            _anchorX = ChatPanelPlacement.SmoothAnchor(_anchorX, target, Time.unscaledDeltaTime, _anchorReady, out _anchorReady);
            return _anchorX;
        }

        bool TryReadAnchor(out float screenX, out int source)
        {
            screenX = 0f;
            source = 0;
            var camera = Camera.main;
            if (camera == null)
                return false;

            if (_anchor == null)
                _anchor = GetComponent<IAvatarAnchor>();
            if (_anchor != null && _anchor.TryGetAnchor(out var world))
            {
                var point = camera.WorldToScreenPoint(world);
                if (point.z > 0f)
                {
                    screenX = point.x;
                    source = 1;
                    return true;
                }
            }

            if (_placeholder == null)
            {
                var found = GameObject.Find("Placeholder Avatar");
                if (found != null)
                    _placeholder = found.transform;
            }

            if (_placeholder != null)
            {
                var point = camera.WorldToScreenPoint(_placeholder.position + Vector3.up);
                if (point.z > 0f)
                {
                    screenX = point.x;
                    source = 2;
                    return true;
                }
            }

            return false;
        }

        void LogPlacement(float screenWidth, float screenHeight, Rect visible)
        {
            var x = Mathf.RoundToInt(_guiRect.x);
            var y = Mathf.RoundToInt(_guiRect.y);
            var w = Mathf.RoundToInt(_guiRect.width);
            var h = Mathf.RoundToInt(_guiRect.height);
            var sw = Mathf.RoundToInt(screenWidth);
            var sh = Mathf.RoundToInt(screenHeight);
            var vx = Mathf.RoundToInt(visible.x);
            var vy = Mathf.RoundToInt(visible.y);
            var vw = Mathf.RoundToInt(visible.width);
            var vh = Mathf.RoundToInt(visible.height);
            var changed = !_placementLogged
                || x != _logX || y != _logY || w != _logW || h != _logH
                || sw != _logScreenW || sh != _logScreenH
                || vx != _logVisibleX || vy != _logVisibleY || vw != _logVisibleW || vh != _logVisibleH;
            if (!changed)
                return;
            if (_placementLogged && Time.unscaledTime - _placementLogAt < 1f)
                return;

            _placementLogged = true;
            _placementLogAt = Time.unscaledTime;
            _logX = x;
            _logY = y;
            _logW = w;
            _logH = h;
            _logScreenW = sw;
            _logScreenH = sh;
            _logVisibleX = vx;
            _logVisibleY = vy;
            _logVisibleW = vw;
            _logVisibleH = vh;
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "ChatPanel: pos {0} {1} size {2}x{3} screen {4}x{5} visible {6} {7} {8}x{9}",
                x, y, w, h, sw, sh, vx, vy, vw, vh));
        }

        void RefreshTranscript(bool force)
        {
            if (_conversation == null || _transcript == null)
                return;
            if (!force && _shownRevision == _conversation.Revision)
                return;

            _shownRevision = _conversation.Revision;
            _transcript.text = _conversation.FormatTranscript();
            var width = _content != null ? _content.rect.width : _guiRect.width - 16f;
            if (width < 8f)
                width = Mathf.Max(8f, _guiRect.width - 16f);

            var textRect = _transcript.rectTransform;
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            Canvas.ForceUpdateCanvases();
            var height = Mathf.Max(8f, _transcript.preferredHeight);
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (_content != null)
            {
                _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }

            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 0f;
        }

        void BuildUi()
        {
            var font = BuiltinFont();
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTexture.hideFlags = HideFlags.HideAndDontSave;
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
            _white = Sprite.Create(_whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _white.hideFlags = HideFlags.HideAndDontSave;

            _canvasObject = new GameObject("ChatCanvas");
            _canvasObject.transform.SetParent(transform, false);
            _canvasObject.hideFlags = HideFlags.DontSave;
            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.planeDistance = 0.5f;
            _canvas.sortingOrder = 50;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            EnsureCamera();

            _panel = new GameObject("ChatPanel");
            _panel.transform.SetParent(_canvasObject.transform, false);
            _panelRect = _panel.AddComponent<RectTransform>();
            var panelImage = _panel.AddComponent<Image>();
            panelImage.sprite = _white;
            panelImage.color = new Color(0.10f, 0.09f, 0.14f, 0.94f);
            panelImage.raycastTarget = true;

            var title = CreateText(_panel.transform, font, 14, TextAnchor.MiddleLeft, "Chat   F10");
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(8f, 0f);
            title.rectTransform.sizeDelta = new Vector2(-140f, 28f);
            title.raycastTarget = false;

            var mute = CreateButton(_panel.transform, font, "Mute", OnMuteClick);
            var muteRect = mute.GetComponent<RectTransform>();
            muteRect.anchorMin = new Vector2(1f, 1f);
            muteRect.anchorMax = new Vector2(1f, 1f);
            muteRect.pivot = new Vector2(1f, 1f);
            muteRect.anchoredPosition = new Vector2(-74f, -4f);
            muteRect.sizeDelta = new Vector2(52f, 22f);
            _muteLabel = mute.GetComponentInChildren<Text>();

            var action = CreateButton(_panel.transform, font, "Send", OnActionClick);
            var actionRect = action.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(1f, 1f);
            actionRect.anchorMax = new Vector2(1f, 1f);
            actionRect.pivot = new Vector2(1f, 1f);
            actionRect.anchoredPosition = new Vector2(-6f, -4f);
            actionRect.sizeDelta = new Vector2(64f, 22f);
            _actionLabel = action.GetComponentInChildren<Text>();

            var scrollObject = new GameObject("History");
            scrollObject.transform.SetParent(_panel.transform, false);
            var scrollRect = scrollObject.AddComponent<RectTransform>();
            Stretch(scrollRect, 6f, 30f, 6f, 64f);
            _scroll = scrollObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 24f;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            Stretch(viewportRect, 0f, 0f, 0f, 0f);
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.sprite = _white;
            viewportImage.color = new Color(0.08f, 0.07f, 0.11f, 0.94f);
            viewport.AddComponent<RectMask2D>();
            _scroll.viewport = viewportRect;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _content = content.AddComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 8f);
            _scroll.content = _content;

            _transcript = CreateText(content.transform, font, 14, TextAnchor.UpperLeft, string.Empty);
            _transcript.horizontalOverflow = HorizontalWrapMode.Wrap;
            _transcript.verticalOverflow = VerticalWrapMode.Overflow;
            var transcriptRect = _transcript.rectTransform;
            transcriptRect.anchorMin = new Vector2(0f, 1f);
            transcriptRect.anchorMax = new Vector2(1f, 1f);
            transcriptRect.pivot = new Vector2(0.5f, 1f);
            transcriptRect.anchoredPosition = Vector2.zero;
            transcriptRect.sizeDelta = new Vector2(-8f, 8f);

            var mic = CreateHoldButton(_panel.transform, font, "Mic");
            var micRect = mic.GetComponent<RectTransform>();
            micRect.anchorMin = new Vector2(0f, 0f);
            micRect.anchorMax = new Vector2(0f, 0f);
            micRect.pivot = new Vector2(0f, 0f);
            micRect.anchoredPosition = new Vector2(6f, 34f);
            micRect.sizeDelta = new Vector2(44f, 26f);
            _micImage = mic.GetComponent<Image>();
            _micLabel = mic.GetComponentInChildren<Text>();

            _input = CreateField(_panel.transform, font, "Message", false);
            var inputRect = _input.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.anchoredPosition = new Vector2(23f, 34f);
            inputRect.sizeDelta = new Vector2(-58f, 26f);
            _input.onSubmit.AddListener(SubmitDraft);

            _keyInput = CreateField(_panel.transform, font, "API key", true);
            var keyRect = _keyInput.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0f, 0f);
            keyRect.anchorMax = new Vector2(1f, 0f);
            keyRect.pivot = new Vector2(0.5f, 0f);
            keyRect.anchoredPosition = new Vector2(-28f, 4f);
            keyRect.sizeDelta = new Vector2(-68f, 26f);
            _keyInput.onSubmit.AddListener(_ => SaveKey());

            var save = CreateButton(_panel.transform, font, "Save", () => SaveKey());
            var saveRect = save.GetComponent<RectTransform>();
            saveRect.anchorMin = new Vector2(1f, 0f);
            saveRect.anchorMax = new Vector2(1f, 0f);
            saveRect.pivot = new Vector2(1f, 0f);
            saveRect.anchoredPosition = new Vector2(-6f, 4f);
            saveRect.sizeDelta = new Vector2(52f, 26f);
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var system = new GameObject("EventSystem");
            system.transform.SetParent(transform, false);
            system.hideFlags = HideFlags.DontSave;
            system.AddComponent<EventSystem>();
            system.AddComponent<StandaloneInputModule>();
        }

        InputField CreateField(Transform parent, Font font, string placeholder, bool password)
        {
            var field = new GameObject(password ? "ApiKey" : "Message");
            field.transform.SetParent(parent, false);
            field.AddComponent<RectTransform>();
            var image = field.AddComponent<Image>();
            image.sprite = _white;
            image.color = new Color(0.18f, 0.16f, 0.22f, 1f);
            var input = field.AddComponent<InputField>();
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.targetGraphic = image;

            var text = CreateText(field.transform, font, 14, TextAnchor.MiddleLeft, string.Empty);
            Stretch(text.rectTransform, 6f, 2f, 6f, 2f);
            text.supportRichText = false;
            var hint = CreateText(field.transform, font, 14, TextAnchor.MiddleLeft, placeholder);
            Stretch(hint.rectTransform, 6f, 2f, 6f, 2f);
            hint.fontStyle = FontStyle.Italic;
            hint.color = new Color(0.75f, 0.7f, 0.78f, 0.85f);
            hint.supportRichText = false;
            input.textComponent = text;
            input.placeholder = hint;
            return input;
        }

        GameObject CreateHoldButton(Transform parent, Font font, string label)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            buttonObject.AddComponent<RectTransform>();
            var image = buttonObject.AddComponent<Image>();
            image.sprite = _white;
            image.color = new Color(0.36f, 0.28f, 0.46f, 1f);
            image.raycastTarget = true;
            var trigger = buttonObject.AddComponent<EventTrigger>();
            AddHold(trigger, EventTriggerType.PointerDown, true);
            AddHold(trigger, EventTriggerType.PointerUp, false);
            AddHold(trigger, EventTriggerType.PointerExit, false);
            var text = CreateText(buttonObject.transform, font, 12, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform, 2f, 1f, 2f, 1f);
            text.raycastTarget = false;
            return buttonObject;
        }

        void AddHold(EventTrigger trigger, EventTriggerType type, bool held)
        {
            var entry = new EventTrigger.Entry();
            entry.eventID = type;
            entry.callback.AddListener(_ => SetMicHeld(held));
            trigger.triggers.Add(entry);
        }

        Button CreateButton(Transform parent, Font font, string label, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            buttonObject.AddComponent<RectTransform>();
            var image = buttonObject.AddComponent<Image>();
            image.sprite = _white;
            image.color = new Color(0.36f, 0.28f, 0.46f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(onClick);
            var text = CreateText(buttonObject.transform, font, 13, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform, 2f, 1f, 2f, 1f);
            text.raycastTarget = false;
            return button;
        }

        static Text CreateText(Transform parent, Font font, int size, TextAnchor anchor, string value)
        {
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(80f, 20f);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.95f, 0.93f, 0.90f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.text = value ?? string.Empty;
            return text;
        }

        static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }
    }
}
