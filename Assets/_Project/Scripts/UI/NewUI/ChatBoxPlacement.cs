using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-104: where the movable chat box goes. Positions are the box centre in Unity screen
    /// pixels (origin bottom-left of the overlay window). The saved position is stored as a
    /// 0..1 fraction of the window, -1 meaning "default". The box is always clamped inside the
    /// part of the window that is on the monitor's work area, so a position saved on another
    /// monitor or before a resolution change snaps back on screen.
    /// </summary>
    public static class ChatBoxPlacement
    {
        /// <summary>Default: bottom centre, <paramref name="margin"/> above the bottom.</summary>
        public static Vector2 DefaultCenter(Vector2 screen, Vector2 size, float margin)
        {
            return new Vector2(screen.x * 0.5f, margin + size.y * 0.5f);
        }

        public static bool HasSaved(float savedX, float savedY)
        {
            return savedX >= 0f && savedY >= 0f;
        }

        /// <summary>The box centre for a saved (or default) position, clamped into <paramref name="visible"/>.</summary>
        public static Vector2 Resolve(float savedX, float savedY, Vector2 screen, Vector2 size, Rect visible, float margin)
        {
            var center = HasSaved(savedX, savedY)
                ? new Vector2(savedX * screen.x, savedY * screen.y)
                : DefaultCenter(screen, size, margin);
            return Clamp(center, size, visible, margin);
        }

        /// <summary>Keeps the whole box inside the bounds (centred when the box is bigger).</summary>
        public static Vector2 Clamp(Vector2 center, Vector2 size, Rect bounds, float margin)
        {
            var halfW = size.x * 0.5f + margin;
            var halfH = size.y * 0.5f + margin;
            center.x = bounds.width < 2f * halfW ? bounds.center.x : Mathf.Clamp(center.x, bounds.xMin + halfW, bounds.xMax - halfW);
            center.y = bounds.height < 2f * halfH ? bounds.center.y : Mathf.Clamp(center.y, bounds.yMin + halfH, bounds.yMax - halfH);
            return center;
        }

        /// <summary>The 0..1 fractions to save for a centre.</summary>
        public static Vector2 ToSaved(Vector2 center, Vector2 screen)
        {
            var x = screen.x > 0f ? Mathf.Clamp01(center.x / screen.x) : 0.5f;
            var y = screen.y > 0f ? Mathf.Clamp01(center.y / screen.y) : 0.5f;
            return new Vector2(x, y);
        }

        /// <summary>
        /// The part of the overlay window that is on its monitor's work area, in Unity screen
        /// pixels. Window and work area are desktop pixels (origin top-left). Falls back to the
        /// whole window when they do not overlap.
        /// </summary>
        public static Rect VisibleArea(Vector2 screen, RectInt window, RectInt workArea)
        {
            var full = new Rect(0f, 0f, screen.x, screen.y);
            if (window.width <= 0 || window.height <= 0)
                return full;
            var left = Math.Max(window.xMin, workArea.xMin);
            var right = Math.Min(window.xMax, workArea.xMax);
            var top = Math.Max(window.yMin, workArea.yMin);
            var bottom = Math.Min(window.yMax, workArea.yMax);
            if (workArea.width <= 0 || workArea.height <= 0 || right - left < 8 || bottom - top < 8)
                return full;

            var sx = screen.x / window.width;
            var sy = screen.y / window.height;
            var x0 = (left - window.xMin) * sx;
            var x1 = (right - window.xMin) * sx;
            var y0 = screen.y - (bottom - window.yMin) * sy;
            var y1 = screen.y - (top - window.yMin) * sy;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }

    /// <summary>The part of the overlay window that is on screen (it can hang off an edge when it has side room).</summary>
    public static class OverlayVisibleArea
    {
        public static Rect Get(AICompanion.Overlay.OverlayWindow window)
        {
            var screen = new Vector2(Screen.width, Screen.height);
            var full = new Rect(0f, 0f, screen.x, screen.y);
            var chrome = window != null ? window.Chrome : null;
            int left;
            int top;
            int right;
            int bottom;
            if (chrome == null || !chrome.IsSupported || !chrome.TryGetWindowRect(out left, out top, out right, out bottom))
                return full;
            RectInt work;
            if (!MonitorWorkArea.TryGet((left + right) / 2, (top + bottom) / 2, out work))
                return full;
            return ChatBoxPlacement.VisibleArea(screen, new RectInt(left, top, right - left, bottom - top), work);
        }
    }

    /// <summary>Work area of the monitor a desktop point is on (both monitors, not only the primary one).</summary>
    public static class MonitorWorkArea
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WinRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MonitorInfo
        {
            public int Size;
            public WinRect Monitor;
            public WinRect Work;
            public int Flags;
        }

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromPoint(Point point, int flags);

        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
#endif

        public static bool TryGet(int x, int y, out RectInt workArea)
        {
            workArea = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                var monitor = MonitorFromPoint(new Point { X = x, Y = y }, 2);
                if (monitor == IntPtr.Zero)
                    return false;
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (!GetMonitorInfo(monitor, ref info))
                    return false;
                workArea = new RectInt(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
                return workArea.width > 0 && workArea.height > 0;
            }
            catch (Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }
    }
}
