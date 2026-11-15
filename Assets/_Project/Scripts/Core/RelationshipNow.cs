using System;

namespace AICompanion.Core
{
    /// <summary>
    /// The relationship between chat and the relationship keeper (JUH-118 section 4,
    /// docs/PISARA.md "Relationship"). The chat assembly cannot see the settings assembly, so
    /// they meet here, like the mood does through <see cref="CompanionMoodNow"/>.
    /// <list type="bullet">
    /// <item>Chat reports each finished reply to a user message once (<see cref="ReportReply"/>).
    /// The keeper listens, lands the cue's <c>landed</c> word with <see cref="RelationshipMath"/>
    /// and records the reason for the Mood card.</item>
    /// <item>The keeper publishes the prompt line (<see cref="RelationshipPrompt.Line"/>); chat
    /// adds it after the mood line. Empty while the Relationship switch is off or there is no
    /// character sheet.</item>
    /// </list>
    /// Main thread only.
    /// </summary>
    public static class RelationshipNow
    {
        static string _line = string.Empty;

        /// <summary>One finished reply to a user message, with its cue (neutral when the tag was missing or unreadable).</summary>
        public static event Action<AnimationCue> ReplyFinished;

        /// <summary>
        /// A landing changed the relationship (JUH-122): raised by <c>RelationshipKeeper.Apply</c> only
        /// when the change was applied, so never while "Relationship changes" is off. The floating
        /// pieces near her listen.
        /// </summary>
        public static event Action<RelationshipNotice> Changed;

        public static void ReportChanged(RelationshipNotice notice)
        {
            var handler = Changed;
            if (handler != null)
                handler(notice);
        }

        public static string PromptLine
        {
            get { return _line; }
        }

        public static void SetPromptLine(string line)
        {
            _line = string.IsNullOrWhiteSpace(line) ? string.Empty : line.Trim();
        }

        public static void ReportReply(AnimationCue cue)
        {
            var handler = ReplyFinished;
            if (handler != null)
                handler(cue);
        }

        /// <summary>The persona prompt with the relationship line after it, or unchanged when there is none.</summary>
        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_line.Length == 0)
                return body;
            return body.Length == 0 ? _line : body + "\n\n" + _line;
        }

        public static void Reset()
        {
            _line = string.Empty;
        }

        /// <summary>
        /// One reply landed (pure, for the keeper and tests): the cue's <c>landed</c> word and
        /// intensity against the current numbers.
        /// </summary>
        public static RelationshipChange Land(AnimationCue cue, float affection, float familiarity)
        {
            // JUH-123 will pass the cue's secret= hit here as the crit hook.
            return RelationshipMath.Land(cue.Landed, cue.Intensity, affection, familiarity, false);
        }
    }
}
