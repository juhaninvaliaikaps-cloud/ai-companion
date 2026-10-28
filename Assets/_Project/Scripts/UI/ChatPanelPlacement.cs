using System;
using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>
    /// Puts the panel to the left of a stable avatar column, with a clear gap.
    /// The right side is used only when the left gutter cannot hold the panel.
    /// The result is clamped inside the screen. Callers clamp again to the
    /// visible window, which can be shorter than the screen where the taskbar overlaps.
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

        public static Rect Place(float screenWidth, float screenHeight, Rect avatarBounds, float gap, bool? preferLeft = null)
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
            // Stay on the side chosen last time while it still fits. Without this the panel
            // flips whenever the avatar drifts past the midpoint, which reads as jumping
            // while she walks along the desktop.
            var leftFits = leftUsable >= MinWidth;
            var rightFits = rightUsable >= MinWidth;

            bool useLeft;
            if (preferLeft.HasValue && (preferLeft.Value ? leftFits : rightFits))
                useLeft = preferLeft.Value;
            else if (leftFits && rightFits)
                useLeft = leftUsable >= rightUsable;
            else if (leftFits)
                useLeft = true;
            else if (rightFits)
                useLeft = false;
            else
                useLeft = leftUsable >= rightUsable;

            float x;
            float width;
            Fit(useLeft, screenWidth, avatarLeft, avatarRight, gap, useLeft ? leftRoom : rightRoom, useLeft ? leftUsable : rightUsable, out x, out width);

            var height = Height;
            var y = screenHeight - height - Margin;
            if (y < TopLimit)
                y = TopLimit;
            if (y + height > screenHeight - Margin)
                height = Mathf.Max(120f, screenHeight - Margin - y);

            return ClampInside(new Rect(x, y, width, height), new Rect(0f, 0f, screenWidth, screenHeight));
        }

        public const float BodyWidth = 140f;

        /// <summary>
        /// A fixed-width column around a root anchor. The anchor is kept inside
        /// the screen so a pose that projects outside cannot drag the panel with it.
        /// </summary>
        public static Rect Column(float anchorX, float width, float screenWidth, float screenHeight)
        {
            if (float.IsNaN(anchorX))
                anchorX = screenWidth * 0.5f;
            if (width < 1f)
                width = 1f;
            if (anchorX < 0f)
                anchorX = 0f;
            if (screenWidth > 0f && anchorX > screenWidth)
                anchorX = screenWidth;
            return new Rect(anchorX - width * 0.5f, 0f, width, Mathf.Max(1f, screenHeight));
        }

        public static float SmoothAnchor(
            float current,
            float target,
            float deltaSeconds,
            bool initialized,
            out bool nowInitialized,
            float deadZone = 0f,
            float maxSpeed = 0f)
        {
            nowInitialized = true;
            if (!initialized || float.IsNaN(current) || deltaSeconds <= 0f)
                return target;

            var delta = target - current;
            if (Math.Abs(delta) <= deadZone)
                return current;

            var blend = 1f - (float)Math.Exp(-8.0 * deltaSeconds);
            var step = delta * blend;
            if (maxSpeed > 0f)
            {
                var maxStep = maxSpeed * deltaSeconds;
                if (Math.Abs(step) > maxStep)
                    step = Math.Sign(step) * maxStep;
            }

            return current + step;
        }

        public static Rect ClampInside(Rect panel, Rect visible)
        {
            if (visible.width <= 0f || visible.height <= 0f)
                return panel;

            var margin = Margin;
            if (visible.width <= margin * 2f + 1f || visible.height <= margin * 2f + 1f)
                margin = 0f;

            var minX = visible.xMin + margin;
            var maxX = visible.xMax - margin;
            var minY = visible.yMin + margin;
            var maxY = visible.yMax - margin;
            if (maxX < minX)
            {
                minX = visible.xMin;
                maxX = visible.xMax;
            }

            if (maxY < minY)
            {
                minY = visible.yMin;
                maxY = visible.yMax;
            }

            var width = panel.width;
            var height = panel.height;
            var roomW = Mathf.Max(1f, maxX - minX);
            var roomH = Mathf.Max(1f, maxY - minY);
            if (width > roomW)
                width = roomW;
            if (height > roomH)
                height = roomH;

            var x = panel.x;
            var y = panel.y;
            if (x < minX)
                x = minX;
            if (x + width > maxX)
                x = maxX - width;
            if (y < minY)
                y = minY;
            if (y + height > maxY)
                y = maxY - height;
            return new Rect(x, y, width, height);
        }

        public static Rect Intersect(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return new Rect(a.x, a.y, 0f, 0f);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>Unity screen rects are bottom-left. Placement rects are top-left.</summary>
        public static Rect FromBottomLeft(Rect bottomLeft, float screenHeight)
        {
            var top = screenHeight - (bottomLeft.y + bottomLeft.height);
            return new Rect(bottomLeft.x, top, bottomLeft.width, bottomLeft.height);
        }

        /// <summary>
        /// Maps the part of the window that is inside the desktop work area
        /// (the area above the taskbar) into Unity screen pixels.
        /// Window and work rects are desktop pixels, origin top-left, y down.
        /// </summary>
        public static Rect VisibleGui(
            float screenWidth,
            float screenHeight,
            int windowLeft,
            int windowTop,
            int windowRight,
            int windowBottom,
            int workLeft,
            int workTop,
            int workRight,
            int workBottom)
        {
            var screen = new Rect(0f, 0f, Mathf.Max(1f, screenWidth), Mathf.Max(1f, screenHeight));
            var clientW = windowRight - windowLeft;
            var clientH = windowBottom - windowTop;
            if (clientW < 1 || clientH < 1)
                return screen;

            var left = Math.Max(windowLeft, workLeft);
            var top = Math.Max(windowTop, workTop);
            var right = Math.Min(windowRight, workRight);
            var bottom = Math.Min(windowBottom, workBottom);
            if (right <= left || bottom <= top)
                return screen;

            var scaleX = screen.width / clientW;
            var scaleY = screen.height / clientH;
            var rect = new Rect(
                (left - windowLeft) * scaleX,
                (top - windowTop) * scaleY,
                (right - left) * scaleX,
                (bottom - top) * scaleY);
            var hit = Intersect(rect, screen);
            if (hit.width < 1f || hit.height < 1f)
                return screen;
            return hit;
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
