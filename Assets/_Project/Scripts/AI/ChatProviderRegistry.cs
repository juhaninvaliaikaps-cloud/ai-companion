using System;
using System.Collections.Generic;

namespace AICompanion.AI
{
    /// <summary>
    /// Providers register here once. Callers resolve <see cref="ChatSettings.providerId"/>
    /// and never switch on the id themselves.
    /// </summary>
    public sealed class ChatProviderRegistry
    {
        readonly Dictionary<string, IChatProvider> _providers = new Dictionary<string, IChatProvider>(StringComparer.OrdinalIgnoreCase);

        public void Register(IChatProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId))
                return;
            if (_providers.ContainsKey(provider.ProviderId))
                return;
            _providers.Add(provider.ProviderId, provider);
        }

        public bool TryGet(string providerId, out IChatProvider provider)
        {
            provider = null;
            if (string.IsNullOrWhiteSpace(providerId))
                return false;
            return _providers.TryGetValue(providerId.Trim(), out provider);
        }
    }

    /// <summary>
    /// Wraps every registered provider once, without the factory knowing what the
    /// wrapper does. The memory layer uses it to add remembered facts to the prompt.
    /// </summary>
    public interface IChatProviderDecorator
    {
        IChatProvider Wrap(IChatProvider provider);
    }

    public static class ChatProviderFactory
    {
        public static ChatProviderRegistry Create(ChatSettings settings, IApiKeySource keys, IChatTransport transport)
        {
            return Create(settings, keys, transport, null);
        }

        public static ChatProviderRegistry Create(
            ChatSettings settings,
            IApiKeySource keys,
            IChatTransport transport,
            IChatProviderDecorator decorator)
        {
            var registry = new ChatProviderRegistry();
            registry.Register(Wrap(decorator, new OpenAiCompatibleChatProvider(new OpenAiProviderConfig
            {
                ProviderId = ChatDefaults.GrokId,
                DisplayName = "Grok",
                RequiresKey = true,
                BaseUrl = () => Value(settings != null ? settings.baseUrl : null, ChatDefaults.BaseUrl),
                Model = () => Value(settings != null ? settings.model : null, ChatDefaults.Model),
                TimeoutSeconds = () => settings != null ? settings.timeoutSeconds : ChatDefaults.TimeoutSeconds,
                ApiKey = () =>
                {
                    if (keys != null && keys.TryGet(out var key))
                        return key;
                    return null;
                },
                Transport = transport
            })));
            registry.Register(Wrap(decorator, new OpenAiCompatibleChatProvider(new OpenAiProviderConfig
            {
                ProviderId = ChatDefaults.LocalId,
                DisplayName = "Local model",
                RequiresKey = false,
                BaseUrl = () => Value(settings != null ? settings.localBaseUrl : null, ChatDefaults.LocalBaseUrl),
                Model = () => Value(settings != null ? settings.localModel : null, ChatDefaults.LocalModel),
                TimeoutSeconds = () => settings != null ? settings.timeoutSeconds : ChatDefaults.TimeoutSeconds,
                ApiKey = () => null,
                Transport = transport
            })));
            return registry;
        }

        static IChatProvider Wrap(IChatProviderDecorator decorator, IChatProvider provider)
        {
            if (decorator == null)
                return provider;

            var wrapped = decorator.Wrap(provider);
            return wrapped ?? provider;
        }

        static string Value(string configured, string fallback)
        {
            if (string.IsNullOrWhiteSpace(configured))
                return fallback;
            return configured.Trim();
        }
    }
}
