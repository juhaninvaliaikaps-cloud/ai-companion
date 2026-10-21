using System.Text;

namespace AICompanion.AI
{
    public static class ChatErrors
    {
        public const int LogBodyLimit = 500;
        public const int ScreenBodyLimit = 150;
        public const string InvalidApiKey = "Invalid API key";
        public const string RateLimited = "Rate limited, try again shortly";
        public const string Cancelled = "Request cancelled";
        public const string MissingApiKey = "No API key is set";
        public const string BillingHint = "xAI account has no API credits or billing is not set up (console.x.ai > Billing)";

        public static string Unreachable(string displayName)
        {
            return "Couldn't reach " + Name(displayName);
        }

        public static string TimedOut(string displayName)
        {
            return Name(displayName) + " took too long to answer";
        }

        public static string Rejected(string displayName)
        {
            return Name(displayName) + " rejected the request";
        }

        public static string Unreadable(string displayName)
        {
            return Name(displayName) + " sent a reply this app could not read";
        }

        public static string UnknownProvider(string providerId)
        {
            var id = providerId ?? string.Empty;
            id = id.Trim();
            if (id.Length > 40)
                id = id.Substring(0, 40);
            if (id.Length == 0)
                return "No chat provider is selected.";
            return "No chat provider is named '" + id + "'.";
        }

        public static string ModelUnavailable(string model)
        {
            var name = string.IsNullOrWhiteSpace(model) ? "the selected model" : Scrub(model.Trim());
            if (name.Length > 80)
                name = name.Substring(0, 80);
            return "Model " + name + " not available for this key; change ChatSettings.model";
        }

        /// <summary>
        /// Drops bearer tokens and <c>xai-</c> keys. The chat log is the caller.
        /// </summary>
        public static string Scrub(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length;)
            {
                if (IsBoundary(text, i) && StartsToken(text, i, "bearer") && HasTokenAfter(text, i + 6))
                {
                    sb.Append("Bearer [redacted]");
                    i = EndOfToken(text, SkipSpaces(text, i + 6));
                    continue;
                }

                if (IsBoundary(text, i) && StartsToken(text, i, "xai-") && KeyLength(text, i + 4) >= 4)
                {
                    sb.Append("[redacted]");
                    i = i + 4 + KeyLength(text, i + 4);
                    continue;
                }

                sb.Append(text[i]);
                i++;
            }

            return sb.ToString();
        }

        public static string ReadProviderMessage(string body, int maxChars)
        {
            var scrubbed = Collapse(Scrub(body));
            if (scrubbed.Length == 0)
                return string.Empty;

            string message = null;
            if (JsonText.TryReadStringField(scrubbed, "error", out var errorText) && !string.IsNullOrWhiteSpace(errorText))
                message = Collapse(Scrub(errorText));
            else if (JsonText.TryReadStringField(scrubbed, "message", out var nested) && !string.IsNullOrWhiteSpace(nested))
                message = Collapse(Scrub(nested));

            if (string.IsNullOrEmpty(message))
                message = scrubbed;
            if (maxChars > 0 && message.Length > maxChars)
                message = message.Substring(0, maxChars);
            return message;
        }

        public static ChatResult FromTransport(string displayName, ChatHttpResult http, string text, bool done, bool malformed, string model = null)
        {
            if (http.Cancelled)
                return ChatResult.Fail(ChatErrorKind.Cancelled, Cancelled);
            if (http.TimedOut)
                return ChatResult.Fail(ChatErrorKind.Timeout, TimedOut(displayName));
            if (http.NetworkFailed || http.StatusCode == 0)
                return NetworkResult(displayName, http.TransportDetail);

            var providerMessage = ReadProviderMessage(http.Body, LogBodyLimit);
            var raw = Collapse(Scrub(http.Body));
            var log = HttpLog(displayName, http.StatusCode, providerMessage);
            if (http.StatusCode == 401 || (http.StatusCode == 400 && LooksLikeInvalidKey(providerMessage, raw)))
                return ChatResult.Fail(ChatErrorKind.InvalidApiKey, InvalidApiKey, log);
            if (http.StatusCode == 403 || LooksLikeBilling(providerMessage, raw))
                return ChatResult.Fail(ChatErrorKind.Malformed, BillingHint, log);
            if (http.StatusCode == 404 || LooksLikeMissingModel(providerMessage, raw))
                return ChatResult.Fail(ChatErrorKind.Malformed, ModelUnavailable(model), log);
            if (http.StatusCode == 429)
                return ChatResult.Fail(ChatErrorKind.RateLimited, RateLimited, log);
            if (http.StatusCode >= 500)
                return ChatResult.Fail(ChatErrorKind.Unreachable, Unreachable(displayName), log);
            if (http.StatusCode >= 400)
                return ChatResult.Fail(ChatErrorKind.Malformed, HttpScreen(displayName, http.StatusCode, providerMessage), log);
            if (malformed && string.IsNullOrEmpty(text) && !done)
                return ChatResult.Fail(ChatErrorKind.Malformed, Unreadable(displayName));
            return ChatResult.Success(text);
        }

