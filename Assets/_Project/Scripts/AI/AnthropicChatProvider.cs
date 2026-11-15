using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AICompanion.AI
{
    public sealed class AnthropicProviderConfig
    {
        public string ProviderId;
        public string DisplayName;
        public Func<string> BaseUrl;
        public Func<string> Model;
        public Func<string> ApiKey;
        public Func<float> TimeoutSeconds;
        public IChatTransport Transport;
    }

    /// <summary>
    /// JUH-126: Claude's native Messages API for the Pisara reading. It is deliberately
    /// non-streamed: the reading only needs the final sheet, and Anthropic's message endpoint
    /// has a different body and headers from the OpenAI-compatible providers.
    /// </summary>
    public sealed class AnthropicChatProvider : IChatProvider
    {
        public const string AnthropicVersion = "2023-06-01";

        readonly AnthropicProviderConfig _config;

        public AnthropicChatProvider(AnthropicProviderConfig config)
        {
            _config = config ?? new AnthropicProviderConfig();
        }

        public string ProviderId => string.IsNullOrWhiteSpace(_config.ProviderId) ? "anthropic" : _config.ProviderId.Trim();

        public string DisplayName => string.IsNullOrWhiteSpace(_config.DisplayName) ? "Claude" : _config.DisplayName.Trim();

        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(ReadKey()); }
        }

        public static bool IsAnthropic(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return false;
            Uri uri;
            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out uri))
                return false;
            return uri.Host.IndexOf("anthropic.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string MessagesUrl(string baseUrl)
        {
            var trimmed = string.IsNullOrWhiteSpace(baseUrl)
                ? "https://api.anthropic.com/v1"
                : baseUrl.Trim().TrimEnd('/');
            if (trimmed.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            return trimmed + "/messages";
        }

        public async Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            if (request == null)
                return ChatResult.Fail(ChatErrorKind.Malformed, ChatErrors.Rejected(DisplayName));

            var key = ReadKey();
            if (string.IsNullOrWhiteSpace(key))
                return ChatResult.Fail(ChatErrorKind.MissingApiKey, ChatErrors.MissingApiKey);
            if (_config.Transport == null)
                return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName));

            var model = string.IsNullOrWhiteSpace(request.Model)
                ? (_config.Model != null ? _config.Model() : string.Empty)
                : request.Model;
            var http = new ChatHttpRequest
            {
                Url = MessagesUrl(_config.BaseUrl != null ? _config.BaseUrl() : null),
                Body = BuildBody(model, request),
                TimeoutSeconds = request.TimeoutSeconds > 0f
                    ? request.TimeoutSeconds
                    : (_config.TimeoutSeconds != null ? _config.TimeoutSeconds() : PisaraReader.RequestTimeoutSeconds),
                FollowRedirects = true,
            };
            http.Headers.Add(new KeyValuePair<string, string>("Content-Type", "application/json"));
            http.Headers.Add(new KeyValuePair<string, string>("x-api-key", key.Trim()));
            http.Headers.Add(new KeyValuePair<string, string>("anthropic-version", AnthropicVersion));

            var accumulated = new StringBuilder();
            ChatHttpResult result;
            try
            {
                result = await _config.Transport.PostAsync(http, chunk => Append(accumulated, chunk), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            }
            catch (Exception)
            {
                return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName));
            }

            var body = accumulated.Length > 0 ? accumulated.ToString() : result.Body;
            if (result.Cancelled)
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            if (result.TimedOut)
                return ChatResult.Fail(ChatErrorKind.Timeout, ChatErrors.TimedOut(DisplayName));
            if (result.NetworkFailed || result.StatusCode == 0)
                return ChatErrors.FromTransport(DisplayName, result, body, true, true, model, key);
            if (result.StatusCode >= 400)
                return ChatErrors.FromTransport(DisplayName, result, body, true, true, model, key);

            if (!TryReadText(body, out var text) || string.IsNullOrWhiteSpace(text))
                return ChatResult.Fail(ChatErrorKind.Malformed, ChatErrors.Unreadable(DisplayName));

            if (onDelta != null)
                onDelta(text);
            return ChatResult.Success(text);
        }

        string ReadKey()
        {
            return _config.ApiKey != null ? _config.ApiKey() : null;
        }

        static void Append(StringBuilder sb, string chunk)
        {
            if (sb != null && !string.IsNullOrEmpty(chunk))
                sb.Append(chunk);
        }

        static string BuildBody(string model, ChatRequest request)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"model\":");
            JsonText.AppendString(sb, string.IsNullOrWhiteSpace(model) ? ChatDefaults.Model : model.Trim());
            sb.Append(",\"max_tokens\":");
            sb.Append(ChatCompletionJson.ClampMaxTokens(request.MaxTokens).ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"temperature\":");
            sb.Append(ChatCompletionJson.ClampTemperature(request.Temperature).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

            var system = request.SystemPrompt ?? string.Empty;
            var messages = new StringBuilder();
            if (request.Messages != null)
            {
                for (var i = 0; i < request.Messages.Count; i++)
                {
                    var message = request.Messages[i];
                    if (message.Role == ChatRole.System)
                    {
                        if (system.Length > 0)
                            system += "\n\n";
                        system += message.Content ?? string.Empty;
                        continue;
                    }

                    if (messages.Length > 0)
                        messages.Append(',');
                    messages.Append("{\"role\":");
                    JsonText.AppendString(sb: messages, value: message.Role == ChatRole.Assistant ? "assistant" : "user");
                    messages.Append(",\"content\":");
                    JsonText.AppendString(messages, message.Content ?? string.Empty);
                    messages.Append('}');
                }
            }

            if (system.Length > 0)
            {
                sb.Append(",\"system\":");
                JsonText.AppendString(sb, system);
            }

            sb.Append(",\"messages\":[");
            sb.Append(messages);
            sb.Append("]}");
            return sb.ToString();
        }

        static bool TryReadText(string json, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                var reply = JsonUtility.FromJson<MessageReply>(json);
                if (reply == null || reply.content == null)
                    return false;
                var sb = new StringBuilder();
                for (var i = 0; i < reply.content.Length; i++)
                {
                    var part = reply.content[i];
                    if (part != null && string.Equals(part.type, "text", StringComparison.OrdinalIgnoreCase))
                        sb.Append(part.text ?? string.Empty);
                }

                text = sb.ToString();
                return text.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [Serializable]
        sealed class MessageReply
        {
            public ContentPart[] content;
        }

        [Serializable]
        sealed class ContentPart
        {
            public string type;
            public string text;
        }
    }
}
