namespace AICompanion.Core
{
    /// <summary>
    /// One line for the system prompt that tells the model how the user's message
    /// reads. It goes to the same provider as the message itself, so it reveals
    /// nothing the request did not already contain.
    /// </summary>
    public static class UserMoodPrompt
    {
        public const float ClearFrom = 0.45f;
        public const float StrongFrom = 0.75f;

        /// <summary>Empty for a neutral mood.</summary>
        public static string Line(UserMood mood)
        {
            if (mood.IsNeutral)
                return string.Empty;

            return "Mood hint: the user's latest message reads as " + Describe(mood.Emotion)
                + " (" + Strength(mood.Intensity) + "). This is a guess from their wording, so trust the message itself over the hint. "
                + "Let it shape your tone and the cue emotion.";
        }

        /// <summary>
        /// Adds the line after the prompt. A neutral mood or an empty prompt returns the
        /// prompt unchanged, so the default system prompt still applies when none is set.
        /// </summary>
        public static string Append(string prompt, UserMood mood)
        {
            var line = Line(mood);
            if (line.Length == 0 || string.IsNullOrWhiteSpace(prompt))
                return prompt;

            return prompt.TrimEnd() + "\n\n" + line;
        }

        public static string Strength(float intensity)
        {
            if (intensity >= StrongFrom)
                return "strong";
            if (intensity >= ClearFrom)
                return "clear";
            return "mild";
        }

        static string Describe(UserEmotion emotion)
        {
            switch (emotion)
            {
                case UserEmotion.Happy:
                    return "happy";
                case UserEmotion.Sad:
                    return "sad";
                case UserEmotion.Angry:
                    return "angry or frustrated";
                case UserEmotion.Anxious:
                    return "anxious or worried";
                case UserEmotion.Surprised:
                    return "surprised";
                case UserEmotion.Affectionate:
                    return "affectionate";
                case UserEmotion.Confused:
                    return "confused";
                default:
                    return "neutral";
            }
        }
    }
}
