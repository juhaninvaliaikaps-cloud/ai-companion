using System.Text;

namespace AICompanion.Voice
{
    /// <summary>
    /// Turns a chat reply into words that should be spoken.
    /// Markdown markers, emoji, and asterisk actions are dropped.
    /// </summary>
    public static class SpeechText
    {
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            var atLineStart = true;
            var i = 0;
            while (i < text.Length)
            {
                if (Starts(text, i, "```"))
                {
                    var end = text.IndexOf("```", i + 3, System.StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 3;
                    atLineStart = false;
                    continue;
                }

                if (atLineStart)
                {
                    while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
                        i++;
                    if (i >= text.Length)
                        break;
                    if (text[i] == '#')
                    {
                        while (i < text.Length && text[i] == '#')
                            i++;
                        if (i < text.Length && text[i] == ' ')
                            i++;
                        atLineStart = false;
                        continue;
                    }

                    if (text[i] == '>')
                    {
                        i++;
                        if (i < text.Length && text[i] == ' ')
                            i++;
                        atLineStart = false;
                        continue;
                    }
                }

                var c = text[i];
                if (c == '\n' || c == '\r')
                {
                    sb.Append(' ');
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    i++;
                    atLineStart = true;
                    continue;
                }

                atLineStart = false;
                if (c == '!' && i + 1 < text.Length && text[i + 1] == '[')
                {
                    if (TryLink(text, i + 1, out var alt, out var afterImage))
                    {
                        sb.Append(Clean(alt));
                        i = afterImage;
                        continue;
                    }
                }

                if (c == '[')
                {
                    if (TryLink(text, i, out var label, out var afterLink))
                    {
                        sb.Append(Clean(label));
                        i = afterLink;
                        continue;
                    }
                }

                if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    var end = text.IndexOf("**", i + 2, System.StringComparison.Ordinal);
                    if (end > i)
                    {
                        sb.Append(Clean(text.Substring(i + 2, end - (i + 2))));
                        i = end + 2;
                        continue;
                    }
                }

                if (c == '*')
                {
                    var end = text.IndexOf('*', i + 1);
                    if (end > i && text.IndexOf('\n', i + 1, end - i) < 0)
                    {
                        i = end + 1;
                        continue;
                    }

                    i++;
                    continue;
                }

                if (c == '`')
                {
                    var end = text.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        sb.Append(Clean(text.Substring(i + 1, end - (i + 1))));
                        i = end + 1;
                        continue;
                    }

                    i++;
                    continue;
                }

                if (c == '_' && IsWrapStart(text, i))
                {
                    var marker = i + 1 < text.Length && text[i + 1] == '_' ? "__" : "_";
                    var from = i + marker.Length;
                    var end = text.IndexOf(marker, from, System.StringComparison.Ordinal);
                    if (end > from && IsWrapEnd(text, end + marker.Length - 1) && text.IndexOf('\n', from, end - from) < 0)
                    {
                        sb.Append(Clean(text.Substring(from, end - from)));
                        i = end + marker.Length;
                        continue;
                    }
                }

                if (SkipEmoji(text, i, out var emoji))
                {
                    i += emoji;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return Collapse(sb.ToString());
        }

        public static bool HasSpokenCharacter(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsLetterOrDigit(text[i]))
                    return true;
            }

            return false;
        }

        static bool TryLink(string text, int openBracket, out string label, out int next)
        {
            label = null;
            next = openBracket;
            if (openBracket < 0 || openBracket >= text.Length || text[openBracket] != '[')
                return false;

            var close = text.IndexOf(']', openBracket + 1);
            if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
                return false;

            var end = text.IndexOf(')', close + 2);
            if (end < 0)
                return false;

            label = text.Substring(openBracket + 1, close - openBracket - 1);
            next = end + 1;
            return true;
        }

        static bool IsWrapStart(string text, int index)
        {
            if (index <= 0)
                return true;
            return !char.IsLetterOrDigit(text[index - 1]);
        }

        static bool IsWrapEnd(string text, int markerLast)
        {
            var after = markerLast + 1;
            if (after >= text.Length)
                return true;
            return !char.IsLetterOrDigit(text[after]);
        }

        static bool SkipEmoji(string text, int index, out int length)
        {
            length = 1;
            var c = text[index];
            if (char.IsHighSurrogate(c))
            {
                length = index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
                return true;
            }

            if (char.IsLowSurrogate(c))
                return true;
            if (c == '\u200B' || c == '\u200C' || c == '\u200D' || c == '\uFEFF' || c == '\uFE0E' || c == '\uFE0F' || c == '\u20E3')
                return true;
            if (c >= 0x2300 && c <= 0x23FF)
                return true;
            if (c >= 0x2600 && c <= 0x27BF)
                return true;
            if (c >= 0x2B00 && c <= 0x2BFF)
                return true;
            return false;
        }

        static bool Starts(string text, int index, string token)
        {
            if (index < 0 || index + token.Length > text.Length)
                return false;
            return string.CompareOrdinal(text, index, token, 0, token.Length) == 0;
        }

        static string Collapse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            var space = false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '\r' || c == '\n' || c == '\t' || c == ' ')
                {
                    space = sb.Length > 0;
                    continue;
                }

                if (space)
                {
                    sb.Append(' ');
                    space = false;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
