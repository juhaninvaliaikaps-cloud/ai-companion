using System;
using System.Collections.Generic;
using System.Globalization;
using AICompanion.Core;

namespace AICompanion.Memory
{
    /// <summary>What happened to her while the user was away. <see cref="Happened"/> is false when nothing did.</summary>
    public struct AwayEvent
    {
        public bool Happened;
        public bool WentWell;

        /// <summary>The new memory, such as "Portfolio review on Friday. It went well."</summary>
        public string Text;

        /// <summary>One line for the next prompt, so she can bring it up herself.</summary>
        public string PromptLine;

        /// <summary>Mood nudges for the mood state. Never null when <see cref="Happened"/>.</summary>
        public CompanionMoodPush[] MoodPushes;
    }

    /// <summary>
    /// Her world in memory (JUH-117, "Pisara"). Plain rules, no model:
    /// <list type="bullet">
    /// <item><see cref="Seed"/> turns the character sheet's world into memories of a life before the user.</item>
    /// <item><see cref="MarkRecalled"/> and <see cref="Fade"/> keep short-term memories for about two
    /// weeks and promote the ones that keep coming up.</item>
    /// <item><see cref="ResolveThread"/> lets one open thread turn out well or badly while the user is away.</item>
    /// </list>
    /// Seeded entries carry the private flag of the session that made them, so the existing
    /// rule holds: a private memory never goes into a cloud prompt.
    /// </summary>
    public static class WorldMemory
    {
        /// <summary>A short-term memory that was not promoted is forgotten after this many days.</summary>
        public const int ShortLifeDays = 14;

        /// <summary>A short-term memory brought into a prompt this many times becomes long-term.</summary>
        public const int PromoteAfterRecalls = 2;

        /// <summary>The user has to be away at least this long before a thread resolves: 4 hours.</summary>
        public const double MinAwaySeconds = 4d * 3600d;

        /// <summary>The share of threads that turn out well.</summary>
        public const double GoodChance = 0.6d;

        /// <summary>Entries older than this get no "(n days ago)" in the prompt.</summary>
        public const int MaxDaysShown = 60;

        // ---- seeding ---------------------------------------------------------------------------

        /// <summary>
        /// True when this sheet's world is not in the store yet. A sheet without a prompt hash
        /// is never seeded: without the hash there is no way to tell it was seeded already, and
        /// seeding it again on every start would bring resolved threads back.
        /// </summary>
        public static bool NeedsSeeding(MemoryStore store, CharacterSheet sheet)
        {
            if (store == null || sheet == null || string.IsNullOrEmpty(sheet.promptHash))
                return false;
            return store.SeededFromHash != sheet.promptHash;
        }

        /// <summary>
        /// Writes her life before the user into memory: backstory as long-term facts, the last
        /// week as short-term memories dated before <paramref name="metAtUtc"/>, and open
        /// threads. An earlier seeding from another sheet is replaced; lived and away memories
        /// are kept. Returns how many entries were added, or 0 when this sheet is already in.
        /// </summary>
        public static int Seed(MemoryStore store, CharacterSheet sheet, DateTime metAtUtc, bool privateMode)
        {
            if (!NeedsSeeding(store, sheet))
                return 0;

            sheet.Normalize();
            store.RemoveWhere(entry => entry.origin == MemoryOrigins.Seeded);

            var met = metAtUtc.ToUniversalTime();
            var added = 0;
            var world = sheet.world;
            for (var i = 0; i < world.backstory.Count; i++)
            {
                added += Added(store, new MemoryEntry
                {
                    text = world.backstory[i],
                    origin = MemoryOrigins.Seeded,
                    horizon = MemoryHorizons.Long,
                    isPrivate = privateMode,
                });
            }

            for (var i = 0; i < world.lastWeek.Count; i++)
            {
                added += Added(store, new MemoryEntry
                {
                    text = world.lastWeek[i].text,
                    origin = MemoryOrigins.Seeded,
                    horizon = MemoryHorizons.Short,
                    happenedAtUtc = met.AddDays(-world.lastWeek[i].daysAgo).ToString("o", CultureInfo.InvariantCulture),
                    isPrivate = privateMode,
                });
            }

            for (var i = 0; i < world.openThreads.Count; i++)
            {
                added += Added(store, new MemoryEntry
                {
                    text = world.openThreads[i].text,
                    origin = MemoryOrigins.Seeded,
                    horizon = MemoryHorizons.Long,
                    isThread = true,
                    goodOutcome = world.openThreads[i].goodOutcome,
                    badOutcome = world.openThreads[i].badOutcome,
                    isPrivate = privateMode,
                });
            }

            store.SetSeededFromHash(sheet.promptHash);
            store.Save();
            return added;
        }

