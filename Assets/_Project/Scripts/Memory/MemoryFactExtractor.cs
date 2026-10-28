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
