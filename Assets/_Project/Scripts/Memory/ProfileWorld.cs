using System;
using System.Collections.Generic;
using System.Globalization;
using AICompanion.Core;

namespace AICompanion.Memory
{
    /// <summary>One line of her week: when, and what.</summary>
    public struct ProfileDated
    {
        public string When;
        public string Text;
    }

    /// <summary>
    /// JUH-121, Profile → Her world: what Pisara wrote about her life, readable by the user.
    /// What she is doing right now (from the sheet), what she is waiting for (open threads),
    /// what happened while you were away, her last days, and her backstory. Built from the
    /// active persona's memory store; nothing here is sent anywhere.
    /// </summary>
    public sealed class ProfileWorld
    {
        public const string Earlier = "earlier";

        public string RightNow = string.Empty;
        public readonly List<string> Waiting = new List<string>();
        public readonly List<ProfileDated> WhileAway = new List<ProfileDated>();
        public readonly List<ProfileDated> Week = new List<ProfileDated>();
        public readonly List<string> Backstory = new List<string>();

        public bool IsEmpty
        {
            get { return RightNow.Length == 0 && Waiting.Count == 0 && WhileAway.Count == 0 && Week.Count == 0 && Backstory.Count == 0; }
        }

        public static ProfileWorld Build(IReadOnlyList<MemoryEntry> entries, CharacterSheet sheet, DateTime nowUtc)
        {
            var view = new ProfileWorld();
            if (sheet != null && sheet.world != null && !string.IsNullOrWhiteSpace(sheet.world.rightNow))
                view.RightNow = sheet.world.rightNow.Trim();
            if (entries == null)
                return view;

            var week = new List<KeyValuePair<DateTime, ProfileDated>>();
            var away = new List<KeyValuePair<DateTime, ProfileDated>>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsValid)
                    continue;
                var text = entry.text.Trim();
                if (entry.isThread)
                {
                    view.Waiting.Add(text);
                    continue;
                }

                if (entry.origin == MemoryOrigins.Away)
                {
                    away.Add(Dated(entry, text, nowUtc));
                    continue;
                }

                if (entry.origin != MemoryOrigins.Seeded)
                    continue;
                if (entry.horizon == MemoryHorizons.Short)
                    week.Add(Dated(entry, text, nowUtc));
                else
                    view.Backstory.Add(text);
            }

            Newest(away, view.WhileAway);
            Newest(week, view.Week);
            return view;
        }

        static KeyValuePair<DateTime, ProfileDated> Dated(MemoryEntry entry, string text, DateTime nowUtc)
        {
            DateTime happened;
            if (!DateTime.TryParse(entry.happenedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out happened))
                happened = DateTime.MinValue;
            var when = WorldMemory.When(entry, nowUtc);
            return new KeyValuePair<DateTime, ProfileDated>(happened.ToUniversalTime(), new ProfileDated
            {
                When = when.Length > 0 ? when : Earlier,
                Text = text,
            });
        }

        static void Newest(List<KeyValuePair<DateTime, ProfileDated>> items, List<ProfileDated> into)
        {
            items.Sort((a, b) => b.Key.CompareTo(a.Key));
            for (var i = 0; i < items.Count; i++)
                into.Add(items[i].Value);
        }
    }

    /// <summary>JUH-121, Profile → Memories: what she remembers about the user, newest first. Her own life is on Her world.</summary>
    public static class ProfileMemories
    {
        public const int MaxShown = 8;

        public static List<string> AboutYou(IReadOnlyList<MemoryEntry> entries, int max, out int total)
        {
            total = 0;
            var list = new List<string>();
            if (entries == null)
                return list;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsValid || entry.isThread || entry.origin != MemoryOrigins.Lived)
                    continue;
                total++;
                if (list.Count < max)
                    list.Add(entry.text.Trim());
            }

            return list;
        }

        public static string Summary(int total)
        {
            if (total == 0)
                return "She does not remember anything about you yet.";
            return total == 1 ? "She remembers 1 thing about you." : "She remembers " + total + " things about you.";
        }
    }
}
