namespace AICompanion.Core
{
    /// <summary>
    /// Sends one user line into the chat session. Speech calls this so it does
    /// not reference the panel, and the panel does not reference speech-to-text.
    /// </summary>
    public interface IChatSubmitter
    {
        bool SubmitUserText(string text);
    }
}
