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
                    if (line == null || line.Streaming && string.IsNullOrEmpty(line.Text))
                        continue;
                    if (string.IsNullOrEmpty(line.Text))
                        continue;
                    if (written > 0)
                        sb.Append(',');
                    sb.Append("{\"role\":");
                    JsonText.AppendString(sb, RoleName(line.Kind));
                    sb.Append(",\"text\":");
                    JsonText.AppendString(sb, line.Text);
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

                if (conversation.ImportLine(ParseRole(role), text))
                    count++;
                i = textAt + 6;
            }
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
