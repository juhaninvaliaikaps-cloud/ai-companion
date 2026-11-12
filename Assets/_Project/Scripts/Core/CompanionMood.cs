using System;

namespace AICompanion.Core
{
    /// <summary>
    /// Her lasting mood (JUH-103). Content is the baseline. Every other mood has a strength
    /// from 0 to 1 in <see cref="CompanionMoodState"/>; only the strongest one shows.
    /// </summary>
    public enum CompanionMood
    {
        Content = 0,
        Happy = 1,
        Affectionate = 2,
        Flustered = 3,
        Annoyed = 4,
        Sad = 5,
        Anxious = 6,
        Sleepy = 7,

        /// <summary>Only with <see cref="IntimateExpressions.Active"/>. Otherwise always 0 and never shown.</summary>
        Intimate = 8,
    }

    public static class CompanionMoodNames
    {
        public const int Count = 9;

        static readonly string[] Names =
        {
            "content", "happy", "affectionate", "flustered", "annoyed", "sad", "anxious", "sleepy", "intimate",
        };

        /// <summary>Lower-case name, as in the mood file and the log.</summary>
        public static string Name(CompanionMood mood)
        {
            var index = (int)mood;
            return index >= 0 && index < Names.Length ? Names[index] : "content";
        }

        public static bool TryParse(string text, out CompanionMood mood)
        {
            mood = CompanionMood.Content;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var wanted = text.Trim();
            for (var i = 0; i < Names.Length; i++)
            {
                if (string.Equals(Names[i], wanted, StringComparison.OrdinalIgnoreCase))
                {
                    mood = (CompanionMood)i;
                    return true;
                }
            }

            return false;
        }
    }
}
