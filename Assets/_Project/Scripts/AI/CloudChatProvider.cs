using System;
using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.AI
{
    /// <summary>Which API a cloud base URL speaks.</summary>
    public enum CloudApiKind
    {
        /// <summary>xAI, OpenAI, OpenRouter, Groq and any other OpenAI-compatible service.</summary>
        OpenAiCompatible = 0,

        /// <summary>DeepSeek: OpenAI-compatible, with its thinking switched off.</summary>
        DeepSeek = 1,

        /// <summary>Claude's native Messages API.</summary>
        Anthropic = 2,
    }

    /// <summary>
    /// JUH-143: the one cloud slot. The user enters a base URL, a model and a key; each request
    /// goes to the API that URL speaks: Claude's Messages API for api.anthropic.com, the
    /// OpenAI-compatible chat API for everything else (xAI Grok by default, DeepSeek, OpenAI,
    /// OpenRouter…). The id stays <see cref="ChatDefaults.GrokId"/> so saved settings keep working.
    ///
    /// The screen look only goes to xAI: its vision path is Grok's Responses API, so a window
    /// image for any other host is wiped and refused before anything is sent.
    /// Private mode and the 18+ gate never reach this provider (routing happens before it); a
    /// request marked <see cref="ChatRequest.BlockCloud"/> is refused here as a backstop.
    /// </summary>
    public sealed class CloudChatProvider : IChatProvider
    {
        readonly Func<string> _baseUrl;
        readonly Func<string> _key;
        readonly IChatProvider _openAi;
        readonly IChatProvider _deepSeek;
        readonly IChatProvider _anthropic;

        public CloudChatProvider(Func<string> baseUrl, Func<string> model, Func<float> timeoutSeconds, Func<string> apiKey,
            IChatTransport transport)
        {
            _baseUrl = baseUrl ?? (() => ChatDefaults.BaseUrl);
            _key = apiKey ?? (() => null);
            _openAi = OpenAi(false, _baseUrl, model, timeoutSeconds, _key, transport);
            _deepSeek = OpenAi(true, _baseUrl, model, timeoutSeconds, _key, transport);
            _anthropic = new AnthropicChatProvider(new AnthropicProviderConfig
            {
                ProviderId = ChatDefaults.GrokId,
                DisplayName = ChatDefaults.CloudShortName,
                BaseUrl = _baseUrl,
                Model = model,
                ApiKey = _key,
                TimeoutSeconds = timeoutSeconds,
                Transport = transport,
                Stream = true,
            });
        }

        public string ProviderId
        {
            get { return ChatDefaults.GrokId; }
        }

        public string DisplayName
        {
            get { return ChatDefaults.CloudShortName; }
        }

        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(_key()); }
        }

        /// <summary>The API this request will use, from the base URL now in the settings.</summary>
        public CloudApiKind Kind
        {
            get { return KindOf(_baseUrl()); }
        }

        public static CloudApiKind KindOf(string baseUrl)
        {
            if (AnthropicChatProvider.IsAnthropic(baseUrl))
                return CloudApiKind.Anthropic;
            return HostEndsWith(baseUrl, "deepseek.com") ? CloudApiKind.DeepSeek : CloudApiKind.OpenAiCompatible;
        }

        /// <summary>Empty is the xAI default. Only an xAI host gets the screen look (Grok's Responses API).</summary>
        public static bool IsXai(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return true;
            return HostEndsWith(baseUrl, "x.ai");
        }

        /// <summary>Shown if a request that must stay on this PC ever reaches the cloud slot.</summary>
        public const string BlockedRefusal = "This request must stay on this PC. Nothing was sent to the cloud.";

        public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
        {
            // Backstop: routing already keeps private, 18+ and observation turns local. If one
            // still arrives here, refuse it before any provider sees it.
            if (request != null && request.BlockCloud)
            {
                if (request.Image != null)
                {
                    request.Image.Wipe();
                    request.Image = null;
                    return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, VisionCapability.PrivateRefusal));
                }

                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, BlockedRefusal));
            }

            var url = _baseUrl();
            if (request != null && request.Image != null && !IsXai(url))
            {
                request.Image.Wipe();
                request.Image = null;
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, VisionCapability.ModelRefusal));
            }

            switch (KindOf(url))
            {
                case CloudApiKind.Anthropic:
                    return _anthropic.StreamChat(request, onDelta, cancellationToken);
                case CloudApiKind.DeepSeek:
                    return _deepSeek.StreamChat(request, onDelta, cancellationToken);
                default:
                    return _openAi.StreamChat(request, onDelta, cancellationToken);
            }
        }

        static IChatProvider OpenAi(bool deepSeek, Func<string> baseUrl, Func<string> model, Func<float> timeout, Func<string> key,
            IChatTransport transport)
        {
            return new OpenAiCompatibleChatProvider(new OpenAiProviderConfig
            {
                ProviderId = ChatDefaults.GrokId,
                DisplayName = ChatDefaults.CloudShortName,
                RequiresKey = true,
                BaseUrl = baseUrl,
                Model = model,
                TimeoutSeconds = timeout,
                ApiKey = key,
                DisableThinking = deepSeek,
                Transport = transport,
            });
        }

        static bool HostEndsWith(string baseUrl, string domain)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return false;
            Uri uri;
            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out uri))
                return false;
            var host = uri.Host ?? string.Empty;
            return host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        }
    }
}
