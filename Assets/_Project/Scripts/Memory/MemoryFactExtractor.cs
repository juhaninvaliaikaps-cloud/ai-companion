using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AICompanion.Memory
{
    /// <summary>
    /// Turns one finished exchange into durable facts. Two parts, both pure: a prompt
    /// that asks the active provider for a short JSON array, and a tolerant parser that
    /// finds the array even when the model wraps it in prose or a code fence.
    /// </summary>
    public static class MemoryFactExtractor
    {
        public const int MaxFactsPerCapture = 6;
        public const int MinFactLength = 3;
        public const int MaxFactLength = 200;

        public const string SystemPrompt =
            "You extract durable facts about the user from a chat exchange. " +
            "Return only a JSON array of short third-person sentences, for example " +
            "[\"The user's name is Jussi\", \"The user likes coffee with oat milk\"]. " +
            "Keep only stable facts: name, identity, preferences, relationships, plans, recurring context. " +
            "Skip small talk, questions, and anything the assistant said about itself. " +
            "Return [] when there is nothing durable.";

        /// <summary>JUH-141: how many stored facts the request may see and correct.</summary>
        public const int MaxKnownFacts = 6;
        public const int MaxUpdatesPerCapture = 3;

        /// <summary>Added after <see cref="SystemPrompt"/> when stored facts are listed.</summary>
        public const string CorrectionRule =
            "Facts already stored are listed below with ids like [f1]. Do not repeat one that is still true. " +
            "If the exchange shows that one has changed or was wrong, put \"update f1: \" and the full corrected " +
            "sentence in the array instead of a new fact, for example [\"update f1: The user's sister moved to Oulu\"]. " +
            "Use only the ids listed.";

        static readonly Regex UpdateItem = new Regex(@"^\s*update\s*\[?\s*f(\d{1,2})\s*\]?\s*[:\-–]\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>One "update fN: ..." item: which listed fact (1-based) and the corrected sentence.</summary>
        public struct FactUpdate
        {
            public int Index;
            public string Text;
        }

        /// <summary>The extraction prompt, with the stored facts the exchange is about when there are any.</summary>
        public static string PromptWith(IReadOnlyList<MemoryEntry> known)
        {
            if (known == null || known.Count == 0)
                return SystemPrompt;
            var builder = new StringBuilder(SystemPrompt.Length + 400);
            builder.Append(SystemPrompt).Append(' ').Append(CorrectionRule).Append("\n\nStored facts:");
            for (var i = 0; i < known.Count && i < MaxKnownFacts; i++)
            {
                if (known[i] != null)
                    builder.Append("\n[f").Append(i + 1).Append("] ").Append(known[i].text.Trim());
            }

            return builder.ToString();
        }

        /// <summary>
        /// The "update fN: ..." items of the reply. An index outside 1..<paramref name="knownCount"/>
        /// is dropped, and so is a second update of the same fact.
        /// </summary>
        public static List<FactUpdate> ParseUpdates(string reply, int knownCount)
        {
            var updates = new List<FactUpdate>();
            if (string.IsNullOrWhiteSpace(reply) || knownCount <= 0)
                return updates;
            var start = reply.IndexOf('[');
            var end = reply.LastIndexOf(']');
            if (start < 0 || end <= start)
                return updates;

            foreach (Match match in Quoted.Matches(reply.Substring(start, end - start + 1)))
            {
                var item = UpdateItem.Match(Unescape(match.Groups[1].Value));
                if (!item.Success)
                    continue;
                int index;
                if (!int.TryParse(item.Groups[1].Value, out index) || index < 1 || index > knownCount)
                    continue;
                var text = item.Groups[2].Value.Trim();
                if (text.Length < MinFactLength || text.Length > MaxFactLength || HasUpdate(updates, index))
                    continue;
                updates.Add(new FactUpdate { Index = index, Text = text });
                if (updates.Count >= MaxUpdatesPerCapture)
                    break;
            }

            return updates;
        }

        static bool HasUpdate(List<FactUpdate> updates, int index)
        {
            for (var i = 0; i < updates.Count; i++)
            {
                if (updates[i].Index == index)
                    return true;
            }

            return false;
        }

        static readonly Regex Quoted = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);

        static readonly string[] CoreMarkers =
        {
            "my name is",
            "name is",
            "call me",
            "my name",
            "i am called",
        };

        public static string BuildExchangeText(string userText, string assistantText)
        {
            var builder = new StringBuilder();
            builder.Append("User: ").Append((userText ?? string.Empty).Trim());
            if (!string.IsNullOrWhiteSpace(assistantText))
                builder.Append("\nAssistant: ").Append(assistantText.Trim());
            return builder.ToString();
        }

        /// <summary>
        /// Parses the model reply into facts. Anything that does not look like a JSON
        /// array of strings yields an empty list, so a chatty answer cannot poison the
        /// store.
        /// </summary>
        public static List<string> Parse(string reply)
        {
            var facts = new List<string>();
            if (string.IsNullOrWhiteSpace(reply))
                return facts;

            var start = reply.IndexOf('[');
            var end = reply.LastIndexOf(']');
            if (start < 0 || end <= start)
                return facts;

            var array = reply.Substring(start, end - start + 1);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Quoted.Matches(array))
            {
                var fact = Unescape(match.Groups[1].Value).Trim();
                if (fact.Length < MinFactLength || fact.Length > MaxFactLength)
                    continue;
                // JUH-141: a correction is not a new fact.
                if (UpdateItem.IsMatch(fact))
                    continue;
                if (!seen.Add(fact))
                    continue;

                facts.Add(fact);
                if (facts.Count >= MaxFactsPerCapture)
                    break;
            }

            return facts;
        }

        /// <summary>
        /// True for identity facts. These are always included in the prompt, so a
        /// question like "what is my name?" does not depend on the wording of the
        /// stored sentence.
        /// </summary>
        public static bool IsCoreFact(string fact)
        {
            if (string.IsNullOrWhiteSpace(fact))
                return false;

            var lowered = fact.ToLowerInvariant();
            for (var i = 0; i < CoreMarkers.Length; i++)
            {
                if (lowered.Contains(CoreMarkers[i]))
                    return true;
            }

            return false;
        }

        static string Unescape(string value)
        {
            if (value.IndexOf('\\') < 0)
                return value;

            return value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", " ")
                .Replace("\\t", " ");
        }
    }
}
