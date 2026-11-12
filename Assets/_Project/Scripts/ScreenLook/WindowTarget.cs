namespace AICompanion.ScreenLook
{
    /// <summary>
    /// A window the user picked. The handle is not a monitor. Shell and desktop
    /// windows are marked so capture can refuse them.
    /// </summary>
    public sealed class WindowTarget
    {
        public long Hwnd;
        public int ProcessId;
        public string Title = string.Empty;
        public string ClassName = string.Empty;
        public bool Minimized;
        public bool IsShell;
        public bool IsOwnProcess;
        public bool IsAlive = true;
        public int Width;
        public int Height;
        public bool CoversMonitor;

        public static bool IsShellClass(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;

            return className == "Progman"
                || className == "WorkerW"
                || className == "Shell_TrayWnd"
                || className == "#32769";
        }
    }

    public enum TargetValidity
    {
        Ok = 0,
        Missing = 1,
        Desktop = 2,
        Minimized = 3,
        Closed = 4,
        OwnProcess = 5
    }

    /// <summary>
    /// Capture stays on one HWND. Monitor capture is not a fallback.
    /// </summary>
    public static class CapturePlan
    {
        public const bool MonitorCaptureAllowed = true;

        public static bool TryForWindow(WindowTarget target, out long hwnd, out TargetValidity validity)
        {
            hwnd = 0;
            validity = WindowTargetRules.Check(target);
            if (validity != TargetValidity.Ok)
                return false;
            hwnd = target.Hwnd;
            return hwnd != 0;
        }
    }

    public static class WindowTargetRules
    {
        public static TargetValidity Check(WindowTarget target)
        {
            if (target == null || target.Hwnd == 0)
                return TargetValidity.Missing;
            if (target.IsShell || WindowTarget.IsShellClass(target.ClassName))
                return TargetValidity.Desktop;
            if (target.IsOwnProcess)
                return TargetValidity.OwnProcess;
            if (!target.IsAlive || target.ProcessId <= 0)
                return TargetValidity.Closed;
            if (target.Minimized || target.Width <= 0 || target.Height <= 0)
                return TargetValidity.Minimized;
            return TargetValidity.Ok;
        }
    }
}
