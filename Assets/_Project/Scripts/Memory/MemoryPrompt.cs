using System.Collections.Generic;
using System.Text;

namespace AICompanion.Memory
{
    /// <summary>
    /// Renders retrieved memories and the stored personality as prompt text. Kept
    /// separate from the store so the wording is testable without a file.
    /// </summary>
    public static class MemoryPrompt
    {
        public const string Header = "What you remember from earlier sessions:";
        public const string TimelineHeader = "Shared history (earlier sessions, background only):";
        public const string PersonaHeader = "Your continuing persona:";

        public static string Build(IReadOnlyList<MemoryEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var builder = new StringBuilder(Header);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                    continue;

                builder.Append("\n- ").Append(entry.text.Trim());
            }

            return builder.Length <= Header.Length ? string.Empty : builder.ToString();
        }

        /// <summary>JUH-141: the sub-heading for what the user told her and what they did together.</summary>
        public const string AboutUserHeader = "About the user and your time together:";

        /// <summary>JUH-141: the sub-heading for her own life (Pisara's world, what happened while away).</summary>
        public const string OwnLifeHeader = "Your own life (things that happened to you, not to the user):";

        /// <summary>
        /// As <see cref="Build(IReadOnlyList{MemoryEntry})"/>, with "(2 days ago)" in front of
        /// entries that say when they happened (JUH-117). JUH-141: when the list mixes her own
        /// life (seeded, away and life entries) with what she knows about the user, the two get
        /// their own sub-headings, so she never mistakes the user's dislike for her own. A list
        /// of one kind stays flat.
        /// </summary>
        public static string Build(IReadOnlyList<MemoryEntry> entries, System.DateTime nowUtc)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var hers = 0;
            var theirs = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                    continue;
                if (IsHerOwnLife(entry))
                    hers++;
                else
                    theirs++;
            }

            var builder = new StringBuilder(Header);
            if (hers > 0 && theirs > 0)
            {
                builder.Append("\n").Append(AboutUserHeader);
                AppendEntries(builder, entries, nowUtc, false);
                builder.Append("\n").Append(OwnLifeHeader);
                AppendEntries(builder, entries, nowUtc, true);
            }
            else
            {
                AppendEntries(builder, entries, nowUtc, null);
            }

            return builder.Length <= Header.Length ? string.Empty : builder.ToString();
        }

        /// <summary>Her own life: what Pisara seeded, what happened while the user was away, and her own new threads.</summary>
        public static bool IsHerOwnLife(MemoryEntry entry)
        {
            if (entry == null)
                return false;
            return entry.origin == MemoryOrigins.Seeded || entry.origin == MemoryOrigins.Away || entry.origin == MemoryOrigins.Life;
        }

        static void AppendEntries(StringBuilder builder, IReadOnlyList<MemoryEntry> entries, System.DateTime nowUtc, bool? herOwn)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                    continue;
                if (herOwn.HasValue && IsHerOwnLife(entry) != herOwn.Value)
                    continue;

                builder.Append("\n- ");
                var when = WorldMemory.When(entry, nowUtc);
                if (when.Length > 0)
                    builder.Append('(').Append(when).Append(") ");
                builder.Append(entry.text.Trim());
            }
        }

        public static string BuildPersona(PersonalityState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.personaName))
                return string.Empty;

            var builder = new StringBuilder(PersonaHeader);
            builder.Append("\n- Name: ").Append(state.personaName.Trim());
            if (!string.IsNullOrWhiteSpace(state.mood))
                builder.Append("\n- Mood: ").Append(state.mood.Trim());
            return builder.ToString();
        }

        /// <summary>
        /// Renders the dated timeline entries selected for this turn. The date is local
        /// calendar text; the kind tells the model whether it is a promise, a boundary,
        /// a fact or only a shared event.
        /// </summary>
        /// <summary>The note on an entry she already brought up a few minutes ago (JUH-141).</summary>
        public const string AlreadyRaisedNote = " (you already brought this up today; keep it in mind, but do not repeat it unprompted)";

        /// <summary>
        /// JUH-141: as <see cref="BuildTimeline(IReadOnlyList{TimelineEntry}, System.DateTime)"/>, with
        /// <see cref="AlreadyRaisedNote"/> on the entries she already brought up, so she keeps the
        /// topic in mind without turning it into a verbal tic.
        /// </summary>
        public static string BuildTimeline(IReadOnlyList<TimelineHit> hits, System.DateTime nowUtc)
        {
            if (hits == null || hits.Count == 0)
                return string.Empty;
            var entries = new List<TimelineEntry>(hits.Count);
            var raised = new HashSet<TimelineEntry>();
            for (var i = 0; i < hits.Count; i++)
            {
                entries.Add(hits[i].Entry);
                if (hits[i].AlreadyRaised)
                    raised.Add(hits[i].Entry);
            }

            return BuildTimeline(entries, nowUtc, raised);
        }

        public static string BuildTimeline(IReadOnlyList<TimelineEntry> entries, System.DateTime nowUtc)
        {
            return BuildTimeline(entries, nowUtc, null);
        }

        static string BuildTimeline(IReadOnlyList<TimelineEntry> entries, System.DateTime nowUtc, HashSet<TimelineEntry> raised)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var builder = new StringBuilder(TimelineHeader);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.summary))
                    continue;

                builder.Append("\n- ");
                System.DateTime at;
                if (System.DateTime.TryParse(entry.happenedAtUtc, out at))
                    builder.Append('(').Append(at.ToLocalTime().ToString("d.M.yyyy")).Append(") ");
                builder.Append('[').Append(MemoryDurability.KindLabel(entry.kind)).Append("] ");
                builder.Append(entry.summary.Trim());
                if (raised != null && raised.Contains(entry))
                    builder.Append(AlreadyRaisedNote);
            }

            return builder.Length <= TimelineHeader.Length ? string.Empty : builder.ToString();
        }
    }
}