        static ChatResult NetworkResult(string displayName, string transportDetail)
        {
            var screen = Unreachable(displayName);
            var detail = ReadProviderMessage(transportDetail, LogBodyLimit);
            if (detail.Length == 0)
                return ChatResult.Fail(ChatErrorKind.Unreachable, screen);
            return ChatResult.Fail(ChatErrorKind.Unreachable, screen, "Chat error: " + screen + ": " + detail);
        }

        static string HttpLog(string displayName, int status, string providerMessage)
        {
            var line = "Chat error: HTTP " + status.ToString() + " from " + Name(displayName);
            if (!string.IsNullOrEmpty(providerMessage))
                line += ": " + providerMessage;
            return line;
        }

        static string HttpScreen(string displayName, int status, string providerMessage)
        {
            var line = Name(displayName) + " rejected the request (HTTP " + status.ToString() + ")";
            if (string.IsNullOrEmpty(providerMessage))
                return line;
            var snippet = providerMessage.Length > ScreenBodyLimit ? providerMessage.Substring(0, ScreenBodyLimit) : providerMessage;
            return line + ": " + snippet;
        }

        static bool LooksLikeInvalidKey(string message, string raw)
        {
            return ContainsAny(message, "invalid api key", "incorrect api key", "invalid api-key", "invalid_api_key", "invalid-api-key", "invalid authentication", "authentication failed", "invalid token")
                || ContainsPair(message, "api key", "invalid", "incorrect", "unauthorized")
                || ContainsAny(raw, "invalid api key", "incorrect api key", "invalid api-key", "invalid_api_key", "invalid-api-key");
        }

        static bool LooksLikeBilling(string message, string raw)
        {
            return ContainsAny(message, "credit", "billing", "spending limit", "spend limit")
                || ContainsAny(raw, "credit", "billing", "spending limit", "spend limit");
        }

        static bool LooksLikeMissingModel(string message, string raw)
        {
            return IsMissingModel(message) || IsMissingModel(raw);
        }

        static bool IsMissingModel(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            var lower = value.ToLowerInvariant();
            if (lower.Contains("model_not_found") || lower.Contains("model not found") || lower.Contains("unknown model") || lower.Contains("no such model"))
                return true;
            return lower.Contains("model") && (lower.Contains("does not exist") || lower.Contains("doesn't exist") || lower.Contains("not available") || lower.Contains("not found"));
        }

        static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            var lower = value.ToLowerInvariant();
            for (var i = 0; i < needles.Length; i++)
            {
                if (lower.Contains(needles[i]))
                    return true;
            }

            return false;
        }

        static bool ContainsPair(string value, string left, params string[] rights)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            var lower = value.ToLowerInvariant();
            if (!lower.Contains(left))
                return false;
            for (var i = 0; i < rights.Length; i++)
            {
                if (lower.Contains(rights[i]))
                    return true;
            }

            return false;
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

        static bool IsBoundary(string text, int index)
        {
            if (index <= 0)
                return true;
            return !IsKeyChar(text[index - 1]);
        }

        static bool StartsToken(string text, int index, string token)
        {
            if (index < 0 || index + token.Length > text.Length)
                return false;
            for (var i = 0; i < token.Length; i++)
            {
                if (char.ToLowerInvariant(text[index + i]) != token[i])
                    return false;
            }

            return true;
        }

        static bool HasTokenAfter(string text, int index)
        {
            index = SkipSpaces(text, index);
            return index < text.Length && !char.IsWhiteSpace(text[index]);
        }

        static int SkipSpaces(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
            return index;
        }

        static int EndOfToken(string text, int index)
        {
            while (index < text.Length && !IsTokenEnd(text[index]))
                index++;
            return index;
        }

        static bool IsTokenEnd(char c)
        {
            return char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == ',' || c == '}' || c == ')' || c == ']' || c == ';';
        }

        static int KeyLength(string text, int index)
        {
            var count = 0;
            while (index + count < text.Length && IsKeyChar(text[index + count]))
                count++;
            return count;
        }

        static bool IsKeyChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
        }

        static string Name(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "the chat provider";
            return displayName.Trim();
        }
    }
}
