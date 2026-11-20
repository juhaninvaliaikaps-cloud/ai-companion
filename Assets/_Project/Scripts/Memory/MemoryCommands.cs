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
        static readonly string[] Prefixes =
        {
            "please remember that ", "please remember ",
            "remember that ", "remember ",
            "don't forget that ", "don't forget ", "do not forget that ", "do not forget ",
            "keep in mind that ", "keep in mind ",
            "muista että ", "muista ",
            "älä unohda että ", "älä unohda ",
        };

        public static bool TryExtract(string text, out string memory)
        {
            memory = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var trimmed = text.Trim();
            var lower = trimmed.ToLowerInvariant();
            for (var i = 0; i < Prefixes.Length; i++)
            {
                if (!lower.StartsWith(Prefixes[i], StringComparison.Ordinal))
                    continue;
                var value = trimmed.Substring(Prefixes[i].Length).Trim();
                value = value.TrimEnd('.', '!', '?', ' ');
                if (value.Length < 4 || value.IndexOf('?') >= 0)
                    return false;
                memory = value;
                return true;
            }

            return false;
        }
    }
}
