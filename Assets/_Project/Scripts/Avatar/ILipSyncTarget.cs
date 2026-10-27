namespace AICompanion.Avatar
{
    /// <summary>
    /// A mouth a future avatar (VRoid VRM, Daz, or anything else) can drive.
    /// Weights are 0 to 1 on A, I, U, E, and O. Emotion shapes are not part of this.
    /// </summary>
    public interface ILipSyncTarget
    {
        LipSyncTargetKind Kind { get; }

        /// <summary>
        /// Words after "LipSync: ", such as "expressions Aa, Ih" or "jaw bone Jaw".
        /// This must not include spoken text.
        /// </summary>
        string Description { get; }

        void Apply(MouthWeights weights);
    }

    /// <summary>
    /// The audio side of lip sync. <see cref="AvatarAnimationController"/> reads the
    /// smoothed weights and fades emotion mouth shapes out while speech owns them.
    /// </summary>
    public interface IMouthDriver
    {
        MouthWeights Weights { get; }

        /// <summary>0 at rest, up to 1 while speech owns the mouth shapes.</summary>
        float MouthOverride { get; }

        LipSyncTargetKind TargetKind { get; }

        /// <summary>
        /// Replaces the auto-selected VRM, blendshape, or jaw target.
        /// Pass null to go back to scanning the loaded avatar.
        /// </summary>
        void SetTarget(ILipSyncTarget target);
    }
}
