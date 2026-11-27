using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Avatar;
using AICompanion.Core;
using AICompanion.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    /// <summary>
    /// Optional drawn face beside the avatar. Images load from the per-user
    /// portraits folder. Clicks outside the frame stay with the desktop.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12200)]
    public sealed class ExpressionPortraitPanel : MonoBehaviour, ICompanionStartup, IDebugPanel
    {
        const float RefreshSeconds = 3f;

        readonly Dictionary<string, PortraitAsset> _sprites = new Dictionary<string, PortraitAsset>(StringComparer.Ordinal);

        readonly struct PortraitAsset
        {
            public readonly Sprite Sprite;
            public readonly PortraitCutout Cutout;

            public PortraitAsset(Sprite sprite, PortraitCutout cutout)
            {
                Sprite = sprite;
                Cutout = cutout;
            }
        }
        readonly PortraitDirector _director = new PortraitDirector();

        SettingsPanel _settings;
        ChatPanel _chat;
        AvatarMount _mount;
        VideoAvatar _video;
        Component _animatorObject;
        IAvatarAnimator _animator;
        Component _mouthObject;
        IMouthDriver _mouth;
        Component _speechObject;
        ISpeechOutput _speech;
        Canvas _canvas;
        GameObject _canvasObject;
        RectTransform _frame;
        Image _plate;
        RibbonPortraitBase _ribbonBase;
        Image _back;
        Image _mid;
        Image _front;
        Sprite _rounded;
        Texture2D _roundedTexture;
        Transform _head;
        PortraitLibrary _library = new PortraitLibrary();
        bool _intimateLogged;
        bool _intimateMode;
        bool _settingKnown;
        bool _wantedIntimate;
        bool _wantedPrivate;
        PortraitMap _map;
        Rect _guiRect;
        string _appData = string.Empty;
        string _persistent = string.Empty;
        string _folder;
        string _requested;
        string _backPath;
        string _midPath;
        string _frontPath;
        long _mapStamp = -1;
        float _refreshIn;
        float _headRetry;
        PortraitHideReason _loggedReason;
        bool _started;
        bool _paths;
        bool _placed;
        bool? _preferLeft;

        /// <summary>JUH-104: the new chat box (window pixels from the top-left) the portrait keeps clear of. Empty while the new UI is off.</summary>
        public static Rect NewUiAvoid;

        /// <summary>JUH-104: true puts the portrait on her left (the chat box took her right side). Null keeps the automatic side.</summary>
        public static bool? NewUiPreferLeft;

        /// <summary>JUH-104: where the new UI wants the portrait (window pixels from the top-left, any size). Empty = automatic.</summary>
        public static Rect NewUiTarget;

        /// <summary>Ribbon chat can temporarily take all of the available work area.</summary>
        public static bool NewUiSuppressForChat;

        /// <summary>Where the portrait is drawn now (window pixels from the top-left); empty while it is hidden.</summary>
        public static Rect ShownRect;

        /// <summary>JUH-132: largest source image in the current portrait set, in pixels. The frame may not grow past this.</summary>
        public static Vector2 NativeSize = new Vector2(PortraitPlacement.Width, PortraitPlacement.Height);
        string _heldExpression = string.Empty;
        float _quietFor;
        readonly PortraitExpressionWatch _portraitVoice = new PortraitExpressionWatch();
        PortraitPreview _preview;
        bool _shown;
        bool _logged;

        public bool RibbonCutouts { get; set; }

        public Rect InteractiveRect
        {
            get { return _shown ? _guiRect : new Rect(0f, 0f, 0f, 0f); }
        }

        public void StartCompanion()
        {
            if (_started)
                return;
            _started = true;
            BindHooks();
        }

        void Start()
        {
            StartCompanion();
        }

        void OnDestroy()
        {
            if (_mount != null)
                _mount.PreferenceChanged -= OnAvatarChanged;
            ClearSprites();
            if (_rounded != null)
                Destroy(_rounded);
            if (_roundedTexture != null)
                Destroy(_roundedTexture);
        }

        void LateUpdate()
        {
            if (!_started)
                return;

            BindHooks();
            var delta = Time.unscaledDeltaTime;
            if (!WantsPortrait() || NewUiSuppressForChat)
            {
                _logged = false;
                _loggedReason = PortraitHideReason.None;
                Hide();
                return;
            }

            Refresh(false, delta);
            var folderFound = !string.IsNullOrEmpty(_folder);
            var neutralFound = _library != null && _map != null && _library.ContainsNeutral(_map);
            var plan = PortraitShowPlan.Describe(true, folderFound, neutralFound);
            if (!plan.Show)
            {
                LogOnce(plan.Reason);
                Hide();
                return;
            }

            var live = AnimatorAlive() ? ExpressionName() : RestExpression();
            var amplitude = 0f;
            var hasAmplitude = MouthAlive();
            if (hasAmplitude)
                amplitude = _mouth.MouthOverride;
            var mouthSpeaking = AnimatorAlive() && _animator.IsTalking;
            if (hasAmplitude && amplitude >= PortraitTalkMotion.AmplitudeThreshold)
                mouthSpeaking = true;
            var uttered = mouthSpeaking || SpeechInFlight() || (_chat != null && _chat.TranscriptBusy);
            var hint = _animator as IExpressionHint;
            var hintName = hint != null && hint.ExpressionName != null ? hint.ExpressionName : string.Empty;
            var face = AnimatorAlive() ? _animator.Emotion : Emotion.Neutral;
            var previewKey = Input.GetKeyDown(KeyCode.F9) && !DebugInputGuard.IsBlocked(KeyCode.F9);
            _preview = _preview.Tick(previewKey, hintName, face, uttered);

            var emotion = live;
            var speaking = mouthSpeaking;
            if (_preview.Active)
            {
                emotion = _preview.Expression;
                if (string.Equals(emotion, "neutral", StringComparison.OrdinalIgnoreCase))
                {
                    _heldExpression = string.Empty;
                    _quietFor = 0f;
                }
                else
                    PortraitHold.ResolveUtterance(emotion, ref _heldExpression, false, 0f, ref _quietFor);

                speaking = true;
                hasAmplitude = false;
                amplitude = 0f;
            }
            else
            {
                emotion = PortraitHold.ResolveUtterance(live, ref _heldExpression, uttered, delta, ref _quietFor);
            }

            var view = _director.Tick(
                delta,
                emotion,
                speaking,
                hasAmplitude,
                amplitude,
                _map,
                _library,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value);
            if (!view.Visible)
            {
                Hide();
                return;
            }

            EnsureUi();
            if (!Apply(view))
            {
                if (!_logged || _loggedReason != PortraitHideReason.NeutralMissing)
                {
                    _logged = true;
                    _loggedReason = PortraitHideReason.NeutralMissing;
                    Debug.LogWarning("Expression portraits: could not read an image in " + _folder + ". The panel stays hidden.");
                }

                Hide();
                return;
            }

            _logged = false;
            _loggedReason = PortraitHideReason.None;
            _shown = true;
            string reaction;
            if (_portraitVoice.TryChange(emotion, out reaction))
                ReactionVoiceRequests.Offer(reaction, ReactionVoiceCause.Portrait);
            if (_canvasObject != null && !_canvasObject.activeSelf)
                _canvasObject.SetActive(true);
            Place(delta);
        }

        void BindHooks()
        {
            if (_settings == null)
                _settings = GetComponent<SettingsPanel>();
            if (_chat == null)
                _chat = GetComponent<ChatPanel>();
            if (_video == null)
                _video = GetComponent<VideoAvatar>();
            if (_mount == null)
            {
                _mount = GetComponent<AvatarMount>();
                if (_mount != null)
                    _mount.PreferenceChanged += OnAvatarChanged;
            }

            if (!AnimatorAlive())
            {
                var animator = GetComponent<IAvatarAnimator>();
                _animator = animator;
                _animatorObject = animator as Component;
            }

            if (!MouthAlive())
            {
                var mouth = GetComponent<IMouthDriver>();
                _mouth = mouth;
                _mouthObject = mouth as Component;
            }

            if (!SpeechAlive())
            {
                var speech = GetComponent<ISpeechOutput>();
                _speech = speech;
                _speechObject = speech as Component;
            }
        }

        bool SpeechInFlight()
        {
            return SpeechAlive() && _speech.IsSpeaking;
        }

        bool WantsPortrait()
        {
            if (_settings == null || _settings.CurrentSettings == null)
                return false;

            // Shown in both avatar modes. Where it goes is decided in HeadPoint.
            return _settings.CurrentSettings.ShowExpressionPortrait;
        }

        string RequestedPortraitFolder()
        {
            if (_settings == null || _settings.CurrentSettings == null)
                return string.Empty;
            return (_settings.EffectiveSettings ?? _settings.CurrentSettings).PortraitFolder ?? string.Empty;
        }

        bool AnimatorAlive()
        {
            return _animator != null && _animatorObject != null;
        }

        bool MouthAlive()
        {
            return _mouth != null && _mouthObject != null;
        }

        bool SpeechAlive()
        {
            return _speech != null && _speechObject != null;
        }

        void OnAvatarChanged(AvatarPreference preference)
        {
            _head = null;
            _headRetry = 0f;
        }

        void Refresh(bool force, float delta)
        {
            EnsurePaths();
            var requested = RequestedPortraitFolder();
            var requestChanged = !string.Equals(requested, _requested, StringComparison.Ordinal);
            var settings = _settings != null ? _settings.CurrentSettings : null;
            var wanted = settings != null && settings.IntimateExpressions;
            var privateMode = settings != null && settings.PrivateMode;
            var settingsChanged = !_settingKnown || wanted != _wantedIntimate || privateMode != _wantedPrivate;
            _settingKnown = true;
            _wantedIntimate = wanted;
            _wantedPrivate = privateMode;
            _refreshIn -= delta;
            if (!force && !requestChanged && !settingsChanged && _refreshIn > 0f && _map != null)
                return;

            _refreshIn = RefreshSeconds;
            _requested = requested;

            // The adult set is a separate folder and a separate consent, and it stays local:
            // Private mode must be on as well. Without the folder the setting silently has no
            // effect, and the warning is written once.
            var adultFolder = PortraitFolders.Adult(_appData, _persistent);
            var adultPresent = !string.IsNullOrEmpty(adultFolder) && Directory.Exists(adultFolder);
            if (wanted && !adultPresent && !_intimateLogged)
            {
                _intimateLogged = true;
                Debug.LogWarning("Expression portraits: intimate expressions are on but " + adultFolder + " is missing. They stay off.");
            }

            IntimateExpressions.Configure(wanted, privateMode, adultPresent);
            if (_intimateMode != IntimateExpressions.Active)
            {
                _intimateMode = IntimateExpressions.Active;
                _mapStamp = -1;
                _map = null;
            }

            var folder = PortraitFolders.Select(requested, _appData, _persistent, Directory.Exists);
            if (!string.Equals(folder, _folder, StringComparison.OrdinalIgnoreCase))
            {
                _folder = folder;
                _mapStamp = -1;
                _backPath = null;
                _midPath = null;
                _frontPath = null;
                ClearSprites();
            }

            _library = string.IsNullOrEmpty(_folder) ? new PortraitLibrary() : PortraitLibrary.Load(_folder);
            if (IntimateExpressions.Active && adultPresent)
                _library.Merge(PortraitLibrary.Load(adultFolder, true));
            _map = ReadMap(_folder);
            DropFailedSprites();
            if (RibbonCutouts && !_intimateMode)
            {
                var neutral = _library.Existing(_map.Resolve("neutral").Open);
                var sprite = neutral != null ? AssetFor(neutral).Sprite : null;
                NativeSize = sprite != null ? sprite.rect.size : new Vector2(PortraitPlacement.Width, PortraitPlacement.Height);
            }
        }

        void DropFailedSprites()
        {
            List<string> drop = null;
            foreach (var pair in _sprites)
            {
                if (pair.Value.Sprite != null)
                    continue;
                if (drop == null)
                    drop = new List<string>();
                drop.Add(pair.Key);
            }

            if (drop == null)
                return;

            for (var i = 0; i < drop.Count; i++)
                _sprites.Remove(drop[i]);
            _backPath = null;
            _midPath = null;
            _frontPath = null;
        }

        PortraitMap ReadMap(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                if (_map != null && _mapStamp == -2)
                    return _map;
                _mapStamp = -2;
                return PortraitMap.CreateDefaults(IntimateExpressions.Active);
            }

            var path = Path.Combine(folder, PortraitFolders.MapFileName);
            if (!File.Exists(path))
            {
                if (_map != null && _mapStamp == 0)
                    return _map;
                _mapStamp = 0;
                return PortraitMap.CreateDefaults(IntimateExpressions.Active);
            }

            long stamp;
            try
            {
                stamp = File.GetLastWriteTimeUtc(path).Ticks;
            }
            catch (Exception)
            {
                stamp = 0;
            }

            if (_map != null && stamp == _mapStamp)
                return _map;

            _mapStamp = stamp;
            try
            {
                return PortraitMapJson.Parse(File.ReadAllText(path), IntimateExpressions.Active);
            }
            catch (Exception)
            {
                return PortraitMap.CreateDefaults(IntimateExpressions.Active);
            }
        }

        void EnsurePaths()
        {
            if (_paths)
                return;
            _paths = true;
            _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? string.Empty;
            _persistent = Application.persistentDataPath ?? string.Empty;
        }

        void LogOnce(PortraitHideReason reason)
        {
            if (reason == PortraitHideReason.None || reason == PortraitHideReason.Disabled)
                return;
            if (_logged && _loggedReason == reason)
                return;

            _logged = true;
            _loggedReason = reason;
            EnsurePaths();
            if (reason == PortraitHideReason.FolderMissing)
            {
                var expected = PortraitFolders.Primary(_appData, _persistent);
                Debug.LogWarning("Expression portraits: folder missing (" + expected + "). The panel stays hidden.");
                return;
            }

            Debug.LogWarning("Expression portraits: neutral image missing in " + (_folder ?? "the portraits folder") + ". The panel stays hidden.");
        }

        void Hide()
        {
            _shown = false;
            _placed = false;
            _guiRect = new Rect(0f, 0f, 0f, 0f);
            ShownRect = new Rect(0f, 0f, 0f, 0f);
            if (_canvasObject != null && _canvasObject.activeSelf)
                _canvasObject.SetActive(false);
        }

        bool Apply(PortraitView view)
        {
            var any = false;
            any |= ApplySlot(_back, ref _backPath, view.Back, view.BackAlpha);
            any |= ApplySlot(_mid, ref _midPath, view.Mid, view.MidAlpha);
            any |= ApplySlot(_front, ref _frontPath, view.Front, view.FrontAlpha);
            if (any)
            {
                if (!RibbonCutouts || _intimateMode)
                    UpdateNativeSize();
                ApplyPortraitStyle();
            }
            return any;
        }

        void ApplyPortraitStyle()
        {
            var line = Vector3.zero;
            var cutouts = 0f;
            var total = 0f;
            if (RibbonCutouts && !_intimateMode)
            {
                IncludeCutout(_back, _backPath, ref line, ref cutouts, ref total);
                IncludeCutout(_mid, _midPath, ref line, ref cutouts, ref total);
                IncludeCutout(_front, _frontPath, ref line, ref cutouts, ref total);
            }

            var fade = total > 0f ? cutouts / total : 0f;
            var color = _plate.color;
            color.a = .92f * (1f - fade);
            if (_plate.color != color)
                _plate.color = color;
            _ribbonBase.SetLine(cutouts > 0f ? line / cutouts : Vector3.zero, fade);
        }

        void IncludeCutout(Image image, string path, ref Vector3 line, ref float cutouts, ref float total)
        {
            if (!image.enabled)
                return;

            var alpha = image.color.a;
            total += alpha;
            if (!_sprites.TryGetValue(path, out var asset) || !asset.Cutout.Valid)
                return;

            var rect = image.rectTransform;
            var corner = rect.anchoredPosition - rect.sizeDelta * .5f;
            var crop = asset.Cutout;
            var left = corner.x + crop.Left * rect.sizeDelta.x;
            var right = corner.x + crop.Right * rect.sizeDelta.x;
            var bottom = corner.y + crop.Bottom * rect.sizeDelta.y;
            line += new Vector3(left, right, bottom) * alpha;
            cutouts += alpha;
        }

        /// <summary>The unzoomed image dimensions, so the resizable frame never upscales a drawing.</summary>
        void UpdateNativeSize()
        {
            var max = Vector2.zero;
            foreach (var pair in _sprites)
            {
                var sprite = pair.Value.Sprite;
                if (sprite == null)
                    continue;
                var size = sprite.rect.size;
                if (size.x > max.x)
                    max.x = size.x;
                if (size.y > max.y)
                    max.y = size.y;
            }
            if (max.x > 0f && max.y > 0f)
                NativeSize = max;
        }

        bool ApplySlot(Image image, ref string current, string path, float alpha)
        {
            if (image == null)
                return false;
            if (string.IsNullOrEmpty(path) || alpha <= 0.001f)
            {
                image.enabled = false;
                return false;
            }

            var asset = AssetFor(path);
            var sprite = asset.Sprite;
            if (sprite == null)
            {
                image.enabled = false;
                return false;
            }

            if (!string.Equals(current, path, StringComparison.Ordinal))
            {
                image.sprite = sprite;
                Fit(image.rectTransform, sprite);
                current = path;
            }

            var color = image.color;
            color.r = 1f;
            color.g = 1f;
            color.b = 1f;
            color.a = alpha;
            image.color = color;
            var masked = !RibbonCutouts || _intimateMode || !asset.Cutout.Valid;
            if (image.maskable != masked)
                image.maskable = masked;
            image.enabled = true;
            return true;
        }

        PortraitAsset AssetFor(string path)
        {
            if (!_sprites.TryGetValue(path, out var asset))
            {
                asset = LoadAsset(path);
                _sprites[path] = asset;
            }
            return asset;
        }

        static PortraitAsset LoadAsset(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    Destroy(texture);
                    return default;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return new PortraitAsset(sprite, PortraitCutout.Measure(texture));
            }
            catch (Exception)
            {
                return default;
            }
        }

        void Fit(RectTransform rect, Sprite sprite)
        {
            if (rect == null || sprite == null)
                return;

            var fitted = PortraitFit.Inside(PortraitPlacement.InnerWidth, PortraitPlacement.InnerHeight, sprite.rect.width, sprite.rect.height);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(fitted.width, fitted.height);
            rect.anchoredPosition = new Vector2(
                fitted.x + fitted.width * 0.5f - PortraitPlacement.InnerWidth * 0.5f,
                PortraitPlacement.InnerHeight * 0.5f - (fitted.y + fitted.height * 0.5f));
        }

        /// <summary>
        /// The director can name an expression the Emotion enum cannot, such as embarrassed
        /// or teasing. Fall back to the emotion name when it does not.
        /// </summary>
        string ExpressionName()
        {
            var hint = _animator as IExpressionHint;
            if (hint != null && !string.IsNullOrEmpty(hint.ExpressionName))
                return hint.ExpressionName;

            // No reply expression: her mood's rest look (JUH-103 phase 2).
            if (_animator.Emotion == Emotion.Neutral)
                return RestExpression();
            return PortraitNames.For(_animator.Emotion);
        }

        static string RestExpression()
        {
            var rest = CompanionMoodNow.Rest;
            return rest.HasExpression ? rest.Expression : "neutral";
        }


        void Place(float delta)
        {
            var avoidChat = _chat != null ? _chat.InteractiveRect : new Rect(0f, 0f, 0f, 0f);
            // JUH-104 new UI: avoid the new chat box, and keep to the side it asks for.
            if (NewUiAvoid.width > 0f)
                avoidChat = NewUiAvoid;
            var head = HeadPoint(delta);
            var target = PortraitPlacement.Beside(head, Screen.width, Screen.height, avoidChat, PortraitPlacement.DebugLabel, NewUiPreferLeft ?? _preferLeft);
            if (target.width > 0f)
                _preferLeft = target.x + target.width * 0.5f < head.x;
            // JUH-104: a place chosen in the new UI (dragged, or docked to the chat box) is used as is.
            if (NewUiTarget.width > 0f)
                _guiRect = NewUiTarget;
            else
                _guiRect = PortraitPlacement.Smooth(_guiRect, target, delta, _placed, PortraitPlacement.DeadZone);
            ShownRect = _guiRect;
            _placed = true;
            if (_frame == null)
                return;

            var origin = Vector2.zero;
            if (_canvas != null)
            {
                var pixel = _canvas.pixelRect;
                if (pixel.width > 1f && pixel.height > 1f)
                {
                    var gui = ChatPanelPlacement.FromBottomLeft(pixel, Screen.height);
                    origin = new Vector2(gui.x, gui.y);
                }
            }

            _frame.anchoredPosition = new Vector2(_guiRect.x - origin.x, -(_guiRect.y - origin.y));
            // A size chosen in the new UI (docked to the chat box) scales the whole framed picture
            // from its top-left corner; otherwise it is drawn at its normal size as on main.
            var zoom = NewUiTarget.width > 0f && _guiRect.height > 0f ? _guiRect.height / PortraitPlacement.Height : 1f;
            _frame.sizeDelta = new Vector2(_guiRect.width / zoom, _guiRect.height / zoom);
            if (Mathf.Abs(_frame.localScale.x - zoom) > 0.0001f)
                _frame.localScale = new Vector3(zoom, zoom, 1f);
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
            _canvas.planeDistance = 0.55f;
        }

        Vector2 HeadPoint(float delta)
        {
            var fallback = new Vector2(Screen.width * 0.62f, Screen.height * 0.34f);

            // In 2D mode the hidden 3D model still animates, and its head bone wanders around
            // the window. Beside the video avatar's head the portrait keeps one place.
            var videoHead = fallback;
            var videoKnown = _video != null && _video.TryGetPortraitHead(out videoHead);
            switch (PortraitAnchor.Source(_mount != null && _mount.ModelHidden, videoKnown))
            {
                case PortraitAnchorSource.Video:
                    return videoHead;
                case PortraitAnchorSource.Fixed:
                    return fallback;
            }

            if (_head == null)
            {
                _headRetry -= delta;
                if (_headRetry > 0f)
                    return fallback;
                _headRetry = 0.5f;
                _head = FindHead();
            }

            var camera = Camera.main;
            if (camera == null || _head == null)
                return fallback;

            var screen = camera.WorldToScreenPoint(_head.position);
            if (screen.z <= 0.01f)
                return fallback;
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        Transform FindHead()
        {
            var animators = GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null || !animator.isHuman)
                    continue;
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                    return head;
            }

            return null;
        }

        void EnsureUi()
        {
            if (_canvasObject != null)
                return;

            _rounded = RoundedSprite(out _roundedTexture);
            _canvasObject = new GameObject("ExpressionPortraitCanvas");
            _canvasObject.transform.SetParent(transform, false);
            _canvasObject.hideFlags = HideFlags.DontSave;
            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.sortingOrder = 40;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _canvasObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var system = new GameObject("EventSystem");
                system.hideFlags = HideFlags.DontSave;
                system.AddComponent<EventSystem>();
                system.AddComponent<StandaloneInputModule>();
            }

            var plate = new GameObject("Frame");
            plate.transform.SetParent(_canvasObject.transform, false);
            _frame = plate.AddComponent<RectTransform>();
            _frame.anchorMin = new Vector2(0f, 1f);
            _frame.anchorMax = new Vector2(0f, 1f);
            _frame.pivot = new Vector2(0f, 1f);
            _plate = plate.AddComponent<Image>();
            _plate.sprite = _rounded;
            _plate.type = Image.Type.Sliced;
            _plate.color = new Color(0.08f, 0.07f, 0.12f, 0.92f);
            _plate.raycastTarget = true;

            var maskObject = new GameObject("Mask");
            maskObject.transform.SetParent(plate.transform, false);
            var maskRect = maskObject.AddComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = new Vector2(PortraitPlacement.Border, PortraitPlacement.Border);
            maskRect.offsetMax = new Vector2(-PortraitPlacement.Border, -PortraitPlacement.Border);
            var maskImage = maskObject.AddComponent<Image>();
            maskImage.sprite = _rounded;
            maskImage.type = Image.Type.Sliced;
            maskImage.raycastTarget = false;
            var mask = maskObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _back = Photo(maskObject.transform, "Back");
            _mid = Photo(maskObject.transform, "Mid");
            _front = Photo(maskObject.transform, "Front");

            var accent = new GameObject("Ribbon portrait base", typeof(RectTransform));
            accent.transform.SetParent(plate.transform, false);
            _ribbonBase = accent.AddComponent<RibbonPortraitBase>();
            var accentRect = _ribbonBase.rectTransform;
            accentRect.anchorMin = Vector2.zero;
            accentRect.anchorMax = Vector2.one;
            accentRect.offsetMin = new Vector2(PortraitPlacement.Border, PortraitPlacement.Border);
            accentRect.offsetMax = -accentRect.offsetMin;
            _ribbonBase.raycastTarget = false;
            _ribbonBase.maskable = false;
        }

        static Image Photo(Transform parent, string name)
        {
            var photo = new GameObject(name);
            photo.transform.SetParent(parent, false);
            photo.AddComponent<RectTransform>();
            var image = photo.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.enabled = false;
            return image;
        }

        static Sprite RoundedSprite(out Texture2D texture)
        {
            const int size = 48;
            const int radius = 12;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, CornerAlpha(x, y, size, radius));
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static float CornerAlpha(int x, int y, int size, int radius)
        {
            var px = x + 0.5f;
            var py = y + 0.5f;
            var dx = 0f;
            var dy = 0f;
            if (px < radius)
                dx = radius - px;
            else if (px > size - radius)
                dx = px - (size - radius);
            if (py < radius)
                dy = radius - py;
            else if (py > size - radius)
                dy = py - (size - radius);
            var dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist <= radius - 1f)
                return 1f;
            if (dist >= radius)
                return 0f;
            return radius - dist;
        }

        void ClearSprites()
        {
            foreach (var pair in _sprites)
            {
                var sprite = pair.Value.Sprite;
                if (sprite == null)
                    continue;
                var texture = sprite.texture;
                Destroy(sprite);
                if (texture != null)
                    Destroy(texture);
            }

            _sprites.Clear();
        }
    }

    static class ExpressionPortraitInstaller
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
                if (host.GetComponent<ExpressionPortraitPanel>() == null)
                    host.AddComponent<ExpressionPortraitPanel>();
            }
        }
    }
}
