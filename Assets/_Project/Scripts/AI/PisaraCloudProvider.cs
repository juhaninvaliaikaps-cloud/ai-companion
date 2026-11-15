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
            IApiKeySource keys,
            IChatTransport transport,
            string displayName = "Pisara cloud")
        {
            var url = string.IsNullOrWhiteSpace(baseUrl) ? ChatDefaults.BaseUrl : baseUrl.Trim();
            var name = string.IsNullOrWhiteSpace(model) ? ChatDefaults.Model : model.Trim();
            if (AnthropicChatProvider.IsAnthropic(url))
            {
                return new AnthropicChatProvider(new AnthropicProviderConfig
                {
                    ProviderId = "pisara",
                    DisplayName = displayName,
                    BaseUrl = () => url,
                    Model = () => name,
                    ApiKey = () => ReadKey(keys),
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
                ApiKey = () => ReadKey(keys),
                Secret = () => ReadKey(keys),
                Transport = transport,
            });
        }

        static string ReadKey(IApiKeySource keys)
        {
            string key;
            return keys != null && keys.TryGet(out key) ? key : null;
        }
    }
}
