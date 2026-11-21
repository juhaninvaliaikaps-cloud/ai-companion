namespace AICompanion.Core
{
    /// <summary>
    /// JUH-164: the game window the game companion has found (a gacha game's window), or 0. The
    /// overlay keeps Happi above it even when it doesn't fill the screen.
    /// </summary>
    public static class GameWindowNow
    {
        public static long Hwnd { get; private set; }

        public static void Set(long hwnd)
        {
            Hwnd = hwnd;
        }

        public static void Clear()
        {
            Hwnd = 0;
        }
    }
}
