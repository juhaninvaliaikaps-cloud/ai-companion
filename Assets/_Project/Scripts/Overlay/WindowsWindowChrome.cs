#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Borderless per-pixel alpha window via DwmExtendFrameIntoClientArea(margins -1).
    /// Compiled only into the Windows player. The editor and the Linux CI editor never see these imports.
    /// </summary>
    public sealed class WindowsWindowChrome : IWindowChrome
    {
        const int GwlStyle = -16;
        const int GwlExStyle = -20;
        const int SmCxScreen = 0;
        const int SmCyScreen = 1;
        const int VkLButton = 0x01;
        const int VkRButton = 0x02;
        const int VkMButton = 0x04;

        const uint SwpNoSize = 0x0001;
        const uint SwpNoMove = 0x0002;
        const uint SwpNoZOrder = 0x0004;
        const uint SwpNoActivate = 0x0010;
        const uint SwpFrameChanged = 0x0020;
        const uint SwpShowWindow = 0x0040;

        const int WsVisible = 0x10000000;
        const int WsCaption = 0x00C00000;
        const int WsThickFrame = 0x00040000;
        const int WsMinimizeBox = 0x00020000;
        const int WsMaximizeBox = 0x00010000;
        const int WsSysMenu = 0x00080000;
        const int WsExLayered = 0x00080000;
        const int WsExTransparent = 0x00000020;

        static readonly IntPtr HwndTopMost = new IntPtr(-1);
        static readonly IntPtr HwndNoTopMost = new IntPtr(-2);

        Process _process;
        IntPtr _hwnd;
        int _refreshedFrame = -1;
        int _stableFrames;
        bool _hwndLocked;
        bool _hasClickThrough;
        bool _clickThrough;

        public bool IsSupported => true;

        public bool TryApplyTransparentPopup()
        {
            if (!TryGetHwnd())
                return false;

            var margins = new Margins
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1,
            };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            int style = GetWindowLong(_hwnd, GwlStyle);
            style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
            style |= unchecked((int)0x80000000) | WsVisible;
            SetWindowLong(_hwnd, GwlStyle, style);
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate);
            _hasClickThrough = false;
            return true;
        }

        public void SetTopmost(bool topmost)
        {
            if (!TryGetHwnd())
                return;

            var after = topmost ? HwndTopMost : HwndNoTopMost;
            SetWindowPos(_hwnd, after, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        }

        public void SetClickThrough(bool clickThrough)
        {
            if (!TryGetHwnd())
                return;

            if (_hasClickThrough && _clickThrough == clickThrough)
                return;

            _clickThrough = clickThrough;
            _hasClickThrough = true;
            int ex = GetWindowLong(_hwnd, GwlExStyle);
            if (clickThrough)
                ex |= WsExLayered | WsExTransparent;
            else
                ex &= ~WsExTransparent;

            SetWindowLong(_hwnd, GwlExStyle, ex);
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate);
        }

        public bool IsLeftButtonHeld()
        {
            return (GetAsyncKeyState(VkLButton) & 0x8000) != 0;
        }

        public bool IsRightButtonHeld()
        {
            return (GetAsyncKeyState(VkRButton) & 0x8000) != 0;
        }

        public bool IsMiddleButtonHeld()
        {
            return (GetAsyncKeyState(VkMButton) & 0x8000) != 0;
        }

        public bool TryGetScreenCursor(out Vector2Int screenPixels)
        {
            if (!GetCursorPos(out var point))
            {
                screenPixels = default;
                return false;
            }

            screenPixels = new Vector2Int(point.x, point.y);
            return true;
        }

        public bool TryGetClientCursor(out Vector2Int clientPixels, out Vector2Int clientSize)
        {
            clientPixels = default;
            clientSize = default;
            if (!TryGetHwnd() || !GetCursorPos(out var point))
                return false;

            if (!ScreenToClient(_hwnd, ref point))
                return false;

            if (!GetClientRect(_hwnd, out var rect))
                return false;

            clientPixels = new Vector2Int(point.x, point.y);
            clientSize = new Vector2Int(rect.right - rect.left, rect.bottom - rect.top);
            return true;
        }

        public bool TryGetWindowPosition(out Vector2Int topLeft)
        {
            if (!TryGetHwnd() || !GetWindowRect(_hwnd, out var rect))
            {
                topLeft = default;
                return false;
            }

            topLeft = new Vector2Int(rect.left, rect.top);
            return true;
        }

        public bool TryGetMonitorSize(out Vector2Int size)
        {
            size = new Vector2Int(GetSystemMetrics(SmCxScreen), GetSystemMetrics(SmCyScreen));
            return size.x > 0 && size.y > 0;
        }

        public void MoveWindow(Vector2Int topLeft, bool topmost)
        {
            if (!TryGetHwnd())
                return;

            var after = topmost ? HwndTopMost : HwndNoTopMost;
            SetWindowPos(_hwnd, after, topLeft.x, topLeft.y, 0, 0, SwpNoSize | SwpNoActivate | SwpShowWindow);
        }

        bool TryGetHwnd()
        {
            if (_hwndLocked)
                return _hwnd != IntPtr.Zero;

            int frame = Time.frameCount;
            if (_refreshedFrame != frame)
            {
                _refreshedFrame = frame;
                if (_process == null)
                    _process = Process.GetCurrentProcess();

                _process.Refresh();
                var hwnd = _process.MainWindowHandle;
                if (hwnd == IntPtr.Zero)
                {
                    _hwnd = IntPtr.Zero;
                    _stableFrames = 0;
                }
                else if (hwnd != _hwnd)
                {
                    _hwnd = hwnd;
                    _stableFrames = 0;
                    _hasClickThrough = false;
                }
                else if (++_stableFrames > 45)
                {
                    _hwndLocked = true;
                }
            }

            return _hwnd != IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Margins
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WinPoint
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WinRect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins margins);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out WinPoint point);

        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr hWnd, ref WinPoint point);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out WinRect rect);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out WinRect rect);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
    }
}
#endif
