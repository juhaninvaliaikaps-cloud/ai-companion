using UnityEngine;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>
    /// Geometry for the radial wheels. Angles are clockwise from the top, in degrees; slice 0 is
    /// centred on the top. Points are in pixels relative to the wheel centre with y up (uGUI).
    /// </summary>
    public static class RadialMath
    {
        /// <summary><see cref="SliceAt"/> result for the centre button.</summary>
        public const int Center = -2;
        /// <summary><see cref="SliceAt"/> result outside the wheel.</summary>
        public const int Outside = -1;

        public static float Step(int count)
        {
            return count <= 0 ? 360f : 360f / count;
        }

        /// <summary>Centre angle of slice <paramref name="index"/>.</summary>
        public static float CenterAngle(int index, int count)
        {
            return index * Step(count);
        }

        /// <summary>Angle of a point, clockwise from the top, 0..360.</summary>
        public static float AngleOf(Vector2 local)
        {
            var degrees = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
            if (degrees < 0f)
                degrees += 360f;
            return degrees;
        }

        /// <summary>Unit direction for an angle clockwise from the top.</summary>
        public static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }

        /// <summary>
        /// The slice under a point: an index, <see cref="Center"/> inside the centre button, or
        /// <see cref="Outside"/>. The thin gaps count as the nearest slice, so the pointer never
        /// flickers between slices.
        /// </summary>
        public static int SliceAt(Vector2 local, int count, float centerRadius, float innerRadius, float outerRadius)
        {
            var r = local.magnitude;
            if (r <= centerRadius)
                return Center;
            if (r > outerRadius || count <= 0)
                return Outside;
            if (r < innerRadius)
                return Outside;
            var step = Step(count);
            var index = Mathf.FloorToInt((AngleOf(local) + step * 0.5f) / step) % count;
            return index;
        }

        /// <summary>
        /// Wheel centre above her head, clamped so the whole wheel stays inside the window
        /// (and so on screen near monitor edges). Unity screen pixels, origin bottom-left.
        /// </summary>
        public static Vector2 PlaceAbove(Vector2 head, float clearance, float radius, float screenWidth, float screenHeight, float margin)
        {
            var center = new Vector2(head.x, head.y + clearance + radius);
            return Clamp(center, radius, screenWidth, screenHeight, margin);
        }

        /// <summary>
        /// A body-part wheel beside its anchor, on the side with more room, a little above it.
        /// </summary>
        public static Vector2 PlaceBeside(Vector2 anchor, float offset, float radius, float screenWidth, float screenHeight, float margin)
        {
            var right = anchor.x <= screenWidth * 0.5f;
            var center = new Vector2(anchor.x + (right ? offset : -offset), anchor.y + offset * 0.45f);
            return Clamp(center, radius, screenWidth, screenHeight, margin);
        }

        public static Vector2 Clamp(Vector2 center, float radius, float screenWidth, float screenHeight, float margin)
        {
            var minX = margin + radius;
            var maxX = screenWidth - margin - radius;
            var minY = margin + radius;
            var maxY = screenHeight - margin - radius;
            center.x = minX > maxX ? screenWidth * 0.5f : Mathf.Clamp(center.x, minX, maxX);
            center.y = minY > maxY ? screenHeight * 0.5f : Mathf.Clamp(center.y, minY, maxY);
            return center;
        }

        /// <summary>
        /// Wheel scale: the theme's DPI scale, but never so large that the wheel cannot fit in the window.
        /// </summary>
        public static float FitScale(float dpiScale, float outerRadius, float screenWidth, float screenHeight, float margin)
        {
            var room = Mathf.Min(screenWidth, screenHeight) - 2f * margin;
            if (outerRadius <= 0f || room <= 0f)
                return dpiScale;
            var fit = room / (2f * outerRadius);
            return Mathf.Max(0.5f, Mathf.Min(dpiScale, fit));
        }

        /// <summary>Where the connector line from the anchor meets the wheel's outer edge.</summary>
        public static Vector2 ConnectorEnd(Vector2 anchor, Vector2 center, float radius)
        {
            var delta = anchor - center;
            if (delta.sqrMagnitude < 0.0001f)
                return center;
            return center + delta.normalized * radius;
        }
    }
}
