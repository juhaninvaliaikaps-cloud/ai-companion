using System;
using System.Collections.Generic;
using System.Text;
using AICompanion.AI;

namespace AICompanion.UI
{
    /// <summary>
    /// Transcript file. It stores roles and text only. Secret fields are ignored.
    /// </summary>
    public static class ChatHistoryCodec
    {
        public const int MaxLines = 200;

        public static string Write(IReadOnlyList<ChatLine> lines)
        {
            var sb = new StringBuilder();
            sb.Append("{\"version\":1,\"lines\":[");
            var written = 0;
            if (lines != null)
            {
                for (var i = 0; i < lines.Count && written < MaxLines; i++)
                {
                    var line = lines[i];
                    if (line == null || !line.KeepInHistory)
                        continue;
                    if (line.Streaming && string.IsNullOrEmpty(line.Text))
                        continue;
                    if (string.IsNullOrEmpty(line.Text))
                        continue;
                    if (written > 0)
                        sb.Append(',');
                    sb.Append("{\"role\":");
                    JsonText.AppendString(sb, RoleName(line.Kind));
                    sb.Append(",\"text\":");
                    JsonText.AppendString(sb, line.Text);
                    if (line.LocalOnly)
                        sb.Append(",\"local\":true");
                    // JUH-174: a scene note or a change line keeps its colour after a restart.
                    if (line.Tag == ChatLineTag.Scene)
                        sb.Append(",\"tag\":\"scene\"");
                    else if (line.Tag == ChatLineTag.Change)
                        sb.Append(",\"tag\":\"change\",\"change\":").Append(line.Change.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (line.Adult)
                        sb.Append(",\"adult\":true");
                    sb.Append('}');
                    written++;
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static void ReadInto(string json, ChatConversation conversation)
        {
            if (conversation == null || string.IsNullOrWhiteSpace(json))
                return;

            var i = 0;
            var count = 0;
            while (count < MaxLines && i < json.Length)
            {
                var roleAt = json.IndexOf("\"role\"", i, StringComparison.Ordinal);
                if (roleAt < 0)
                    return;
                if (!JsonText.TryReadStringField(json.Substring(roleAt), "role", out var role))
                    return;

                var textAt = json.IndexOf("\"text\"", roleAt + 6, StringComparison.Ordinal);
                if (textAt < 0)
                    return;
                if (!JsonText.TryReadStringField(json.Substring(textAt), "text", out var text))
                    return;

                var nextRole = json.IndexOf("\"role\"", textAt + 6, StringComparison.Ordinal);
                var end = nextRole < 0 ? json.Length : nextRole;
                var localOnly = json.IndexOf("\"local\":true", textAt, end - textAt, StringComparison.Ordinal) >= 0;
                var adult = json.IndexOf("\"adult\":true", textAt, end - textAt, StringComparison.Ordinal) >= 0;
                var tag = ChatLineTag.None;
                var change = 0;
                if (json.IndexOf("\"tag\":\"scene\"", textAt, end - textAt, StringComparison.Ordinal) >= 0)
                    tag = ChatLineTag.Scene;
                else if (json.IndexOf("\"tag\":\"change\"", textAt, end - textAt, StringComparison.Ordinal) >= 0)
                {
                    tag = ChatLineTag.Change;
                    change = ReadInt(json, "\"change\":", textAt, end);
                }

                if (conversation.ImportLine(ParseRole(role), text, localOnly, tag, change, adult))
                    count++;
                i = textAt + 6;
            }
        }

        /// <summary>The whole number after <paramref name="key"/> between <paramref name="from"/> and <paramref name="end"/>, or 0.</summary>
        static int ReadInt(string json, string key, int from, int end)
        {
            var at = json.IndexOf(key, from, end - from, StringComparison.Ordinal);
            if (at < 0)
                return 0;
            var i = at + key.Length;
            var value = 0;
            while (i < end && i < json.Length && char.IsDigit(json[i]))
            {
                value = value * 10 + (json[i] - '0');
                i++;
            }

            return value;
        }

        static string RoleName(ChatLineKind kind)
        {
            switch (kind)
            {
                case ChatLineKind.User:
                    return "user";
                case ChatLineKind.Assistant:
                    return "assistant";
                default:
                    return "note";
            }
        }

        static ChatLineKind ParseRole(string role)
        {
            if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
                return ChatLineKind.User;
            if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
                return ChatLineKind.Assistant;
            return ChatLineKind.System;
        }
    }
}
