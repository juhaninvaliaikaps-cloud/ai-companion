using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// A small frame beside the head, in GUI space (origin at the top left).
    /// The debug-label rect matches the overlay label so the portrait does not cover it.
    /// </summary>
    public static class PortraitPlacement
    {
        public const float Width = 128f;
        public const float Height = 144f;
        public const float Gap = 10f;
        public const float Margin = 8f;
        public const float Border = 4f;

        /// <summary>Movement smaller than this never moves the frame, so a pose cannot make it jitter.</summary>
        public const float DeadZone = 24f;

        public static readonly Rect DebugLabel = new Rect(8f, 8f, 360f, 108f);

        public static float InnerWidth
        {
            get { return Width - Border * 2f; }
        }

        public static float InnerHeight
        {
            get { return Height - Border * 2f; }
        }

        public static Rect Beside(Vector2 head, float screenWidth, float screenHeight, Rect avoidA, Rect avoidB, bool? preferLeft = null)
        {
            if (float.IsNaN(head.x) || float.IsNaN(head.y))
                head = new Vector2(screenWidth * 0.62f, screenHeight * 0.34f);
            if (screenWidth < Width + Margin * 2f)
                screenWidth = Width + Margin * 2f;
            if (screenHeight < Height + Margin * 2f)
                screenHeight = Height + Margin * 2f;

            var y = head.y - Height * 0.72f;
            var right = Clamp(new Rect(head.x + Gap, y, Width, Height), screenWidth, screenHeight);
            var left = Clamp(new Rect(head.x - Width - Gap, y, Width, Height), screenWidth, screenHeight);
            var rightBlocked = Overlaps(right, avoidA) || Overlaps(right, avoidB);
            var leftBlocked = Overlaps(left, avoidA) || Overlaps(left, avoidB);

            // The side used to come from the midpoint on every frame, so walking across
            // the middle flipped the frame left and right. Keep the remembered side unless
            // the head is clearly past the middle on the other side or that side is blocked.
            const float flipMargin = 64f;
            var midpoint = screenWidth * 0.5f;
            var wantLeft = preferLeft ?? head.x > midpoint;
            if (wantLeft && head.x < midpoint - flipMargin)
                wantLeft = false;
            else if (!wantLeft && head.x > midpoint + flipMargin)
                wantLeft = true;

            var preferred = wantLeft ? left : right;
            var preferredBlocked = wantLeft ? leftBlocked : rightBlocked;
            var other = wantLeft ? right : left;
            var otherBlocked = wantLeft ? rightBlocked : leftBlocked;

            if (!preferredBlocked)
                return preferred;
            if (!otherBlocked)
                return other;

            var corner = Clamp(new Rect(screenWidth - Width - Margin, DebugLabel.yMax + Margin, Width, Height), screenWidth, screenHeight);
            if (!Overlaps(corner, avoidA) && !Overlaps(corner, avoidB))
                return corner;
            return right;
        }

        public static Rect Smooth(Rect current, Rect target, float deltaSeconds, bool initialized, float deadZone = 0f)
        {
            if (!initialized || deltaSeconds <= 0f || float.IsNaN(deltaSeconds))
                return target;

            var dx = target.x - current.x;
            var dy = target.y - current.y;
            if (Mathf.Abs(dx) <= deadZone && Mathf.Abs(dy) <= deadZone)
                return new Rect(current.x, current.y, target.width, target.height);

            var blend = 1f - (float)System.Math.Exp(-10.0 * deltaSeconds);
            return new Rect(
                current.x + dx * blend,
                current.y + dy * blend,
                target.width,
                target.height);
        }

        public static bool Overlaps(Rect a, Rect b)
        {
            if (a.width <= 0f || a.height <= 0f || b.width <= 0f || b.height <= 0f)
                return false;
            return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        }

        public static Rect Clamp(Rect rect, float screenWidth, float screenHeight)
        {
            var x = rect.x;
            var y = rect.y;
            var maxX = screenWidth - Margin - rect.width;
            var maxY = screenHeight - Margin - rect.height;
            if (maxX < Margin)
                maxX = 0f;
            if (maxY < Margin)
                maxY = 0f;
            if (x < Margin)
                x = Margin;
            if (y < Margin)
                y = Margin;
            if (x > maxX)
                x = maxX;
            if (y > maxY)
                y = maxY;
            if (x < 0f)
                x = 0f;
            if (y < 0f)
                y = 0f;
            return new Rect(x, y, rect.width, rect.height);
        }
    }
}
