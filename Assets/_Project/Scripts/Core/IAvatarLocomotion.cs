namespace AICompanion.Core
{
    /// <summary>
    /// Locomotion flags the desktop presence sets while she walks or falls. Separate
    /// from <see cref="IAvatarAnimator"/> because these are bool parameters on the
    /// gesture layer, not emotions.
    /// </summary>
    public interface IAvatarLocomotion
    {
        void SetWalking(bool walking);

        void SetFalling(bool falling);
    }
}
