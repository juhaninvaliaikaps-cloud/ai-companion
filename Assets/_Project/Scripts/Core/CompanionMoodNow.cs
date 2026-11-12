namespace AICompanion.Core
{
    /// <summary>
    /// Her shown mood right now, for the parts that render it (JUH-103 phase 2). The mood
    /// keeper writes it; the face, the portrait, the 2D clips, the director and the prompt
    /// read it. Intimate is filtered through <see cref="IntimateExpressions.Active"/> on read.
    /// </summary>
    public static class CompanionMoodNow
    {
        public static CompanionMood Shown { get; private set; }

        /// <summary>Strength of the shown mood, 0 to 1. 0 for Content.</summary>
        public static float Strength { get; private set; }

        public static void Set(CompanionMood shown, float strength)
        {
            Shown = shown;
            Strength = float.IsNaN(strength) ? 0f : (strength < 0f ? 0f : (strength > 1f ? 1f : strength));
        }

        public static void Reset()
        {
            Set(CompanionMood.Content, 0f);
        }

        /// <summary>The mood that counts now, after the Intimate gate.</summary>
        public static CompanionMood Effective
        {
            get { return CompanionMoodLooks.Effective(Shown, IntimateExpressions.Active); }
        }

        /// <summary>The rest look now.</summary>
        public static MoodLook Rest
        {
            get { return CompanionMoodLooks.Rest(Shown, Strength, IntimateExpressions.Active); }
        }

        /// <summary>The softer face for a cue's face word now, or empty.</summary>
        public static string Soften(string cueFace)
        {
            return CompanionMoodLooks.Soften(Shown, cueFace, IntimateExpressions.Active);
        }
    }
}
