using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Memory
{
    /// <summary>
    /// Decides whether a candidate line is durable relationship memory or only scene state.
    /// The session model is instructed to make this distinction, but local models still write
    /// "she trembled" or "the room went quiet". Those lines are filtered here before they can
    /// reach the fact store or the timeline.
    /// </summary>
    public static class MemoryDurability
    {
        public const string Fact = "fact";
        public const string Preference = "preference";
        public const string Boundary = "boundary";
        public const string Promise = "promise";
        public const string Event = "event";
        public const string RelationshipChange = "relationship_change";
        public const string OpenThread = "open_thread";

        static readonly string[] TransientMarkers =
        {
            "trembl", "blush", "shiver", "shudder", "heart raced", "heart pounded",
            "breath caught", "breath hitched", "looked at", "looks at", "smiled",
            "smiles", "sighed", "sighs", "the room went", "the room fell", "silence",
            "eyes widened", "eyes darted", "cheeks burned", "felt flustered",
            "felt shy", "felt embarrassed", "felt nervous", "in that moment",
            "for a moment", "just then", "suddenly",
        };

        static readonly string[] TransientStarts =
        {
            "she ", "he ", "they ", "the room ", "the air ", "it felt ",
        };

        static readonly HashSet<string> KeyStopWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "and", "for", "with", "that", "this", "was", "were", "are", "is",
            "called", "mentioned", "about", "from", "has", "have", "had", "her", "his",
            "she", "him", "them", "they", "their", "its", "into", "over", "after",
        };

        public static bool IsDurable(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var trimmed = text.Trim();
            if (trimmed.Length < 18)
                return false;

            var lower = trimmed.ToLowerInvariant();
            for (var i = 0; i < TransientMarkers.Length; i++)
            {
                if (lower.IndexOf(TransientMarkers[i], StringComparison.Ordinal) >= 0)
                    return false;
            }

            // "She was afraid of being left" is durable even though it starts with "she".
            // A bare physical beat ("She trembled.") is not. Requiring a durable noun or
            // relationship verb keeps the second kind out without dropping the first.
            if (StartsWithAny(lower, TransientStarts) && !HasDurableSubject(lower))
                return false;
            return true;
        }

        static bool HasDurableSubject(string lower)
        {
            return lower.IndexOf(" afraid", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" wants", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" likes", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" loves", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" hates", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" prefers", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" needs", StringComparison.Ordinal) >= 0
                || lower.IndexOf("does not want", StringComparison.Ordinal) >= 0
                || lower.IndexOf("doesn't want", StringComparison.Ordinal) >= 0
                || lower.IndexOf("do not want", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" promised", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" agreed", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" admitted", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" told", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" said", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" works", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" lives", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" family", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" sister", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" brother", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" mother", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" father", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" boundary", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" trigger", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" relationship", StringComparison.Ordinal) >= 0
                || lower.IndexOf(" name", StringComparison.Ordinal) >= 0;
        }

        static bool StartsWithAny(string text, string[] prefixes)
        {
            for (var i = 0; i < prefixes.Length; i++)
            {
                if (text.StartsWith(prefixes[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static string KindOf(string text)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            if (lower.IndexOf("promise", StringComparison.Ordinal) >= 0
                || lower.IndexOf("promised", StringComparison.Ordinal) >= 0
                || lower.IndexOf("agreed", StringComparison.Ordinal) >= 0)
                return Promise;
            if (lower.IndexOf("boundary", StringComparison.Ordinal) >= 0
                || lower.IndexOf("trigger", StringComparison.Ordinal) >= 0
                || lower.IndexOf("doesn't want", StringComparison.Ordinal) >= 0
                || lower.IndexOf("does not want", StringComparison.Ordinal) >= 0
                || lower.IndexOf("never touch", StringComparison.Ordinal) >= 0)
                return Boundary;
            if (lower.IndexOf("likes", StringComparison.Ordinal) >= 0
                || lower.IndexOf("loves", StringComparison.Ordinal) >= 0
                || lower.IndexOf("hates", StringComparison.Ordinal) >= 0
                || lower.IndexOf("prefers", StringComparison.Ordinal) >= 0)
                return Preference;
            if (lower.IndexOf("first time", StringComparison.Ordinal) >= 0
                || lower.IndexOf("confessed", StringComparison.Ordinal) >= 0
                || lower.IndexOf("admitted", StringComparison.Ordinal) >= 0
                || lower.IndexOf("relationship", StringComparison.Ordinal) >= 0)
                return RelationshipChange;
            if (lower.IndexOf("still", StringComparison.Ordinal) >= 0
                && (lower.IndexOf("unresolved", StringComparison.Ordinal) >= 0
                    || lower.IndexOf("open", StringComparison.Ordinal) >= 0
                    || lower.IndexOf("waiting", StringComparison.Ordinal) >= 0))
                return OpenThread;
            if (lower.IndexOf("name is", StringComparison.Ordinal) >= 0
                || lower.IndexOf("called", StringComparison.Ordinal) >= 0
                || lower.IndexOf("works", StringComparison.Ordinal) >= 0
                || lower.IndexOf("lives", StringComparison.Ordinal) >= 0
                || lower.IndexOf("has a", StringComparison.Ordinal) >= 0
                || lower.IndexOf("afraid", StringComparison.Ordinal) >= 0)
                return Fact;
            return Event;
        }

        public static string Why(string kind)
        {
            switch (kind)
            {
                case Promise:
                    return "A promise changes what she should do or remember next time.";
                case Boundary:
                    return "A boundary must be respected in later scenes.";
                case Preference:
                    return "A stable preference can shape later choices.";
                case RelationshipChange:
                    return "It changes how close or open she should be.";
                case OpenThread:
                    return "It is unresolved and may come up again.";
                case Fact:
                    return "It is stable background she should not ask about again.";
                default:
                    return "It is a shared event worth knowing later.";
            }
        }

        /// <summary>Normalised key for exact/similar duplicate checks.</summary>
        public static string CanonicalKey(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var lower = text.ToLowerInvariant();
            var builder = new StringBuilder(lower.Length);
            var inWord = false;
            for (var i = 0; i < lower.Length; i++)
            {
                var c = lower[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    inWord = true;
                }
                else if (inWord)
                {
                    builder.Append(' ');
                    inWord = false;
                }
            }

            var parts = builder.ToString().Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length >= 3 && !KeyStopWords.Contains(parts[i]))
                    set.Add(parts[i]);
            }

            var sorted = new List<string>(set);
            sorted.Sort(StringComparer.Ordinal);
            return string.Join(" ", sorted.ToArray());
        }

        /// <summary>Fuzzy duplicate check for paraphrased summaries.</summary>
        public static bool Similar(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;
            var a = CanonicalKey(left);
            var b = CanonicalKey(right);
            if (a.Length == 0 || b.Length == 0)
                return false;
            if (string.Equals(a, b, StringComparison.Ordinal))
                return true;

            var leftWords = new HashSet<string>(a.Split(' '), StringComparer.Ordinal);
            var rightWords = new HashSet<string>(b.Split(' '), StringComparer.Ordinal);
            if (leftWords.Count == 0 || rightWords.Count == 0)
                return false;
            var overlap = 0;
            foreach (var word in leftWords)
            {
                if (rightWords.Contains(word))
                    overlap++;
            }

            var union = leftWords.Count + rightWords.Count - overlap;
            return union > 0 && overlap / (float)union >= 0.70f;
        }

        public static string KindLabel(string kind)
        {
            switch (kind)
            {
                case Fact: return "fact";
                case Preference: return "preference";
                case Boundary: return "boundary";
                case Promise: return "promise";
                case RelationshipChange: return "relationship";
                case OpenThread: return "open thread";
                default: return "event";
            }
        }
    }
}
