using System.Text;

namespace AICompanion.Voice
{
    /// <summary>
    /// Decides whether a local transcript is worth sending. Blank whisper tokens
    /// and punctuation-only text are dropped. The accepted text is not logged.
    /// </summary>
    public static class TranscriptText
    {
        static readonly string[] Dropped =
        {
            "[BLANK_AUDIO]",
            "[NOISE]",
            "[MUSIC]",
            "[SILENCE]",
            "(blank audio)",
            "(noise)",
            "(music)",
            "(silence)"
        };

        public static bool TryAccept(string raw, out string text)
        {
            text = raw ?? string.Empty;
            for (var i = 0; i < Dropped.Length; i++)
                text = ReplaceIgnoreCase(text, Dropped[i], " ");

            text = Collapse(text);
            if (!HasLetterOrDigit(text))
            {
                text = string.Empty;
                return false;
            }

            return true;
        }

        static bool HasLetterOrDigit(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsLetterOrDigit(value[i]))
                    return true;
            }

            return false;
        }

        static string Collapse(string value)
        {
            var sb = new StringBuilder(value.Length);
            var pendingSpace = false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                        pendingSpace = true;
                    continue;
                }

                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        static string ReplaceIgnoreCase(string value, string token, string with)
        {
            var index = 0;
            while (index < value.Length)
            {
                var found = IndexOfIgnoreCase(value, token, index);
                if (found < 0)
                    break;

                value = value.Substring(0, found) + with + value.Substring(found + token.Length);
                index = found + with.Length;
            }

            return value;
        }

        static int IndexOfIgnoreCase(string value, string token, int start)
        {
            var limit = value.Length - token.Length;
            for (var i = start; i <= limit; i++)
            {
                var match = true;
                for (var j = 0; j < token.Length; j++)
                {
                    if (char.ToUpperInvariant(value[i + j]) != char.ToUpperInvariant(token[j]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }
    }
}
