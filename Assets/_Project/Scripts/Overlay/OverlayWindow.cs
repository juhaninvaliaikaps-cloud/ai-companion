using System.Collections;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Borderless transparent window, topmost toggle, drag, and click-through.
    /// A point is on the avatar only when the framebuffer pixel under the cursor is
    /// opaque. Win32 runs only in the Windows player. In the editor this still clears
    /// the camera, limits the frame rate, and updates the debug label.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OverlayWindow : MonoBehaviour, ICompanionStartup
    {
        const string TopmostOnLabel = "Topmost on   F8";
        const string TopmostOffLabel = "Topmost off  F8";
        static readonly Rect PanelRect = new Rect(8f, 8f, 248f, 88f);
        static readonly Color AvatarTint = new Color(0.95f, 0.45f, 0.62f, 1f);

        [SerializeField] Renderer placeholderAvatar;
        [SerializeField] int windowWidth = 480;
        [SerializeField] int windowHeight = 720;
        [SerializeField] bool startTopmost = true;
        [SerializeField] KeyCode topmostKey = KeyCode.F8;
        [SerializeField] int activeFrameRate = OverlayFrameRate.Active;
        [SerializeField] int idleFrameRate = OverlayFrameRate.Idle;
        [SerializeField] float idleAfterSeconds = OverlayFrameRate.IdleAfterSeconds;
        [SerializeField] float opaqueAlphaThreshold = ClickThroughDecision.DefaultOpaqueAlpha;

        IWindowChrome _chrome;
        Camera _camera;
        bool _started;
        bool _topmost;
        bool _resolutionApplied;
        bool _chromeApplied;
        bool _placed;
        bool _dragging;
        bool _userMoved;
        bool _leftWasHeld;
        bool _clickThrough;
        int _warmup;
        float _idleFor;
        int _appliedRate = -1;
        WindowDrag _drag;
        bool _samplePending;
        bool _sampleRoutineRunning;
        bool _onSilhouette;
        bool _hasSample;
        Vector2Int _pendingSample = new Vector2Int(-1, -1);
        Vector2Int _sampledPixel = new Vector2Int(int.MinValue, int.MinValue);
        Texture2D _pixel;
        WaitForEndOfFrame _endOfFrame;
        MaterialPropertyBlock _tintBlock;
        PointerEventData _pointer;
        EventSystem _pointerSystem;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>(4);
        int _fpsFrames;
        float _fpsTimer;
        string _fpsText = "--";
        IAvatarAnimator _animator;
        string _emotionText = "";
        Emotion _shownEmotion;
        float _shownIntensity = -1f;
        bool _shownTalking;
        bool _emotionReady;

        public bool IsTopmost => _topmost;
        public bool IsClickThrough => _clickThrough;

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            _chrome = WindowChrome.Create();
            _topmost = startTopmost;
            _endOfFrame = new WaitForEndOfFrame();
            _camera = Camera.main;
            ApplyTransparentCamera(_camera);
            TintAvatar();
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            ApplyFrameRate(activeFrameRate);
            ApplyPlayerResolution();
            EnsureChrome();
        }

        void Start()
        {
            StartCompanion();
        }

        void Update()
        {
            if (!_started)
                return;

            EnsureChrome();
            bool inside = TryReadPointer(out var unityScreen, out var screenCursor);
            bool overPanel = inside && OverlayPointerMath.IsInsideTopLeftRect(PanelRect, unityScreen, Screen.height);
            bool overUi = overPanel || (inside && IsOverUi(unityScreen));
            var pixel = new Vector2Int(Mathf.RoundToInt(unityScreen.x), Mathf.RoundToInt(unityScreen.y));
            bool outsideSlack = !_hasSample || !SilhouetteHit.WithinSlack(_sampledPixel, pixel, SilhouetteHit.SampleSlackPixels);
            bool onSilhouette = inside && _onSilhouette;
            bool leftHeld = LeftHeld();
            bool leftDown = leftHeld && !_leftWasHeld;
            _leftWasHeld = leftHeld;

            if (!_dragging && leftDown && (onSilhouette || overPanel) && _chrome.TryGetWindowPosition(out var origin) && _chrome.TryGetScreenCursor(out var cursor))
            {
                _dragging = true;
                _userMoved = true;
                _drag = new WindowDrag(origin, cursor);
            }

            if (_dragging)
            {
                if (leftHeld && _chrome.TryGetScreenCursor(out screenCursor))
                    _chrome.MoveWindow(_drag.PositionFor(screenCursor), _topmost);
                else if (!leftHeld)
                {
                    _dragging = false;
                }
            }

            bool interactive = !SilhouetteHit.ShouldClickThrough(overUi, onSilhouette, _dragging);
            _clickThrough = !interactive;
            _chrome.SetClickThrough(_clickThrough);
            QueueAlphaSample(inside, outsideSlack, pixel);

            if (Input.GetKeyDown(topmostKey))
                SetTopmost(!_topmost);

            bool awake = inside || leftHeld || _dragging || Input.anyKey;
            if (awake)
                _idleFor = 0f;
            else
                _idleFor += Time.unscaledDeltaTime;

            ApplyFrameRate(OverlayFrameRate.Choose(_idleFor, idleAfterSeconds, activeFrameRate, idleFrameRate));
            AccumulateFps();

            if (_warmup > 0 && _chrome.IsSupported)
            {
                _chrome.TryApplyTransparentPopup();
                _chrome.SetTopmost(_topmost);
                _chrome.SetClickThrough(_clickThrough);
                _warmup--;
                if (_warmup == 0 && !_userMoved)
                    TryPlace(force: true);
            }
        }

        void OnGUI()
        {
            if (!_started)
                return;

            RefreshEmotionLabel();
            GUI.Box(PanelRect, GUIContent.none);
            GUI.Label(new Rect(12f, 10f, 232f, 18f), _topmost ? TopmostOnLabel : TopmostOffLabel);
            GUI.Label(new Rect(12f, 28f, 232f, 18f), _fpsText);
            GUI.Label(new Rect(12f, 46f, 232f, 18f), _emotionText);
            GUI.Label(new Rect(12f, 64f, 232f, 18f), AvatarAnimatorModel.DebugHint);
        }

        void OnDestroy()
        {
            if (_pixel != null)
                Destroy(_pixel);
        }

        public static void ApplyTransparentCamera(Camera camera)
        {
            if (camera == null)
                return;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
        }

        public void SetTopmost(bool topmost)
        {
            _topmost = topmost;
            if (_chrome != null && _chrome.IsSupported)
                _chrome.SetTopmost(topmost);
        }

        void ApplyPlayerResolution()
        {
            if (_resolutionApplied)
                return;

            _resolutionApplied = true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            _warmup = 30;
#endif
        }

        void EnsureChrome()
        {
            if (_chrome == null || !_chrome.IsSupported || _chromeApplied)
                return;

            if (!_chrome.TryApplyTransparentPopup())
                return;

            _chromeApplied = true;
            _warmup = 30;
            _chrome.SetTopmost(_topmost);
            TryPlace(force: false);
        }

        void TryPlace(bool force)
        {
            if ((_placed && !force) || !_chrome.TryGetMonitorSize(out var monitor))
                return;

            int x = monitor.x - windowWidth - 48;
            int y = monitor.y - windowHeight - 96;
            if (x < 0)
                x = 0;
            if (y < 0)
                y = 0;

            _chrome.MoveWindow(new Vector2Int(x, y), _topmost);
            _placed = true;
        }

        bool TryReadPointer(out Vector2 unityScreen, out Vector2Int screenCursor)
        {
            if (_chrome != null && _chrome.IsSupported && _chrome.TryGetClientCursor(out var client, out var clientSize))
            {
                bool inside = OverlayPointerMath.IsInsideClient(client.x, client.y, clientSize.x, clientSize.y);
                unityScreen = OverlayPointerMath.ToUnityScreen(client.x, client.y, clientSize.x, clientSize.y, Screen.width, Screen.height);
                _chrome.TryGetScreenCursor(out screenCursor);
                return inside;
            }

            var mouse = Input.mousePosition;
            unityScreen = new Vector2(mouse.x, mouse.y);
            screenCursor = new Vector2Int(Mathf.RoundToInt(mouse.x), Mathf.RoundToInt(Screen.height - mouse.y));
            return mouse.x >= 0f && mouse.y >= 0f && mouse.x < Screen.width && mouse.y < Screen.height;
        }

        bool IsOverUi(Vector2 unityScreen)
        {
            var system = EventSystem.current;
            if (system == null)
                return false;

            if (_pointer == null || _pointerSystem != system)
            {
                _pointerSystem = system;
                _pointer = new PointerEventData(system);
            }

            _pointer.position = unityScreen;
            _uiHits.Clear();
            system.RaycastAll(_pointer, _uiHits);
            return _uiHits.Count > 0;
        }

        bool LeftHeld()
        {
            if (_chrome != null && _chrome.IsSupported)
                return _chrome.IsLeftButtonHeld();

            return Input.GetMouseButton(0);
        }

        void QueueAlphaSample(bool inside, bool outsideSlack, Vector2Int pixel)
        {
            if (!SilhouetteHit.ShouldSample(inside, outsideSlack, _sampleRoutineRunning))
                return;

            _pendingSample = pixel;
            _samplePending = true;
            StartCoroutine(SampleAlphaAtEndOfFrame());
        }

        IEnumerator SampleAlphaAtEndOfFrame()
        {
            _sampleRoutineRunning = true;
            yield return _endOfFrame;
            _sampleRoutineRunning = false;
            if (!_samplePending)
                yield break;

            _samplePending = false;
            var pixel = _pendingSample;
            float alpha = 0f;
            if (pixel.x >= 0 && pixel.y >= 0 && pixel.x < Screen.width && pixel.y < Screen.height)
                alpha = ReadFramebufferAlpha(pixel);

            _sampledPixel = pixel;
            _hasSample = true;
            _onSilhouette = SilhouetteHit.NextOpaque(_onSilhouette, alpha, opaqueAlphaThreshold, SilhouetteHit.ReleaseAlpha);
        }

        float ReadFramebufferAlpha(Vector2Int pixel)
        {
            if (_pixel == null)
                _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            var previous = RenderTexture.active;
            RenderTexture.active = null;
            _pixel.ReadPixels(new Rect(pixel.x, pixel.y, 1f, 1f), 0, 0, false);
            RenderTexture.active = previous;

            var data = _pixel.GetRawTextureData<Color32>();
            return data.Length > 0 ? data[0].a / 255f : 0f;
        }

        void ApplyFrameRate(int rate)
        {
            if (rate == _appliedRate)
                return;

            _appliedRate = rate;
            Application.targetFrameRate = rate;
        }

        void RefreshEmotionLabel()
        {
            if (_animator == null)
                _animator = GetComponent<IAvatarAnimator>();
            if (_animator == null)
            {
                if (_emotionText.Length != 0)
                    _emotionText = string.Empty;
                return;
            }

            var emotion = _animator.Emotion;
            var intensity = _animator.EmotionIntensity;
            var talking = _animator.IsTalking;
            if (_emotionReady && emotion == _shownEmotion && talking == _shownTalking && intensity == _shownIntensity)
                return;

            _emotionReady = true;
            _shownEmotion = emotion;
            _shownIntensity = intensity;
            _shownTalking = talking;
            _emotionText = AvatarAnimatorModel.FormatLabel(emotion, intensity, talking);
        }

        void AccumulateFps()
        {
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer < 0.5f)
                return;

            int fps = Mathf.RoundToInt(_fpsFrames / _fpsTimer);
            _fpsText = fps.ToString() + " fps";
            _fpsFrames = 0;
            _fpsTimer = 0f;
        }

        void TintAvatar()
        {
            if (placeholderAvatar == null)
            {
                var found = GameObject.Find("Placeholder Avatar");
                if (found != null)
                    placeholderAvatar = found.GetComponent<Renderer>();
            }

            if (placeholderAvatar == null)
                return;

            if (_tintBlock == null)
                _tintBlock = new MaterialPropertyBlock();

            placeholderAvatar.GetPropertyBlock(_tintBlock);
            _tintBlock.SetColor("_Color", AvatarTint);
            _tintBlock.SetColor("_BaseColor", AvatarTint);
            placeholderAvatar.SetPropertyBlock(_tintBlock);
        }
    }
}