        static int Added(MemoryStore store, MemoryEntry entry)
        {
            var before = store.Count;
            store.AddEntry(entry);
            return store.Count - before;
        }

        /// <summary>
        /// The line that opens the very first conversation mid-scene, or empty when the sheet
        /// does not say what she was doing.
        /// </summary>
        public static string FirstScene(CharacterSheet sheet)
        {
            if (sheet == null || sheet.world == null || string.IsNullOrWhiteSpace(sheet.world.rightNow))
                return string.Empty;
            return "This is the first time the user talks to you. You were in the middle of this: "
                + sheet.world.rightNow.Trim() + " Start from there, in your own words.";
        }

        // ---- a lived short-term memory -----------------------------------------------------------

        /// <summary>
        /// Stores something from this session as a short-term memory (the session-end
        /// summaries of JUH-118). It fades unless it keeps coming up.
        /// </summary>
        public static MemoryEntry AddShortTerm(MemoryStore store, string text, DateTime nowUtc, bool privateMode)
        {
            if (store == null || string.IsNullOrWhiteSpace(text))
                return null;
            var entry = store.AddEntry(new MemoryEntry
            {
                text = text,
                kind = MemoryKind.Summary,
                origin = MemoryOrigins.Lived,
                horizon = MemoryHorizons.Short,
                happenedAtUtc = nowUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                createdAtUtc = nowUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                isPrivate = privateMode,
            });
            store.Save();
            return entry;
        }

        // ---- promotion and fading ----------------------------------------------------------------

        /// <summary>
        /// Call with the entries that went into a prompt. A short-term memory that came up
        /// <see cref="PromoteAfterRecalls"/> times becomes long-term. Returns how many were promoted.
        /// </summary>
        public static int MarkRecalled(MemoryStore store, IReadOnlyList<MemoryEntry> recalled)
        {
            if (store == null || recalled == null)
                return 0;
            var promoted = 0;
            var changed = false;
            for (var i = 0; i < recalled.Count; i++)
            {
                var entry = recalled[i];
                if (entry == null || !entry.IsShortTerm)
                    continue;
                entry.recalls++;
                changed = true;
                if (entry.recalls >= PromoteAfterRecalls)
                {
                    entry.horizon = MemoryHorizons.Long;
                    promoted++;
                }
            }

            if (changed)
                store.Save();
            return promoted;
        }

        /// <summary>
        /// Forgets short-term memories older than <see cref="ShortLifeDays"/> (counted from when
        /// they were stored). Core facts and threads are never forgotten this way. Returns how
        /// many were removed.
        /// </summary>
        public static int Fade(MemoryStore store, DateTime nowUtc)
        {
            if (store == null)
                return 0;
            var now = nowUtc.ToUniversalTime();
            var removed = store.RemoveWhere(entry =>
            {
                if (!entry.IsShortTerm || entry.isCore || entry.isThread)
                    return false;
                DateTime stored;
                if (!TryParse(entry.createdAtUtc, out stored))
                    return false;
                return (now - stored).TotalDays > ShortLifeDays;
            });
            if (removed > 0)
                store.Save();
            return removed;
        }

