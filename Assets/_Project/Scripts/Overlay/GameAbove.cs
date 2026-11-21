using System;
using System.Text;

namespace AICompanion.Overlay
{
    /// <summary>
    /// JUH-164: Happi has to stay over the game she is watching. "Always on top" is the desktop
    /// setting; "Stay above games" puts her on top whenever a game is the window in front, even
    /// with "Always on top" off. Windows can't draw anything over exclusive fullscreen, so that
    /// mode is left out; borderless and windowed games work.
    /// </summary>
    public static class GameAbove
    {
        /// <summary>Whether her window should be topmost right now.</summary>
        public static bool Want(bool alwaysOnTop, bool stayAboveGames, bool gameInFront)
        {
            return alwaysOnTop || (stayAboveGames && gameInFront);
        }

        /// <summary>
        /// Whether the foreground window is a game: another program's window that fills its
        /// monitor (borderless or windowed fullscreen), or a game window Happi has detected, and
        /// not the desktop, the taskbar or Alt+Tab.
        /// </summary>
        public static bool IsGameWindow(bool ownProcess, bool shell, bool coversMonitor, bool knownGame)
        {
            if (ownProcess || shell)
                return false;
            return coversMonitor || knownGame;
        }

        /// <summary>
        /// Windows of the shell itself. The desktop (Progman, WorkerW) fills the monitor too, so
        /// it would otherwise look like a full-screen game.
        /// </summary>
        static readonly string[] ShellClasses =
        {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
            "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "MultitaskingViewFrame",
            "ForegroundStaging", "TaskListThumbnailWnd", "NotifyIconOverflowWindow",
        };

        public static bool IsShellClass(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;
            for (var i = 0; i < ShellClasses.Length; i++)
                if (string.Equals(className, ShellClasses[i], StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>The same check on a reused buffer, so the per-second check doesn't allocate.</summary>
        public static bool IsShellClass(StringBuilder className)
        {
            if (className == null || className.Length == 0)
                return false;
            for (var i = 0; i < ShellClasses.Length; i++)
            {
                var name = ShellClasses[i];
                if (name.Length != className.Length)
                    continue;
                var same = true;
                for (var c = 0; c < name.Length && same; c++)
                    same = className[c] == name[c];
                if (same)
                    return true;
            }

            return false;
        }

        /// <summary>A window fills its monitor when its rectangle covers the monitor's rectangle.</summary>
        public static bool Covers(int left, int top, int right, int bottom, int monitorLeft, int monitorTop, int monitorRight, int monitorBottom)
        {
            if (monitorRight <= monitorLeft || monitorBottom <= monitorTop)
                return false;
            return left <= monitorLeft && top <= monitorTop && right >= monitorRight && bottom >= monitorBottom;
        }
    }
}
