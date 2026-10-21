using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// A perspective camera at identity rotation. It looks along world +Z.
    /// </summary>
    public readonly struct CameraPose
    {
        public CameraPose(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public Vector3 Position => new Vector3(X, Y, Z);
    }

    /// <summary>
    /// Full-body framing and zoom-to-cursor. The camera never tilts. Zoom 0 is the
    /// fitted pose, and zooming out cannot pull back past it.
    /// </summary>
    public static class AvatarFrameMath
    {
        public const float DefaultFov = 60f;
        public const float DefaultEdgePadding = 0.08f;
        public const float DefaultBottomPadding = 0.08f;
        public const float DefaultMinZoomFraction = 0.30f;
        public const float ZoomNearFloor = 0.35f;

        /// <summary>
        /// Place the camera so every point sits inside a padded frustum.
        /// <paramref name="bottomPadding"/> is a fraction of the point height and is
        /// reserved under the lowest point, at the closest depth, so soles and toe
        /// tips are not on the last pixel. An empty list returns the scene pose
        /// (0, 1, -3.2).
        /// </summary>
        public static CameraPose Fit(
            Vector3[] points,
            int count,
            float fovDegrees,
            float aspect,
            float nearClip,
            float edgePadding = DefaultEdgePadding,
            float bottomPadding = DefaultBottomPadding)
        {
            if (points == null || count <= 0)
                return new CameraPose(0f, 1f, -3.2f);

            if (count > points.Length)
                count = points.Length;

            float minX = points[0].x;
            float maxX = points[0].x;
            float minY = points[0].y;
            float maxY = points[0].y;
            float minZ = points[0].z;
            for (var i = 1; i < count; i++)
            {
                var p = points[i];
                if (p.x < minX)
                    minX = p.x;
                if (p.x > maxX)
                    maxX = p.x;
                if (p.y < minY)
                    minY = p.y;
                if (p.y > maxY)
                    maxY = p.y;
                if (p.z < minZ)
                    minZ = p.z;
            }

            float height = maxY - minY;
            if (height < 0.001f)
                height = 0.001f;

            if (bottomPadding < 0f)
                bottomPadding = 0f;
            if (edgePadding < 0f)
                edgePadding = 0f;
            if (edgePadding > 0.45f)
                edgePadding = 0.45f;

            float paddedMinY = minY - bottomPadding * height;
            float cx = (minX + maxX) * 0.5f;
            float cy = (paddedMinY + maxY) * 0.5f;

            if (fovDegrees < 1f)
                fovDegrees = 1f;
            if (fovDegrees > 179f)
                fovDegrees = 179f;
            if (aspect < 0.01f)
                aspect = 0.01f;
            if (nearClip < 0.01f)
                nearClip = 0.01f;

            float tanHalf = Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad) * (1f - edgePadding);
            if (tanHalf < 0.001f)
                tanHalf = 0.001f;

            float minDepth = nearClip + 0.05f;
            float cz = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var p = points[i];
                cz = PullBack(cz, p.z, p.x - cx, p.y - cy, tanHalf, aspect, minDepth);
            }

            cz = PullBack(cz, minZ, 0f, paddedMinY - cy, tanHalf, aspect, minDepth);
            if (float.IsInfinity(cz) || float.IsNaN(cz))
                cz = minZ - minDepth;

            return new CameraPose(cx, cy, cz);
        }

        /// <summary>
        /// Dolly from the full-body pose toward <paramref name="pivot"/>.
        /// 0 returns the full-body pose. 1 stops at <paramref name="minFraction"/> of
        /// that distance, and never closer than <see cref="ZoomNearFloor"/> unless the
        /// full-body pose is already closer. The pivot's NDC does not change.
        /// </summary>
        public static CameraPose ZoomToward(CameraPose fullBody, Vector3 pivot, float zoom01, float minFraction = DefaultMinZoomFraction)
        {
            if (zoom01 < 0f)
                zoom01 = 0f;
            if (zoom01 > 1f)
                zoom01 = 1f;
            if (minFraction < 0.05f)
                minFraction = 0.05f;
            if (minFraction > 1f)
                minFraction = 1f;

            float dx = fullBody.X - pivot.x;
            float dy = fullBody.Y - pivot.y;
            float dz = fullBody.Z - pivot.z;
            float distance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            float depth = pivot.z - fullBody.Z;
            if (distance < 0.0001f || depth <= 0.0001f)
                return fullBody;

            float minDistance = minFraction * distance;
            if (minDistance < ZoomNearFloor)
                minDistance = ZoomNearFloor;
            if (minDistance > distance)
                minDistance = distance;

            float desired = Mathf.Lerp(distance, minDistance, zoom01);
            float scale = desired / distance;
            return new CameraPose(pivot.x + dx * scale, pivot.y + dy * scale, pivot.z + dz * scale);
        }

        /// <summary>
        /// Normalized device coordinates for an identity camera that looks along +Z.
        /// The visible range is -1 to 1. A point behind the camera returns a value
        /// outside that range.
        /// </summary>
        public static Vector2 ProjectNdc(CameraPose camera, Vector3 point, float fovDegrees, float aspect)
        {
            float depth = point.z - camera.Z;
            if (fovDegrees < 1f)
                fovDegrees = 1f;
            if (fovDegrees > 179f)
                fovDegrees = 179f;
            if (aspect < 0.01f)
                aspect = 0.01f;

            float tanHalf = Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            if (depth <= 0.00001f || tanHalf < 0.00001f)
                return new Vector2(99f, 99f);

            return new Vector2(
                (point.x - camera.X) / (depth * tanHalf * aspect),
                (point.y - camera.Y) / (depth * tanHalf));
        }

        public static bool Contains(CameraPose camera, Vector3[] points, int count, float fovDegrees, float aspect, float nearClip, float ndcLimit = 0.98f)
        {
            if (points == null || count <= 0)
                return false;
            if (count > points.Length)
                count = points.Length;
            if (nearClip < 0.01f)
                nearClip = 0.01f;

            float minDepth = nearClip + 0.02f;
            for (var i = 0; i < count; i++)
            {
                if (points[i].z - camera.Z < minDepth)
                    return false;

                var ndc = ProjectNdc(camera, points[i], fovDegrees, aspect);
                if (Mathf.Abs(ndc.x) > ndcLimit || Mathf.Abs(ndc.y) > ndcLimit)
                    return false;
            }

            return true;
        }

        static float PullBack(float cz, float pointZ, float dx, float dy, float tanHalf, float aspect, float minDepth)
        {
            float depthX = Mathf.Abs(dx) / (tanHalf * aspect);
            float depthY = Mathf.Abs(dy) / tanHalf;
            float need = depthX > depthY ? depthX : depthY;
            if (need < minDepth)
                need = minDepth;

            float candidate = pointZ - need;
            return candidate < cz ? candidate : cz;
        }
    }

    /// <summary>
    /// Uniform window scale for the 480×720 overlay. The bottom-right corner stays
    /// put, and the rect is kept inside the monitor work area.
    /// </summary>
    public static class OverlayWindowSize
    {
        public const int BaseWidth = 480;
        public const int BaseHeight = 720;
        public const float MinScale = 0.6f;
        public const float MaxScale = 2f;
        public const int EdgeMargin = 16;
        public const string ScalePrefKey = "ai-companion.window-scale";
        public const string ZoomPrefKey = "ai-companion.camera-zoom";

        /// <summary>
        /// Same pixels the click-through test treats as the model: an opaque
        /// silhouette sample, and not a uGUI hit.
        /// </summary>
        public static bool CursorCanAdjust(bool onSilhouette, bool overUi)
        {
            return onSilhouette && !overUi;
        }

        public static float ClampScale(float scale, int workWidth, int workHeight)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f)
                scale = 1f;

            float fit = MaxScale;
            int innerW = workWidth - EdgeMargin * 2;
            int innerH = workHeight - EdgeMargin * 2;
            if (innerW > 0 && innerH > 0)
            {
                float fitW = innerW / (float)BaseWidth;
                float fitH = innerH / (float)BaseHeight;
                fit = fitW < fitH ? fitW : fitH;
            }
            else if (workWidth > 0 && workHeight > 0)
            {
                float fitW = workWidth / (float)BaseWidth;
                float fitH = workHeight / (float)BaseHeight;
                fit = fitW < fitH ? fitW : fitH;
            }

            float hi = fit < MaxScale ? fit : MaxScale;
            if (hi < 1f / BaseHeight)
                hi = 1f / BaseHeight;

            float lo = MinScale < hi ? MinScale : hi;
            if (scale < lo)
                return lo;
            if (scale > hi)
                return hi;
            return scale;
        }

        public static void SizeFor(float scale, out int width, out int height)
        {
            if (float.IsNaN(scale) || scale < 0f)
                scale = 0f;

            width = Mathf.RoundToInt(BaseWidth * scale);
            height = Mathf.RoundToInt(BaseHeight * scale);
            if (width < 1)
                width = 1;
            if (height < 1)
                height = 1;
        }

        public static void PlaceAnchored(
            int anchorRight,
            int anchorBottom,
            int workLeft,
            int workTop,
            int workRight,
            int workBottom,
            int width,
            int height,
            out int x,
            out int y)
        {
            if (width < 1)
                width = 1;
            if (height < 1)
                height = 1;

            x = anchorRight - width;
            y = anchorBottom - height;
            if (x < workLeft)
                x = workLeft;
            if (y < workTop)
                y = workTop;

            int roomW = workRight - workLeft;
            int roomH = workBottom - workTop;
            if (width <= roomW && x + width > workRight)
                x = workRight - width;
            if (height <= roomH && y + height > workBottom)
                y = workBottom - height;
            if (x < workLeft)
                x = workLeft;
            if (y < workTop)
                y = workTop;
        }

        public static float NextScale(float scale, float scrollY, int workWidth, int workHeight)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale))
                scale = 1f;
            if (float.IsNaN(scrollY) || float.IsInfinity(scrollY))
                scrollY = 0f;

            return ClampScale(scale * Mathf.Exp(scrollY * 0.12f), workWidth, workHeight);
        }

        public static float NextZoom(float zoom, float scrollY)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom))
                zoom = 0f;
            if (float.IsNaN(scrollY) || float.IsInfinity(scrollY))
                scrollY = 0f;

            float next = zoom + scrollY * 0.15f;
            if (next < 0f)
                return 0f;
            if (next > 1f)
                return 1f;
            return next;
        }
    }
}
