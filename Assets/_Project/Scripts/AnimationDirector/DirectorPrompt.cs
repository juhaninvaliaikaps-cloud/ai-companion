using System;
using AICompanion.Core;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// Joins a persona's own wording to the director suffix. The suffix is the catalog
    /// plus <see cref="AICompanion.Core.AnimationCueParser.Instruction"/> and
    /// <see cref="AICompanion.Core.EmotionNames.CatalogLine"/>. It is always added, so a
    /// custom persona cannot drop expression, gesture, or portrait cues.
    /// <see cref="IntimateBoundaries"/> goes between the persona and the suffix for the same
    /// reason: no persona can remove the consent rule or the closed-gate rule (JUH-112).
    /// </summary>
    public static class DirectorPrompt
    {
        public static string Append(string personaPrompt, string directorSuffix)
        {
            return Append(personaPrompt, directorSuffix, IntimateBoundaries.Current);
        }

        public static string Append(string personaPrompt, string directorSuffix, string boundaries)
        {
            var body = personaPrompt == null ? string.Empty : personaPrompt.TrimEnd();
            var rules = string.IsNullOrWhiteSpace(boundaries) ? string.Empty : boundaries.Trim();
            if (rules.Length > 0)
                body = body.Length == 0 ? rules : body + "\n\n" + rules;

            if (string.IsNullOrWhiteSpace(directorSuffix))
                return body;

            var suffix = directorSuffix.Trim();
            if (body.Length == 0)
                return suffix;
            return body + "\n\n" + suffix;
        }
    }
}
