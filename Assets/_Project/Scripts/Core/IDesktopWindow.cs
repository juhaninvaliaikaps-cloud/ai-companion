namespace AICompanion.Core
{
    /// <summary>
    /// Desktop pixels for the player window and the work area above the taskbar.
    /// Origin is the top left of the primary desktop, y growing downward.
    /// </summary>
    public struct DesktopWindow
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int WorkLeft;
        public int WorkTop;
        public int WorkRight;
        public int WorkBottom;
    }

    /// <summary>
    /// The overlay window reports its real desktop rect. The editor and Linux
    /// player have no chrome, so the query fails and placement uses the screen.
    /// </summary>
    public interface IDesktopWindow
    {
        bool TryGetDesktopWindow(out DesktopWindow window);
    }

    /// <summary>
    /// Show or hide the overlay window. The tray toggles this. The editor has no
    /// Win32 window, so the flag still changes and the chrome call does nothing.
    /// </summary>
    public interface ICompanionVisibility
    {
        bool IsCompanionVisible { get; }

        void SetCompanionVisible(bool visible);
    }
}
