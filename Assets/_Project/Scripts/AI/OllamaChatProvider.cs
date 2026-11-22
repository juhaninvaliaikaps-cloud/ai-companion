using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>
    /// Local Ollama chat through its native <c>/api/chat</c> endpoint. The OpenAI-compatible
    /// endpoint ignores <c>num_ctx</c> and reloads the model at Ollama's 4096 default, which
    /// truncates the persona and recent scene. Native chat applies the context on every request.
    /// </summary>
    public sealed class OllamaChatProvider : IChatProvider
    {
        public const int DefaultContextTokens = 8192;

        readonly OllamaProviderConfig _config;

        public OllamaChatProvider(OllamaProviderConfig config)
        {
            _config = config ?? new OllamaProviderConfig();
        }

        public string ProviderId => string.IsNullOrWhiteSpace(_config.ProviderId) ? ChatDefaults.LocalId : _config.ProviderId;

        public string DisplayName => string.IsNullOrWhiteSpace(_config.DisplayName) ? "Local model" : _config.DisplayName;

        public bool IsConfigured => true;

        public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
        {
            if (request == null)
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, ChatErrors.Rejected(DisplayName)));
            if (request.Image != null)
            {
                request.Image.Wipe();
                request.Image = null;
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, "The local model does not accept images."));
            }
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled));
            if (_config.Transport == null)
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));

            var model = request.Model;
            if (string.IsNullOrWhiteSpace(model) && _config.Model != null)
                model = _config.Model();
            if (string.IsNullOrWhiteSpace(model))
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, "No local model is selected."));

            var body = OllamaChatJson.Build(
                model,
                request.Temperature,
                request.MaxTokens,
                request.SystemPrompt,
                request.Messages,
                _config.ContextTokens > 0 ? _config.ContextTokens : DefaultContextTokens,
                request.Roleplay);

            var http = new ChatHttpRequest
            {
                Url = OllamaChatJson.ChatUrl(_config.BaseUrl != null ? _config.BaseUrl() : null),
                Body = body,
                TimeoutSeconds = request.TimeoutSeconds > 0f
                    ? request.TimeoutSeconds
                    : (_config.TimeoutSeconds != null ? _config.TimeoutSeconds() : ChatDefaults.TimeoutSeconds),
                FollowRedirects = false,
            };
            http.Headers.Add(new KeyValuePair<string, string>("Content-Type", "application/json"));

            var parser = new OllamaStreamParser(onDelta);
            Task<ChatHttpResult> task;
            try
            {
                task = _config.Transport.PostAsync(http, parser.Push, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled));
            }
            catch (Exception)
            {
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName)));
            }

            return Finish(task, parser, onDelta, model);
        }

        async Task<ChatResult> Finish(Task<ChatHttpResult> task, OllamaStreamParser parser, Action<string> onDelta, string model)
        {
            ChatHttpResult http;
            try
            {
                http = await task;
            }
            catch (OperationCanceledException)
            {
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
            }
            catch (Exception)
            {
                return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable(DisplayName));
            }

            parser.Flush(onDelta);
            return ChatErrors.FromTransport(DisplayName, http, parser.Text, parser.Done, parser.Malformed, model);
        }
    }

    public sealed class OllamaProviderConfig
    {
        public string ProviderId = ChatDefaults.LocalId;
        public string DisplayName = "Local model";
        public Func<string> BaseUrl;
        public Func<string> Model;
        public Func<float> TimeoutSeconds;
        public IChatTransport Transport;
        public int ContextTokens = OllamaChatProvider.DefaultContextTokens;
    }

    /// <summary>Pure JSON-lines request builder for Ollama's native chat endpoint.</summary>
    public static class OllamaChatJson
    {
        /// <summary>
        /// JUH-171: sampler options for her replies. Ollama's default repeat penalty looks back only
        /// 64 tokens, so a stock line from her last reply ("you can't just...") came back again and
        /// again. A mild penalty over the last 1024 tokens and min_p 0.05 (drops the unlikely tail
        /// without flattening her voice). Temperature stays the user's setting.
        /// </summary>
        public const string RoleplayOptions = ",\"repeat_penalty\":1.08,\"repeat_last_n\":1024,\"min_p\":0.05";

        public static string Build(
            string model,
            float temperature,
            int maxTokens,
            string systemPrompt,
            IReadOnlyList<ChatMessage> messages,
            int contextTokens)
        {
            return Build(model, temperature, maxTokens, systemPrompt, messages, contextTokens, false);
        }

        /// <summary>As above; <paramref name="roleplay"/> adds <see cref="RoleplayOptions"/>.</summary>
        public static string Build(
            string model,
            float temperature,
            int maxTokens,
            string systemPrompt,
            IReadOnlyList<ChatMessage> messages,
            int contextTokens,
            bool roleplay)
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"model\":");
            JsonText.AppendString(sb, model ?? ChatDefaults.LocalModel);
            sb.Append(",\"stream\":true,\"messages\":[");
            var first = true;
            if (!string.IsNullOrWhiteSpace(systemPrompt))
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
                    AppendMessage(sb, Role(messages[i].Role), messages[i].Content);
                }
            }

            sb.Append("],\"options\":{\"num_ctx\":");
            sb.Append(contextTokens <= 0 ? OllamaChatProvider.DefaultContextTokens : contextTokens);
            sb.Append(",\"temperature\":");
            sb.Append(ChatCompletionJson.ClampTemperature(temperature).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"num_predict\":");
            sb.Append(ChatCompletionJson.ClampMaxTokens(maxTokens));
            if (roleplay)
                sb.Append(RoleplayOptions);
            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>Accept both http://host:11434 and http://host:11434/v1.</summary>
        public static string ChatUrl(string baseUrl)
        {
            var value = string.IsNullOrWhiteSpace(baseUrl) ? ChatDefaults.LocalBaseUrl : baseUrl.Trim();
            value = value.TrimEnd('/');
            if (value.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 3).TrimEnd('/');
            if (value.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
                return value;
            return value + "/api/chat";
        }

        static void AppendMessage(StringBuilder sb, string role, string content)
        {
            sb.Append("{\"role\":");
            JsonText.AppendString(sb, role);
            sb.Append(",\"content\":");
            JsonText.AppendString(sb, content ?? string.Empty);
            sb.Append('}');
        }

        static string Role(ChatRole role)
        {
            if (role == ChatRole.Assistant)
                return "assistant";
            if (role == ChatRole.System)
                return "system";
            return "user";
        }
    }

    /// <summary>Reads the JSON lines Ollama streams from /api/chat.</summary>
    internal sealed class OllamaStreamParser
    {
        readonly Action<string> _onDelta;
        readonly StringBuilder _line = new StringBuilder();
        readonly StringBuilder _text = new StringBuilder();
        bool _done;
        bool _malformed;

        public OllamaStreamParser(Action<string> onDelta)
        {
            _onDelta = onDelta;
        }

        public string Text => _text.ToString();
        public bool Done => _done;
        public bool Malformed => _malformed;

        public void Push(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return;
            for (var i = 0; i < chunk.Length; i++)
            {
                var c = chunk[i];
                if (c == '\n')
                {
                    ParseLine();
                    continue;
                }
                if (c != '\r')
                    _line.Append(c);
            }
        }

        public void Flush(Action<string> onDelta)
        {
            if (_line.Length > 0)
                ParseLine();
        }

        void ParseLine()
        {
            var json = _line.ToString().Trim();
            _line.Length = 0;
            if (json.Length == 0)
                return;
            OllamaReply reply;
            try
            {
                reply = JsonUtility.FromJson<OllamaReply>(json);
            }
            catch (ArgumentException)
            {
                _malformed = true;
                return;
            }

            if (reply == null)
                return;
            if (!string.IsNullOrEmpty(reply.error))
            {
                _malformed = true;
                return;
            }
            if (reply.message != null && !string.IsNullOrEmpty(reply.message.content))
            {
                _text.Append(reply.message.content);
                _onDelta?.Invoke(reply.message.content);
            }
            if (reply.done)
                _done = true;
        }
    }

    [Serializable]
    internal sealed class OllamaReply
    {
        public OllamaMessage message;
        public bool done;
        public string error;
    }

    [Serializable]
    internal sealed class OllamaMessage
    {
        public string role;
        public string content;
    }
}
