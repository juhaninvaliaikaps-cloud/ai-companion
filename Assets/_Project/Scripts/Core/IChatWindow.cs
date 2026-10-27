namespace AICompanion.Core
{
    /// <summary>
    /// Opens the session chat panel. The tray calls this. It does not know about the panel.
    /// </summary>
    public interface IChatWindow
    {
        void OpenChat();
    }
}
