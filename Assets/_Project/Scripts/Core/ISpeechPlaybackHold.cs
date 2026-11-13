namespace AICompanion.Core
{
    /// <summary>
    /// JUH-108 prefetch: while held, synthesized speech is queued but does not start playing.
    /// Releasing the hold plays it at once. The voice still applies its own privacy route,
    /// mute and pause; a hold only delays playback. Cancel, mute and a new utterance clear it.
    /// </summary>
    public interface ISpeechPlaybackHold
    {
        bool PlaybackHeld { get; }
        void SetPlaybackHeld(bool held);
    }
}
