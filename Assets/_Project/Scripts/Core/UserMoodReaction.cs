namespace AICompanion.Core
{
    /// <summary>
    /// The avatar's quick face for the user's mood, shown while the model is still
    /// answering. It is deliberately soft, and it carries no gesture, so the cue in
    /// the reply replaces it and the gesture cooldown is not touched.
    /// </summary>
    public static class UserMoodReaction
    {
        public const float MinIntensity = 0.3f;
        public const float MaxIntensity = 0.7f;

        public static bool TryGetCue(UserMood mood, out AnimationCue cue)
        {
            cue = AnimationCueParser.NeutralCue;
            if (mood.IsNeutral)
                return false;

            var intensity = MinIntensity + (MaxIntensity - MinIntensity) * mood.Intensity;
            cue = new AnimationCue(FaceFor(mood.Emotion), intensity, string.Empty);
            return true;
        }

        /// <summary>She mirrors joy and surprise, and answers distress with concern.</summary>
        public static Emotion FaceFor(UserEmotion emotion)
        {
            switch (emotion)
            {
                case UserEmotion.Happy:
                    return Emotion.Happy;
                case UserEmotion.Sad:
                case UserEmotion.Angry:
                case UserEmotion.Anxious:
                    return Emotion.Concerned;
                case UserEmotion.Surprised:
                    return Emotion.Surprised;
                case UserEmotion.Affectionate:
                    return Emotion.Shy;
                case UserEmotion.Confused:
                    return Emotion.Thinking;
                default:
                    return Emotion.Neutral;
            }
        }
    }
}
