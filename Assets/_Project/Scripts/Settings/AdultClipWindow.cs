using System;
using System.IO;
using AICompanion.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-188: a separate adult reaction window. It sits beside the avatar and the expression
    /// portrait instead of replacing either one. It only runs while the 18+ gate is open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdultClipWindow : MonoBehaviour
    {
        const string FolderName = "adult-animations";
        const string BreastFile = "breast_touch_clip_v3.mp4";
        const string ButtFile = "butt_squeeze_clip_v3.mp4";
        const float PanelSize = 280f;
        const float Margin = 18f;
        const float HideAfterSeconds = 7f;

        static AdultClipWindow _instance;

        GameObject _canvasObject;
        GameObject _panel;
        RawImage _image;
        VideoPlayer _video;
        RenderTexture _texture;
        float _hideAt;
        string _playing = string.Empty;

        public static bool IsAdultCue(string name)
        {
            var id = Normalize(name);
            return id == "touch_breast" || id == "breast_touch" || id == "breast_touch_clip_v3"
                || id == "touch_butt" || id == "butt_touch" || id == "butt_squeeze" || id == "butt_squeeze_clip_v3";
        }

        public static void Play(string cue)
        {
            if (!IntimateExpressions.Active || !IsAdultCue(cue))
                return;
            Ensure();
            if (_instance != null)
                _instance.Show(cue);
        }

        public static void Ensure()
        {
            if (_instance != null)
                return;
            var settings = FindFirstObjectByType<SettingsPanel>();
            if (settings == null)
                return;
            _instance = settings.gameObject.GetComponent<AdultClipWindow>();
            if (_instance == null)
                _instance = settings.gameObject.AddComponent<AdultClipWindow>();
        }

        void Awake()
        {
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_video != null)
            {
                _video.prepareCompleted -= OnPrepared;
                _video.loopPointReached -= OnEnded;
                _video.Stop();
            }
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        void Update()
        {
            if (!IntimateExpressions.Active)
            {
                Hide();
                return;
            }
            if (_panel == null || !_panel.activeSelf)
                return;
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
            if (id == "touch_breast" || id == "breast_touch" || id == "breast_touch_clip_v3")
                return BreastFile;
            if (id == "touch_butt" || id == "butt_touch" || id == "butt_squeeze" || id == "butt_squeeze_clip_v3")
                return ButtFile;
            return string.Empty;
        }

        void Show(string cue)
        {
            var file = FileFor(cue);
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
            _playing = cue;
            _hideAt = 0f;
            _video.url = new Uri(path).AbsoluteUri;
            _video.Prepare();
            Debug.Log("Adult clip window: playing " + file);
        }

        void OnPrepared(VideoPlayer player)
        {
            if (player == null || _panel == null || !_panel.activeSelf)
                return;
            _hideAt = Time.unscaledTime + HideAfterSeconds;
            player.Play();
        }

        void OnEnded(VideoPlayer player)
        {
            Hide();
        }

        void Hide()
        {
            if (_panel != null && _panel.activeSelf)
                _panel.SetActive(false);
            if (_video != null && _video.isPlaying)
                _video.Stop();
            _hideAt = 0f;
            _playing = string.Empty;
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
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.sortingOrder = 6; // above the video avatar, below the portrait and chat
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            _panel = new GameObject("Adult clip");
            _panel.transform.SetParent(_canvasObject.transform, false);
            var rect = _panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-Margin, Margin);
            rect.sizeDelta = new Vector2(PanelSize, PanelSize);

            var back = _panel.AddComponent<Image>();
            back.color = new Color(0.04f, 0.04f, 0.06f, 0.94f);
            back.raycastTarget = false;
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

            _texture = new RenderTexture(544, 544, 0, RenderTextureFormat.ARGB32);
            _texture.Create();
            _image.texture = _texture;

            _video = _panel.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.isLooping = false;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _texture;
            _video.audioOutputMode = VideoAudioOutputMode.None;
            _video.prepareCompleted += OnPrepared;
            _video.loopPointReached += OnEnded;
            _panel.SetActive(false);
        }
    }
}
