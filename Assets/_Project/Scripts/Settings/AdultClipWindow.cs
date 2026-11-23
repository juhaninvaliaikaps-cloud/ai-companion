using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Overlay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-188: a separate adult reaction window. It sits beside the avatar and the expression
    /// portrait instead of replacing either one. It only runs while the 18+ gate is open.
    /// </summary>
    public sealed class AdultClipWindow : MonoBehaviour, IDebugPanel,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        const string FolderName = "adult-animations";
        const string BreastFile = "breast_touch_clip_v3.mp4";
        const string ButtFile = "butt_squeeze_clip_v3.mp4";
        const string ButtSlapFile = "butt_slap_clip_v3.mp4";
        const string KissFile = "happi_kiss_pov.mp4";
        const string KissLoopFile = "happi_kiss_loop_60s.mp4";
        const string LyingKissFile = "happi_lying_kiss_pov.mp4";
        const string LyingKissLoopFile = "happi_lying_kiss_loop_60s.mp4";
        const string LyingKissLoop2File = "happi_lying_kiss_loop2_60s.mp4";
        const string KneelingFingerFile = "happi_kneeling_finger_loop_60s.mp4";
        const string LyingFingerFile = "happi_lying_finger_loop_60s.mp4";
        const string OnTopFingerFile = "happi_ontop_finger_loop_60s.mp4";
        const string FingerSuckFile = "fingersuckloop60s.mp4";
        const float NativeSize = 544f;
        const float DefaultPanelSize = 480f;
        const float Margin = 12f;
        const float MinPanelSize = 180f;
        const float HideAfterSeconds = 7f;
        const string SizeKey = "happi.adultClip.size";
        const string XKey = "happi.adultClip.x";
        const string YKey = "happi.adultClip.y";
        const float PlaneDistance = 0.58f;

        static readonly System.Collections.Generic.Dictionary<string, AdultClipWindow> Windows =
            new System.Collections.Generic.Dictionary<string, AdultClipWindow>();
        static string _activeCue = string.Empty;
        static int _spawnIndex;

        SettingsPanel _host;
        string _cue = string.Empty;

        GameObject _canvasObject;
        Canvas _canvas;
        GameObject _panel;
        RectTransform _panelRect;
        RawImage _image;
        VideoPlayer _video;
        AudioSource _audio;
        AICompanion.Voice.VoiceSpeaker _voice;
        bool _audioPriority;
        RenderTexture _texture;
        float _hideAt;
        string _playing = string.Empty;
        int _showId;
        VideoPlayer.EventHandler _onThisPrepare;
        float _panelSize;
        Vector2 _panelPos;
        bool _pointerActive;
        bool _resizing;
        bool _dragged;
        bool _paused;
        Vector2 _pointerStart;
        Vector2 _panelStart;
        float _sizeStart;
        Rect _interactive;
        bool _loop;
        readonly Vector3[] _corners = new Vector3[4];

        /// <summary>A prepare callback is for the clip that was just asked for, not an earlier one.</summary>
        public static bool AcceptsPrepared(int requestId, int latestId)
        {
            return requestId > 0 && requestId == latestId;
        }

        /// <summary>
        /// Square panel edge in overlay pixels. The normal 480-wide overlay uses 480.
        /// A larger window may go up to the clip's native 544. The video is not cropped
        /// to reach that size.
        /// </summary>
        public static float PanelEdge(float screenWidth, float screenHeight)
        {
            var room = Mathf.Min(screenWidth, screenHeight) - Margin * 2f;
            return Mathf.Min(NativeSize, Mathf.Max(DefaultPanelSize, room));
        }

        /// <summary>
        /// Draws the panel into the transparent overlay camera, like the avatar and the portrait.
        /// Without a camera it stays a screen-space overlay.
        /// </summary>
        public static void PlaceOnOverlay(Canvas canvas, Camera camera)
        {
            if (canvas == null)
                return;
            if (camera == null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = PlaneDistance;
        }

        public static bool IsAdultCue(string name)
        {
            var id = Normalize(name);
            return id == "touch_breast" || id == "breast_touch" || id == "breast_touch_clip_v3"
                || id == "touch_breast_loop" || id == "breast_touch_loop"
                || id == "touch_butt" || id == "butt_touch" || id == "butt_squeeze" || id == "butt_squeeze_clip_v3"
                || id == "touch_butt_loop" || id == "butt_squeeze_loop" || id == "butt_slap"
                || id == "kiss" || id == "make_out" || id == "kiss_loop" || id == "happi_kiss_loop"
                || id == "blowjob"
                || id == "lying_kiss" || id == "lying_make_out" || id == "lying_make_out_2"
                || id == "blowjob_kneeling" || id == "blowjob_lying" || id == "blowjob_ontop"
                || id == "blowjob" || id == "finger_suck_loop";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallCueRouter()
        {
            AdultCueRouter.Handler = Play;
        }

        public static void InstallRouter()
        {
            InstallCueRouter();
        }

        public Rect InteractiveRect
        {
            get { return _panel != null && _panel.activeSelf ? _interactive : new Rect(0f, 0f, 0f, 0f); }
        }

        public static void Play(string cue)
        {
            if (!IntimateExpressions.Active || !IsAdultCue(cue))
                return;
            var key = Normalize(cue);
            var host = FindFirstObjectByType<SettingsPanel>();
            if (host == null)
                return;

            AdultClipWindow window;
            if (!Windows.TryGetValue(key, out window) || window == null)
            {
                window = host.gameObject.AddComponent<AdultClipWindow>();
                window._host = host;
                window._cue = key;
                Windows[key] = window;
                var overlay = host.GetComponent<OverlayWindow>();
                if (overlay != null)
                    overlay.RefreshClickTargets();
            }

            _activeCue = key;
            window.Show();
        }

        static void SetActive(string cue)
        {
            _activeCue = cue;
        }

        static bool IsActive(string cue)
        {
            return string.Equals(cue, _activeCue, System.StringComparison.Ordinal);
        }

        /// <summary>True while this cue's window is on screen and is the active loop.</summary>
        public static bool IsActiveCue(string cue)
        {
            var key = Normalize(cue);
            if (!IsActive(key))
                return false;
            AdultClipWindow window;
            return Windows.TryGetValue(key, out window)
                && window != null
                && window._panel != null
                && window._panel.activeSelf;
        }

        void OnDestroy()
        {
            if (!string.IsNullOrEmpty(_cue))
                Windows.Remove(_cue);
            if (string.Equals(_activeCue, _cue, System.StringComparison.Ordinal))
                _activeCue = string.Empty;
            if (_video != null)
            {
                if (_onThisPrepare != null)
                    _video.prepareCompleted -= _onThisPrepare;
                _video.loopPointReached -= OnEnded;
                _video.Stop();
            }
            SetAudioPriority(false);
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        void Update()
        {
            FollowOverlayCamera();
            if (!IntimateExpressions.Active)
            {
                Hide();
                return;
            }
            if (_panel == null || !_panel.activeSelf)
                return;
            PlacePanel();
            var active = IsActive(_cue);
            if (_loop && !active && _video != null && _video.isPlaying)
                _video.Pause();
            else if (_loop && active && !_paused && _video != null && _video.isPrepared && !_video.isPlaying && _hideAt <= 0f)
                _video.Play();
            if (active != _audioPriority)
                SetAudioPriority(active);
            if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
                Hide();
        }

        static string Normalize(string cue)
        {
            return (cue ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        }

        string FileFor(string cue)
        {
            var id = Normalize(cue);
            if (id == "touch_breast" || id == "breast_touch" || id == "breast_touch_clip_v3"
                || id == "touch_breast_loop" || id == "breast_touch_loop")
                return BreastFile;
            if (id == "touch_butt" || id == "butt_touch" || id == "butt_squeeze" || id == "butt_squeeze_clip_v3"
                || id == "touch_butt_loop" || id == "butt_squeeze_loop")
                return ButtFile;
            if (id == "butt_slap")
                return ButtSlapFile;
            if (id == "kiss")
                return KissFile;
            if (id == "make_out" || id == "kiss_loop" || id == "happi_kiss_loop")
                return KissLoopFile;
            if (id == "lying_kiss")
                return LyingKissFile;
            if (id == "lying_make_out")
                return LyingKissLoopFile;
            if (id == "lying_make_out_2")
                return LyingKissLoop2File;
            if (id == "blowjob_kneeling" || id == "blowjob")
                return KneelingFingerFile;
            if (id == "blowjob_lying")
                return LyingFingerFile;
            if (id == "blowjob_ontop")
                return OnTopFingerFile;
            if (id == "finger_suck_loop")
                return FingerSuckFile;
            return string.Empty;
        }

        static bool Loops(string cue)
        {
            var id = Normalize(cue);
            return id == "touch_breast_loop" || id == "breast_touch_loop"
                || id == "touch_butt_loop" || id == "butt_squeeze_loop"
                || id == "make_out" || id == "kiss_loop" || id == "happi_kiss_loop"
                || id == "lying_make_out" || id == "lying_make_out_2"
                || id == "blowjob_kneeling" || id == "blowjob_lying" || id == "blowjob_ontop"
                || id == "blowjob" || id == "finger_suck_loop";
        }

        void Show()
        {
            var file = FileFor(_cue);
            if (file.Length == 0)
                return;
            if (_canvasObject == null)
                BuildUi();
            if (_video == null)
                return;

            var path = FindClip(file);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("Adult clip window: " + file + " was not found in " + Folder());
                return;
            }

            _panel.SetActive(true);
            PlacePanel();
            _playing = _cue;
            _loop = Loops(_cue);
            _paused = false;
            _video.isLooping = _loop;
            SetAudioPriority(true);
            _hideAt = _loop ? 0f : Time.unscaledTime + HideAfterSeconds;
            var showId = ++_showId;
            if (_onThisPrepare != null)
            {
                _video.prepareCompleted -= _onThisPrepare;
                _onThisPrepare = null;
            }

            _video.Stop();
            _video.url = new Uri(path).AbsoluteUri;
            VideoPlayer.EventHandler prepared = null;
            prepared = player =>
            {
                player.prepareCompleted -= prepared;
                if (_onThisPrepare == prepared)
                    _onThisPrepare = null;
                if (!AcceptsPrepared(showId, _showId) || player == null || _panel == null || !_panel.activeSelf)
                    return;
                _hideAt = _loop ? 0f : Time.unscaledTime + HideAfterSeconds;
                player.Play();
            };
            _onThisPrepare = prepared;
            _video.prepareCompleted += prepared;
            _video.Prepare();
            Debug.Log("Adult clip window: playing " + file);
        }

        void OnEnded(VideoPlayer player)
        {
            if (!_loop)
                Hide();
        }

        void SetAudioPriority(bool on)
        {
            if (_audioPriority == on)
                return;
            _audioPriority = on;
            if (_voice == null)
                _voice = GetComponent<AICompanion.Voice.VoiceSpeaker>();
            if (_voice != null)
                _voice.SetClipAudioPriority(on);
            if (_audio != null)
                _audio.mute = false;
        }

        void Hide()
        {
            var pending = _onThisPrepare != null;
            if (pending && _video != null)
            {
                _video.prepareCompleted -= _onThisPrepare;
                _onThisPrepare = null;
                _showId++;
            }
            var showing = _panel != null && _panel.activeSelf;
            if (showing)
                _panel.SetActive(false);
            if (_video != null && (showing || pending || _video.isPlaying))
                _video.Stop();
            _hideAt = 0f;
            _loop = false;
            _paused = false;
            _playing = string.Empty;
            _pointerActive = false;
            _resizing = false;
            SetAudioPriority(false);
        }

        static string Folder()
        {
            var root = SettingsLocations.Folder(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.persistentDataPath);
            return string.IsNullOrEmpty(root) ? string.Empty : Path.Combine(root, FolderName);
        }

        static string FindClip(string file)
        {
            var folder = Folder();
            if (string.IsNullOrEmpty(folder))
                return string.Empty;
            var path = Path.Combine(folder, file);
            return File.Exists(path) ? path : string.Empty;
        }

        void BuildUi()
        {
            _canvasObject = new GameObject("AdultClipCanvas");
            _canvasObject.transform.SetParent(transform, false);
            _canvasObject.hideFlags = HideFlags.DontSave;
            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.sortingOrder = 6; // above the video avatar, below the portrait and chat
            FollowOverlayCamera();
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _canvasObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Adult clip");
            _panel.transform.SetParent(_canvasObject.transform, false);
            _panelRect = _panel.AddComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0f, 0f);
            _panelRect.anchorMax = new Vector2(0f, 0f);
            _panelRect.pivot = new Vector2(0f, 0f);
            _panelSize = PlayerPrefs.GetFloat(SizeKey + "." + _cue, PlayerPrefs.GetFloat(SizeKey, 0f));
            if (_panelSize < MinPanelSize)
                _panelSize = PanelEdge(Screen.width, Screen.height);
            var area = _canvas.pixelRect;
            if (area.width < 1f || area.height < 1f)
                area = new Rect(0f, 0f, Screen.width, Screen.height);
            var cascade = Mathf.Min(4, Windows.Count) * 26f;
            _panelPos = new Vector2(
                PlayerPrefs.GetFloat(XKey + "." + _cue,
                    Mathf.Max(0f, area.width - _panelSize - Margin - cascade)),
                PlayerPrefs.GetFloat(YKey + "." + _cue, Margin + cascade));
            PlacePanel();

            var back = _panel.AddComponent<Image>();
            back.color = new Color(0.04f, 0.04f, 0.06f, 0.94f);
            back.raycastTarget = true;
            var outline = _panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.45f, 0.32f, 0.55f, 0.8f);
            outline.effectDistance = new Vector2(2f, 2f);

            var picture = new GameObject("Picture");
            picture.transform.SetParent(_panel.transform, false);
            var pictureRect = picture.AddComponent<RectTransform>();
            pictureRect.anchorMin = new Vector2(0f, 0f);
            pictureRect.anchorMax = new Vector2(1f, 1f);
            pictureRect.offsetMin = new Vector2(4f, 4f);
            pictureRect.offsetMax = new Vector2(-4f, -4f);
            _image = picture.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _image.uvRect = new Rect(0f, 0f, 1f, 1f);

            // A visible grip in the bottom-right corner: drag it to resize; drag the picture
            // to move the window. Both work through the overlay's clickable panel rect.
            var grip = new GameObject("Resize");
            grip.transform.SetParent(_panel.transform, false);
            var gripRect = grip.AddComponent<RectTransform>();
            gripRect.anchorMin = new Vector2(1f, 0f);
            gripRect.anchorMax = new Vector2(1f, 0f);
            gripRect.pivot = new Vector2(1f, 0f);
            gripRect.anchoredPosition = new Vector2(-2f, 2f);
            gripRect.sizeDelta = new Vector2(18f, 18f);
            var gripImage = grip.AddComponent<Image>();
            gripImage.color = new Color(0.75f, 0.6f, 0.85f, 0.65f);
            gripImage.raycastTarget = false;

            _texture = new RenderTexture((int)NativeSize, (int)NativeSize, 0, RenderTextureFormat.ARGB32);
            _texture.useMipMap = false;
            _texture.autoGenerateMips = false;
            _texture.filterMode = FilterMode.Bilinear;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.Create();
            _image.texture = _texture;

            _audio = _panel.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = false;
            _audio.volume = 1f;

            _video = _panel.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.isLooping = false;
            _video.aspectRatio = VideoAspectRatio.FitInside;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _texture;
            _video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _video.controlledAudioTrackCount = 1;
            _video.SetTargetAudioSource(0, _audio);
            _video.EnableAudioTrack(0, true);
            _video.loopPointReached += OnEnded;
            _panel.SetActive(false);
        }

        void FollowOverlayCamera()
        {
            PlaceOnOverlay(_canvas, Camera.main);
        }

        void PlacePanel()
        {
            if (_panelRect == null || _canvas == null)
                return;
            var area = _canvas.pixelRect;
            var edge = Mathf.Clamp(_panelSize, MinPanelSize, NativeSize);
            _panelSize = edge;
            _panelRect.sizeDelta = new Vector2(edge, edge);
            var maxX = Mathf.Max(0f, area.width - edge);
            var maxY = Mathf.Max(0f, area.height - edge);
            _panelPos.x = Mathf.Clamp(_panelPos.x, 0f, maxX);
            _panelPos.y = Mathf.Clamp(_panelPos.y, 0f, maxY);
            _panelRect.anchoredPosition = _panelPos;
            UpdateInteractiveRect();
        }

        void UpdateInteractiveRect()
        {
            if (_panelRect == null)
                return;
            _panelRect.GetWorldCorners(_corners);
            var camera = _canvas != null ? _canvas.worldCamera : null;
            var bl = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
            var tr = RectTransformUtility.WorldToScreenPoint(camera, _corners[2]);
            _interactive = Rect.MinMaxRect(bl.x, Screen.height - tr.y, tr.x, Screen.height - bl.y);
        }

        void HandlePointer()
        {
            if (_panelRect == null || _canvas == null)
                return;

            var camera = _canvas.worldCamera;
            Vector2 local;
            var inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRect, Input.mousePosition, camera, out local)
                && _panelRect.rect.Contains(local);

            if (inside)
            {
                var scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    SetActive(_cue);
                    _panelSize = Mathf.Clamp(_panelSize + scroll * 24f, MinPanelSize, NativeSize);
                    PlacePanel();
                    SavePlacement();
                }
            }

            if (Input.GetMouseButtonDown(0) && inside)
            {
                SetActive(_cue);
                _pointerActive = true;
                _dragged = false;
                _pointerStart = Input.mousePosition;
                _panelStart = _panelPos;
                _sizeStart = _panelSize;
                _resizing = local.x > _panelSize - 22f && local.y < 22f;
            }

            if (!_pointerActive)
                return;

            if (Input.GetMouseButton(0))
            {
                var delta = (Vector2)Input.mousePosition - _pointerStart;
                if (delta.magnitude > 4f)
                    _dragged = true;
                if (_resizing)
                {
                    var grow = Mathf.Max(delta.x, -delta.y);
                    _panelSize = Mathf.Clamp(_sizeStart + grow, MinPanelSize, NativeSize);
                }
                else
                {
                    _panelPos = _panelStart + delta;
                }
                PlacePanel();
            }

            if (Input.GetMouseButtonUp(0))
            {
                // A click without a drag is a pause/resume, not a move.
                if (!_dragged && !_resizing)
                {
                    _paused = !_paused;
                    if (_video != null)
                    {
                        if (_paused)
                            _video.Pause();
                        else if (_video.isPrepared)
                            _video.Play();
                    }
                }
                SavePlacement();
                _pointerActive = false;
                _resizing = false;
                _dragged = false;
            }
        }

        void SavePlacement()
        {
            PlayerPrefs.SetFloat(SizeKey + "." + _cue, _panelSize);
            PlayerPrefs.SetFloat(XKey + "." + _cue, _panelPos.x);
            PlayerPrefs.SetFloat(YKey + "." + _cue, _panelPos.y);
            PlayerPrefs.Save();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_panelRect == null)
                return;
            SetActive(_cue);
            _pointerActive = true;
            _dragged = false;
            _pointerStart = eventData.position;
            _panelStart = _panelPos;
            _sizeStart = _panelSize;
            Vector2 local;
            _resizing = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRect, eventData.position, _canvas != null ? _canvas.worldCamera : null, out local)
                && local.x > _panelSize - 22f && local.y < 22f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pointerActive)
                return;
            if (eventData.delta.sqrMagnitude > 0.01f)
                _dragged = true;
            if (_resizing)
            {
                var grow = Mathf.Max(eventData.delta.x, -eventData.delta.y);
                _panelSize = Mathf.Clamp(_panelSize + grow, MinPanelSize, NativeSize);
            }
            else
            {
                _panelPos += eventData.delta;
            }
            PlacePanel();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pointerActive)
                return;
            if (!_dragged && !_resizing)
            {
                _paused = !_paused;
                if (_video != null)
                {
                    if (_paused)
                        _video.Pause();
                    else if (_video.isPrepared)
                        _video.Play();
                }
            }
            SavePlacement();
            _pointerActive = false;
            _resizing = false;
            _dragged = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            SetActive(_cue);
            _panelSize = Mathf.Clamp(_panelSize + eventData.scrollDelta.y * 24f, MinPanelSize, NativeSize);
            PlacePanel();
            SavePlacement();
        }
    }
}
