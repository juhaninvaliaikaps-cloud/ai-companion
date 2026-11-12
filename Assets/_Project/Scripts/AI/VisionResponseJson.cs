using System;
using System.Globalization;
using System.Text;

namespace AICompanion.AI
{
    /// <summary>
    /// One image request for the xAI Responses API, which is what the image
    /// understanding guide uses for grok-4.7. <c>store</c> is false so the
    /// screenshot is not kept as server history. Detail is low to bound tokens.
    /// </summary>
    public static class VisionResponseJson
    {
        public const string Detail = "low";

        public static string ResponsesUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = ChatDefaults.BaseUrl;

            baseUrl = baseUrl.Trim().TrimEnd('/');
            if (baseUrl.EndsWith("/responses", StringComparison.Ordinal))
                return baseUrl;
            if (baseUrl.EndsWith("/chat/completions", StringComparison.Ordinal))
                baseUrl = baseUrl.Substring(0, baseUrl.Length - "/chat/completions".Length);

            return baseUrl + "/responses";
        }

        public static string Build(string model, int maxOutputTokens, string systemPrompt, string userText, byte[] jpeg)
        {
            var sb = new StringBuilder(1024 + (jpeg != null ? jpeg.Length * 2 : 0));
            sb.Append("{\"model\":");
            JsonText.AppendString(sb, string.IsNullOrWhiteSpace(model) ? ChatDefaults.Model : model.Trim());
            sb.Append(",\"store\":false,\"stream\":false,\"reasoning\":{\"effort\":\"low\"},\"max_output_tokens\":");
            var tokens = maxOutputTokens <= 0 ? VisionCapability.MaxOutputTokens : maxOutputTokens;
            if (tokens > VisionCapability.MaxOutputTokens)
                tokens = VisionCapability.MaxOutputTokens;
            sb.Append(tokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"input\":[");
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                sb.Append("{\"role\":\"system\",\"content\":");
                JsonText.AppendString(sb, systemPrompt);
                sb.Append("},");
            }

            sb.Append("{\"role\":\"user\",\"content\":[");
            sb.Append("{\"type\":\"input_image\",\"image_url\":\"data:image/jpeg;base64,");
            sb.Append(jpeg != null && jpeg.Length > 0 ? Convert.ToBase64String(jpeg) : string.Empty);
            sb.Append("\",\"detail\":\"");
            sb.Append(Detail);
            sb.Append("\"},{\"type\":\"input_text\",\"text\":");
            JsonText.AppendString(sb, userText ?? string.Empty);
            sb.Append("}]}]}");
            return sb.ToString();
        }

        /// <summary>
        /// Reads assistant <c>output_text</c> parts. Encrypted reasoning is ignored.
        /// </summary>
        public static bool TryReadOutputText(string json, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(json))
                return false;

            const string marker = "\"output_text\"";
            var sb = new StringBuilder();
            var i = 0;
            var found = false;
            while (i < json.Length)
            {
                var at = json.IndexOf(marker, i, StringComparison.Ordinal);
                if (at < 0)
                    break;

                var textAt = json.IndexOf("\"text\"", at + marker.Length, StringComparison.Ordinal);
                if (textAt < 0)
                    break;

                if (JsonText.TryReadStringField(json.Substring(textAt), "text", out var piece) && !string.IsNullOrEmpty(piece))
                {
                    if (sb.Length > 0)
                        sb.Append('\n');
                    sb.Append(piece);
                    found = true;
                }

                i = textAt + 6;
            }

            if (!found)
                return false;

            text = sb.ToString();
            return text.Length > 0;
        }
    }
}
