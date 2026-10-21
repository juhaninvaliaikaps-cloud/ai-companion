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
        public const float SurfaceHeightFraction = 0.32f;
        public const float SurfaceNearMargin = 0.25f;

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

        /// <summary>
        /// How far in front of the camera the closest surface point must stay.
        /// A 1.7 m body keeps about half a metre. A short body still clears the near plane.
        /// </summary>
        public static float SurfaceClearance(float bodyHeight, float nearClip)
        {
            if (float.IsNaN(bodyHeight) || bodyHeight < 0.01f)
                bodyHeight = 0.01f;
            if (float.IsNaN(nearClip) || nearClip < 0.01f)
                nearClip = 0.01f;

            float byHeight = bodyHeight * SurfaceHeightFraction;
            float byNear = nearClip + SurfaceNearMargin;
            return byHeight > byNear ? byHeight : byNear;
        }

        /// <summary>
        /// Move an identity camera backward until every point is at least
        /// <paramref name="minDepth"/> in front of it. A pose that already clears
        /// the surface is returned unchanged.
        /// </summary>
        public static CameraPose KeepInFront(CameraPose pose, Vector3[] points, int count, float minDepth)
        {
            if (points == null || count <= 0)
                return pose;
            if (count > points.Length)
                count = points.Length;
            if (float.IsNaN(minDepth) || minDepth < 0.05f)
                minDepth = 0.05f;

            float closest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                float depth = points[i].z - pose.Z;
                if (depth < closest)
                    closest = depth;
            }

            if (float.IsNaN(closest) || closest >= minDepth)
                return pose;

            return new CameraPose(pose.X, pose.Y, pose.Z - (minDepth - closest));
        }

        /// <summary>
        /// Full-body pose for a model whose root was scaled by <paramref name="modelScale"/>
        /// around <paramref name="pivot"/>. <paramref name="windowFactor"/> is the window
        /// size divided by the 480×720 base. 1 frames the unscaled body, so the model
        /// scale is what changes her size. A matching factor above 1 frames the scaled
        /// body into the grown window.
        /// </summary>
        public static CameraPose FrameForAvatarScale(
            Vector3[] worldPoints,
            int count,
            Vector3 pivot,
            float modelScale,
            float windowFactor,
            float fovDegrees,
            float aspect,
            float nearClip,
            Vector3[] unitBuffer)
        {
            if (worldPoints == null || count <= 0)
                return Fit(null, 0, fovDegrees, aspect, nearClip);
            if (count > worldPoints.Length)
                count = worldPoints.Length;
            if (modelScale < 0.0001f || float.IsNaN(modelScale))
                modelScale = 0.0001f;
            if (windowFactor < 0.0001f || float.IsNaN(windowFactor))
                windowFactor = 1f;

            float ratio = windowFactor / modelScale;
            if (unitBuffer == null || unitBuffer.Length < count || Mathf.Abs(ratio - 1f) <= 0.0001f)
                return Fit(worldPoints, count, fovDegrees, aspect, nearClip);

            for (var i = 0; i < count; i++)
                unitBuffer[i] = pivot + (worldPoints[i] - pivot) * ratio;

            return Fit(unitBuffer, count, fovDegrees, aspect, nearClip);
        }

        /// <summary>
        /// Foot height at scale 1, measured from a root pivot that is not on the ground.
        /// </summary>
        public static float UnitFootY(float rootY, float footY, float scale)
        {
            if (scale < 0.0001f || float.IsNaN(scale))
                scale = 0.0001f;
            return rootY + (footY - rootY) / scale;
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
    /// Avatar size for the 480×720 overlay. The window stays at that base until she
    /// would be clipped, then it grows. The bottom-right corner stays put, and the
    /// rect is kept inside the monitor work area. Chat and labels stay at 1× pixels.
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

        /// <summary>
        /// Window size relative to 480×720. Shrinking her does not shrink the window.
        /// Growing her past the base grows the window by the same factor.
        /// </summary>
        public static float WindowFactorFor(float avatarScale)
        {
            if (float.IsNaN(avatarScale) || float.IsInfinity(avatarScale) || avatarScale < 1f)
                return 1f;
            return avatarScale;
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
