using System.Collections;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Borderless transparent window, topmost toggle, drag, and click-through.
    /// A point is on the avatar only when the framebuffer pixel under the cursor is
    /// opaque. Scroll over that pixel zooms the camera. Ctrl+scroll scales the avatar.
    /// The window stays at 480×720 until she would be clipped, then the transparent
    /// window grows. Chat and labels stay at their normal pixel size. Win32 runs only
    /// in the Windows player. In the editor this still clears the camera, limits the
    /// frame rate, frames the avatar, and updates the debug label.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OverlayWindow : MonoBehaviour, ICompanionStartup, ITypingFocus, IDesktopWindow, ICompanionVisibility, IScreenCursor
    {
        const string TopmostOnLabel = "Topmost on   F8";
        const string TopmostOffLabel = "Topmost off  F8";
        static readonly Rect PanelRect = new Rect(8f, 8f, 360f, 108f);
        static GUIStyle _rotateStyle;
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
        bool _dragging;
        bool _yawDrag;
        bool _rotating;
        bool _leftWasHeld;
        bool _rightWasHeld;
        bool _middleWasHeld;
        float _yawPointerX;
        bool _clickThrough;
        OverlayClickThrough _clickThroughMode = OverlayClickThrough.Silhouette;
        float _windowScale = 1f;
        int _anchorRight;
        int _anchorBottom;
        bool _hasAnchor;
        int _repositionFrames;
        int _appliedWidth = -1;
        int _appliedHeight = -1;
        AvatarCameraFrame _frame;
        int _shownSizePct = -1;
        int _shownZoomPct = -1;
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
        MonoBehaviour[] _behaviours;
        IAvatarAnimator _animator;
        IAvatarTurntable _turntable;
        string _emotionText = "";
        Emotion _shownEmotion;
        float _shownIntensity = -1f;
        bool _shownTalking;
        bool _emotionReady;
        string _rotateText = AvatarRotateMode.OffLabel;
        bool _shownRotating;
        bool _rotateReady;
        float _lastSilhouetteClick = -1f;
        bool _companionVisible = true;

        public bool IsTopmost => _topmost;
        public bool IsClickThrough => _clickThrough;
        public float WindowScale => _windowScale;
        public int IdleFrameRate => idleFrameRate;
        public OverlayClickThrough ClickThroughMode => _clickThroughMode;
        public bool IsCompanionVisible => _companionVisible;
        public IWindowChrome Chrome => _chrome;

        /// <summary>True while the user drags the window. Desktop presence pauses then.</summary>
        public bool IsDragging => _dragging;

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
            _windowScale = PlayerPrefs.GetFloat(OverlayWindowSize.ScalePrefKey, 1f);
            EnsureCameraFrame();
            TintAvatar();
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            ApplyFrameRate(activeFrameRate);
            ApplyPlayerResolution();
            EnsureChrome();
            PushAvatarScale();
            LockUiPixels();
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
            bool overDebug = inside && IsOverDebugPanel(unityScreen);
            bool eventUi = inside && IsOverUi(unityScreen);
            bool overOtherUi = overDebug || eventUi;
            bool overUi = overPanel || overOtherUi;
            var pixel = new Vector2Int(Mathf.RoundToInt(unityScreen.x), Mathf.RoundToInt(unityScreen.y));
            bool outsideSlack = !_hasSample || !SilhouetteHit.WithinSlack(_sampledPixel, pixel, SilhouetteHit.SampleSlackPixels);
            bool onSilhouette = inside && _onSilhouette;
            bool leftHeld = LeftHeld();
            bool leftDown = leftHeld && !_leftWasHeld;
            _leftWasHeld = leftHeld;
            bool rightHeld = RightHeld();
            bool rightDown = rightHeld && !_rightWasHeld;
            _rightWasHeld = rightHeld;
            bool middleHeld = MiddleHeld();
            bool middleDown = middleHeld && !_middleWasHeld;
            _middleWasHeld = middleHeld;

            if (_turntable == null)
                _turntable = GetComponent<IAvatarTurntable>();

            bool toggle = AvatarRotateMode.ShouldToggle(rightDown, onSilhouette, overUi);
            _rotating = AvatarRotateMode.Next(_rotating, toggle, Input.GetKeyDown(KeyCode.Escape));
            bool resetKey = Input.GetKeyDown(KeyCode.R) && !DebugInputGuard.IsBlocked(KeyCode.R);
            if (_turntable != null && AvatarRotateMode.ShouldReset(_rotating, resetKey, middleDown, onSilhouette, overUi))
                _turntable.ResetYaw();
            if (middleDown && onSilhouette && !overUi)
                ResetCameraZoom();

            if (leftDown && overOtherUi)
                FocusForTyping();

            if (leftDown && onSilhouette && !overUi)
            {
                if (AvatarDoubleClick.IsSecond(_lastSilhouetteClick, Time.unscaledTime))
                {
                    _lastSilhouetteClick = -1f;
                    NotifyAvatarDoubleClick();
                }
                else
                {
                    _lastSilhouetteClick = Time.unscaledTime;
                }
            }

            if (!_yawDrag && leftDown && AvatarRotateMode.ShouldRotateInsteadOfMovingWindow(_rotating, onSilhouette, overUi))
            {
                _yawDrag = true;
                _yawPointerX = unityScreen.x;
            }

            if (_yawDrag)
            {
                if (leftHeld && _rotating)
                {
                    float delta = unityScreen.x - _yawPointerX;
                    _yawPointerX = unityScreen.x;
                    if (delta != 0f && _turntable != null)
                        _turntable.AddYawPixels(delta);
                }
                else
                {
                    _yawDrag = false;
                    if (_turntable != null)
                        _turntable.CommitYaw();
                }
            }

            if (!_dragging && !_yawDrag && WindowDragGate.ShouldStart(leftDown, onSilhouette, overPanel, overOtherUi) && _chrome.TryGetWindowPosition(out var origin) && _chrome.TryGetScreenCursor(out var cursor))
            {
                _dragging = true;
                _drag = new WindowDrag(origin, cursor);
            }

            if (_dragging)
            {
                if (leftHeld && _chrome.TryGetScreenCursor(out screenCursor))
                    _chrome.MoveWindow(_drag.PositionFor(screenCursor), _topmost);
                else if (!leftHeld)
                {
                    _dragging = false;
                    RememberAnchor();
                }
            }

            float scroll = Input.mouseScrollDelta.y;
            bool adjustView = OverlayWindowSize.CursorCanAdjust(onSilhouette, overUi) && Mathf.Abs(scroll) > 0.01f;
            if (adjustView)
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (ctrl)
                    AdjustSize(scroll);
                else
                    AdjustZoom(scroll, unityScreen);
            }

            _clickThrough = OverlayClickThroughPolicy.ShouldClickThrough(_clickThroughMode, overUi, onSilhouette, _dragging || _yawDrag);
            _chrome.SetClickThrough(_clickThrough);
            QueueAlphaSample(inside, outsideSlack, pixel);

            if (Input.GetKeyDown(topmostKey) && !DebugInputGuard.IsBlocked(topmostKey))
                SetTopmost(!_topmost);

            bool awake = inside || leftHeld || rightHeld || middleHeld || _dragging || _yawDrag || adjustView || Input.anyKey || KeepAwake();
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
                if (_warmup == 0 && !_dragging)
                    ApplySizedWindow(false);
            }

            if (_repositionFrames > 0 && !_dragging && _chrome != null && _chrome.IsSupported)
            {
                ApplySizedWindow(false);
                _repositionFrames--;
            }

            if (!_companionVisible && _chrome != null && _chrome.IsHandleLocked)
                _chrome.SetWindowVisible(false);
        }

        void OnGUI()
        {
            if (!_started)
                return;

            RefreshEmotionLabel();
            RefreshRotateLabel();
            var line = RotateStyle();
            GUI.Box(PanelRect, GUIContent.none);
            GUI.Label(new Rect(12f, 10f, 336f, 18f), _topmost ? TopmostOnLabel : TopmostOffLabel, line);
            GUI.Label(new Rect(12f, 28f, 336f, 18f), _fpsText, line);
            GUI.Label(new Rect(12f, 46f, 336f, 18f), _emotionText, line);
            GUI.Label(new Rect(12f, 64f, 336f, 18f), AvatarAnimatorModel.DebugHint, line);
            GUI.Label(new Rect(12f, 82f, 336f, 18f), _rotateText, line);
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

        public void FocusForTyping()
        {
            if (_chrome != null)
                _chrome.FocusForTyping();
        }

        public void SetTopmost(bool topmost)
        {
            _topmost = topmost;
            if (_chrome != null && _chrome.IsSupported)
                _chrome.SetTopmost(topmost);
        }

        public void SetClickThroughMode(OverlayClickThrough mode)
        {
            _clickThroughMode = mode;
        }

        public void SetIdleFrameRate(int fps)
        {
            if (fps < 1)
                fps = 1;
            if (fps > OverlayFrameRate.Active)
                fps = OverlayFrameRate.Active;
            idleFrameRate = fps;
        }

        public void SetWindowScale(float scale)
        {
            if (!_started)
                StartCompanion();

            _windowScale = scale;
            ApplySizedWindow(true);
            PlayerPrefs.SetFloat(OverlayWindowSize.ScalePrefKey, _windowScale);
            PushAvatarScale();
            LockUiPixels();
        }

        public void SetCompanionVisible(bool visible)
        {
            _companionVisible = visible;
            if (_chrome == null)
                return;

            _chrome.SetWindowVisible(visible);
            if (visible && _chrome.IsSupported)
                _chrome.SetTopmost(_topmost);
        }

        void ApplyPlayerResolution()
        {
            if (_resolutionApplied)
                return;

            _resolutionApplied = true;
            _windowScale = OverlayWindowSize.ClampScale(_windowScale, 10000, 10000);
            OverlayWindowSize.SizeFor(WindowFactor(), out windowWidth, out windowHeight);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            _appliedWidth = windowWidth;
            _appliedHeight = windowHeight;
            _warmup = 30;
            _repositionFrames = 30;
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
            ApplySizedWindow(true);
        }

        public bool TryGetDesktopWindow(out DesktopWindow window)
        {
            window = default;
            if (_chrome == null || !_chrome.IsSupported)
                return false;
            if (!_chrome.TryGetWindowRect(out var left, out var top, out var right, out var bottom))
                return false;

            if (!_chrome.TryGetWorkArea(out var workLeft, out var workTop, out var workRight, out var workBottom))
            {
                workLeft = left;
                workTop = top;
                workRight = right;
                workBottom = bottom;
            }

            window = new DesktopWindow
            {
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom,
                WorkLeft = workLeft,
                WorkTop = workTop,
                WorkRight = workRight,
                WorkBottom = workBottom,
            };
            return true;
        }

        void EnsureCameraFrame()
        {
            if (_frame == null)
                _frame = GetComponent<AvatarCameraFrame>();
            if (_frame == null)
                _frame = gameObject.AddComponent<AvatarCameraFrame>();

            _frame.Attach(_camera);
            _frame.SetZoom(Mathf.Clamp01(PlayerPrefs.GetFloat(OverlayWindowSize.ZoomPrefKey, 0f)));
            PushAvatarScale();
        }

        void ResetCameraZoom()
        {
            if (_frame == null)
                return;

            _frame.ResetZoom();
            PlayerPrefs.SetFloat(OverlayWindowSize.ZoomPrefKey, 0f);
        }

        void AdjustZoom(float scrollY, Vector2 unityScreen)
        {
            if (_frame == null)
                return;

            float next = OverlayWindowSize.NextZoom(_frame.TargetZoom, scrollY);
            if (_frame.TryPivotFromCursor(unityScreen, out var pivot))
                _frame.SetZoomToward(next, pivot);
            else
                _frame.SetZoom(next);

            PlayerPrefs.SetFloat(OverlayWindowSize.ZoomPrefKey, next);
        }

        void AdjustSize(float scrollY)
        {
            int workWidth = 10000;
            int workHeight = 10000;
            if (TryReadWorkArea(out var left, out var top, out var right, out var bottom))
            {
                workWidth = right - left;
                workHeight = bottom - top;
            }

            _windowScale = OverlayWindowSize.NextScale(_windowScale, scrollY, workWidth, workHeight);
            PlayerPrefs.SetFloat(OverlayWindowSize.ScalePrefKey, _windowScale);
            ApplySizedWindow(true);
            PushAvatarScale();
            LockUiPixels();
        }

        float WindowFactor()
        {
            return OverlayWindowSize.ClampScale(OverlayWindowSize.WindowFactorFor(_windowScale), 10000, 10000);
        }

        void PushAvatarScale()
        {
            if (_frame == null)
                return;

            float factor = 1f;
            if (_chrome != null && _chrome.IsSupported && windowHeight > 0)
                factor = windowHeight / (float)OverlayWindowSize.BaseHeight;
            _frame.SetModelScale(_windowScale, factor);
        }

        void LockUiPixels()
        {
            var scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < scalers.Length; i++)
            {
                var scaler = scalers[i];
                if (scaler == null)
                    continue;

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }
        }

        void ApplySizedWindow(bool allowResize)
        {
            bool haveWork = TryReadWorkArea(out var workLeft, out var workTop, out var workRight, out var workBottom);
            int workWidth = haveWork ? workRight - workLeft : 10000;
            int workHeight = haveWork ? workBottom - workTop : 10000;
            _windowScale = OverlayWindowSize.ClampScale(_windowScale, workWidth, workHeight);
            float factor = OverlayWindowSize.ClampScale(OverlayWindowSize.WindowFactorFor(_windowScale), workWidth, workHeight);
            OverlayWindowSize.SizeFor(factor, out var width, out var height);
            windowWidth = width;
            windowHeight = height;
            if (!haveWork || _chrome == null || !_chrome.IsSupported)
                return;

            if (!_hasAnchor)
            {
                _anchorRight = workRight - OverlayWindowSize.EdgeMargin;
                _anchorBottom = workBottom - OverlayWindowSize.EdgeMargin;
                _hasAnchor = true;
            }

            OverlayWindowSize.PlaceAnchored(
                _anchorRight,
                _anchorBottom,
                workLeft,
                workTop,
                workRight,
                workBottom,
                width,
                height,
                out var x,
                out var y);

            bool sizeChanged = width != _appliedWidth || height != _appliedHeight;
            _appliedWidth = width;
            _appliedHeight = height;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (allowResize && sizeChanged)
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
                _repositionFrames = 30;
                if (_warmup < 30)
                    _warmup = 30;
            }
