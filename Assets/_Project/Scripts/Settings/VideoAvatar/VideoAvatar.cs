using System;
using System.IO;
using AICompanion.Avatar;
using AICompanion.Overlay;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AICompanion.Settings
{
    /// <summary>
    /// Optional full-body 2D avatar: one looping clip drawn in the overlay window in place
    /// of the 3D model. The 3D model is only hidden, never unloaded, so every system that
    /// talks to it keeps working and switching back is instant.
    ///
    /// The picture is a uGUI RawImage that is not a raycast target. Click-through, drag,
    /// and double-click therefore still follow the visible pixels in the framebuffer, the
    /// same as for the 3D model. The clip's sound is never played.
    ///
    /// Only the idle clip is played today. See docs/VIDEO_AVATAR.md.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(215)]
    public sealed class VideoAvatar : MonoBehaviour
    {
        const string ShaderResource = "VideoAvatar";
        const float RetrySeconds = 2f;
        const float LoadTimeoutSeconds = 20f;
        const float HideRescanSeconds = 0.5f;
        const float ResyncCooldownSeconds = 0.5f;
        const float MaskLow = 0.06f;
        const float MaskHigh = 0.94f;

        static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        static readonly int ColorRectId = Shader.PropertyToID("_ColorRect");
        static readonly int MaskRectId = Shader.PropertyToID("_MaskRect");
        static readonly int MaskSourceId = Shader.PropertyToID("_MaskSource");
        static readonly int MaskDecodeId = Shader.PropertyToID("_MaskDecode");
        static readonly int MaskLowId = Shader.PropertyToID("_MaskLow");
        static readonly int MaskHighId = Shader.PropertyToID("_MaskHigh");

        SettingsPanel _settings;
        AvatarMount _mount;
        DesktopPresence _presence;

        GameObject _canvasObject;
        Canvas _canvas;
        RawImage _image;
        RectTransform _imageRect;
        Material _material;

        VideoPlayer _color;
        VideoPlayer _mask;
        Texture2D _maskImage;

        VideoAvatarState _state = VideoAvatarState.Off;
        VideoAvatarClip _clip;
        string _folder = string.Empty;
        string _requested;
        string _error = string.Empty;
        string _status = "3D avatar.";
        string _appData = string.Empty;
        string _persistent = string.Empty;
        Rect _placed;
        float _retryIn;
        float _loadingFor;
        float _rescanIn;
        float _resyncIn;
        bool _paths;
        bool _reload;
        bool _colorReady;
        bool _maskReady;
        bool _shaderWarned;

        public VideoAvatarState State
        {
            get { return _state; }
        }

        /// <summary>One line for the settings panel.</summary>
        public string StatusText
        {
            get { return _status; }
        }

        /// <summary>Looks at the folder again on the next frame, for a clip that was just copied in.</summary>
        public void Reload()
        {
            _reload = true;
        }

        void OnDestroy()
        {
            ReleasePlayers();
            if (_mount != null && _mount.ModelHidden)
                _mount.SetModelHidden(false);
            if (_material != null)
                Destroy(_material);
            if (_canvasObject != null)
                Destroy(_canvasObject);
        }

        void LateUpdate()
        {
            Bind();
            var settings = _settings != null ? _settings.CurrentSettings : null;
            if (settings == null || !AvatarModeNames.IsVideo(settings.AvatarMode))
            {
                if (_state != VideoAvatarState.Off)
                    SwitchOff();
                return;
            }

            var delta = Time.unscaledDeltaTime;
            var requested = settings.VideoAvatarFolder ?? string.Empty;
            var waiting = _state == VideoAvatarState.Off
                || _state == VideoAvatarState.NoFolder
                || _state == VideoAvatarState.NoClip;
            _retryIn -= delta;
            if (_reload
                || !string.Equals(requested, _requested, StringComparison.Ordinal)
                || (waiting && _retryIn <= 0f))
            {
                Resolve(requested);
            }

            if (_state == VideoAvatarState.Loading)
                TickLoading(delta);

            if (_state == VideoAvatarState.Playing)
                TickPlaying(settings, delta);
        }

        void Bind()
        {
            if (_settings == null)
                _settings = GetComponent<SettingsPanel>();
            if (_mount == null)
                _mount = GetComponent<AvatarMount>();
            if (_presence == null)
                _presence = GetComponent<DesktopPresence>();
        }

        /// <summary>Reads the folder once. This runs on a setting change, on Reload, and every two seconds while there is nothing to play.</summary>
        void Resolve(string requested)
        {
            var forced = _reload;
            _reload = false;
            _requested = requested;
            _retryIn = RetrySeconds;
            EnsurePaths();

            var folder = VideoAvatarFolders.Select(requested, _appData, _persistent, Directory.Exists);
            if (string.IsNullOrEmpty(folder))
            {
                ReleasePlayers();
                _folder = string.IsNullOrWhiteSpace(requested)
                    ? VideoAvatarFolders.Primary(_appData, _persistent) ?? string.Empty
                    : requested.Trim();
                SetState(VideoAvatarState.NoFolder);
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception)
            {
                files = Array.Empty<string>();
            }

            var clip = VideoAvatarFiles.Find(files, VideoAvatarClipNames.Idle);
            _folder = folder;
            if (!clip.Found)
            {
                ReleasePlayers();
                _clip = clip;
                SetState(VideoAvatarState.NoClip);
                return;
            }

            var busy = _state == VideoAvatarState.Loading || _state == VideoAvatarState.Playing;
            if (!forced && busy && clip.SameFiles(_clip))
                return;
            // A clip that already failed is not retried on its own. Reload or a new folder tries again.
            if (!forced && _state == VideoAvatarState.Failed && clip.SameFiles(_clip))
                return;

            Load(clip);
        }

        void Load(VideoAvatarClip clip)
        {
            ReleasePlayers();
            EnsureUi();
            _clip = clip;
            _error = string.Empty;
            _colorReady = false;
            _maskReady = clip.Mask != VideoMaskKind.MaskVideo;
            _loadingFor = 0f;

            if (clip.Mask == VideoMaskKind.MaskImage && !LoadMaskImage(clip.MaskPath))
            {
                Fail("The mask picture " + VideoAvatarFiles.FileName(clip.MaskPath) + " could not be read");
                return;
            }

            _color = CreatePlayer("VideoAvatarColor", clip.ColorPath);
            if (clip.Mask == VideoMaskKind.MaskVideo)
                _mask = CreatePlayer("VideoAvatarMask", clip.MaskPath);

            SetState(VideoAvatarState.Loading);
            _color.Prepare();
            if (_mask != null)
                _mask.Prepare();
        }

        VideoPlayer CreatePlayer(string objectName, string path)
        {
            var host = new GameObject(objectName);
            host.hideFlags = HideFlags.DontSave;
            host.transform.SetParent(transform, false);
            var player = host.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = path;
            player.isLooping = true;
            // The clip's sound is never routed anywhere, so a breathing track cannot be heard.
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.APIOnly;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.prepareCompleted += OnPrepared;
            player.errorReceived += OnError;
            return player;
        }

        bool LoadMaskImage(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                // Linear, so a mid-grey in the file stays mid-grey when the shader reads it.
                _maskImage = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                _maskImage.hideFlags = HideFlags.DontSave;
                _maskImage.wrapMode = TextureWrapMode.Clamp;
                if (ImageConversion.LoadImage(_maskImage, bytes, true))
                    return true;
            }
            catch (Exception)
            {
            }

            if (_maskImage != null)
                Destroy(_maskImage);
            _maskImage = null;
            return false;
        }

        void OnPrepared(VideoPlayer source)
        {
            if (source == null)
                return;
            if (source == _color)
                _colorReady = true;
            else if (source == _mask)
                _maskReady = true;
        }

        void OnError(VideoPlayer source, string message)
        {
            if (source == null || (source != _color && source != _mask))
                return;
            Fail(string.IsNullOrWhiteSpace(message) ? "The video decoder reported an error" : message);
        }

        void TickLoading(float delta)
        {
            _loadingFor += delta;
            if (_loadingFor > LoadTimeoutSeconds)
            {
                Fail("The clip did not start within " + Mathf.RoundToInt(LoadTimeoutSeconds) + " seconds");
                return;
            }

            if (_color == null || !_colorReady || !_maskReady)
                return;

            if (!_color.isPlaying)
            {
                _color.Play();
                if (_mask != null)
                    _mask.Play();
            }

            var texture = _color.texture;
            if (texture == null || texture.width <= 0 || texture.height <= 0)
                return;
            if (_mask != null && _mask.texture == null)
                return;

            ApplyMaterial();
            _image.texture = texture;
            _image.enabled = true;
            _placed = new Rect(0f, 0f, 0f, 0f);
            _rescanIn = 0f;
            SetState(VideoAvatarState.Playing);
            Debug.Log("VideoAvatar: playing " + VideoAvatarFiles.FileName(_clip.ColorPath)
                + " " + texture.width + "x" + texture.height + " mask=" + _clip.Mask);
        }

        void TickPlaying(CompanionSettings settings, float delta)
        {
            if (_color == null || _image == null)
            {
                Fail("The video player was removed");
                return;
            }

            var texture = _color.texture;
            if (texture != null && _image.texture != texture)
                _image.texture = texture;
            if (_mask != null && _material != null && _mask.texture != null)
                _material.SetTexture(MaskTexId, _mask.texture);

            EnsureCamera();
            Place(settings, _image.texture);
            SyncMask(delta);
            KeepModelHidden(delta);

            // The clip stands on the bottom edge of the window, so that edge is the ground line.
            // This runs after FootGrounding and before DesktopPresence reads the offset.
            if (_presence != null)
                _presence.SetFootOffset(0f);
        }

        void Place(CompanionSettings settings, Texture texture)
        {
            if (texture == null || _imageRect == null)
                return;

            var width = VideoAvatarLayout.DisplayWidth(texture.width, _clip.Mask);
            var rect = VideoAvatarLayout.Compute(
                Screen.width,
                Screen.height,
                width,
                texture.height,
                settings.VideoAvatarSize,
                settings.VideoAvatarOffsetX,
                settings.VideoAvatarOffsetY);
            if (rect == _placed)
                return;

            _placed = rect;
            _imageRect.anchoredPosition = new Vector2(rect.x, rect.y);
            _imageRect.sizeDelta = new Vector2(rect.width, rect.height);
        }

        void SyncMask(float delta)
        {
            if (_mask == null || _color == null)
                return;

            _resyncIn -= delta;
            if (_resyncIn > 0f)
                return;
            if (!VideoMaskSync.NeedsResync(_color.frame, _mask.frame, (long)_color.frameCount))
                return;

            _mask.frame = _color.frame;
            _resyncIn = ResyncCooldownSeconds;
        }

        void KeepModelHidden(float delta)
        {
            if (_mount == null)
                return;

            if (!_mount.ModelHidden)
            {
                _mount.SetModelHidden(true);
                _rescanIn = HideRescanSeconds;
                return;
            }

            // Parts such as the blush cheeks are added to the model after it loads.
            _rescanIn -= delta;
            if (_rescanIn > 0f)
                return;
            _rescanIn = HideRescanSeconds;
            _mount.ApplyModelHidden();
        }

        void ApplyMaterial()
        {
            if (_material == null)
            {
                var shader = Resources.Load<Shader>(ShaderResource);
                if (shader != null && shader.isSupported)
                {
                    _material = new Material(shader);
                    _material.hideFlags = HideFlags.DontSave;
                }
                else if (!_shaderWarned)
                {
                    _shaderWarned = true;
                    Debug.LogWarning("VideoAvatar: the VideoAvatar shader is missing or unsupported. The clip is shown without a mask.");
                }
            }

            _image.material = _material;
            if (_material == null)
                return;

            var colorRect = new Vector4(1f, 1f, 0f, 0f);
            var maskRect = new Vector4(1f, 1f, 0f, 0f);
            var source = 0f;
            var decode = 0f;
            Texture maskTexture = Texture2D.whiteTexture;
            // A video frame is decoded as sRGB. In a linear project that darkens the mask's
            // greys, so the shader puts them back. A mask picture is loaded linear already.
            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            switch (_clip.Mask)
            {
                case VideoMaskKind.MaskImage:
                    source = 1f;
                    if (_maskImage != null)
                        maskTexture = _maskImage;
                    break;
                case VideoMaskKind.MaskVideo:
                    source = 1f;
                    decode = linear ? 1f : 0f;
                    if (_mask != null && _mask.texture != null)
                        maskTexture = _mask.texture;
                    break;
                case VideoMaskKind.Packed:
                    source = 2f;
                    decode = linear ? 1f : 0f;
                    colorRect = new Vector4(0.5f, 1f, 0f, 0f);
                    maskRect = new Vector4(0.5f, 1f, 0.5f, 0f);
                    break;
            }

            _material.SetTexture(MaskTexId, maskTexture);
            _material.SetVector(ColorRectId, colorRect);
            _material.SetVector(MaskRectId, maskRect);
            _material.SetFloat(MaskSourceId, source);
            _material.SetFloat(MaskDecodeId, decode);
            _material.SetFloat(MaskLowId, MaskLow);
            _material.SetFloat(MaskHighId, MaskHigh);
        }

        void EnsureUi()
        {
            if (_canvasObject != null)
                return;

            _canvasObject = new GameObject("VideoAvatarCanvas");
            _canvasObject.transform.SetParent(transform, false);
            _canvasObject.hideFlags = HideFlags.DontSave;
            _canvas = _canvasObject.AddComponent<Canvas>();
            // Behind the portrait (40), the chat panel (50), settings (80), and the wizard (90).
            _canvas.sortingOrder = 5;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            // No GraphicRaycaster on purpose: the clip must not catch clicks as a rectangle.

            var picture = new GameObject("Clip");
            picture.transform.SetParent(_canvasObject.transform, false);
            _imageRect = picture.AddComponent<RectTransform>();
            _imageRect.anchorMin = new Vector2(0f, 0f);
            _imageRect.anchorMax = new Vector2(0f, 0f);
            _imageRect.pivot = new Vector2(0f, 0f);
            _image = picture.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _image.color = Color.white;
            _image.enabled = false;
            EnsureCamera();
        }

        void EnsureCamera()
        {
            if (_canvas == null)
                return;
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera && _canvas.worldCamera != null)
                return;

            var camera = Camera.main;
            if (camera == null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = camera;
            _canvas.planeDistance = 0.6f;
        }

        void EnsurePaths()
        {
            if (_paths)
                return;
            _paths = true;
            _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? string.Empty;
            _persistent = Application.persistentDataPath ?? string.Empty;
        }

        void Fail(string error)
        {
            _error = error ?? string.Empty;
            ReleasePlayers();
            SetState(VideoAvatarState.Failed);
            Debug.LogWarning("VideoAvatar: " + _status);
        }

        void SwitchOff()
        {
            ReleasePlayers();
            _requested = null;
            _clip = default(VideoAvatarClip);
            SetState(VideoAvatarState.Off);
        }

        void SetState(VideoAvatarState state)
        {
            _state = state;
            _status = VideoAvatarStatus.Describe(state, _folder, _clip, _error);
            if (_mount != null && !VideoAvatarStatus.HidesModel(state) && _mount.ModelHidden)
                _mount.SetModelHidden(false);
        }

        /// <summary>Stops decoding and hides the picture. The canvas and the material are kept for the next clip.</summary>
        void ReleasePlayers()
        {
            if (_image != null)
            {
                _image.enabled = false;
                _image.texture = null;
            }

            DestroyPlayer(_color);
            DestroyPlayer(_mask);
            _color = null;
            _mask = null;
            _colorReady = false;
            _maskReady = false;
            if (_maskImage != null)
                Destroy(_maskImage);
            _maskImage = null;
        }

        void DestroyPlayer(VideoPlayer player)
        {
            if (player == null)
                return;

            player.prepareCompleted -= OnPrepared;
            player.errorReceived -= OnError;
            player.Stop();
            Destroy(player.gameObject);
        }
    }
}
