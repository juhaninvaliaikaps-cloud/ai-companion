namespace AICompanion.Core
{
    /// <summary>
    /// Which chat produced the reply that is about to be spoken.
    /// The voice uses this instead of a second settings copy that can stay on Grok.
    /// </summary>
    public struct SpeechOrigin
    {
        public string ChatProviderId;
        public string Model;
        public bool Private;
    }

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

        /// <summary>Starts speech for the chat provider that is about to answer.</summary>
        void BeginUtterance(SpeechOrigin origin);

        void Append(string text);

        void CompleteUtterance();

        void Cancel();
    }
}
