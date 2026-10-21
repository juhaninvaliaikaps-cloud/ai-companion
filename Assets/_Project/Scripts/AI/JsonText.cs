using System;
using System.Globalization;
using System.Text;

namespace AICompanion.AI
{
    public static class JsonText
    {
        public static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (value != null)
            {
                for (var i = 0; i < value.Length; i++)
                {
                    var c = value[i];
                    switch (c)
                    {
                        case '"':
                            sb.Append("\\\"");
                            break;
                        case '\\':
                            sb.Append("\\\\");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        default:
                            if (c < 0x20)
                            {
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }

                            break;
                    }
                }
            }

            sb.Append('"');
        }

        public static bool TryReadStringField(string json, string field, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field))
                return false;

            var key = "\"" + field + "\"";
            var i = 0;
            while (i < json.Length)
            {
                var at = json.IndexOf(key, i, StringComparison.Ordinal);
                if (at < 0)
                    return false;

                if (at > 0)
                {
                    var prev = json[at - 1];
                    if (char.IsLetterOrDigit(prev) || prev == '_')
                    {
                        i = at + key.Length;
                        continue;
                    }
                }

                var cursor = SkipWs(json, at + key.Length);
                if (cursor >= json.Length || json[cursor] != ':')
                {
                    i = at + key.Length;
                    continue;
                }

                cursor = SkipWs(json, cursor + 1);
                if (cursor >= json.Length || json[cursor] != '"')
                {
                    i = cursor < json.Length ? cursor + 1 : json.Length;
                    continue;
                }

                return TryReadString(json, cursor, out value);
            }

            return false;
        }

        public static bool TryReadBoolField(string json, string field, out bool value)
        {
            value = false;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field))
                return false;

            var key = "\"" + field + "\"";
            var at = json.IndexOf(key, StringComparison.Ordinal);
            if (at < 0)
                return false;

            var cursor = SkipWs(json, at + key.Length);
            if (cursor >= json.Length || json[cursor] != ':')
                return false;

            cursor = SkipWs(json, cursor + 1);
            if (StartsAt(json, cursor, "true"))
            {
                value = true;
                return true;
            }

            if (StartsAt(json, cursor, "false"))
            {
                value = false;
                return true;
            }

            return false;
        }

        public static bool TryReadString(string json, int quoteIndex, out string value)
        {
            value = null;
            if (json == null || quoteIndex < 0 || quoteIndex >= json.Length || json[quoteIndex] != '"')
                return false;

            var sb = new StringBuilder();
            for (var i = quoteIndex + 1; i < json.Length; i++)
            {
                var c = json[i];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (i + 1 >= json.Length)
                    return false;

                var next = json[++i];
                switch (next)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(next);
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        if (i + 4 >= json.Length)
                            return false;
                        var hex = json.Substring(i + 1, 4);
                        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            return false;
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default:
                        return false;
                }
            }

            return false;
        }

        static int SkipWs(string json, int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
            return index;
        }

        static bool StartsAt(string json, int index, string token)
        {
            if (index < 0 || index + token.Length > json.Length)
                return false;
            return string.CompareOrdinal(json, index, token, 0, token.Length) == 0;
        }
    }
}
