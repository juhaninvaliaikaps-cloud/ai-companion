using System.Threading.Tasks;

namespace AICompanion.Voice
{
    /// <summary>
    /// One local transcription. <see cref="Text"/> is for the chat panel only.
    /// <see cref="LogLine"/> must stay free of transcript text and audio.
    /// </summary>
    public readonly struct SttResult
    {
        public bool Ok { get; }
        public string Text { get; }
        public string UserMessage { get; }
        public string LogLine { get; }

        SttResult(bool ok, string text, string userMessage, string logLine)
        {
            Ok = ok;
            Text = text ?? string.Empty;
            UserMessage = userMessage ?? string.Empty;
            LogLine = logLine ?? string.Empty;
        }

        public static SttResult Ready(string text, string logLine)
        {
            return new SttResult(true, text, string.Empty, logLine);
        }

        public static SttResult Empty(string logLine)
        {
            return new SttResult(false, string.Empty, string.Empty, logLine);
        }

        public static SttResult Fail(string userMessage, string logLine)
        {
            return new SttResult(false, string.Empty, userMessage, logLine);
        }
    }

    /// <summary>
    /// Local speech-to-text. A later module can replace the Whisper implementation.
    /// </summary>
    public interface ISttProvider
    {
        string ModelFileName { get; }

        Task<SttResult> TranscribeAsync(float[] samples16kMono, string language);
    }
}
