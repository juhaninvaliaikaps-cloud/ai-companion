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

        /// <summary>The emotion attribute text, trimmed, before aliasing. Empty when the tag omitted it.</summary>
        public string EmotionField { get; }

        /// <summary>
        /// A body reaction clip the reply asks for by name, such as "pout" (2D video avatar).
        /// Lower case letters, digits and underscores. Empty when the tag named none.
        /// </summary>
        public string Reaction { get; }

        /// <summary>
        /// How the user's last message landed with her (JUH-117), from the tag's
        /// <c>landed="..."</c> attribute. Neutral when the tag did not say.
        /// </summary>
        public LandingEvent Landed { get; }

        public AnimationCue(Emotion emotion, float intensity, string gesture, float direction = 0f, string emotionField = null, string reaction = null,
            LandingEvent landed = LandingEvent.Neutral)
        {
            Landed = landed;
            Emotion = emotion;
            Intensity = ClampIntensity(intensity);
            Gesture = gesture ?? string.Empty;
            Direction = NormalizeDirection(direction);
            var emotionValue = EmotionNames.SanitizeField(emotionField);
            var reactionValue = AnimationCueParser.SanitizeGesture(reaction);
            // Small models sometimes put a gated portrait id in reaction= or gesture=
            // instead of emotion=. The portraits are emotion values, so promote it before
            // the reaction path can reject an id that only exists in the portrait set.
            if (IntimateExpressions.IsIntimateId(reactionValue) && IntimateExpressions.Allows(reactionValue))
            {
                emotionValue = reactionValue;
                reactionValue = string.Empty;
            }
            else if (IntimateExpressions.IsIntimateId(Gesture) && IntimateExpressions.Allows(Gesture))
            {
                emotionValue = Gesture;
                Gesture = string.Empty;
            }

            EmotionField = emotionValue;
            Reaction = reactionValue;
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
