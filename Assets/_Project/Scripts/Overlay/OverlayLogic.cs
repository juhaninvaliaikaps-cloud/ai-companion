using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// What the pointer is over. The window is click-through only when every signal is false.
    /// </summary>
    public interface IOverlayHitQuery
    {
        bool OverCollider { get; }
        bool OverUi { get; }
        bool OpaquePixel { get; }
    }

    public static class ClickThroughDecision
    {
        public const float DefaultOpaqueAlpha = 0.08f;

        public static bool IsOpaque(float alpha, float threshold = DefaultOpaqueAlpha)
        {
            return alpha >= threshold;
        }

        public static bool ShouldClickThrough(bool overCollider, bool overUi, bool opaquePixel)
        {
            return !overCollider && !overUi && !opaquePixel;
        }

        public static bool ShouldClickThrough(IOverlayHitQuery hit)
        {
            if (hit == null)
                return true;

            return ShouldClickThrough(hit.OverCollider, hit.OverUi, hit.OpaquePixel);
        }
    }

    public static class OverlayFrameRate
    {
        public const int Active = 60;
        public const int Idle = 15;
        public const float IdleAfterSeconds = 3f;

        public static int Choose(float idleSeconds, float idleAfterSeconds = IdleAfterSeconds, int active = Active, int idle = Idle)
        {
            if (idleSeconds < idleAfterSeconds)
                return active;

            return idle;
        }
    }

    public readonly struct WindowDrag
    {
        public WindowDrag(Vector2Int windowOrigin, Vector2Int cursorOrigin)
        {
            WindowOrigin = windowOrigin;
            CursorOrigin = cursorOrigin;
        }

        public Vector2Int WindowOrigin { get; }
        public Vector2Int CursorOrigin { get; }

        public Vector2Int PositionFor(Vector2Int cursor)
        {
            return WindowOrigin + (cursor - CursorOrigin);
        }
    }

    public static class OverlayPointerMath
    {
        public static Vector2 ToUnityScreen(int clientX, int clientY, int clientWidth, int clientHeight, int unityWidth, int unityHeight)
        {
            if (clientWidth <= 0 || clientHeight <= 0 || unityWidth <= 0 || unityHeight <= 0)
                return Vector2.zero;

            float x = clientX * (unityWidth / (float)clientWidth);
            float y = (clientHeight - clientY) * (unityHeight / (float)clientHeight);
            return new Vector2(x, y);
        }

        public static bool IsInsideClient(int clientX, int clientY, int clientWidth, int clientHeight)
        {
            return clientWidth > 0
                && clientHeight > 0
                && clientX >= 0
                && clientY >= 0
                && clientX < clientWidth
                && clientY < clientHeight;
        }

        /// <summary>
        /// <paramref name="topLeftRect"/> is GUI space (origin at the top left, y grows down).
        /// <paramref name="unityScreen"/> is Unity screen space (origin at the bottom left).
        /// The far edges are exclusive, matching <see cref="Rect.Contains(Vector2)"/>.
        /// </summary>
        public static bool IsInsideTopLeftRect(Rect topLeftRect, Vector2 unityScreen, float screenHeight)
        {
            float left = topLeftRect.x;
            float right = topLeftRect.xMax;
            float top = screenHeight - topLeftRect.y;
            float bottom = screenHeight - topLeftRect.yMax;
            return unityScreen.x >= left
                && unityScreen.x < right
                && unityScreen.y >= bottom
                && unityScreen.y < top;
        }
    }
}
