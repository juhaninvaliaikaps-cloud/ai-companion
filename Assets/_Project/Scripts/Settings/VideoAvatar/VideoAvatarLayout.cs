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

        /// <summary>UV scale (x, y) and offset (z, w) for the picture half and the mask half of a packed clip.</summary>
        public readonly struct PackedUv
        {
            public Vector4 Color { get; }
            public Vector4 Mask { get; }

            public PackedUv(Vector4 color, Vector4 mask)
            {
                Color = color;
                Mask = mask;
            }
        }

        /// <summary>
        /// Where the shader reads a packed clip: the picture from the left half and the mask
        /// from the right half, each kept half a texel away from its own edges so the two
        /// halves never blend into each other. <paramref name="texelWidth"/> is one divided
        /// by the file's width in pixels. A mirrored clip reads each half right to left.
        /// </summary>
        public static PackedUv PackedRects(float texelWidth, bool mirrored)
        {
            var texel = texelWidth > 0f && texelWidth < 0.25f ? texelWidth : 0f;
            var width = 0.5f - texel;
            var inset = texel * 0.5f;
            if (!mirrored)
            {
                return new PackedUv(
                    new Vector4(width, 1f, inset, 0f),
                    new Vector4(width, 1f, 0.5f + inset, 0f));
            }

            return new PackedUv(
                new Vector4(-width, 1f, 0.5f - inset, 0f),
                new Vector4(-width, 1f, 1f - inset, 0f));
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
    /// <summary>
    /// Counts how well a clip's mask kept to its picture, once per picture frame.
    /// A frame is measured only when both decoders have settled: no seek in flight and
    /// a frame from this run. Frames that cannot be measured are counted as skipped, so a
    /// rewind or a resync is never reported as drift. A packed clip has one decoder: its
    /// frames are counted and are in step by construction.
    /// </summary>
    public sealed class VideoSyncCounter
    {
        long _lastFrame = long.MinValue;

        public int Frames { get; private set; }
        public int InStep { get; private set; }
        public int OffByOne { get; private set; }
        public int OffByMore { get; private set; }
        public int Skipped { get; private set; }
        public int Seeks { get; private set; }

        /// <summary>A new run starts: the next frame is counted even if its number repeats the last run's.</summary>
        public void Restart()
        {
            _lastFrame = long.MinValue;
        }

        /// <summary>
        /// One app frame. True when a new picture frame was measured; then
        /// <paramref name="offset"/> is the mask's offset in frames. Repeated picture
        /// frames (the app draws faster than the clip plays) are ignored.
        /// </summary>
        public bool Sample(long colorFrame, long maskFrame, long frameCount, bool settled, out long offset)
        {
            offset = 0;
            if (colorFrame < 0 || colorFrame == _lastFrame)
                return false;

            _lastFrame = colorFrame;
            if (!settled || maskFrame < 0)
            {
                Skipped++;
                return false;
            }

            offset = VideoMaskSync.SignedOffset(colorFrame, maskFrame, frameCount);
            Frames++;
            if (offset == 0)
                InStep++;
            else if (offset == 1 || offset == -1)
                OffByOne++;
            else
                OffByMore++;
            return true;
        }

        public void CountSeek()
        {
            Seeks++;
        }

        public bool HasData
        {
            get { return Frames + Skipped > 0; }
        }

        /// <summary>
        /// One log line, then a new count. Null when nothing was counted. <paramref name="files"/>
        /// names the file or files the clip was read from.
        /// </summary>
        public string Report(string name, string files, int decoders)
        {
            if (!HasData)
                return null;

            string line;
            if (decoders < 2)
            {
                line = "sync " + name + " file=" + files + " decoders=1 frames=" + Frames
                    + " skipped=" + Skipped + " (one decoder: the mask is always on the picture's frame)";
            }
            else
            {
                line = "sync " + name + " file=" + files + " decoders=2 frames=" + Frames
                    + " inStep=" + Percent(InStep, Frames)
                    + " offByOne=" + Percent(OffByOne, Frames)
                    + " offByMore=" + Percent(OffByMore, Frames)
                    + " skipped=" + Skipped
                    + " seeks=" + Seeks;
            }

            Frames = 0;
            InStep = 0;
            OffByOne = 0;
            OffByMore = 0;
            Skipped = 0;
            Seeks = 0;
            return line;
        }

        static string Percent(int part, int whole)
        {
            if (whole <= 0)
                return "n/a";
            return (100f * part / whole).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }
    }

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

        /// <summary>The mask plays this much faster while it is one frame behind the picture.</summary>
        public const float CatchUpSpeed = 1.08f;

        /// <summary>The mask plays this much slower while it is one frame ahead of the picture.</summary>
        public const float FallBackSpeed = 0.92f;

        /// <summary>
        /// Mask frame minus picture frame, taking the loop point into account: a mask on
        /// frame 0 and a picture on the last frame is +1, not minus the clip length.
        /// 0 when either decoder has no frame yet.
        /// </summary>
        public static long SignedOffset(long colorFrame, long maskFrame, long frameCount)
        {
            if (colorFrame < 0 || maskFrame < 0)
                return 0;

            var offset = maskFrame - colorFrame;
            if (frameCount > 0)
            {
                if (offset > frameCount / 2)
                    offset -= frameCount;
                else if (offset < -(frameCount / 2))
                    offset += frameCount;
            }

            return offset;
        }

        /// <summary>
        /// Playback speed for the mask. A mask one frame off is not worth a seek, which
        /// would stall it for longer than that. It is nudged instead until the frame
        /// numbers match again. A larger offset is left at normal speed for the seek.
        /// </summary>
        public static float MaskSpeed(long offset)
        {
            if (offset == -1)
                return CatchUpSpeed;
            if (offset == 1)
                return FallBackSpeed;
            return 1f;
        }
    }

    /// <summary>Which head the expression portrait is placed beside.</summary>
    public enum PortraitAnchorSource
    {
        /// <summary>The 3D model's head bone. 3D mode, and 2D mode that fell back to 3D.</summary>
        Model3D = 0,

        /// <summary>The 2D video avatar's head, from the idle's framing. Steady while she walks.</summary>
        Video = 1,

        /// <summary>The 3D model is hidden and the video's head is not known yet: a fixed point.</summary>
        Fixed = 2,
    }

    /// <summary>
    /// The expression portrait shows in both avatar modes, beside the head of the avatar
    /// that is on screen. In 2D mode the hidden 3D model keeps playing its own animations,
    /// so its head bone wanders around the window; following it put the portrait on her body
    /// once she walked. The video's head is taken from the default idle's rectangle, which
    /// does not change between clips, so the portrait keeps one place in the window.
    /// </summary>
    public static class PortraitAnchor
    {
        /// <summary>Head centre across the idle frame, measured on the delivered idle: 0.498.</summary>
        public const float DefaultHeadX = 0.5f;

        /// <summary>Head centre down from the top of the idle frame, measured on the delivered idle: 0.149 to 0.155.</summary>
        public const float DefaultHeadY = 0.15f;

        public static PortraitAnchorSource Source(bool modelHidden, bool videoHeadKnown)
        {
            if (!modelHidden)
                return PortraitAnchorSource.Model3D;
            return videoHeadKnown ? PortraitAnchorSource.Video : PortraitAnchorSource.Fixed;
        }

        /// <summary>
        /// The video avatar's head in window pixels from the top-left (as the portrait uses),
        /// from the idle's rectangle in pixels from the bottom-left (as the video canvas uses).
        /// <paramref name="headX"/> and <paramref name="headY"/> are shares of the frame, from
        /// its left and its top. A value outside 0..1 uses the default.
        /// </summary>
        public static Vector2 VideoHead(Rect idleRect, float windowHeight, float headX, float headY)
        {
            var x = headX > 0f && headX < 1f ? headX : DefaultHeadX;
            var y = headY > 0f && headY < 1f ? headY : DefaultHeadY;
            var fromBottom = idleRect.y + idleRect.height * (1f - y);
            return new Vector2(idleRect.x + idleRect.width * x, windowHeight - fromBottom);
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

        public const string PairWarning =
            "Picture and mask are two decoders, so her outline can slip by a frame. A folder of packed .sbs.mp4 clips cannot.";

        static string MaskText(VideoAvatarClip clip)
        {
            switch (clip.Mask)
            {
                case VideoMaskKind.MaskVideo:
                    // Said on screen because a folder of pairs looks like a worse build: the
                    // two decoders drift on the walk and the outline slips (JUH-99, 2026-11-12).
                    return "Transparency from " + VideoAvatarFiles.FileName(clip.MaskPath) + ". "
                        + PairWarning;
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
