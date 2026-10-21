using UnityEngine;

namespace AICompanion.Overlay
{
    public interface IWindowChrome
    {
        bool IsSupported { get; }
        bool TryApplyTransparentPopup();
        void SetTopmost(bool topmost);
        void SetClickThrough(bool clickThrough);
        bool IsLeftButtonHeld();
        bool IsRightButtonHeld();
        bool IsMiddleButtonHeld();
        bool TryGetScreenCursor(out Vector2Int screenPixels);
        bool TryGetClientCursor(out Vector2Int clientPixels, out Vector2Int clientSize);
        bool TryGetWindowPosition(out Vector2Int topLeft);
        bool TryGetMonitorSize(out Vector2Int size);
        void MoveWindow(Vector2Int topLeft, bool topmost);
    }

    public static class WindowChrome
    {
        public static IWindowChrome Create()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return new WindowsWindowChrome();
#else
            return new UnsupportedWindowChrome();
#endif
        }
    }

    public sealed class UnsupportedWindowChrome : IWindowChrome
    {
        public bool IsSupported => false;

        public bool TryApplyTransparentPopup() => false;

        public void SetTopmost(bool topmost)
        {
        }

        public void SetClickThrough(bool clickThrough)
        {
        }

        public bool IsLeftButtonHeld() => false;

        public bool IsRightButtonHeld() => false;

        public bool IsMiddleButtonHeld() => false;

        public bool TryGetScreenCursor(out Vector2Int screenPixels)
        {
            screenPixels = default;
            return false;
        }

        public bool TryGetClientCursor(out Vector2Int clientPixels, out Vector2Int clientSize)
        {
            clientPixels = default;
            clientSize = default;
            return false;
        }

        public bool TryGetWindowPosition(out Vector2Int topLeft)
        {
            topLeft = default;
            return false;
        }

        public bool TryGetMonitorSize(out Vector2Int size)
        {
            size = default;
            return false;
        }

        public void MoveWindow(Vector2Int topLeft, bool topmost)
        {
        }
    }
}
