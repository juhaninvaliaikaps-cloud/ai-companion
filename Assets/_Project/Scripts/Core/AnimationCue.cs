namespace AICompanion.Core
{
    /// <summary>
    /// Emotion and gesture request shared by chat providers and the animation director.
    /// Intensity is always stored in the 0 to 1 range. <see cref="Direction"/> is -1 for
    /// left, +1 for right, and 0 when the cue does not name a side.
    /// </summary>
    public readonly struct AnimationCue
    {
        public const float MinIntensity = 0f;
        public const float MaxIntensity = 1f;

        public Emotion Emotion { get; }
        public float Intensity { get; }
        public string Gesture { get; }
        public float Direction { get; }

        public AnimationCue(Emotion emotion, float intensity, string gesture, float direction = 0f)
        {
            Emotion = emotion;
            Intensity = ClampIntensity(intensity);
            Gesture = gesture ?? string.Empty;
            Direction = NormalizeDirection(direction);
        }

        /// <summary>-1 left, +1 right, 0 when the side was not named or the value is not a number.</summary>
        public static float NormalizeDirection(float direction)
        {
            if (float.IsNaN(direction) || direction == 0f)
                return 0f;

            return direction < 0f ? -1f : 1f;
        }

        public static float ClampIntensity(float intensity)
        {
            if (float.IsNaN(intensity) || intensity < MinIntensity)
            {
                return MinIntensity;
            }

            if (float.IsPositiveInfinity(intensity) || intensity > MaxIntensity)
            {
                return MaxIntensity;
            }

            return intensity;
        }
    }
}
