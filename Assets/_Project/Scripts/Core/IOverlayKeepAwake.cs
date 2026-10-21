namespace AICompanion.Core
{
    /// <summary>
    /// Asks the overlay to stay at the active frame rate. Typing and a
    /// streaming reply use this so text is not stuck at the idle rate.
    /// </summary>
    public interface IOverlayKeepAwake
    {
        bool KeepFrameRateActive { get; }
    }
}
