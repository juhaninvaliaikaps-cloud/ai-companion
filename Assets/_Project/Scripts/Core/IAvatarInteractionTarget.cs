namespace AICompanion.Core
{
    /// <summary>
    /// Optional 2D avatar that can show a touch interaction. Implemented by the video avatar
    /// (and later by the sprite avatar). A missing target only means no visual reaction; the
    /// chat event still reaches the model.
    /// </summary>
    public interface IAvatarInteractionTarget
    {
        /// <summary>Plays the interaction's visual reaction, if it has one. False when not available.</summary>
        bool TryPlayInteraction(string interactionId);
    }
}
