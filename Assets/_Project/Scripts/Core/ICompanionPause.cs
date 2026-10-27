namespace AICompanion.Core
{
    /// <summary>
    /// Tray pause. While this is on, speech is held, push-to-talk does not record,
    /// and animation cues are not applied.
    /// </summary>
    public interface ICompanionPause
    {
        bool IsPaused { get; }

        void SetPaused(bool paused);
    }
}
