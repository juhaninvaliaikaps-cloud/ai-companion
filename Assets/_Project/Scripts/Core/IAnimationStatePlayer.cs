namespace AICompanion.Core
{
    /// <summary>
    /// Lets the animation director ask for a named Animator state without knowing how
    /// the avatar is wired. The implementation returns false when the state is not on
    /// the current controller, so the director can fall back to an emotion.
    /// </summary>
    public interface IAnimationStatePlayer
    {
        bool TryPlayState(string stateName, float crossfadeSeconds);
    }
}
