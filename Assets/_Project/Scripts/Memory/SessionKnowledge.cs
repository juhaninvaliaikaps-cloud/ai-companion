using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AICompanion.Memory
{
    /// <summary>One memory she already has, shown to the session summary under a short id ("m1").</summary>
    public struct KnownMemory
    {
        public string Label;
        public string EntryId;
        public TimelineEntry Entry;
    }

    /// <summary>A correction the session summary asked for: replace an entry's sentence.</summary>
    public struct MemoryUpdate
    {
        public string EntryId;
        public SessionMemory Memory;
    }

    /// <summary>
    /// JUH-141 (improvements 4 and 5): what the session summary knows before it writes.
    ///
    /// Before, the summary model never saw her memories (the request is <c>NoMemory</c>), so a
    /// correction ("her sister moved from Tampere to Helsinki") became a second, contradicting
    /// entry, and a promise stayed open forever. Now the summary gets the related entries and the
    /// open promises and threads, each with a short id, plus today's date. It can answer
    /// <c>update m1: …</c> to correct an entry and <c>resolved m2</c> to close one, and it writes
    /// real dates instead of "on Sunday". Code keeps the state: an unknown id, a vague or passing
    /// sentence, or an id the model was never shown is ignored.
    ///
    /// Privacy: a summary that goes to a cloud model is shown only entries that are not private.
    /// </summary>
    public static class SessionKnowledge
    {
        public const int MaxRelated = 6;
        public const int MaxKnown = 8;

        /// <summary>
        /// The memories to show the summary: the entries the conversation is about (ranked by
        /// <see cref="TimelineSearch"/> over the transcript), then the open promises and threads,
        /// newest first. Labels m1, m2, … in that order.
        /// </summary>
        public static List<KnownMemory> For(IReadOnlyList<TimelineEntry> entries, string transcript, bool keepsLocal, DateTime nowUtc)
        {
            var known = new List<KnownMemory>(MaxKnown);
            if (entries == null || entries.Count == 0)
                return known;
            var chosen = new HashSet<TimelineEntry>();

            // Ranked without the "already raised" rule: the summary must see the topics the talk was about.
            var hits = TimelineSearch.Rank(Fresh(entries), transcript, string.Empty, keepsLocal, nowUtc, MaxRelated);
            for (var i = 0; i < hits.Count; i++)
            {
                var original = Original(entries, hits[i].Entry);
                if (original != null && chosen.Add(original))
                    known.Add(new KnownMemory { Entry = original, EntryId = original.id });
            }

            var open = new List<TimelineEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsValid || entry.resolved || chosen.Contains(entry))
                    continue;
                if (entry.isPrivate && !keepsLocal)
                    continue;
                if (entry.kind == MemoryDurability.Promise || entry.kind == MemoryDurability.OpenThread)
                    open.Add(entry);
            }

            open.Sort((a, b) => string.CompareOrdinal(b.happenedAtUtc, a.happenedAtUtc));
            for (var i = 0; i < open.Count && known.Count < MaxKnown; i++)
                known.Add(new KnownMemory { Entry = open[i], EntryId = open[i].id });

            for (var i = 0; i < known.Count; i++)
            {
                var k = known[i];
                k.Label = "m" + (i + 1);
                known[i] = k;
            }

            return known;
        }

        // Copies without recall times, so "already raised" never hides a topic from the summary.
        static List<TimelineEntry> Fresh(IReadOnlyList<TimelineEntry> entries)
        {
            var copies = new List<TimelineEntry>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                    continue;
                copies.Add(new TimelineEntry
                {
                    id = e.id, title = e.title, summary = e.summary, kind = e.kind, importance = e.importance,
                    confidence = e.confidence, evidenceCount = e.evidenceCount, source = e.source, isPrivate = e.isPrivate,
                    resolved = e.resolved, happenedAtUtc = e.happenedAtUtc, createdAtUtc = e.createdAtUtc,
                });
            }

            return copies;
        }

        static TimelineEntry Original(IReadOnlyList<TimelineEntry> entries, TimelineEntry copy)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && string.Equals(entries[i].id, copy.id, StringComparison.Ordinal))
                    return entries[i];
            }

            return null;
        }

        // ---- The prompt -----------------------------------------------------------------------

        /// <summary>"Today is Friday 20.11.2026." in English, whatever the PC's language.</summary>
        public static string TodayLine(DateTime localDate)
        {
            return "Today is " + localDate.ToString("dddd", CultureInfo.InvariantCulture) + " "
                   + localDate.ToString("d.M.yyyy", CultureInfo.InvariantCulture)
                   + ". Write any date as a real date (for example \"on Sunday "
                   + NextSunday(localDate).ToString("d.M.yyyy", CultureInfo.InvariantCulture)
                   + "\"), never only \"tomorrow\" or \"on Sunday\".";
        }

        static DateTime NextSunday(DateTime date)
        {
            var days = ((int)DayOfWeek.Sunday - (int)date.DayOfWeek + 7) % 7;
            return date.Date.AddDays(days == 0 ? 7 : days);
        }

        /// <summary>The block added to the summary prompt; only the date when she remembers nothing yet.</summary>
        public static string PromptBlock(IReadOnlyList<KnownMemory> known, DateTime localDate)
        {
            var sb = new StringBuilder(512);
            sb.Append(TodayLine(localDate));
            if (known == null || known.Count == 0)
                return sb.ToString();

            sb.Append("\nWhat she already remembers (do not write these again):");
            for (var i = 0; i < known.Count; i++)
            {
                var entry = known[i].Entry;
                if (entry == null)
                    continue;
                sb.Append("\n[").Append(known[i].Label).Append("] ");
                DateTime at;
                if (DateTime.TryParse(entry.happenedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out at))
                    sb.Append('(').Append(at.ToLocalTime().ToString("d.M.yyyy", CultureInfo.InvariantCulture)).Append(") ");
                sb.Append('[').Append(MemoryDurability.KindLabel(entry.kind)).Append("] ").Append((entry.summary ?? string.Empty).Trim());
            }

            sb.Append("\n- If the conversation changed or corrected one of these, write \"update m1: \" and the full new sentence instead of a new line.");
            sb.Append("\n- If it shows that a promise or open thread was kept, is done, or is no longer true, write a line \"resolved m2\".");
            sb.Append("\n- Use only the ids above. Write nothing about the ones that did not change.");
            return sb.ToString();
        }

        // ---- The reply ---------------------------------------------------------------------

        static readonly Regex UpdateLine = new Regex(@"^\s*[-*•]?\s*\**update\**\s*\[?\s*(m\d{1,2})\s*\]?\s*[:\-–]\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static readonly Regex ResolvedLine = new Regex(@"^\s*[-*•]?\s*\**resolved\**\s*:?\s*\[?\s*(m\d{1,2})\s*\]?\s*\.?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>True when the line is an update or resolved line (it must not be stored as a memory).</summary>
        public static bool IsChangeLine(string line)
        {
            return !string.IsNullOrWhiteSpace(line) && (UpdateLine.IsMatch(line) || ResolvedLine.IsMatch(line));
        }

        /// <summary>
        /// The updates and resolved ids in a summary reply, mapped from labels to entry ids. A label
        /// the model was not shown is dropped; so is a line that resolves and updates the same entry.
        /// </summary>
        public static void ParseChanges(string reply, IReadOnlyList<KnownMemory> known, List<MemoryUpdate> updates, List<string> resolved)
        {
            if (string.IsNullOrWhiteSpace(reply) || known == null || known.Count == 0)
                return;
            var lines = reply.Replace("\r", string.Empty).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var match = UpdateLine.Match(line);
                if (match.Success)
                {
                    var id = EntryIdFor(known, match.Groups[1].Value);
                    var memory = SessionMemories.ParseLineForUpdate(match.Groups[2].Value);
                    if (id != null && memory != null && memory.IsValid && !Contains(updates, id))
                        updates.Add(new MemoryUpdate { EntryId = id, Memory = memory });
                    continue;
                }

                match = ResolvedLine.Match(line);
                if (match.Success)
                {
                    var id = EntryIdFor(known, match.Groups[1].Value);
                    if (id != null && !resolved.Contains(id))
                        resolved.Add(id);
                }
            }

            // An entry that is both updated and resolved: the update wins (it says what is true now).
            for (var i = resolved.Count - 1; i >= 0; i--)
            {
                if (Contains(updates, resolved[i]))
                    resolved.RemoveAt(i);
            }
        }

        static string EntryIdFor(IReadOnlyList<KnownMemory> known, string label)
        {
            var wanted = (label ?? string.Empty).Trim().ToLowerInvariant();
            for (var i = 0; i < known.Count; i++)
            {
                if (string.Equals(known[i].Label, wanted, StringComparison.Ordinal))
                    return known[i].EntryId;
            }

            return null;
        }

        static bool Contains(List<MemoryUpdate> updates, string entryId)
        {
            for (var i = 0; i < updates.Count; i++)
            {
                if (string.Equals(updates[i].EntryId, entryId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
