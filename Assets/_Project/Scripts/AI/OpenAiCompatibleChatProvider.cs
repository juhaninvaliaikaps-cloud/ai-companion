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
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled));
            if (request == null)
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, ChatErrors.Rejected(DisplayName)));

            var key = ReadKey();
            if (_config.RequiresKey && string.IsNullOrWhiteSpace(key))
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.MissingApiKey, ChatErrors.MissingApiKey));

            if (_config.Transport == null)
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));

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
                return Task.FromResult(ChatErrors.FromTransport(DisplayName, httpTask.Result, state.Text, state.Done, state.Malformed));
            }

            return Finish(httpTask, state, onDelta);
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

        async Task<ChatResult> Finish(Task<ChatHttpResult> httpTask, StreamState state, Action<string> onDelta)
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
            return ChatErrors.FromTransport(DisplayName, http, state.Text, state.Done, state.Malformed);
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
