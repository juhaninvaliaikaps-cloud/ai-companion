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

    /// <summary>
    /// Click-through follows the rendered silhouette. A pixel counts only when its
    /// alpha crosses the engage threshold, and it stays counted until alpha falls
    /// below the release threshold so the edge does not flicker.
    /// </summary>
    public static class SilhouetteHit
    {
        public const float EngageAlpha = ClickThroughDecision.DefaultOpaqueAlpha;
        public const float ReleaseAlpha = 0.04f;
        public const int SampleSlackPixels = 3;

        public static bool NextOpaque(bool wasOpaque, float alpha, float engage = EngageAlpha, float release = ReleaseAlpha)
        {
            if (release > engage)
                release = engage;

            if (wasOpaque)
                return alpha >= release;

            return alpha >= engage;
        }

        public static bool WithinSlack(Vector2Int origin, Vector2Int current, int slackPixels = SampleSlackPixels)
        {
            if (slackPixels < 0)
                slackPixels = 0;

            int dx = origin.x - current.x;
            int dy = origin.y - current.y;
            return dx * dx + dy * dy <= slackPixels * slackPixels;
        }

        public static bool ShouldSample(bool insideWindow, bool outsideSlack, bool readInFlight)
        {
            return insideWindow && outsideSlack && !readInFlight;
        }

        /// <summary>
        /// The window lets clicks through unless the cursor is on UI or on an opaque
        /// silhouette pixel. A drag that has already started keeps the window
        /// interactive until the button is released.
        /// </summary>
        public static bool ShouldClickThrough(bool overUi, bool onSilhouette, bool dragging)
        {
            if (dragging)
                return false;

            return !overUi && !onSilhouette;
        }
    }

    /// <summary>
    /// Right-click rotate mode. The click has to land on a visible pixel, the
    /// same test click-through uses. A horizontal drag then yaws the avatar.
    /// Left-drag still moves the window while this mode is off.
    /// </summary>
    public static class AvatarRotateMode
    {
        public const string OnLabel = "Rotate: on (right-click to finish)";
        public const string OffLabel = "Rotate: off";

        public static string Format(bool active)
        {
            return active ? OnLabel : OffLabel;
        }

        public static bool ShouldToggle(bool rightPressed, bool onSilhouette, bool overUi)
        {
            return rightPressed && onSilhouette && !overUi;
        }

        public static bool Next(bool active, bool toggle, bool escape)
        {
            if (escape)
                return false;

            if (toggle)
                return !active;

            return active;
        }

        /// <summary>
        /// R resets only while the mode is on. Middle-click resets on a visible
        /// pixel either way. The debug panel is not a reset target.
        /// </summary>
        public static bool ShouldReset(bool active, bool resetKeyPressed, bool middlePressed, bool onSilhouette, bool overUi)
        {
            if (middlePressed && onSilhouette && !overUi)
                return true;

            return active && resetKeyPressed;
        }

        public static bool ShouldRotateInsteadOfMovingWindow(bool active, bool onSilhouette, bool overUi)
        {
            return active && onSilhouette && !overUi;
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
