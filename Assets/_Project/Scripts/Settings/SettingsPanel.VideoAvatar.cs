using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    public partial class SettingsPanel
    {
        Text _videoStatus;
        string _videoStatusShown;

        /// <summary>
        /// The video avatar is added here, not in the scene file, so 3D mode costs one
        /// idle component and the scene does not change.
        /// </summary>
        void EnsureVideoAvatar()
        {
            if (GetComponent<VideoAvatar>() == null)
                gameObject.AddComponent<VideoAvatar>();
        }

        void BuildVideoAvatar()
        {
            _videoStatus = null;
            _videoStatusShown = null;
            var video = AvatarModeNames.IsVideo(_settings.AvatarMode);
            AddButton(video ? "Avatar: 3D model" : "Avatar: 3D model (selected)", () => SetAvatarMode(AvatarModeNames.ThreeD));
            AddButton(video ? "Avatar: 2D video (selected)" : "Avatar: 2D video", () => SetAvatarMode(AvatarModeNames.Video));
            if (!video)
            {
                Note("2D video loops idle.mp4 or idle.webm from %APPDATA%\\ProjectLeia\\video-avatar. The 3D avatar stays loaded.");
                return;
            }

            var player = GetComponent<VideoAvatar>();
            _videoStatusShown = player != null ? player.StatusText : "The video avatar is not running.";
            _videoStatus = CreateText(_body, _font, 12, TextAnchor.UpperLeft, _videoStatusShown);
            _videoStatus.fontStyle = FontStyle.Italic;
            _videoStatus.color = new Color(0.80f, 0.76f, 0.86f, 1f);
            _videoStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            _videoStatus.verticalOverflow = VerticalWrapMode.Overflow;
            _videoStatus.raycastTarget = false;
            Place(_videoStatus.rectTransform, 50f);

            AddField("Video folder (empty uses the default)", _settings.VideoAvatarFolder, false, value =>
            {
                _settings.VideoAvatarFolder = value;
                ApplyLive();
                _rebuild = true;
            });
            AddButton("Reload video", () =>
            {
                var current = GetComponent<VideoAvatar>();
                if (current != null)
                    current.Reload();
                SetStatus("Looking for the clip again.");
            });
            Note("Put idle.mp4 with idle.mask.mp4 or idle.mask.png in the folder, or one idle.webm that has alpha. The clip's sound is never played.");

            var sizeLabel = AddLabel(VideoSizeText(_settings.VideoAvatarSize));
            AddSlider(_settings.VideoAvatarSize, VideoAvatarLayout.MinSize, VideoAvatarLayout.MaxSize, false, value =>
            {
                _settings.VideoAvatarSize = value;
                sizeLabel.text = VideoSizeText(value);
                ApplyLive();
            });
            var sideLabel = AddLabel(VideoSideText(_settings.VideoAvatarOffsetX));
            AddSlider(_settings.VideoAvatarOffsetX, -VideoAvatarLayout.MaxOffsetX, VideoAvatarLayout.MaxOffsetX, false, value =>
            {
                _settings.VideoAvatarOffsetX = value;
                sideLabel.text = VideoSideText(value);
                ApplyLive();
            });
            var heightLabel = AddLabel(VideoHeightText(_settings.VideoAvatarOffsetY));
            AddSlider(_settings.VideoAvatarOffsetY, -VideoAvatarLayout.MaxOffsetY, VideoAvatarLayout.MaxOffsetY, false, value =>
            {
                _settings.VideoAvatarOffsetY = value;
                heightLabel.text = VideoHeightText(value);
                ApplyLive();
            });
            AddButton("Reset video size and position", () =>
            {
                _settings.VideoAvatarSize = VideoAvatarLayout.DefaultSize;
                _settings.VideoAvatarOffsetX = 0f;
                _settings.VideoAvatarOffsetY = 0f;
                ApplyLive();
                _rebuild = true;
            });
            Note("Size and position apply immediately. The window size is on the Overlay tab. The settings below are for the 3D avatar.");
        }

        void SetAvatarMode(string mode)
        {
            _settings.AvatarMode = mode;
            ApplyLive();
            _rebuild = true;
        }

        /// <summary>Keeps the status line current while the panel is open, for example from "Loading" to "Playing".</summary>
        void TickVideoAvatarStatus()
        {
            if (_videoStatus == null)
                return;

            var player = GetComponent<VideoAvatar>();
            if (player == null)
                return;

            var text = player.StatusText;
            if (ReferenceEquals(text, _videoStatusShown))
                return;

            _videoStatusShown = text;
            _videoStatus.text = text;
        }

        static string VideoSizeText(float size)
        {
            return "Video size " + Mathf.RoundToInt(size * 100f) + "%";
        }

        static string VideoSideText(float offset)
        {
            return "Video left or right " + Mathf.RoundToInt(offset * 100f) + "%";
        }

        static string VideoHeightText(float offset)
        {
            return "Video up or down " + Mathf.RoundToInt(offset * 100f) + "%";
        }
    }
}
