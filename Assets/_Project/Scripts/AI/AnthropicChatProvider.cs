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

        /// <summary>JUH-143: chat streams the reply (server-sent events). The Pisara reading does not.</summary>
        public bool Stream;
    }

    /// <summary>
    /// Claude's native Messages API. JUH-126: the Pisara reading uses it non-streamed, since it
    /// only needs the final sheet. JUH-143: chat in the cloud slot streams it
    /// (<see cref="AnthropicProviderConfig.Stream"/>), reading each <c>content_block_delta</c>.
    /// Images are sent as base64 blocks when the model reads pictures (JUH-149).
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
            var model = string.IsNullOrWhiteSpace(request.Model)
                ? (_config.Model != null ? _config.Model() : string.Empty)
                : request.Model;

            // JUH-149: a picture goes to Claude only when the model reads pictures, the request
            // may use the cloud, and the picture is within the size limit. It is wiped either way.
            byte[] jpeg = null;
            if (request.Image != null)
            {
                var image = request.Image;
                request.Image = null;
                string refusal = null;
                if (request.BlockCloud)
                    refusal = VisionCapability.PrivateRefusal;
                else if (!VisionCapability.CloudModelAcceptsImages(model))
                    refusal = VisionCapability.ModelRefusal;
                else if (!VisionCapability.AcceptsPayload(image))
                    refusal = VisionCapability.PayloadRefusal;
                if (refusal != null)
                {
                    image.Wipe();
                    return ChatResult.Fail(ChatErrorKind.Malformed, refusal);
                }

                jpeg = (byte[])image.JpegBytes.Clone();
                image.Wipe();
            }

            var key = ReadKey();
            if (string.IsNullOrWhiteSpace(key))
                return ChatResult.Fail(ChatErrorKind.MissingApiKey, ChatErrors.MissingApiKey);
            if (_config.Transport == null)
                return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName));

            string body;
            try
            {
                body = BuildBody(model, request, _config.Stream, jpeg);
            }
            finally
            {
                if (jpeg != null)
                    Array.Clear(jpeg, 0, jpeg.Length);
            }

            var http = new ChatHttpRequest
            {
                Url = MessagesUrl(_config.BaseUrl != null ? _config.BaseUrl() : null),
                Body = body,
                TimeoutSeconds = request.TimeoutSeconds > 0f
                    ? request.TimeoutSeconds
                    : (_config.TimeoutSeconds != null ? _config.TimeoutSeconds() : PisaraReader.RequestTimeoutSeconds),
                FollowRedirects = true,
            };
            http.Headers.Add(new KeyValuePair<string, string>("Content-Type", "application/json"));
            http.Headers.Add(new KeyValuePair<string, string>("x-api-key", key.Trim()));
            http.Headers.Add(new KeyValuePair<string, string>("anthropic-version", AnthropicVersion));

            if (_config.Stream)
                return await StreamMessages(http, onDelta, model, key, cancellationToken);

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

        /// <summary>
        /// The streamed reply: text arrives as <c>content_block_delta</c> / <c>text_delta</c>
        /// events and is passed on as it comes. An HTTP error has a plain JSON body.
        /// </summary>
        async Task<ChatResult> StreamMessages(ChatHttpRequest http, Action<string> onDelta, string model, string key,
            CancellationToken cancellationToken)
        {
            var stream = new StreamState();
            ChatHttpResult result;
            try
            {
                result = await _config.Transport.PostAsync(http, chunk => stream.Push(chunk, onDelta), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            }
            catch (Exception)
            {
                return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName));
            }

            stream.Flush(onDelta);
            if (result.Cancelled)
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            if (result.TimedOut)
                return stream.Text.Length > 0
                    ? ChatResult.Success(stream.Text.ToString())
                    : ChatResult.Fail(ChatErrorKind.Timeout, ChatErrors.TimedOut(DisplayName));
            var raw = stream.Raw.Length > 0 ? stream.Raw.ToString() : result.Body;
            if (result.NetworkFailed || result.StatusCode == 0 || result.StatusCode >= 400)
                return ChatErrors.FromTransport(DisplayName, result, raw, true, true, model, key);
            if (stream.Error != null && stream.Text.Length == 0)
                return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName));
            if (stream.Text.Length == 0)
                return ChatResult.Fail(ChatErrorKind.Malformed, ChatErrors.Unreadable(DisplayName));
            return ChatResult.Success(stream.Text.ToString());
        }

        /// <summary>The streamed events of one reply. Public for tests.</summary>
        public sealed class StreamState
        {
            const int MaxRaw = 64 * 1024;

            readonly SseParser _parser = new SseParser();
            readonly List<SseEvent> _events = new List<SseEvent>();

            public readonly StringBuilder Text = new StringBuilder();
            public readonly StringBuilder Raw = new StringBuilder();
            public bool Stopped;
            public string Error;

            public void Push(string chunk, Action<string> onDelta)
            {
                if (string.IsNullOrEmpty(chunk))
                    return;
                if (Raw.Length < MaxRaw)
                    Raw.Append(chunk);
                _parser.Push(chunk, _events);
                Drain(onDelta);
            }

            public void Flush(Action<string> onDelta)
            {
                _parser.Flush(_events);
                Drain(onDelta);
            }

            void Drain(Action<string> onDelta)
            {
                for (var i = 0; i < _events.Count; i++)
                {
                    var e = _events[i];
                    if (e.Kind != SseKind.Data || string.IsNullOrEmpty(e.Data))
                        continue;
                    StreamEvent parsed;
                    try
                    {
                        parsed = JsonUtility.FromJson<StreamEvent>(e.Data);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (parsed == null || string.IsNullOrEmpty(parsed.type))
                        continue;
                    switch (parsed.type)
                    {
                        case "content_block_delta":
                            if (parsed.delta != null && string.Equals(parsed.delta.type, "text_delta", StringComparison.Ordinal)
                                && !string.IsNullOrEmpty(parsed.delta.text))
                            {
                                Text.Append(parsed.delta.text);
                                if (onDelta != null)
                                    onDelta(parsed.delta.text);
                            }

                            break;
                        case "message_stop":
                            Stopped = true;
                            break;
                        case "error":
                            Error = parsed.error != null && !string.IsNullOrEmpty(parsed.error.type) ? parsed.error.type : "error";
                            break;
                    }
                }

                _events.Clear();
            }
        }

        [Serializable]
        sealed class StreamEvent
        {
            public string type;
            public StreamDelta delta;
            public StreamError error;
        }

        [Serializable]
        sealed class StreamDelta
        {
            public string type;
            public string text;
        }

        [Serializable]
        sealed class StreamError
        {
            public string type;
            public string message;
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

        static string BuildBody(string model, ChatRequest request, bool stream = false, byte[] jpeg = null)
        {
            var lastUser = -1;
            if (request.Messages != null)
            {
                for (var i = request.Messages.Count - 1; i >= 0 && lastUser < 0; i--)
                {
                    if (request.Messages[i].Role == ChatRole.User)
                        lastUser = i;
                }
            }

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
                    if (jpeg != null && i == lastUser)
                        AppendImageContent(messages, jpeg, message.Content);
                    else
                        JsonText.AppendString(messages, message.Content ?? string.Empty);
                    messages.Append('}');
                }
            }

            if (system.Length > 0)
            {
                sb.Append(",\"system\":");
                JsonText.AppendString(sb, system);
            }

            if (jpeg != null && lastUser < 0)
            {
                if (messages.Length > 0)
                    messages.Append(',');
                messages.Append("{\"role\":\"user\",\"content\":");
                AppendImageContent(messages, jpeg, string.Empty);
                messages.Append('}');
            }

            if (stream)
                sb.Append(",\"stream\":true");
            sb.Append(",\"messages\":[");
            sb.Append(messages);
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>JUH-149: a base64 JPEG block, then the text, as Claude's Messages API expects.</summary>
        static void AppendImageContent(StringBuilder sb, byte[] jpeg, string text)
        {
            sb.Append("[{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/jpeg\",\"data\":\"");
            sb.Append(Convert.ToBase64String(jpeg));
            sb.Append("\"}},{\"type\":\"text\",\"text\":");
            JsonText.AppendString(sb, string.IsNullOrEmpty(text) ? "Look at this." : text);
            sb.Append("}]");
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
