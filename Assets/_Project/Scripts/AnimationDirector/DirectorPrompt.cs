using System;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// Joins a persona's own wording to the director suffix. The suffix is the catalog
    /// plus <see cref="AICompanion.Core.AnimationCueParser.Instruction"/> and
    /// <see cref="AICompanion.Core.EmotionNames.CatalogLine"/>. It is always added, so a
    /// custom persona cannot drop expression, gesture, or portrait cues.
    /// </summary>
    public static class DirectorPrompt
    {
        public static string Append(string personaPrompt, string directorSuffix)
        {
            var body = personaPrompt == null ? string.Empty : personaPrompt.TrimEnd();
            if (string.IsNullOrWhiteSpace(directorSuffix))
                return body;

            var suffix = directorSuffix.Trim();
            if (body.Length == 0)
                return suffix;
            return body + "\n\n" + suffix;
        }
    }
}
