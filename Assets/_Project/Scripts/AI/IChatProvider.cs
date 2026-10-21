using System;
using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.AI
{
    /// <summary>
    /// Chat backend. Cloud providers (xAI Grok first) and a local OpenAI-compatible
    /// server both implement this. Callers ask the registry for the selected id.
    /// They do not branch on the provider type.
    /// </summary>
    public interface IChatProvider
    {
        string ProviderId { get; }

        string DisplayName { get; }

        bool IsConfigured { get; }

        Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken);
    }
}
