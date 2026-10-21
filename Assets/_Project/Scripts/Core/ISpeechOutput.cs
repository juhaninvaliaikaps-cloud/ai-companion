namespace AICompanion.Core
{
    /// <summary>
    /// Spoken replies. The chat panel calls this. The voice assembly implements it.
    /// </summary>
    public interface ISpeechOutput
    {
        bool IsMuted { get; }

        /// <summary>
        /// True while a sentence is being synthesized or audio is queued or playing.
        /// The talk animation uses playback only; this keeps the overlay frame rate up.
        /// </summary>
        bool IsSpeaking { get; }

        void SetMuted(bool muted);

        void BeginUtterance();

        void Append(string text);

        void CompleteUtterance();

        void Cancel();
    }
}
