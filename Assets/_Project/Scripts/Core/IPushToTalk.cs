namespace AICompanion.Core
{
    /// <summary>
    /// Hold-to-talk control. The chat panel's mic button and the recording
    /// indicator use this. The voice assembly implements it.
    /// </summary>
    public interface IPushToTalk
    {
        bool IsRecording { get; }

        bool IsTranscribing { get; }

        void SetButtonHeld(bool held);
    }
}
