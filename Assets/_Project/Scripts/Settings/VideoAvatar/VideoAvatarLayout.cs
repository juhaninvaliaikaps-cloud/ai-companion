using System;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// Where the clip sits in the window. It is scaled to fit inside the window with its
    /// own aspect ratio, then made smaller by the size setting. It stands on the bottom
    /// edge, centred, and the two offsets move it by a share of the window.
    /// </summary>
    public static class VideoAvatarLayout
    {
        public const float MinSize = 0.2f;
        public const float MaxSize = 1f;
        public const float DefaultSize = 1f;
        public const float MaxOffsetX = 0.5f;
        public const float MaxOffsetY = 0.3f;

        public static float ClampSize(float size)
        {
            if (float.IsNaN(size) || float.IsInfinity(size))
                return DefaultSize;
            return Mathf.Clamp(size, MinSize, MaxSize);
        }

        public static float ClampOffsetX(float offset)
        {
            if (float.IsNaN(offset) || float.IsInfinity(offset))
                return 0f;
            return Mathf.Clamp(offset, -MaxOffsetX, MaxOffsetX);
        }

        public static float ClampOffsetY(float offset)
        {
            if (float.IsNaN(offset) || float.IsInfinity(offset))
                return 0f;
            return Mathf.Clamp(offset, -MaxOffsetY, MaxOffsetY);
        }

        /// <summary>A packed clip shows only its left half, so it is half as wide as the file.</summary>
        public static float DisplayWidth(float textureWidth, VideoMaskKind mask)
        {
            return mask == VideoMaskKind.Packed ? textureWidth * 0.5f : textureWidth;
        }

        /// <summary>
        /// The clip rectangle in window pixels, measured from the bottom-left corner.
        /// Width and height are zero when the window or the clip has no size yet.
        /// </summary>
        public static Rect Compute(float windowWidth, float windowHeight, float clipWidth, float clipHeight, float size, float offsetX, float offsetY)
        {
            if (!(windowWidth > 0f) || !(windowHeight > 0f) || !(clipWidth > 0f) || !(clipHeight > 0f))
                return new Rect(0f, 0f, 0f, 0f);

            var fit = Mathf.Min(windowWidth / clipWidth, windowHeight / clipHeight) * ClampSize(size);
            var width = clipWidth * fit;
            var height = clipHeight * fit;
            var x = (windowWidth - width) * 0.5f + ClampOffsetX(offsetX) * windowWidth;
            var y = ClampOffsetY(offsetY) * windowHeight;
            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// The rectangle for one clip of a set. The default idle (the reference) decides how
        /// big the character is in the window. Every other clip is drawn at the reference
        /// height times its own <paramref name="clipScale"/>, with its own aspect ratio, and
        /// is then shifted by its own offsets (shares of its height on screen). That keeps
        /// her size and her feet in one place when clips were rendered with different
        /// framing. A mirrored clip flips its sideways offset with it.
        /// </summary>
        public static Rect ComputeAligned(
            float windowWidth,
            float windowHeight,
            float referenceWidth,
            float referenceHeight,
            float clipWidth,
            float clipHeight,
            float size,
            float offsetX,
            float offsetY,
            float clipScale,
            float clipOffsetX,
            float clipOffsetY,
            bool mirrored)
        {
            var reference = Compute(windowWidth, windowHeight, referenceWidth, referenceHeight, size, offsetX, offsetY);
            if (!(reference.height > 0f) || !(clipWidth > 0f) || !(clipHeight > 0f))
                return new Rect(0f, 0f, 0f, 0f);

            var scale = clipScale > 0f && !float.IsNaN(clipScale) && !float.IsInfinity(clipScale) ? clipScale : 1f;
            var height = reference.height * scale;
            var width = height * (clipWidth / clipHeight);
            var centre = reference.x + reference.width * 0.5f;
            var shift = Finite(clipOffsetX) * height;
            var x = centre - width * 0.5f + (mirrored ? -shift : shift);
            var y = reference.y + Finite(clipOffsetY) * height;
            return new Rect(x, y, width, height);
        }

        /// <summary>Height in pixels of a clip drawn at <paramref name="clipScale"/> against the reference. Walking speed is a share of this.</summary>
        public static float DisplayedHeight(float windowWidth, float windowHeight, float referenceWidth, float referenceHeight, float size, float clipScale)
        {
            var reference = Compute(windowWidth, windowHeight, referenceWidth, referenceHeight, size, 0f, 0f);
            var scale = clipScale > 0f && !float.IsNaN(clipScale) && !float.IsInfinity(clipScale) ? clipScale : 1f;
            return reference.height * scale;
        }

        static float Finite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    /// <summary>
    /// A mask clip is a second decoder, so it can drift from the picture. This says when
    /// the mask is far enough off to be moved back onto the picture's frame.
    /// </summary>
    public static class VideoMaskSync
    {
        public const long ToleranceFrames = 1;

        public static bool NeedsResync(long colorFrame, long maskFrame, long frameCount)
        {
            // A decoder reports -1 until its first frame is ready.
            if (colorFrame < 0 || maskFrame < 0)
                return false;

            var apart = Math.Abs(colorFrame - maskFrame);
            // At the loop point one clip can be on the last frame and the other on the first.
            if (frameCount > 0 && frameCount - apart < apart)
                apart = frameCount - apart;
            return apart > ToleranceFrames;
        }
    }

    public enum VideoAvatarState
    {
        /// <summary>3D mode. The video avatar holds nothing.</summary>
        Off = 0,
        NoFolder = 1,
        NoClip = 2,
        Loading = 3,
        Playing = 4,
        Failed = 5,
    }

    /// <summary>What each state means for the 3D model, and the one line the settings panel shows.</summary>
    public static class VideoAvatarStatus
    {
        /// <summary>The 3D model is hidden only while a clip is really on screen, so a missing or broken clip never leaves an empty window.</summary>
        public static bool HidesModel(VideoAvatarState state)
        {
            return state == VideoAvatarState.Playing;
        }

        public static string Describe(VideoAvatarState state, string folder, VideoAvatarClip clip, string error)
        {
            switch (state)
            {
                case VideoAvatarState.NoFolder:
                    return "Video folder not found: " + Shown(folder) + ". Showing the 3D avatar.";
                case VideoAvatarState.NoClip:
                    return "No idle.webm or idle.mp4 in " + Shown(folder) + ". Showing the 3D avatar.";
                case VideoAvatarState.Loading:
                    return "Loading " + VideoAvatarFiles.FileName(clip.ColorPath) + "...";
                case VideoAvatarState.Playing:
                    return "Playing " + VideoAvatarFiles.FileName(clip.ColorPath) + ". " + MaskText(clip);
                case VideoAvatarState.Failed:
                    return "Could not play " + VideoAvatarFiles.FileName(clip.ColorPath) + ". "
                        + (string.IsNullOrWhiteSpace(error) ? "Unknown error" : error.Trim())
                        + ". Showing the 3D avatar.";
                default:
                    return "3D avatar.";
            }
        }

        /// <summary>The status line while a set of clips plays: the default idle, what else is loaded, and any clip that was left out.</summary>
        public static string DescribeSet(VideoAvatarClipSet set)
        {
            if (set == null || !set.Found)
                return "3D avatar.";

            var idle = set.Clips[set.Idle];
            var text = "Playing " + VideoAvatarFiles.FileName(idle.ColorPath) + ". "
                + MaskText(new VideoAvatarClip(idle.Name, idle.ColorPath, idle.MaskPath, idle.Mask));
            text += " Idle variations: " + set.Variations.Count + ".";
            text += " Walk: " + (set.Walk >= 0 ? set.Clips[set.Walk].Name : "none") + ".";
            text += " Walk-stop: " + (set.WalkStop >= 0 ? set.Clips[set.WalkStop].Name : "none") + ".";
            if (set.SideIdle >= 0)
                text += " Side idle: " + set.Clips[set.SideIdle].Name + ".";
            text += set.FromManifest ? " Clips are listed in " + VideoAvatarManifest.FileName + "." : " Clips were found by file name.";
            for (var i = 0; i < set.Warnings.Count; i++)
                text += " " + set.Warnings[i];
            return text;
        }

        static string MaskText(VideoAvatarClip clip)
        {
            switch (clip.Mask)
            {
                case VideoMaskKind.MaskVideo:
                case VideoMaskKind.MaskImage:
                    return "Transparency from " + VideoAvatarFiles.FileName(clip.MaskPath) + ".";
                case VideoMaskKind.Packed:
                    return "Transparency from the right half of the clip.";
                default:
                    return "No mask file, so transparency comes from the clip's own alpha. A clip without alpha shows its whole background.";
            }
        }

        static string Shown(string folder)
        {
            return string.IsNullOrWhiteSpace(folder) ? "(none)" : folder;
        }
    }
}