        // ---- life goes on while the user is away -------------------------------------------------

        /// <summary>
        /// When the user was away for at least <see cref="MinAwaySeconds"/> and she has an open
        /// thread, it turns out well or badly: the thread is replaced by a short-term "away"
        /// memory. <paramref name="roll"/> is a random number from 0 to 1 (below
        /// <see cref="GoodChance"/> it goes well). With <paramref name="includePrivate"/> false,
        /// private threads are left alone, so nothing private reaches a cloud prompt.
        /// </summary>
        public static AwayEvent ResolveThread(MemoryStore store, double awaySeconds, double roll, DateTime nowUtc, bool includePrivate)
        {
            var nothing = new AwayEvent();
            if (store == null || double.IsNaN(awaySeconds) || awaySeconds < MinAwaySeconds)
                return nothing;

            MemoryEntry thread = null;
            var entries = store.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsValid || !entry.isThread)
                    continue;
                if (entry.isPrivate && !includePrivate)
                    continue;
                if (string.IsNullOrWhiteSpace(entry.goodOutcome) && string.IsNullOrWhiteSpace(entry.badOutcome))
                    continue;
                thread = entry;
                break;
            }

            if (thread == null)
                return nothing;

            var wentWell = double.IsNaN(roll) || roll < GoodChance;
            // A thread with only one written outcome takes that one.
            if (wentWell && string.IsNullOrWhiteSpace(thread.goodOutcome))
                wentWell = false;
            else if (!wentWell && string.IsNullOrWhiteSpace(thread.badOutcome))
                wentWell = true;

            var outcome = (wentWell ? thread.goodOutcome : thread.badOutcome).Trim();
            var text = thread.text.Trim() + " " + outcome;
            var now = nowUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            var isPrivate = thread.isPrivate;
            var threadId = thread.id;

            store.RemoveWhere(entry => entry.id == threadId);
            store.AddEntry(new MemoryEntry
            {
                text = text,
                origin = MemoryOrigins.Away,
                horizon = MemoryHorizons.Short,
                happenedAtUtc = now,
                createdAtUtc = now,
                isPrivate = isPrivate,
            });
            store.Save();

            return new AwayEvent
            {
                Happened = true,
                WentWell = wentWell,
                Text = text,
                PromptLine = "While the user was away, this happened to you: " + text
                    + " Bring it up in your own words if it fits; do not recite it.",
                MoodPushes = wentWell
                    ? new[] { new CompanionMoodPush(CompanionMood.Happy, 0.25f) }
                    : new[] { new CompanionMoodPush(CompanionMood.Sad, 0.2f), new CompanionMoodPush(CompanionMood.Anxious, 0.08f) },
            };
        }

        // ---- time ------------------------------------------------------------------------------

        /// <summary>
        /// "today", "yesterday" or "3 days ago" for an entry that says when it happened; empty
        /// when it does not, when the date is in the future, or after <see cref="MaxDaysShown"/> days.
        /// </summary>
        public static string When(MemoryEntry entry, DateTime nowUtc)
        {
            DateTime happened;
            if (entry == null || !TryParse(entry.happenedAtUtc, out happened))
                return string.Empty;
            var days = (int)Math.Floor((nowUtc.ToUniversalTime() - happened).TotalDays);
            if (days < 0 || days > MaxDaysShown)
                return string.Empty;
            if (days == 0)
                return "today";
            if (days == 1)
                return "yesterday";
            return days.ToString(CultureInfo.InvariantCulture) + " days ago";
        }

        static bool TryParse(string value, out DateTime utc)
        {
            utc = default(DateTime);
            if (string.IsNullOrWhiteSpace(value))
                return false;
            DateTime parsed;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                return false;
            utc = parsed.ToUniversalTime();
            return true;
        }
    }
}
