using System;
using System.Collections.Generic;
using System.IO;

namespace AICompanion.Settings
{
    /// <summary>Which avatar is on screen. The 3D humanoid is the default.</summary>
    public static class AvatarModeNames
    {
        public const string ThreeD = "3d";
        public const string Video = "video";

        public static string Normalize(string name)
        {
            return IsVideo(name) ? Video : ThreeD;
        }

        public static bool IsVideo(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && string.Equals(name.Trim(), Video, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Clip names the video avatar knows. Only idle is played today. A later talking or
    /// reaction clip is another name here and another file in the same folder.
    /// </summary>
    public static class VideoAvatarClipNames
    {
        public const string Idle = "idle";
    }

    /// <summary>
    /// The reaction clip that goes with an emotion: the emotion's name in lower case, so
    /// "happy.mp4" plays for <see cref="AICompanion.Core.Emotion.Happy"/>. A folder without
    /// that clip simply has no reaction for it.
    /// </summary>
    public static class VideoReactionNames
    {
        static readonly string[] Names =
        {
            null, "happy", "sad", "surprised", "thinking", "embarrassed", "shy", "flustered", "concerned",
        };

        /// <summary>The clip name for an emotion, or null for neutral and for a value this list does not know.</summary>
        public static string For(AICompanion.Core.Emotion emotion)
        {
            var index = (int)emotion;
            return index >= 0 && index < Names.Length ? Names[index] : null;
        }
    }

    /// <summary>Where a clip's transparency comes from.</summary>
    public enum VideoMaskKind
    {
        /// <summary>The clip's own alpha channel. A clip without one is fully opaque.</summary>
        VideoAlpha = 0,

        /// <summary>A still picture next to the clip. White shows her, black is see-through.</summary>
        MaskImage = 1,

        /// <summary>A second clip of the same length. White shows her, black is see-through.</summary>
        MaskVideo = 2,

        /// <summary>One clip with the picture in its left half and the mask in its right half.</summary>
        Packed = 3,
    }

    /// <summary>The files that make up one clip. <see cref="Found"/> is false when there is no picture file.</summary>
    public readonly struct VideoAvatarClip
    {
        public string Name { get; }
        public string ColorPath { get; }
        public string MaskPath { get; }
        public VideoMaskKind Mask { get; }

        public VideoAvatarClip(string name, string colorPath, string maskPath, VideoMaskKind mask)
        {
            Name = name ?? string.Empty;
            ColorPath = colorPath ?? string.Empty;
            MaskPath = maskPath ?? string.Empty;
            Mask = mask;
        }

        public bool Found
        {
            get { return !string.IsNullOrEmpty(ColorPath); }
        }

        public bool SameFiles(VideoAvatarClip other)
        {
            return Mask == other.Mask
                && string.Equals(ColorPath, other.ColorPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(MaskPath, other.MaskPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Finds a clip in a folder listing by file name. For the clip "idle":
    /// idle.webm or idle.mp4 is the picture, idle.mask.mp4 or idle.mask.png is the
    /// mask, and idle.sbs.mp4 is a packed clip with both halves in one file.
    /// </summary>
    public static class VideoAvatarFiles
    {
        public const string MaskSuffix = ".mask";
        public const string PackedSuffix = ".sbs";

        // WebM is first because it is the format that can carry its own alpha.
        static readonly string[] VideoExtensions = { ".webm", ".mp4", ".mov", ".m4v" };
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

        public static VideoAvatarClip Find(IList<string> filePaths, string clipName)
        {
            var name = string.IsNullOrWhiteSpace(clipName) ? string.Empty : clipName.Trim().ToLowerInvariant();
            if (filePaths == null || name.Length == 0)
                return new VideoAvatarClip(name, null, null, VideoMaskKind.VideoAlpha);

            var color = First(filePaths, name, VideoExtensions);
            var maskVideo = First(filePaths, name + MaskSuffix, VideoExtensions);
            var maskImage = First(filePaths, name + MaskSuffix, ImageExtensions);
            var packed = First(filePaths, name + PackedSuffix, VideoExtensions);

            if (color != null && maskVideo != null)
                return new VideoAvatarClip(name, color, maskVideo, VideoMaskKind.MaskVideo);
            if (color != null && maskImage != null)
                return new VideoAvatarClip(name, color, maskImage, VideoMaskKind.MaskImage);
            if (packed != null)
                return new VideoAvatarClip(name, packed, null, VideoMaskKind.Packed);
            return new VideoAvatarClip(name, color, null, VideoMaskKind.VideoAlpha);
        }

        static string First(IList<string> filePaths, string stem, string[] extensions)
        {
            for (var e = 0; e < extensions.Length; e++)
            {
                var wanted = stem + extensions[e];
                for (var i = 0; i < filePaths.Count; i++)
                {
                    var path = filePaths[i];
                    if (string.IsNullOrEmpty(path))
                        continue;
                    if (string.Equals(FileName(path), wanted, StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }

            return null;
        }

        /// <summary>The last path segment. Both slash kinds count, so a Windows path reads the same on any machine.</summary>
        public static string FileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            var cut = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            return cut < 0 ? path : path.Substring(cut + 1);
        }
    }

    /// <summary>
    /// The clip folder. A folder named in settings wins when it exists. Otherwise the
    /// standard one, %APPDATA%\ProjectLeia\video-avatar. Clips never come from the network.
    /// </summary>
    public static class VideoAvatarFolders
    {
        public const string FolderName = "video-avatar";

        public static string Primary(string applicationData, string persistentDataPath)
        {
            var root = SettingsLocations.Folder(applicationData, persistentDataPath);
            if (string.IsNullOrEmpty(root))
                return null;
            return Path.Combine(root, FolderName);
        }

        public static string Select(string requested, string applicationData, string persistentDataPath, Func<string, bool> directoryExists)
        {
            if (directoryExists == null)
                return null;

            if (!string.IsNullOrWhiteSpace(requested))
            {
                var trimmed = requested.Trim();
                if (!LooksRemote(trimmed) && directoryExists(trimmed))
                    return trimmed;
            }

            var primary = Primary(applicationData, persistentDataPath);
            if (!string.IsNullOrEmpty(primary) && directoryExists(primary))
                return primary;
            return null;
        }

        /// <summary>True for an address such as https://... A clip folder is always a local path.</summary>
        public static bool LooksRemote(string path)
        {
            return !string.IsNullOrEmpty(path) && path.IndexOf("://", StringComparison.Ordinal) >= 0;
        }
    }
}
