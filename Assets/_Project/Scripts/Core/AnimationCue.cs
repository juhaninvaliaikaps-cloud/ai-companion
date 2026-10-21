namespace AICompanion.Core
{
    /// <summary>
    /// Emotion and gesture request shared by chat providers and the animation director.
    /// Intensity is always stored in the 0 to 1 range.
    /// </summary>
    public readonly struct AnimationCue
    {
        public const float MinIntensity = 0f;
        public const float MaxIntensity = 1f;

        public Emotion Emotion { get; }
        public float Intensity { get; }
        public string Gesture { get; }

        public AnimationCue(Emotion emotion, float intensity, string gesture)
        {
            Emotion = emotion;
            Intensity = ClampIntensity(intensity);
            Gesture = gesture ?? string.Empty;
        }

        public static float ClampIntensity(float intensity)
        {
            if (intensity < MinIntensity)
            {
                return MinIntensity;
            }

            if (intensity > MaxIntensity)
            {
                return MaxIntensity;
            }

            return intensity;
        }
    }
}
