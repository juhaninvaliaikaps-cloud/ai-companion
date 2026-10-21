using System;
using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>
    /// Puts the panel to the left of the avatar's screen bounds, with a clear gap.
    /// The right side is used only when the left gutter cannot hold the panel.
    /// Rects are GUI space: origin at the top left, y growing downward.
    /// </summary>
    public static class ChatPanelPlacement
    {
        public const float Width = 220f;
        public const float Height = 340f;
        public const float Margin = 8f;
        public const float TopLimit = 124f;
        public const float DefaultGap = 40f;
        public const float MinWidth = 96f;

        public static Rect Place(float screenWidth, float screenHeight, Rect avatarBounds, float gap)
        {
            if (float.IsNaN(gap) || gap < 0f)
                gap = 0f;
            if (screenWidth < Margin * 2f + 1f)
                screenWidth = Margin * 2f + Width;
            if (screenHeight < Margin * 2f + 1f)
                screenHeight = Margin * 2f + Height;

            var avatarLeft = Mathf.Min(avatarBounds.xMin, avatarBounds.xMax);
            var avatarRight = Mathf.Max(avatarBounds.xMin, avatarBounds.xMax);
            var leftRoom = Mathf.Max(0f, avatarLeft - Margin);
            var rightRoom = Mathf.Max(0f, screenWidth - Margin - avatarRight);
            var leftUsable = leftRoom - gap;
            var rightUsable = rightRoom - gap;
            var useLeft = leftUsable >= MinWidth || (rightUsable < MinWidth && leftUsable >= rightUsable);

            float x;
            float width;
            Fit(useLeft, screenWidth, avatarLeft, avatarRight, gap, useLeft ? leftRoom : rightRoom, useLeft ? leftUsable : rightUsable, out x, out width);

            var height = Height;
            var y = screenHeight - height - Margin;
            if (y < TopLimit)
                y = TopLimit;
            if (y + height > screenHeight - Margin)
                height = Mathf.Max(120f, screenHeight - Margin - y);

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// Folds one Unity screen point (origin bottom left) into a GUI-space min/max.
        /// Points behind the camera are ignored. Min starts at positive infinity and max at negative infinity.
        /// </summary>
        public static bool TryIncludeScreenPoint(float screenHeight, Vector3 unityScreen, ref Vector2 min, ref Vector2 max)
        {
            if (unityScreen.z <= 0f)
                return false;

            var x = unityScreen.x;
            var y = screenHeight - unityScreen.y;
            if (x < min.x)
                min.x = x;
            if (y < min.y)
                min.y = y;
            if (x > max.x)
                max.x = x;
            if (y > max.y)
                max.y = y;
            return true;
        }

        public static void WriteBoundsCorners(Bounds bounds, Vector3[] corners)
        {
            if (corners == null || corners.Length < 8)
                throw new ArgumentException("Need eight corners.", "corners");

            var center = bounds.center;
            var extents = bounds.extents;
            var index = 0;
            for (var xi = -1; xi <= 1; xi += 2)
            {
                for (var yi = -1; yi <= 1; yi += 2)
                {
                    for (var zi = -1; zi <= 1; zi += 2)
                    {
                        corners[index++] = center + new Vector3(extents.x * xi, extents.y * yi, extents.z * zi);
                    }
                }
            }
        }

        static void Fit(bool left, float screenWidth, float avatarLeft, float avatarRight, float gap, float room, float usable, out float x, out float width)
        {
            if (usable > 0f)
            {
                width = Mathf.Min(Width, usable);
                x = left ? avatarLeft - gap - width : avatarRight + gap;
                return;
            }

            width = Mathf.Min(Width, room);
            if (width < 1f)
                width = 1f;
            x = left ? Margin : screenWidth - Margin - width;
        }
    }
}
