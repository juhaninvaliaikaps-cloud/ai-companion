namespace AICompanion.Core
{
    /// <summary>
    /// One prompt line with her mood, so her wording matches what she shows (JUH-103 phase 2).
    /// It goes right after the persona text. Intimate is named only while
    /// <see cref="IntimateExpressions.Active"/>; otherwise it reads as content.
    /// </summary>
    public static class CompanionMoodPrompt
    {
        public const float ModerateFrom = 0.5f;
        public const float StrongFrom = 0.75f;

        public static string Degree(float strength)
        {
            if (strength >= StrongFrom)
                return "strong";
            if (strength >= ModerateFrom)
                return "moderate";
            return "mild";
        }

        public static string Line(CompanionMood mood, float strength, bool intimateActive)
        {
            var effective = CompanionMoodLooks.Effective(mood, intimateActive);
            if (effective == CompanionMood.Content)
                return "Your current mood: content. Let it colour your tone a little; the conversation still decides what you say.";
            return "Your current mood: " + CompanionMoodNames.Name(effective) + ", " + Degree(strength)
                + ". Let it colour your tone a little; the conversation still decides what you say.";
        }

        /// <summary>The persona text with the mood line after it.</summary>
        public static string Append(string personaPrompt, CompanionMood mood, float strength, bool intimateActive)
        {
            var body = personaPrompt == null ? string.Empty : personaPrompt.TrimEnd();
            var line = Line(mood, strength, intimateActive);
            return body.Length == 0 ? line : body + "\n" + line;
        }

        /// <summary>The persona text with the current mood line after it.</summary>
        public static string AppendCurrent(string personaPrompt)
        {
            return Append(personaPrompt, CompanionMoodNow.Shown, CompanionMoodNow.Strength, IntimateExpressions.Active);
        }
    }
}
