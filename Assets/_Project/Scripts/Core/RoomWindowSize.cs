using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-226: the room window is square and resizable (Jussi), at least <see cref="Min"/>
    /// and at most the height of the work area. Clicks are turned into points on the room
    /// picture, so walking works at any size.
    /// </summary>
    public static class RoomWindowSize
    {
        public const float Min = 480f;
        public const float Default = 720f;

        /// <summary>The side of the square window for a wanted size on a work area this tall.</summary>
        public static float Clamp(float wanted, float workAreaHeight)
        {
            var max = workAreaHeight > 0f ? workAreaHeight : Default;
            var min = Mathf.Min(Min, max);
            if (float.IsNaN(wanted) || wanted <= 0f)
                wanted = Default;
            return Mathf.Clamp(wanted, min, max);
        }

        /// <summary>
        /// A point in the room area (left and top of the area, its side length, the point, all in
        /// the same pixels with y down) as a fraction of the room picture. False outside the area.
        /// </summary>
        public static bool TryToPicture(float left, float top, float side, float x, float y, out Vector2 point)
        {
            point = default(Vector2);
            if (side <= 0f)
                return false;
            var u = (x - left) / side;
            var v = (y - top) / side;
            if (u < 0f || v < 0f || u > 1f || v > 1f)
                return false;
            point = new Vector2(u, v);
            return true;
        }
    }
}
