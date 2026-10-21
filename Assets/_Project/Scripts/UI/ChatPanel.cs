using System;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
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
    public sealed class ChatPanel : MonoBehaviour, ICompanionStartup, IDebugPanel, IOverlayKeepAwake, IAvatarDoubleClick
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
        InputField _input;
        InputField _keyInput;
        Sprite _white;
        Texture2D _whiteTexture;
        Rect _guiRect;
        Renderer[] _avatarRenderers = new Renderer[0];
        Renderer[] _placeholderRenderers = new Renderer[0];
        Vector3[] _boundsCorners = new Vector3[8];
        int _measuredChildCount = -1;
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
            _exchange.Talking = SetTalking;
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
                if (_input != null && _input.isFocused)
                    _input.DeactivateInputField();
                if (_keyInput != null && _keyInput.isFocused)
                    _keyInput.DeactivateInputField();
            }

            RefreshPlacement();
            RefreshTranscript(false);
            if (_actionLabel != null && _conversation != null)
                _actionLabel.text = _conversation.IsBusy ? "Stop" : "Send";
            if (_input != null && _conversation != null)
                _input.interactable = !_conversation.IsBusy;

            fieldFocused = (_input != null && _input.isFocused) || (_keyInput != null && _keyInput.isFocused);
            DebugInputGuard.SetTextFieldFocused(fieldFocused);
        }

        public void OnAvatarDoubleClick()
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
            var prompt = settings.systemPrompt;
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
                Debug.Log("Chat reply: " + result.Text);
            else if (!string.IsNullOrEmpty(result.LogMessage))
                Debug.LogWarning(result.LogMessage);
            else if (!string.IsNullOrEmpty(result.UserMessage))
                Debug.Log("Chat error: " + result.UserMessage);

            RefreshTranscript(true);
        }

        void CancelReply()
        {
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

            var avatar = MeasureAvatarBounds(Camera.main);
            _guiRect = ChatPanelPlacement.Place(Screen.width, Screen.height, avatar, gap);
            if (_panelRect == null)
                return;

            _panelRect.anchorMin = new Vector2(0f, 1f);
            _panelRect.anchorMax = new Vector2(0f, 1f);
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.anchoredPosition = new Vector2(_guiRect.x, -_guiRect.y);
            _panelRect.sizeDelta = new Vector2(_guiRect.width, _guiRect.height);
        }

        Rect MeasureAvatarBounds(Camera camera)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var any = false;
            if (camera != null)
                any = IncludeAvatar(camera, Screen.height, ref min, ref max);

            if (!any)
            {
                var x = Screen.width * 0.5f;
                if (camera != null)
                {
                    var point = camera.WorldToScreenPoint(new Vector3(0f, 1f, 0f));
                    if (point.z > 0f)
                        x = point.x;
                }

                return new Rect(x - 70f, 0f, 140f, 10f);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        bool IncludeAvatar(Camera camera, float screenHeight, ref Vector2 min, ref Vector2 max)
        {
            if (_placeholderRenderers.Length == 0)
            {
                var placeholder = GameObject.Find("Placeholder Avatar");
                if (placeholder != null)
                    _placeholderRenderers = placeholder.GetComponentsInChildren<Renderer>(true);
            }

            if (_measuredChildCount != transform.childCount || MissingRenderer(_avatarRenderers))
            {
                _measuredChildCount = transform.childCount;
                _avatarRenderers = GetComponentsInChildren<Renderer>(true);
            }

            var any = IncludeRenderers(_avatarRenderers, camera, screenHeight, ref min, ref max);
            any |= IncludeRenderers(_placeholderRenderers, camera, screenHeight, ref min, ref max);
            return any;
        }

        static bool MissingRenderer(Renderer[] renderers)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    return true;
            }

            return false;
        }

        bool IncludeRenderers(Renderer[] renderers, Camera camera, float screenHeight, ref Vector2 min, ref Vector2 max)
        {
            var any = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                ChatPanelPlacement.WriteBoundsCorners(renderer.bounds, _boundsCorners);
                for (var c = 0; c < _boundsCorners.Length; c++)
                {
                    var screen = camera.WorldToScreenPoint(_boundsCorners[c]);
                    if (ChatPanelPlacement.TryIncludeScreenPoint(screenHeight, screen, ref min, ref max))
                        any = true;
                }
            }

            return any;
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
            title.rectTransform.sizeDelta = new Vector2(-76f, 28f);
            title.raycastTarget = false;

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

            _input = CreateField(_panel.transform, font, "Message", false);
            var inputRect = _input.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.anchoredPosition = new Vector2(0f, 34f);
            inputRect.sizeDelta = new Vector2(-12f, 26f);
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
