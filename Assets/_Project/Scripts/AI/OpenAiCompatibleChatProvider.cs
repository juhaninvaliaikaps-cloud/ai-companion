using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.AI
{
    public sealed class OpenAiProviderConfig
    {
        public string ProviderId;
        public string DisplayName;
        public Func<string> BaseUrl;
        public Func<string> Model;
        public Func<string> ApiKey;
        public Func<float> TimeoutSeconds;
        public IChatTransport Transport;
        public bool RequiresKey;

        /// <summary>A non-empty value is shown as the chat error instead of calling the server.</summary>
        public Func<string> ConfigurationError;

        /// <summary>Optional bearer token, used only to redact it from an error body.</summary>
        public Func<string> Secret;

        /// <summary>Called once just before a request leaves the app. Never receives the key.</summary>
        public Action BeforeRequest;
    }

    /// <summary>
    /// OpenAI-compatible <c>/chat/completions</c> stream. Grok and a local
    /// server (Ollama, LM Studio, llama.cpp) are two configurations of this class.
    /// </summary>
    public sealed class OpenAiCompatibleChatProvider : IChatProvider
    {
        readonly OpenAiProviderConfig _config;

        public OpenAiCompatibleChatProvider(OpenAiProviderConfig config)
        {
            _config = config ?? new OpenAiProviderConfig();
        }

        public string ProviderId => string.IsNullOrWhiteSpace(_config.ProviderId) ? "openai" : _config.ProviderId.Trim();

        public string DisplayName => string.IsNullOrWhiteSpace(_config.DisplayName) ? ProviderId : _config.DisplayName.Trim();

        public bool IsConfigured
        {
            get
            {
                if (!_config.RequiresKey)
                    return true;
                return !string.IsNullOrWhiteSpace(ReadKey());
            }
        }

        public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (request != null && request.Image != null)
                {
                    request.Image.Wipe();
                    request.Image = null;
                }

                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled));
            }

            if (request == null)
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, ChatErrors.Rejected(DisplayName)));

            var configurationError = _config.ConfigurationError != null ? _config.ConfigurationError() : null;
            if (!string.IsNullOrEmpty(configurationError))
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, configurationError));

            if (request.Image != null)
                return StreamVision(request, onDelta, cancellationToken);

            var key = ReadKey();
            if (_config.RequiresKey && string.IsNullOrWhiteSpace(key))
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.MissingApiKey, ChatErrors.MissingApiKey));

            if (_config.Transport == null)
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));

            if (_config.BeforeRequest != null)
                _config.BeforeRequest();

            var model = request.Model;
            if (string.IsNullOrWhiteSpace(model) && _config.Model != null)
                model = _config.Model();

            var http = new ChatHttpRequest
            {
                Url = ChatEndpoints.CompletionsUrl(_config.BaseUrl != null ? _config.BaseUrl() : null),
                Body = ChatCompletionJson.Build(model, request.Temperature, request.MaxTokens, request.SystemPrompt, request.Messages),
                TimeoutSeconds = ReadTimeout()
            };
            http.Headers.Add(new KeyValuePair<string, string>("Content-Type", "application/json"));
            if (!string.IsNullOrWhiteSpace(key))
                http.Headers.Add(new KeyValuePair<string, string>("Authorization", "Bearer " + key.Trim()));

            var state = new StreamState();

            Task<ChatHttpResult> httpTask;
            try
            {
                httpTask = _config.Transport.PostAsync(http, chunk => state.Push(chunk, onDelta), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled));
            }
            catch (Exception)
            {
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));
            }

            if (httpTask == null)
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));

            if (httpTask.IsCompleted)
            {
                if (httpTask.IsFaulted || httpTask.IsCanceled)
                    return Task.FromResult(FailedTask(httpTask, DisplayName));
                state.Flush(onDelta);
                return Task.FromResult(ChatErrors.FromTransport(DisplayName, httpTask.Result, state.Text, state.Done, state.Malformed, model, Secret()));
            }

            return Finish(httpTask, state, onDelta, model);
        }

        Task<ChatResult> StreamVision(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
        {
            var image = request.Image;
            if (request.BlockCloud || !string.Equals(ProviderId, ChatDefaults.GrokId, StringComparison.OrdinalIgnoreCase))
            {
                Wipe(image);
                var message = request.BlockCloud ? VisionCapability.PrivateRefusal : VisionCapability.LocalRefusal;
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, message));
            }

            var model = request.Model;
            if (string.IsNullOrWhiteSpace(model) && _config.Model != null)
                model = _config.Model();
            if (!VisionCapability.CloudModelAcceptsImages(model))
            {
                Wipe(image);
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, VisionCapability.ModelRefusal));
            }

            if (!VisionCapability.AcceptsPayload(image))
            {
                Wipe(image);
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, VisionCapability.PayloadRefusal));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Wipe(image);
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled));
            }

            var key = ReadKey();
            if (_config.RequiresKey && string.IsNullOrWhiteSpace(key))
            {
                Wipe(image);
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.MissingApiKey, ChatErrors.MissingApiKey));
            }

            if (_config.Transport == null)
            {
                Wipe(image);
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));
            }

            var userText = string.Empty;
            if (request.Messages != null)
            {
                for (var i = request.Messages.Count - 1; i >= 0; i--)
                {
                    if (request.Messages[i].Role == ChatRole.User)
                    {
                        userText = request.Messages[i].Content;
                        break;
                    }
                }
            }

            string body;
            try
            {
                body = VisionResponseJson.Build(
                    model,
                    VisionCapability.MaxOutputTokens,
                    request.SystemPrompt,
                    userText,
                    image.JpegBytes);
            }
            finally
            {
                Wipe(image);
                request.Image = null;
            }

            var timeout = ReadTimeout();
            if (timeout > VisionCapability.MaxTimeoutSeconds)
                timeout = VisionCapability.MaxTimeoutSeconds;

            var http = new ChatHttpRequest
            {
                Url = VisionResponseJson.ResponsesUrl(_config.BaseUrl != null ? _config.BaseUrl() : null),
                Body = body,
                TimeoutSeconds = timeout
            };
            http.Headers.Add(new KeyValuePair<string, string>("Content-Type", "application/json"));
            if (!string.IsNullOrWhiteSpace(key))
                http.Headers.Add(new KeyValuePair<string, string>("Authorization", "Bearer " + key.Trim()));

            var accumulated = new StringBuilder();
            Task<ChatHttpResult> httpTask;
            try
            {
                httpTask = _config.Transport.PostAsync(http, chunk => AppendCapped(accumulated, chunk), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                http.Body = null;
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled));
            }
            catch (Exception)
            {
                http.Body = null;
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));
            }

            if (httpTask == null)
            {
                http.Body = null;
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));
            }

            if (httpTask.IsCompleted)
            {
                http.Body = null;
                if (httpTask.IsFaulted || httpTask.IsCanceled)
                    return Task.FromResult(FailedTask(httpTask, DisplayName));
                return Task.FromResult(ReadVision(httpTask.Result, accumulated.ToString(), onDelta, model));
            }

            return FinishVision(httpTask, http, accumulated, onDelta, model);
        }

        async Task<ChatResult> FinishVision(Task<ChatHttpResult> httpTask, ChatHttpRequest http, StringBuilder accumulated, Action<string> onDelta, string model)
        {
            try
            {
                ChatHttpResult result;
                try
                {
                    result = await httpTask;
                }
                catch (OperationCanceledException)
                {
                    return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
                }
                catch (Exception)
                {
                    return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName));
                }

                return ReadVision(result, accumulated.ToString(), onDelta, model);
            }
            finally
            {
                if (http != null)
                    http.Body = null;
            }
        }

        ChatResult ReadVision(ChatHttpResult http, string accumulated, Action<string> onDelta, string model)
        {
            if (http.Cancelled)
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            if (http.TimedOut)
                return ChatResult.Fail(ChatErrorKind.Timeout, ChatErrors.TimedOut(DisplayName));
            if (http.NetworkFailed || (http.StatusCode == 0 && string.IsNullOrEmpty(accumulated) && string.IsNullOrEmpty(http.Body)))
                return ChatErrors.FromTransport(DisplayName, http, string.Empty, false, false, model, Secret());

            if (http.StatusCode >= 400 || http.StatusCode == 0)
                return ChatErrors.FromTransport(DisplayName, http, string.Empty, false, true, model, Secret());

            var json = string.IsNullOrEmpty(accumulated) ? http.Body : accumulated;
            if (json != null && json.Length > VisionCapability.MaxResponseChars)
                json = json.Substring(0, VisionCapability.MaxResponseChars);

            if (!VisionResponseJson.TryReadOutputText(json, out var text))
                return ChatResult.Fail(ChatErrorKind.Malformed, ChatErrors.Unreadable(DisplayName));

            if (onDelta != null && text.Length > 0)
                onDelta(text);
            return ChatResult.Success(text);
        }

        static void AppendCapped(StringBuilder accumulated, string chunk)
        {
            if (accumulated == null || string.IsNullOrEmpty(chunk) || accumulated.Length >= VisionCapability.MaxResponseChars)
                return;
            var room = VisionCapability.MaxResponseChars - accumulated.Length;
            accumulated.Append(chunk.Length <= room ? chunk : chunk.Substring(0, room));
        }

        static void Wipe(ChatImageAttachment image)
        {
            if (image != null)
                image.Wipe();
        }

        string ReadKey()
        {
            if (_config.ApiKey == null)
                return null;
            return _config.ApiKey();
        }

        float ReadTimeout()
        {
            var timeout = _config.TimeoutSeconds != null ? _config.TimeoutSeconds() : ChatDefaults.TimeoutSeconds;
            if (timeout <= 0f)
                return ChatDefaults.TimeoutSeconds;
            return timeout;
        }

        async Task<ChatResult> Finish(Task<ChatHttpResult> httpTask, StreamState state, Action<string> onDelta, string model)
        {
            ChatHttpResult http;
            try
            {
                http = await httpTask;
            }
            catch (OperationCanceledException)
            {
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            }
            catch (Exception)
            {
                return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName));
            }

            state.Flush(onDelta);
            return ChatErrors.FromTransport(DisplayName, http, state.Text, state.Done, state.Malformed, model, Secret());
        }

        string Secret()
        {
            return _config.Secret != null ? _config.Secret() : null;
        }

        static ChatResult FailedTask(Task<ChatHttpResult> httpTask, string displayName)
        {
            if (httpTask.IsCanceled)
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(displayName));
        }

        sealed class StreamState
        {
            readonly SseParser _parser = new SseParser();
            readonly List<SseEvent> _events = new List<SseEvent>(8);
            readonly StringBuilder _text = new StringBuilder();

            public bool Malformed { get; private set; }
            public bool Done { get; private set; }
            public string Text => _text.ToString();

            public void Push(string chunk, Action<string> onDelta)
            {
                _parser.Push(chunk, _events);
                Consume(onDelta);
            }

            public void Flush(Action<string> onDelta)
            {
                _parser.Flush(_events);
                Consume(onDelta);
            }

            void Consume(Action<string> onDelta)
            {
                for (var i = 0; i < _events.Count; i++)
                {
                    var ev = _events[i];
                    if (ev.Kind == SseKind.Done)
                    {
                        Done = true;
                        continue;
                    }

                    if (ev.Kind == SseKind.Malformed)
                    {
                        Malformed = true;
                        if (string.IsNullOrEmpty(ev.Data))
                            continue;
                    }

                    if (string.IsNullOrEmpty(ev.Data))
                        continue;

                    if (ChatCompletionJson.TryReadDeltaContent(ev.Data, out var piece))
                    {
                        if (string.IsNullOrEmpty(piece))
                            continue;
                        _text.Append(piece);
                        if (onDelta != null)
                            onDelta(piece);
                        continue;
                    }

                    var trimmed = ev.Data.TrimStart();
                    if (trimmed.Length == 0 || trimmed[0] != '{')
                        Malformed = true;
                }

                _events.Clear();
            }
        }
    }
}
