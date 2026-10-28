using System;
using System.Collections.Generic;

namespace AICompanion.Memory
{
    /// <summary>
    /// Formatting and paging for the memory viewer. Kept free of Unity UI so the rows
    /// the panel shows can be tested without a scene.
    /// </summary>
    public static class MemoryViewerModel
    {
        public const int MaxLabelLength = 64;
        public const int DefaultPageSize = 8;

        public static string Summary(IReadOnlyList<MemoryEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return "No memories yet. Facts appear here after a conversation.";

            var core = 0;
            var privateCount = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;
                if (entry.isCore)
                    core++;
                if (entry.isPrivate)
                    privateCount++;
            }

            var text = entries.Count + (entries.Count == 1 ? " memory" : " memories");
            if (core > 0)
                text += " · " + core + " core";
            if (privateCount > 0)
                text += " · " + privateCount + " private";
            return text;
        }

        /// <summary>Core memories first, then the newest, capped at <paramref name="max"/>.</summary>
        public static IReadOnlyList<MemoryEntry> Page(IReadOnlyList<MemoryEntry> entries, int max = DefaultPageSize)
        {
            var page = new List<MemoryEntry>();
            if (entries == null || max <= 0)
                return page;

            var sorted = new List<MemoryEntry>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.IsValid)
                    sorted.Add(entry);
            }

            sorted.Sort((a, b) =>
            {
                if (a.isCore != b.isCore)
                    return a.isCore ? -1 : 1;
                return string.CompareOrdinal(b.createdAtUtc, a.createdAtUtc);
            });

            for (var i = 0; i < sorted.Count && page.Count < max; i++)
                page.Add(sorted[i]);

            return page;
        }

        /// <summary>One button label: the fact, truncated, plus its badges.</summary>
        public static string Label(MemoryEntry entry, int maxLength = MaxLabelLength)
        {
            if (entry == null)
                return "Delete: (missing)";

            var text = (entry.text ?? string.Empty).Trim();
            if (text.Length > maxLength)
                text = text.Substring(0, Math.Max(0, maxLength - 1)).TrimEnd() + "…";

            var suffix = entry.isPrivate ? " (private)" : entry.isCore ? " (core)" : string.Empty;
            return "Delete: " + text + suffix;
        }
    }
}
