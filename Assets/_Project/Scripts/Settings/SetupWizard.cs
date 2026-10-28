using System;
using System.Collections;
using System.Collections.Generic;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Overlay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    /// <summary>
    /// First-run setup inside the existing overlay window. Opening and closing
    /// it does not restyle the HWND: transparency, topmost, and click-through
    /// stay as they were. The panel is ordinary uGUI, so the hit test keeps it
    /// clickable while the rest of the window still clicks through.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class SetupWizard : MonoBehaviour, ICompanionStartup, IDebugPanel, IOverlayKeepAwake
    {
        readonly List<InputField> _fields = new List<InputField>();

        Canvas _canvas;
        GameObject _canvasObject;
        RectTransform _panelRect;
        RectTransform _body;
        Text _status;
        Text _stepLabel;
        Text _nextLabel;
        Button _backButton;
        Font _font;
        Sprite _white;
        Texture2D _whiteTexture;
        InputField _keyInput;
        InputField _nameInput;
        Rect _guiRect;
        string _statusMessage = string.Empty;
        string _nameDraft = string.Empty;
        int _step;
        int _focusClaims;
        int _probeSerial;
        int _fieldGeneration;
        bool _open;
        bool _started;
        bool _building;
        bool _rebuild;
        bool _probeBusy;
        bool _nameDraftLive;

        public bool IsOpen => _open;

        public Rect InteractiveRect => _open ? _guiRect : new Rect(0f, 0f, 0f, 0f);

        public bool KeepFrameRateActive => _open || _focusClaims > 0;

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            var panel = GetComponent<SettingsPanel>();
            var firstRun = panel != null && panel.ClaimSetupGate();
            if (panel != null)
                panel.SetupReplayRequested += Show;
            BuildChrome();
            if (firstRun)
                Show();
            else
                Hide();
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
            var panel = GetComponent<SettingsPanel>();
            if (panel != null)
                panel.SetupReplayRequested -= Show;
            if (_white != null)
                Destroy(_white);
            if (_whiteTexture != null)
                Destroy(_whiteTexture);
        }

        void Update()
        {
            if (!_started || !_open)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (AnyFieldFocused())
                    BlurFields();
            }

            SyncFocus(AnyFieldFocused());
            Place();
        }

        void LateUpdate()
        {
            if (!_rebuild || !_open)
                return;
            _rebuild = false;
            RebuildBody();
            RefreshFooter();
        }

        public void Show()
        {
            if (!_started)
                StartCompanion();

            var panel = GetComponent<SettingsPanel>();
            if (panel != null)
            {
                panel.Close();
                panel.HoldSetupSave();
            }

            _step = SetupWizardFlow.Welcome;
            _nameDraftLive = false;
            _probeSerial++;
            _probeBusy = false;
            _open = true;
            if (_canvasObject != null)
                _canvasObject.SetActive(true);
            var window = GetComponent<OverlayWindow>();
            if (window != null)
                window.FocusForTyping();
            _rebuild = true;
            Place();
            RefreshFooter();
        }

        public void Hide()
        {
            _open = false;
            _probeSerial++;
            _probeBusy = false;
            if (_keyInput != null)
                _keyInput.text = string.Empty;
            _keyInput = null;
            BlurFields();
            ReleaseFocus();
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
            _guiRect = new Rect(0f, 0f, 0f, 0f);
        }

        void Back()
        {
            if (!SetupWizardFlow.CanGoBack(_step))
                return;
            _step = SetupWizardFlow.Back(_step);
            _rebuild = true;
        }

        void Skip()
        {
            if (_step == SetupWizardFlow.Ai && _keyInput != null)
                _keyInput.text = string.Empty;
            if (_step == SetupWizardFlow.Personality)
            {
                _nameDraftLive = false;
                _nameInput = null;
            }

            var settings = CurrentSettings();
            SetupWizardFlow.ApplySkip(settings, _step);
            var panel = GetComponent<SettingsPanel>();
            if (panel != null && (_step == SetupWizardFlow.Ai || _step == SetupWizardFlow.Avatar || _step == SetupWizardFlow.Personality))
                panel.PreviewSetup();
            Advance();
        }

        void Next()
        {
            if (_step == SetupWizardFlow.Ai)
            {
                if (!CommitTypedKey(false))
                    return;
            }
            else if (_step == SetupWizardFlow.Personality)
            {
                var name = _nameDraftLive ? _nameDraft : (_nameInput != null ? _nameInput.text : CurrentSettings().CompanionName);
                SetupWizardFlow.ApplyPersonality(CurrentSettings(), name, CurrentSettings().Relationship);
                _nameDraftLive = false;
                var panel = GetComponent<SettingsPanel>();
                if (panel != null)
                    panel.PreviewSetup();
            }

            Advance();
        }

        void Advance()
        {
            if (SetupWizardFlow.IsLast(_step) || SetupWizardFlow.IsComplete(_step))
            {
                Finish();
                return;
            }

            _step = SetupWizardFlow.Forward(_step);
            _rebuild = true;
        }

        void Finish()
        {
            var panel = GetComponent<SettingsPanel>();
            if (panel != null)
                panel.CommitSetup();
            Hide();
        }

        CompanionSettings CurrentSettings()
        {
            var panel = GetComponent<SettingsPanel>();
            if (panel != null && panel.CurrentSettings != null)
                return panel.CurrentSettings;
            return CompanionSettings.CreateDefault();
        }

        bool CommitTypedKey(bool testOnly)
        {
            var typed = _keyInput != null ? (_keyInput.text ?? string.Empty).Trim() : string.Empty;
            var entered = typed.Length > 0;
            if (entered)
            {
                try
                {
                    ApiKeyFile.SavePreferred(typed);
                }
                catch (Exception)
                {
                    SetStatus("Could not save the API key.");
                    return false;
                }

                typed = string.Empty;
                if (_keyInput != null)
                    _keyInput.text = string.Empty;
                SetStatus(ApiKeyMask.SavedLabel + ". It is in the secret store, not in settings.");
            }

            if (!testOnly)
            {
                var hasKey = entered || SetupWizardGate.HasSavedApiKey() || SetupWizardGate.EnvironmentHasApiKey();
                SetupWizardFlow.ApplyAiContinue(CurrentSettings(), hasKey);
                var panel = GetComponent<SettingsPanel>();
                if (panel != null)
                    panel.PreviewSetup();
            }

            return true;
        }

        void TestConnection()
        {
            if (_probeBusy)
                return;
            if (!CommitTypedKey(true))
                return;

            _probeSerial++;
            _probeBusy = true;
            SetStatus("Testing connection...");
            _rebuild = true;
            StartCoroutine(Probe(_probeSerial));
        }

        IEnumerator Probe(int serial)
        {
            var settings = CurrentSettings();
            var provider = settings.ProviderId;
            if (!settings.PrivateMode && (SetupWizardGate.HasSavedApiKey() || SetupWizardGate.EnvironmentHasApiKey()))
                provider = ChatDefaults.GrokId;
            var plan = ConnectionProbe.Plan(settings.PrivateMode, provider, ChatDefaults.BaseUrl, settings.LocalBaseUrl);
            SetStatus(plan.Summary);
            if (!plan.Allowed)
            {
                FinishProbe(serial);
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
                    FinishProbe(serial);
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
                if (serial != _probeSerial || !_open)
                    yield break;

                var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                var status = ConnectionProbe.FormatResult((int)request.responseCode, request.error, body);
                SetStatus(status);
                Debug.Log(status);
            }

            FinishProbe(serial);
        }

        void FinishProbe(int serial)
        {
            if (serial != _probeSerial)
                return;
            _probeBusy = false;
            if (_open && _step == SetupWizardFlow.Ai)
                _rebuild = true;
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

            _canvasObject = new GameObject("SetupWizardCanvas");
            _canvasObject.transform.SetParent(transform, false);
            _canvasObject.hideFlags = HideFlags.DontSave;
            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.sortingOrder = 90;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var panel = new GameObject("SetupWizardPanel");
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
            title.rectTransform.sizeDelta = new Vector2(-16f, 24f);
            title.raycastTarget = false;

            _stepLabel = CreateText(panel.transform, _font, 13, TextAnchor.MiddleLeft, SetupWizardCopy.StepLabel(0));
            _stepLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _stepLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _stepLabel.rectTransform.pivot = new Vector2(0f, 1f);
            _stepLabel.rectTransform.anchoredPosition = new Vector2(10f, -28f);
            _stepLabel.rectTransform.sizeDelta = new Vector2(-16f, 20f);
            _stepLabel.color = new Color(0.86f, 0.78f, 0.92f, 1f);
            _stepLabel.raycastTarget = false;

            var body = new GameObject("Body");
            body.transform.SetParent(panel.transform, false);
            var bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(8f, 64f);
            bodyRect.offsetMax = new Vector2(-8f, -52f);
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

            var back = CreateButton(panel.transform, "Back", Back);
            _backButton = back.GetComponent<Button>();
            var backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0f);
            backRect.anchorMax = new Vector2(0f, 0f);
            backRect.pivot = new Vector2(0f, 0f);
            backRect.anchoredPosition = new Vector2(8f, 28f);
            backRect.sizeDelta = new Vector2(88f, 26f);

            var skip = CreateButton(panel.transform, "Skip", Skip);
            var skipRect = skip.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0f, 0f);
            skipRect.anchorMax = new Vector2(0f, 0f);
            skipRect.pivot = new Vector2(0f, 0f);
            skipRect.anchoredPosition = new Vector2(104f, 28f);
            skipRect.sizeDelta = new Vector2(88f, 26f);

            var next = CreateButton(panel.transform, "Next", Next);
            _nextLabel = next.GetComponentInChildren<Text>();
            var nextRect = next.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(1f, 0f);
            nextRect.anchorMax = new Vector2(1f, 0f);
            nextRect.pivot = new Vector2(1f, 0f);
            nextRect.anchoredPosition = new Vector2(-8f, 28f);
            nextRect.sizeDelta = new Vector2(96f, 26f);
            var nextImage = next.GetComponent<Image>();
            if (nextImage != null)
                nextImage.color = new Color(0.55f, 0.32f, 0.62f, 1f);

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

            if (_nameInput != null)
            {
                _nameDraft = _nameInput.text ?? string.Empty;
                _nameDraftLive = true;
            }

            _building = true;
            _fieldGeneration++;
            for (var i = _body.childCount - 1; i >= 0; i--)
                Destroy(_body.GetChild(i).gameObject);
            _fields.Clear();
            _keyInput = null;
            _nameInput = null;
            _y = 4f;
            switch (_step)
            {
                case SetupWizardFlow.Ai:
                    BuildAi();
                    break;
                case SetupWizardFlow.Avatar:
                    BuildAvatar();
                    break;
                case SetupWizardFlow.Personality:
                    BuildPersonality();
                    break;
                case SetupWizardFlow.Tour:
                    BuildTour();
                    break;
                default:
                    BuildWelcome();
                    break;
            }

            _body.sizeDelta = new Vector2(0f, _y + 8f);
            if (_status != null)
                _status.text = _statusMessage ?? string.Empty;
            _building = false;
        }

        float _y;

        void BuildWelcome()
        {
            Note(SetupWizardCopy.WelcomeLead());
            Note(SetupWizardCopy.WelcomeCloud());
            Note(SetupWizardCopy.WelcomeLocal());
        }

        void BuildAi()
        {
            var settings = CurrentSettings();
            Note(SetupWizardCopy.AiNote());
            Note(ApiKeyMask.Label(SetupWizardGate.HasSavedApiKey()) + ".");
            if (SetupWizardGate.EnvironmentHasApiKey())
                Note("XAI_API_KEY is set for this process and overrides the saved key. The value is not shown here.");
            if (settings.PrivateMode)
                Note("Private mode is on. Test connection stays on the local model and does not send the key.");
            _keyInput = AddField("Paste xAI API key", string.Empty, true, null);
            AddButton(_probeBusy ? "Testing..." : "Test connection", () =>
            {
                if (!_probeBusy)
                    TestConnection();
            });
            Note(SetupWizardCopy.AiSkipNote());
        }

        void BuildAvatar()
        {
            Note(SetupWizardCopy.AvatarNote());
            AddSourceButton("Auto", AvatarSourceNames.Auto);
            AddSourceButton("Local prefab", AvatarSourceNames.Local);
            AddSourceButton("VRM sample", AvatarSourceNames.Vrm);
            AddSourceButton("FBX sample", AvatarSourceNames.Fbx);
        }

        void BuildPersonality()
        {
            var settings = CurrentSettings();
            Note(SetupWizardCopy.PersonalityNote());
            var shown = _nameDraftLive ? _nameDraft : settings.CompanionName;
            if (string.IsNullOrEmpty(shown))
                shown = CompanionPersona.DefaultName;
            _nameInput = AddField("Companion name", shown, false, value =>
            {
                _nameDraft = value ?? string.Empty;
                _nameDraftLive = true;
            });
            AddStyleButton(RelationshipStyles.Friend);
            AddStyleButton(RelationshipStyles.Girlfriend);
        }

        void BuildTour()
        {
            var notes = SetupWizardCopy.TourNotes();
            for (var i = 0; i < notes.Length; i++)
                Note(notes[i]);
            var entries = SetupWizardFlow.TourHotkeys(CurrentSettings().PushToTalkKey);
            for (var i = 0; i < entries.Length; i++)
                AddLabel(entries[i].Keys + "   " + entries[i].Action);
        }

        void AddSourceButton(string label, string source)
        {
            var selected = CurrentSettings().AvatarSource == source;
            AddButton(selected ? label + " (selected)" : label, () =>
            {
                SetupWizardFlow.ApplyAvatar(CurrentSettings(), source);
                var panel = GetComponent<SettingsPanel>();
                if (panel != null)
                    panel.PreviewSetup();
                _rebuild = true;
            });
        }

        void AddStyleButton(string style)
        {
            var selected = CurrentSettings().Relationship == RelationshipStyles.Normalize(style);
            var label = RelationshipStyles.Label(style);
            AddButton(selected ? label + " (selected)" : label, () =>
            {
                CurrentSettings().Relationship = RelationshipStyles.Normalize(style);
                _rebuild = true;
            });
        }

        void RefreshFooter()
        {
            if (_backButton != null)
                _backButton.interactable = SetupWizardFlow.CanGoBack(_step);
            if (_nextLabel != null)
                _nextLabel.text = SetupWizardFlow.IsLast(_step) ? "Done" : "Next";
            if (_stepLabel != null)
                _stepLabel.text = SetupWizardCopy.StepLabel(_step);
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
            _canvas.planeDistance = 0.3f;
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

        Text AddLabel(string text)
        {
            var label = CreateText(_body, _font, 14, TextAnchor.MiddleLeft, text);
            label.raycastTarget = false;
            PlaceRow(label.rectTransform, 22f);
            return label;
        }

        void Note(string text)
        {
            var label = CreateText(_body, _font, 12, TextAnchor.UpperLeft, text);
            label.fontStyle = FontStyle.Italic;
            label.color = new Color(0.80f, 0.76f, 0.86f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            PlaceRow(label.rectTransform, NoteHeight(text));
        }

        static float NoteHeight(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 20f;

            var lines = 1;
            var column = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lines++;
                    column = 0;
                    continue;
                }

                column++;
                if (column >= 46)
                {
                    lines++;
                    column = 0;
                }
            }

            return Mathf.Clamp(lines * 16f, 20f, 128f);
        }

        void AddButton(string label, Action action)
        {
            var button = CreateButton(_body, label, action);
            PlaceRow(button.GetComponent<RectTransform>(), 26f);
        }

        InputField AddField(string placeholder, string value, bool password, Action<string> onEnd)
        {
            var field = new GameObject(password ? "Secret" : "Field");
            field.transform.SetParent(_body, false);
            var rect = field.AddComponent<RectTransform>();
            PlaceRow(rect, 26f);
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
            if (!password)
            {
                input.onValueChanged.AddListener(textValue =>
                {
                    if (_building || generation != _fieldGeneration)
                        return;
                    _nameDraft = textValue ?? string.Empty;
                    _nameDraftLive = true;
                });
            }

            _fields.Add(input);
            return input;
        }

        void PlaceRow(RectTransform rect, float height)
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
}
