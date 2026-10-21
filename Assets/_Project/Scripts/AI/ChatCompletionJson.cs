using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AICompanion.AI
{
    public static class ChatEndpoints
    {
        public static string CompletionsUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = ChatDefaults.BaseUrl;

            baseUrl = baseUrl.Trim().TrimEnd('/');
            if (baseUrl.EndsWith("/chat/completions", StringComparison.Ordinal))
                return baseUrl;

            return baseUrl + "/chat/completions";
        }
    }

    public static class ChatCompletionJson
    {
        public static float ClampTemperature(float temperature)
        {
            if (temperature < 0f)
                return 0f;
            if (temperature > 2f)
                return 2f;
            return temperature;
        }

        public static int ClampMaxTokens(int maxTokens)
        {
            if (maxTokens <= 0)
                return ChatDefaults.MaxTokens;
            if (maxTokens > 8192)
                return 8192;
            return maxTokens;
        }

        public static string Build(string model, float temperature, int maxTokens, string systemPrompt, IReadOnlyList<ChatMessage> messages)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"model\":");
            JsonText.AppendString(sb, string.IsNullOrWhiteSpace(model) ? ChatDefaults.Model : model.Trim());
            sb.Append(",\"stream\":true,\"temperature\":");
            sb.Append(ClampTemperature(temperature).ToString("0.###", CultureInfo.InvariantCulture));
            var tokens = ClampMaxTokens(maxTokens);
            sb.Append(",\"max_tokens\":");
            sb.Append(tokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"max_completion_tokens\":");
            sb.Append(tokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"messages\":[");

            var first = true;
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                AppendMessage(sb, "system", systemPrompt);
                first = false;
            }

            if (messages != null)
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    AppendMessage(sb, RoleName(messages[i].Role), messages[i].Content);
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Reads the assistant text in a chat-completion chunk.
        /// <c>reasoning_content</c> is a different field and is left out.
        /// </summary>
        public static bool TryReadDeltaContent(string json, out string content)
        {
            content = null;
            if (string.IsNullOrEmpty(json))
                return false;

            const string key = "\"content\"";
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

                var cursor = at + key.Length;
                while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
                    cursor++;
                if (cursor >= json.Length || json[cursor] != ':')
                {
                    i = at + key.Length;
                    continue;
                }

                cursor++;
                while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
                    cursor++;
                if (cursor >= json.Length)
                    return false;

                if (cursor + 4 <= json.Length && string.CompareOrdinal(json, cursor, "null", 0, 4) == 0)
                {
                    i = cursor + 4;
                    continue;
                }

                if (json[cursor] != '"')
                {
                    i = cursor + 1;
                    continue;
                }

                return JsonText.TryReadString(json, cursor, out content);
            }

            return false;
        }

        static void AppendMessage(StringBuilder sb, string role, string content)
        {
            sb.Append("{\"role\":");
            JsonText.AppendString(sb, role);
            sb.Append(",\"content\":");
            JsonText.AppendString(sb, content ?? string.Empty);
            sb.Append('}');
        }

        static string RoleName(ChatRole role)
        {
            switch (role)
            {
                case ChatRole.System:
                    return "system";
                case ChatRole.Assistant:
                    return "assistant";
                default:
                    return "user";
            }
        }
    }
}
