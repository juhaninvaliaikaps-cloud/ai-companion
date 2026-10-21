namespace AICompanion.Core
{
    /// <summary>
    /// Body and face animation for the loaded humanoid.
    /// Chat and the animation director call this. They do not touch the Animator.
    /// </summary>
    public interface IAvatarAnimator
    {
        bool IsTalking { get; }

        Emotion Emotion { get; }

        float EmotionIntensity { get; }

        void SetTalking(bool talking);

        void SetEmotion(Emotion emotion, float intensity);
    }
}
