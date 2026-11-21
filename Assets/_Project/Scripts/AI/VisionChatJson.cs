using System;
using System.Globalization;
using System.Text;

namespace AICompanion.AI
{
    /// <summary>
    /// JUH-149: one image request in the OpenAI chat-completions shape, for every
    /// OpenAI-compatible cloud API that is not xAI (DeepSeek, OpenAI, OpenRouter and others):
    /// the user content is a text block and an <c>image_url</c> block with a base64 data URL.
    /// Not streamed. DeepSeek's thinking is turned off, as for text.
    /// </summary>
    public static class VisionChatJson
    {
        public static string Build(string model, int maxTokens, string systemPrompt, string userText, byte[] jpeg, bool disableThinking)
        {
            var sb = new StringBuilder(1024 + (jpeg != null ? jpeg.Length * 2 : 0));
            sb.Append("{\"model\":");
            JsonText.AppendString(sb, string.IsNullOrWhiteSpace(model) ? ChatDefaults.Model : model.Trim());
            var tokens = maxTokens <= 0 || maxTokens > VisionCapability.MaxOutputTokens ? VisionCapability.MaxOutputTokens : maxTokens;
            sb.Append(",\"stream\":false,\"max_tokens\":").Append(tokens.ToString(CultureInfo.InvariantCulture));
            if (disableThinking)
                sb.Append(",\"thinking\":{\"type\":\"disabled\"}");
            sb.Append(",\"messages\":[");
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                sb.Append("{\"role\":\"system\",\"content\":");
                JsonText.AppendString(sb, systemPrompt);
                sb.Append("},");
            }

            sb.Append("{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":");
            JsonText.AppendString(sb, userText ?? string.Empty);
            sb.Append("},{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/jpeg;base64,");
            sb.Append(jpeg != null && jpeg.Length > 0 ? Convert.ToBase64String(jpeg) : string.Empty);
            sb.Append("\"}}]}]}");
            return sb.ToString();
        }

        /// <summary>The assistant text in <c>choices[0].message.content</c>. Reasoning text is left out.</summary>
        public static bool TryReadContent(string json, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(json))
                return false;
            var at = json.IndexOf("\"message\"", StringComparison.Ordinal);
            if (at < 0)
                return false;
            var rest = json.Substring(at);
            var content = rest.IndexOf("\"content\"", StringComparison.Ordinal);
            if (content < 0)
                return false;
            if (!JsonText.TryReadStringField(rest.Substring(content), "content", out text) || string.IsNullOrEmpty(text))
            {
                text = null;
                return false;
            }

            return true;
        }
    }
}
