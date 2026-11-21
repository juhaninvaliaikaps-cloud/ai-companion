using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-156: one lorebook entry, as SillyTavern, Chub and Character Tavern use them: when a
    /// key turns up in the recent chat, its content is added to her prompt. A constant entry is
    /// always added. A selective entry also needs one of its secondary keys.
    /// </summary>
    public sealed class LoreEntry
    {
        public string Name = string.Empty;
        public List<string> Keys = new List<string>();
        public List<string> SecondaryKeys = new List<string>();
        public string Content = string.Empty;
        public bool Constant;
        public bool Selective;
        public bool Enabled = true;
        public bool CaseSensitive;

        /// <summary>Insertion order: lower comes first in the prompt and is kept first when the budget runs out.</summary>
        public int Order = 100;

        public string Label
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Name))
                    return Name.Trim();
                return Keys.Count > 0 ? Keys[0] : "(no name)";
            }
        }
    }

    /// <summary>JUH-156: a persona's lorebook, kept encrypted beside her persona file.</summary>
    public sealed class Lorebook
    {
        public const int MaxEntries = 500;
        public const int MaxEntryLength = 4000;
        public const int MaxKeys = 24;
        public const int MaxKeyLength = 100;

        public string Name = string.Empty;

        /// <summary>
        /// True for an 18+ lorebook: it is used only while the 18+ gate is open, and the open gate
        /// keeps chat on the local model, so it never reaches a cloud model.
        /// </summary>
        public bool Adult;

        public List<LoreEntry> Entries = new List<LoreEntry>();

        public int EnabledCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Entries.Count; i++)
                    if (Entries[i].Enabled)
                        n++;
                return n;
            }
        }

        public int ConstantCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Entries.Count; i++)
                    if (Entries[i].Enabled && Entries[i].Constant)
                        n++;
                return n;
            }
        }

        public bool IsEmpty => Entries.Count == 0;
    }

    /// <summary>JUH-156: picks the entries for one message, within a character budget.</summary>
    public static class LoreMatcher
    {
        /// <summary>About 750 tokens: room for a few entries without crowding a small local model.</summary>
        public const int Budget = 3000;

        /// <summary>How many earlier chat lines are scanned for keys, besides the new message.</summary>
        public const int ScanLines = 4;

        public const string Heading = "What you know about this world (use it when it fits; don't recite it):";

        /// <summary>
        /// The entries whose keys are in <paramref name="recentText"/>, plus the constant ones, in
        /// insertion order, until <paramref name="budget"/> characters. Empty for an 18+ book while
        /// <paramref name="adultOpen"/> is false.
        /// </summary>
        public static List<LoreEntry> Select(Lorebook book, string recentText, bool adultOpen, int budget = Budget)
        {
            var picked = new List<LoreEntry>();
            if (book == null || book.Entries.Count == 0 || (book.Adult && !adultOpen))
                return picked;
            var text = recentText ?? string.Empty;
            var candidates = new List<KeyValuePair<int, LoreEntry>>();
            for (var i = 0; i < book.Entries.Count; i++)
            {
                var entry = book.Entries[i];
                if (entry == null || !entry.Enabled || string.IsNullOrWhiteSpace(entry.Content))
                    continue;
                if (entry.Constant || Triggered(entry, text))
                    candidates.Add(new KeyValuePair<int, LoreEntry>(i, entry));
            }

            // Constant entries first, then insertion order, then file order.
            candidates.Sort((a, b) =>
            {
                if (a.Value.Constant != b.Value.Constant)
                    return a.Value.Constant ? -1 : 1;
                var order = a.Value.Order.CompareTo(b.Value.Order);
                return order != 0 ? order : a.Key.CompareTo(b.Key);
            });

            var used = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                var length = candidates[i].Value.Content.Trim().Length + 2;
                if (used + length > budget)
                    continue;
                used += length;
                picked.Add(candidates[i].Value);
            }

            return picked;
        }

        public static string Block(Lorebook book, string recentText, bool adultOpen, int budget = Budget)
        {
            var picked = Select(book, recentText, adultOpen, budget);
            if (picked.Count == 0)
                return string.Empty;
            var sb = new StringBuilder(Heading);
            for (var i = 0; i < picked.Count; i++)
                sb.Append("\n- ").Append(picked[i].Content.Trim());
            return sb.ToString();
        }

        public static bool Triggered(LoreEntry entry, string text)
        {
            if (entry == null || string.IsNullOrEmpty(text) || !AnyKey(entry.Keys, text, entry.CaseSensitive))
                return false;
            if (!entry.Selective || entry.SecondaryKeys.Count == 0)
                return true;
            return AnyKey(entry.SecondaryKeys, text, entry.CaseSensitive);
        }

        static bool AnyKey(List<string> keys, string text, bool caseSensitive)
        {
            for (var i = 0; i < keys.Count; i++)
                if (Contains(text, keys[i], caseSensitive))
                    return true;
            return false;
        }

        /// <summary>A key is a whole word or phrase: "cat" is in "the cat sat", not in "category".</summary>
        public static bool Contains(string text, string key, bool caseSensitive)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(text))
                return false;
            var needle = key.Trim();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var start = 0;
            while (start <= text.Length - needle.Length)
            {
                var at = text.IndexOf(needle, start, comparison);
                if (at < 0)
                    return false;
                var before = at == 0 || !char.IsLetterOrDigit(text[at - 1]) || !char.IsLetterOrDigit(needle[0]);
                var end = at + needle.Length;
                var after = end >= text.Length || !char.IsLetterOrDigit(text[end]) || !char.IsLetterOrDigit(needle[needle.Length - 1]);
                if (before && after)
                    return true;
                start = at + 1;
            }

            return false;
        }
    }

    /// <summary>
    /// JUH-156: the active persona's lorebook, handed to chat like <see cref="AdultNotesNow"/>.
    /// An 18+ lorebook adds nothing while the 18+ gate is closed; an open gate keeps chat local.
    /// </summary>
    public static class LoreNow
    {
        static Lorebook _book;

        public static Lorebook Book => _book;

        public static void Set(Lorebook book)
        {
            _book = book != null && !book.IsEmpty ? book : null;
        }

        public static void Reset()
        {
            _book = null;
        }

        /// <summary>The prompt with the entries that <paramref name="recentText"/> calls up.</summary>
        public static string Append(string prompt, string recentText)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_book == null)
                return body;
            var block = LoreMatcher.Block(_book, recentText, IntimateExpressions.Active);
            if (block.Length == 0)
                return body;
            return body.Length == 0 ? block : body + "\n\n" + block;
        }
    }
}
