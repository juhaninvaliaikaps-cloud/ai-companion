namespace AICompanion.Core
{
    /// <summary>
    /// Where voice failures appear. The chat panel shows them as Note lines.
    /// </summary>
    public interface ISpeechNotes
    {
        void ShowSpeechNote(string message);
    }
}
