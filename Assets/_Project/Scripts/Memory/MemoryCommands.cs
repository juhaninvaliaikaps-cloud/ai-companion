using System;

namespace AICompanion.Memory
{
    /// <summary>
    /// Detects an explicit "remember this" request and extracts the durable sentence.
    /// This is a local command, not a prompt instruction: it never asks the model to
    /// narrate that a memory was saved.
    /// </summary>
    public static class MemoryCommands
    {
        // JUH-134 review: no bare "remember " or "muista ". "Remember when we went to the
        // beach" is a recollection, not a request. Longest prefix first.
        static readonly string[] Prefixes =
        {
            "please remember that ", "please remember this: ", "please remember this, ", "please remember to ",
            "remember that ", "remember this: ", "remember this, ", "remember this - ", "remember to ",
            "don't forget that ", "don't forget to ", "don't forget ",
            "do not forget that ", "do not forget to ", "do not forget ",
            "keep in mind that ", "keep in mind ",
            "muista että ", "muista tämä: ", "muista tämä, ",
            "älä unohda että ", "älä unohda ",
        };

        // What follows these is a shared moment or a plea, not a fact to store:
        // "remember that time we...", "don't forget me", "älä unohda minua".
        static readonly string[] Recollections =
        {
            "when ", "how ", "what ", "where ", "who ", "why ", "the time", "that time", "time ",
            "the day", "that day", "day ", "the night", "that night", "night ", "the moment", "moment ",
            "last time", "yesterday", "me", "us", "our ", "about me", "about us",
            "kun ", "miten ", "mitä ", "milloin ", "missä ", "minua", "minut", "meitä", "meidät",
        };

        /// <summary>The words her memory keeps, so "I" and "my" stay the user's and are never read as hers.</summary>
        public const string StoredPrefix = "The user asked you to remember: ";

        public static bool TryExtract(string text, out string memory)
        {
            memory = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var trimmed = text.Trim();
            var lower = trimmed.ToLowerInvariant().Replace('’', '\'');
            for (var i = 0; i < Prefixes.Length; i++)
            {
                if (!lower.StartsWith(Prefixes[i], StringComparison.Ordinal))
                    continue;
                // "remember to call mom" keeps its "to".
                var keep = Prefixes[i].EndsWith(" to ", StringComparison.Ordinal) ? 3 : 0;
                var value = trimmed.Substring(Prefixes[i].Length - keep).Trim();
                value = value.TrimEnd('.', '!', '?', ' ');
                if (value.Length < 4 || value.IndexOf('?') >= 0 || IsRecollection(value))
                    return false;
                memory = value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// JUH-134 review: "Remember that I hate pickles" is stored as the user's words, not as
        /// "I hate pickles", which she would read under "What you remember" as her own dislike.
        /// </summary>
        public static string Stored(string memory)
        {
            if (string.IsNullOrWhiteSpace(memory))
                return string.Empty;
            return StoredPrefix + "\"" + memory.Trim().Trim('"') + "\"";
        }

        static bool IsRecollection(string value)
        {
            var lower = value.ToLowerInvariant();
            for (var i = 0; i < Recollections.Length; i++)
            {
                var marker = Recollections[i];
                if (!lower.StartsWith(marker, StringComparison.Ordinal))
                    continue;
                // "me", "us", "minua": the whole remainder or a whole first word, not "meeting".
                if (marker.EndsWith(" ", StringComparison.Ordinal) || lower.Length == marker.Length
                    || !char.IsLetter(lower[marker.Length]))
                    return true;
            }

            return false;
        }
    }
}
