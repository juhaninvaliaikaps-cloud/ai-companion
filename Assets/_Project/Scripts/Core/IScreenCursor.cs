namespace AICompanion.Core
{
    /// <summary>
    /// Cursor in Unity screen pixels (origin at the bottom left), the same space as
    /// <c>Camera.WorldToScreenPoint</c>. The Windows player reads the desktop cursor
    /// so an unfocused overlay still tracks it. The editor and other players fall
    /// back to the game-view pointer.
    /// </summary>
    public interface IScreenCursor
    {
        bool TryGetUnityScreenCursor(out float x, out float y);
    }
}
