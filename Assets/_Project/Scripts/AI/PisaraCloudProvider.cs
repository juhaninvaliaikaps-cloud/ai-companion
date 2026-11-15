using System;

namespace AICompanion.AI
{
    /// <summary>
    /// JUH-126: builds the provider for the separate Pisara cloud reading. Anthropic's own
    /// endpoint uses its native Messages API; every other service is OpenAI-compatible.
    /// </summary>
    public static class PisaraCloudProvider
    {
        public static IChatProvider Create(
            string baseUrl,
            string model,
            Func<string> apiKey,
            IChatTransport transport,
            string displayName = "Pisara cloud")
        {
            var url = string.IsNullOrWhiteSpace(baseUrl) ? ChatDefaults.BaseUrl : baseUrl.Trim();
            var name = string.IsNullOrWhiteSpace(model) ? ChatDefaults.Model : model.Trim();
            var deepSeek = IsDeepSeek(url);
            if (AnthropicChatProvider.IsAnthropic(url))
            {
                return new AnthropicChatProvider(new AnthropicProviderConfig
                {
                    ProviderId = "pisara",
                    DisplayName = displayName,
                    BaseUrl = () => url,
                    Model = () => name,
                    ApiKey = apiKey,
                    TimeoutSeconds = () => PisaraReader.RequestTimeoutSeconds,
                    Transport = transport,
                });
            }

            return new OpenAiCompatibleChatProvider(new OpenAiProviderConfig
            {
                ProviderId = "pisara",
                DisplayName = displayName,
                RequiresKey = true,
                BaseUrl = () => url,
                Model = () => name,
                TimeoutSeconds = () => PisaraReader.RequestTimeoutSeconds,
                ApiKey = apiKey,
                Secret = apiKey,
                DisableThinking = deepSeek,
                Transport = transport,
            });
        }

        static bool IsDeepSeek(string baseUrl)
        {
            Uri uri;
            return Uri.TryCreate(baseUrl, UriKind.Absolute, out uri)
                && uri.Host.IndexOf("deepseek.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
