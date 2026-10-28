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

        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        readonly PortraitDirector _director = new PortraitDirector();

        SettingsPanel _settings;
        ChatPanel _chat;
        AvatarMount _mount;
        Component _animatorObject;
        IAvatarAnimator _animator;
        Component _mouthObject;
        IMouthDriver _mouth;
        Canvas _canvas;
        GameObject _canvasObject;
        RectTransform _frame;
        Image _back;
        Image _mid;
        Image _front;
        Sprite _rounded;
        Texture2D _roundedTexture;
        Transform _head;
        PortraitLibrary _library = new PortraitLibrary();
        PortraitMap _map;
        Rect _guiRect;
        string _appData = string.Empty;
        string _persistent = string.Empty;
        string _folder;
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
        string _heldExpression = string.Empty;
        bool _shown;
        bool _logged;

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
            if (!WantsPortrait())
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

            var emotion = AnimatorAlive() ? ExpressionName() : "neutral";
            var amplitude = 0f;
            var hasAmplitude = MouthAlive();
            if (hasAmplitude)
                amplitude = _mouth.MouthOverride;
            var speaking = AnimatorAlive() && _animator.IsTalking;
            if (hasAmplitude && amplitude >= PortraitTalkMotion.AmplitudeThreshold)
                speaking = true;

            emotion = PortraitHold.Resolve(emotion, ref _heldExpression, speaking);

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
        }

        bool WantsPortrait()
        {
            return _settings != null && _settings.CurrentSettings != null && _settings.CurrentSettings.ShowExpressionPortrait;
        }

        bool AnimatorAlive()
        {
            return _animator != null && _animatorObject != null;
        }

        bool MouthAlive()
        {
            return _mouth != null && _mouthObject != null;
        }

        void OnAvatarChanged(AvatarPreference preference)
        {
            _head = null;
            _headRetry = 0f;
        }

        void Refresh(bool force, float delta)
        {
            _refreshIn -= delta;
            if (!force && _refreshIn > 0f && _map != null)
                return;

            _refreshIn = RefreshSeconds;
            EnsurePaths();
            var folder = PortraitFolders.Resolve(_appData, _persistent, Directory.Exists);
            if (!string.Equals(folder, _folder, StringComparison.Ordinal))
            {
                _folder = folder;
                _mapStamp = -1;
                _backPath = null;
                _midPath = null;
                _frontPath = null;
                ClearSprites();
            }

            _library = string.IsNullOrEmpty(_folder) ? new PortraitLibrary() : PortraitLibrary.Load(_folder);
            _map = ReadMap(_folder);
            DropFailedSprites();
        }

        void DropFailedSprites()
        {
            List<string> drop = null;
            foreach (var pair in _sprites)
            {
                if (pair.Value != null)
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
                return PortraitMap.CreateDefaults();
            }

            var path = Path.Combine(folder, PortraitFolders.MapFileName);
            if (!File.Exists(path))
            {
                if (_map != null && _mapStamp == 0)
                    return _map;
                _mapStamp = 0;
                return PortraitMap.CreateDefaults();
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
                return PortraitMapJson.Parse(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return PortraitMap.CreateDefaults();
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
            if (_canvasObject != null && _canvasObject.activeSelf)
                _canvasObject.SetActive(false);
        }

        bool Apply(PortraitView view)
        {
            var any = false;
            any |= ApplySlot(_back, ref _backPath, view.Back, view.BackAlpha);
            any |= ApplySlot(_mid, ref _midPath, view.Mid, view.MidAlpha);
            any |= ApplySlot(_front, ref _frontPath, view.Front, view.FrontAlpha);
            return any;
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

            var sprite = SpriteFor(path);
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
            image.enabled = true;
            return true;
        }

        Sprite SpriteFor(string path)
        {
            Sprite sprite;
            if (_sprites.TryGetValue(path, out sprite))
                return sprite;

            sprite = LoadSprite(path);
            _sprites[path] = sprite;
            return sprite;
        }

        static Sprite LoadSprite(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    Destroy(texture);
                    return null;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }
            catch (Exception)
            {
                return null;
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

            return PortraitNames.For(_animator.Emotion);
        }


        void Place(float delta)
        {
            var avoidChat = _chat != null ? _chat.InteractiveRect : new Rect(0f, 0f, 0f, 0f);
            var head = HeadPoint(delta);
            var target = PortraitPlacement.Beside(head, Screen.width, Screen.height, avoidChat, PortraitPlacement.DebugLabel, _preferLeft);
            if (target.width > 0f)
                _preferLeft = target.x + target.width * 0.5f < head.x;
            _guiRect = PortraitPlacement.Smooth(_guiRect, target, delta, _placed, PortraitPlacement.DeadZone);
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
            _frame.sizeDelta = new Vector2(_guiRect.width, _guiRect.height);
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
            var plateImage = plate.AddComponent<Image>();
            plateImage.sprite = _rounded;
            plateImage.type = Image.Type.Sliced;
            plateImage.color = new Color(0.08f, 0.07f, 0.12f, 0.92f);
            plateImage.raycastTarget = true;

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
                if (pair.Value == null)
                    continue;
                var texture = pair.Value.texture;
                Destroy(pair.Value);
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