#endif
            _chrome.PlaceWindow(new Vector2Int(x, y), width, height, _topmost);
        }

        bool TryReadWorkArea(out int left, out int top, out int right, out int bottom)
        {
            left = 0;
            top = 0;
            right = 0;
            bottom = 0;
            if (_chrome == null || !_chrome.IsSupported)
                return false;

            if (_chrome.TryGetWorkArea(out left, out top, out right, out bottom) && right > left && bottom > top)
                return true;

            if (_chrome.TryGetMonitorSize(out var monitor) && monitor.x > 0 && monitor.y > 0)
            {
                left = 0;
                top = 0;
                right = monitor.x;
                bottom = monitor.y;
                return true;
            }

            return false;
        }

        void RememberAnchor()
        {
            if (_chrome == null || !_chrome.TryGetWindowRect(out _, out _, out var right, out var bottom))
                return;

            _anchorRight = right;
            _anchorBottom = bottom;
            _hasAnchor = true;
        }

        public bool TryGetUnityScreenCursor(out float x, out float y)
        {
            if (_chrome != null && _chrome.IsSupported && _chrome.TryGetClientCursor(out var client, out var clientSize)
                && clientSize.x > 0 && clientSize.y > 0)
            {
                var unity = OverlayPointerMath.ToUnityScreen(client.x, client.y, clientSize.x, clientSize.y, Screen.width, Screen.height);
                x = unity.x;
                y = unity.y;
                return true;
            }

            var mouse = Input.mousePosition;
            x = mouse.x;
            y = mouse.y;
            return true;
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

        bool KeepAwake()
        {
            if (_behaviours == null)
                _behaviours = GetComponents<MonoBehaviour>();

            for (var i = 0; i < _behaviours.Length; i++)
            {
                var wake = _behaviours[i] as IOverlayKeepAwake;
                if (wake != null && wake.KeepFrameRateActive)
                    return true;
            }

            return false;
        }

        void NotifyAvatarDoubleClick()
        {
            if (_behaviours == null)
                _behaviours = GetComponents<MonoBehaviour>();

            for (var i = 0; i < _behaviours.Length; i++)
            {
                var listener = _behaviours[i] as IAvatarDoubleClick;
                if (listener != null)
                    listener.OnAvatarDoubleClick();
            }
        }

        bool IsOverDebugPanel(Vector2 unityScreen)
        {
            if (_behaviours == null)
                _behaviours = GetComponents<MonoBehaviour>();

            for (var i = 0; i < _behaviours.Length; i++)
            {
                var panel = _behaviours[i] as IDebugPanel;
                if (panel == null)
                    continue;

                var rect = panel.InteractiveRect;
                if (rect.width <= 0f || rect.height <= 0f)
                    continue;
                if (OverlayPointerMath.IsInsideTopLeftRect(rect, unityScreen, Screen.height))
                    return true;
            }

            return false;
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

        bool RightHeld()
        {
            if (_chrome != null && _chrome.IsSupported)
                return _chrome.IsRightButtonHeld();

            return Input.GetMouseButton(1);
        }

        bool MiddleHeld()
        {
            if (_chrome != null && _chrome.IsSupported)
                return _chrome.IsMiddleButtonHeld();

            return Input.GetMouseButton(2);
        }

        static GUIStyle RotateStyle()
        {
            if (_rotateStyle == null)
            {
                _rotateStyle = new GUIStyle(GUI.skin.label);
                _rotateStyle.wordWrap = false;
                _rotateStyle.clipping = TextClipping.Clip;
            }

            return _rotateStyle;
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

        void RefreshRotateLabel()
        {
            int sizePct = Mathf.RoundToInt(_windowScale * 100f);
            int zoomPct = _frame != null && _frame.ShownZoom > 0.01f
                ? Mathf.Max(1, Mathf.RoundToInt(_frame.ShownZoom * 100f))
                : 0;
            if (_rotateReady && _shownRotating == _rotating && _shownSizePct == sizePct && _shownZoomPct == zoomPct)
                return;

            _rotateReady = true;
            _shownRotating = _rotating;
            _shownSizePct = sizePct;
            _shownZoomPct = zoomPct;
            _rotateText = AvatarRotateMode.Format(_rotating) + "   Size " + sizePct + "%";
            if (zoomPct > 1)
                _rotateText += "   Zoom " + zoomPct + "%";
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
