namespace AICompanion.Core
{
    /// <summary>
    /// Holds spoken output without changing the user's mute preference.
    /// Pause uses this. The mute button still owns <see cref="ISpeechOutput.IsMuted"/>.
    /// </summary>
    public interface ISpeechSuppression
    {
        void SetSuppressed(bool suppressed);
    }
}
