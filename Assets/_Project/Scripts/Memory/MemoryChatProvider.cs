using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;

namespace AICompanion.Memory
{
    /// <summary>
    /// Adds remembered facts to the system prompt of any provider, and keeps
    /// private-mode memories away from the cloud. The inner provider is untouched;
    /// only the <see cref="ChatRequest.SystemPrompt"/> grows.
    /// </summary>
    public sealed class MemoryChatProvider : IChatProvider
    {
        readonly IChatProvider _inner;
        readonly Func<MemoryStore> _store;
        readonly Func<PersonalityStore> _personality;

        public MemoryChatProvider(IChatProvider inner, MemoryStore store, PersonalityStore personality = null)
            : this(inner, () => store, () => personality)
        {
        }

        /// <summary>
        /// Resolves the store on each request. The chat registry keeps one wrapper
        /// for the whole session, so a wipe can swap the runtime store underneath it.
        /// </summary>
        public MemoryChatProvider(IChatProvider inner, Func<MemoryStore> store, Func<PersonalityStore> personality)
        {
            if (inner == null)
                throw new ArgumentNullException(nameof(inner));

            _inner = inner;
            _store = store ?? (() => null);
            _personality = personality ?? (() => null);
        }

        public string ProviderId
        {
            get { return _inner.ProviderId; }
        }

        public string DisplayName
        {
            get { return _inner.DisplayName; }
        }

        public bool IsConfigured
        {
            get { return _inner.IsConfigured; }
        }

        public bool IsLocal
        {
            get { return string.Equals(_inner.ProviderId, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase); }
        }

        public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
        {
            return _inner.StreamChat(Enrich(request), onDelta, cancellationToken);
        }

        /// <summary>
        /// Returns the same request with the memory and persona blocks appended to the
        /// prompt, or the original request when there is nothing to add. Public so a
        /// test can check the prompt without a transport.
        /// </summary>
        public ChatRequest Enrich(ChatRequest request)
        {
            if (request == null)
                return null;

            var store = _store();
            var personality = _personality();
            var memories = store == null
                ? null
                : store.SearchFor(LastUserMessage(request), MemoryStore.MaxSearchResults, IsLocal);

            var memoryBlock = MemoryPrompt.Build(memories);
            var personaBlock = personality == null ? string.Empty : MemoryPrompt.BuildPersona(personality.Current);
            if (memoryBlock.Length == 0 && personaBlock.Length == 0)
                return request;

            var builder = new StringBuilder();
            var prompt = request.SystemPrompt == null ? string.Empty : request.SystemPrompt.TrimEnd();
            builder.Append(prompt);
            if (personaBlock.Length > 0)
            {
                if (builder.Length > 0)
                    builder.Append("\n\n");
                builder.Append(personaBlock);
            }

            if (memoryBlock.Length > 0)
            {
                if (builder.Length > 0)
                    builder.Append("\n\n");
                builder.Append(memoryBlock);
            }

            return new ChatRequest
            {
                SystemPrompt = builder.ToString(),
                Messages = request.Messages,
                Model = request.Model,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
            };
        }

        static string LastUserMessage(ChatRequest request)
        {
            var messages = request.Messages;
            if (messages == null || messages.Count == 0)
                return string.Empty;

            for (var i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                if (message.Role == ChatRole.User)
                    return message.Content;
            }

            return string.Empty;
        }
    }

    /// <summary>Wraps each provider the factory registers, so callers never see it.</summary>
    public sealed class MemoryChatDecorator : IChatProviderDecorator
    {
        readonly Func<MemoryStore> _store;
        readonly Func<PersonalityStore> _personality;

        public MemoryChatDecorator(MemoryStore store, PersonalityStore personality = null)
            : this(() => store, () => personality)
        {
        }

        MemoryChatDecorator(Func<MemoryStore> store, Func<PersonalityStore> personality)
        {
            _store = store ?? (() => null);
            _personality = personality ?? (() => null);
        }

        /// <summary>
        /// Wrapper that reads <see cref="MemoryRuntime.Store"/> on every request.
        /// Chat keeps the wrapped provider, so this is what sees a wipe.
        /// </summary>
        public static MemoryChatDecorator FollowRuntime()
        {
            return new MemoryChatDecorator(() => MemoryRuntime.Store, () => null);
        }

        public IChatProvider Wrap(IChatProvider provider)
        {
            if (provider == null)
                return null;

            return new MemoryChatProvider(provider, _store, _personality);
        }
    }

    /// <summary>
    /// Process-wide memory and personality, loaded from the per-user folder on first
    /// use. The chat panel passes <see cref="Decorator"/> to the provider factory.
    /// </summary>
    public static class MemoryRuntime
    {
        static readonly object Gate = new object();
        static readonly MemoryChatDecorator LiveDecorator = MemoryChatDecorator.FollowRuntime();
        static MemoryStore _store;
        static PersonalityStore _personality;
        static MemoryChatDecorator _decorator;

        public static MemoryStore Store
        {
            get
            {
                Ensure();
                return _store;
            }
        }

        public static PersonalityStore Personality
        {
            get
            {
                Ensure();
                return _personality;
            }
        }

        public static IChatProviderDecorator Decorator
        {
            get
            {
                Ensure();
                return _decorator;
            }
        }

        static string _personaId = string.Empty;

        /// <summary>Drops the in-memory stores so the next use reads the folder again.</summary>
        public static void Discard()
        {
            lock (Gate)
            {
                _store = null;
                _personality = null;
                _decorator = null;
            }
        }

        /// <summary>
        /// Switches which persona's memory is used. Called whenever settings are applied, so
        /// facts told to Lola stay with Lola. The previous store is already saved on every
        /// change, so nothing is lost.
        /// </summary>
        public static void SwitchPersona(string personaId)
        {
            var id = personaId == null ? string.Empty : personaId.Trim();
            lock (Gate)
            {
                if (_store != null && string.Equals(id, _personaId, StringComparison.Ordinal))
                    return;

                _personaId = id;
                _store = MemoryStore.CreateDefault(id);
                _personality = PersonalityStore.CreateDefault();
                _decorator = LiveDecorator;
            }
        }

        static void Ensure()
        {
            if (_store != null)
                return;

            lock (Gate)
            {
                if (_store != null)
                    return;

                _store = MemoryStore.CreateDefault(_personaId);
                _personality = PersonalityStore.CreateDefault();

                // The persona wording in the prompt belongs to the setup wizard
                // (SettingsApplicator.ApplyPersona). Only the remembered facts are
                // appended here, so the two do not restate the same lines.
                // The decorator reads Store on each request, so a wipe is visible
                // without rebuilding the chat registry.
                _decorator = LiveDecorator;
            }
        }
    }
}
