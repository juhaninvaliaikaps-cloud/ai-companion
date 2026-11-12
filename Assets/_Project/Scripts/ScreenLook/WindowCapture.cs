using System.Collections.Generic;

namespace AICompanion.ScreenLook
{
    public readonly struct CapturedWindowFrame
    {
        public bool Ok { get; }
        public string Error { get; }
        public byte[] Bgra { get; }
        public int Width { get; }
        public int Height { get; }

        public CapturedWindowFrame(bool ok, string error, byte[] bgra, int width, int height)
        {
            Ok = ok;
            Error = error ?? string.Empty;
            Bgra = bgra;
            Width = width;
            Height = height;
        }

        public static CapturedWindowFrame Fail(string error)
        {
            return new CapturedWindowFrame(false, error, null, 0, 0);
        }
    }

    /// <summary>
    /// Lists top-level windows and grabs one frame from the selected HWND.
    /// The Windows player uses Windows Graphics Capture. Other runtimes report
    /// that capture is unavailable. There is no desktop fallback.
    /// </summary>
    public static class WindowCapture
    {
        public static bool IsSupported
        {
            get
            {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
                return WindowsGraphicsCaptureBackend.IsSupported;
#else
                return false;
#endif
            }
        }

        public static IReadOnlyList<WindowTarget> ListWindows()
        {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            return WindowsGraphicsCaptureBackend.ListWindows();
#else
            return new WindowTarget[0];
#endif
        }

        public static void CancelActive()
        {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            WindowsGraphicsCaptureBackend.CancelActive();
#endif
        }

        public static CapturedWindowFrame CaptureWindow(WindowTarget target)
        {
            if (!CapturePlan.TryForWindow(target, out var hwnd, out var validity))
                return CapturedWindowFrame.Fail(Message(validity));

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            return WindowsGraphicsCaptureBackend.CaptureWindow(target, hwnd);
#else
            return CapturedWindowFrame.Fail(ScreenLookPolicy.PlayerOnly);
#endif
        }

        public static CapturedWindowFrame CaptureMonitor(MonitorTarget target)
        {
            if (target == null || target.Handle == System.IntPtr.Zero)
                return CapturedWindowFrame.Fail(ScreenLookPolicy.NeedMonitor);

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            return WindowsGraphicsCaptureBackend.CaptureMonitor(target);
#else
            return CapturedWindowFrame.Fail(ScreenLookPolicy.PlayerOnly);
#endif
        }

        static string Message(TargetValidity validity)
        {
            switch (validity)
            {
                case TargetValidity.Desktop:
                    return ScreenLookPolicy.DesktopRefused;
                case TargetValidity.OwnProcess:
                    return ScreenLookPolicy.OwnWindow;
                case TargetValidity.Closed:
                    return ScreenLookPolicy.Closed;
                case TargetValidity.Minimized:
                    return ScreenLookPolicy.Minimized;
                default:
                    return ScreenLookPolicy.NeedWindow;
            }
        }
    }
}
