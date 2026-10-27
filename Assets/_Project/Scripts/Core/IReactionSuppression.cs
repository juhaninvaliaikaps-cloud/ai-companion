namespace AICompanion.Core
{
    /// <summary>
    /// While suppressed, chat cues and the debug emotion keys do not change the avatar.
    /// Entering the suppressed state returns the face to neutral.
    /// </summary>
    public interface IReactionSuppression
    {
        bool ReactionsSuppressed { get; }

        void SetSuppressed(bool suppressed);
    }
}
