using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using UnityEngine;

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

        public async Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
        {
            var enriched = await EnrichAsync(request, cancellationToken);
            return await _inner.StreamChat(enriched, onDelta, cancellationToken);
        }

        /// <summary>
        /// <see cref="Enrich(ChatRequest)"/>, with the smarter search (JUH-141) when it is on: the
        /// local model gives the query a meaning vector first. Words only when it is off, the
        /// model is missing, or it takes longer than <see cref="MemoryEmbeddings.QueryWaitSeconds"/>.
        /// </summary>
        public async Task<ChatRequest> EnrichAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            if (request == null || request.NoMemory || !MemoryEmbeddings.Enabled
                || ReferenceEquals(request.SystemPrompt, MemoryFactExtractor.SystemPrompt))
                return Enrich(request);

            Func<TimelineEntry, float> semantic = null;
            try
            {
                var store = _store();
                var timeline = store == null ? null : RelationshipTimeline.ForStore(store);
                semantic = await MemoryEmbeddings.SemanticFor(timeline, LastUserMessage(request), PreviousUserMessage(request), cancellationToken);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Memory: smarter search skipped (" + e.GetType().Name + ")");
            }

            return Enrich(request, semantic);
        }

        /// <summary>
        /// Returns the same request with the memory and persona blocks appended to the
        /// prompt, or the original request when there is nothing to add. Public so a
        /// test can check the prompt without a transport.
        /// </summary>
        public ChatRequest Enrich(ChatRequest request)
        {
            return Enrich(request, null);
        }

        /// <param name="semantic">The meaning score of each timeline entry for this turn, or null for words only.</param>
        public ChatRequest Enrich(ChatRequest request, Func<TimelineEntry, float> semantic)
        {
            if (request == null)
                return null;
            if (request.NoMemory)
                return request;

            var store = _store();
            var personality = _personality();
            var query = LastUserMessage(request);
            var memories = store == null
                ? null
                : store.SearchFor(query, MemoryStore.MaxSearchResults, IsLocal);

            // JUH-134 review, bug 1: the fact-extraction request is not her talking. Its "user"
            // message is the whole exchange, so a timeline search would find what she just said,
            // restart its recall cooldown, and let old history be extracted again as new facts.
            var extraction = ReferenceEquals(request.SystemPrompt, MemoryFactExtractor.SystemPrompt);
            var timeline = store == null || extraction ? null : RelationshipTimeline.ForStore(store);
            // JUH-141: the latest user message, and the one before it at half weight, so a
            // follow-up like "do you remember what I told you?" still has a topic.
            var timelineHits = timeline == null
                ? null
                : timeline.SearchHits(query, PreviousUserMessage(request), TimelineSearch.DefaultMax, IsLocal, DateTime.UtcNow, memories, semantic);

            // JUH-118: memories that say when they happened get "(3 days ago)", and a short-term
            // memory that keeps coming up becomes long-term. The fact-extraction request reads the
            // same memories but is not her recalling them.
            var memoryBlock = MemoryPrompt.Build(memories, DateTime.UtcNow);
            var timelineBlock = MemoryPrompt.BuildTimeline(timelineHits, DateTime.UtcNow);
            if (memoryBlock.Length > 0 && !extraction)
                WorldMemory.MarkRecalled(store, memories);
            if (timelineBlock.Length > 0 && timeline != null)
            {
                timeline.MarkRecalled(Entries(timelineHits));
                Debug.Log("Memory: " + timelineHits.Count + " timeline entries injected");
            }
            var personaBlock = personality == null ? string.Empty : MemoryPrompt.BuildPersona(personality.Current);
            if (memoryBlock.Length == 0 && timelineBlock.Length == 0 && personaBlock.Length == 0)
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

            if (timelineBlock.Length > 0)
            {
                if (builder.Length > 0)
                    builder.Append("\n\n");
                builder.Append(timelineBlock);
            }

            return new ChatRequest
            {
                SystemPrompt = builder.ToString(),
                Messages = request.Messages,
                Model = request.Model,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                TimeoutSeconds = request.TimeoutSeconds,
                Image = request.Image,
                BlockCloud = request.BlockCloud,
                NoMemory = request.NoMemory,
                Roleplay = request.Roleplay,
            };
        }

        /// <summary>The user message before the latest one, or empty.</summary>
        static string PreviousUserMessage(ChatRequest request)
        {
            var messages = request.Messages;
            if (messages == null)
                return string.Empty;
            var seen = 0;
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].Role != ChatRole.User)
                    continue;
                seen++;
                if (seen == 2)
                    return messages[i].Content ?? string.Empty;
            }

            return string.Empty;
        }

        static List<TimelineEntry> Entries(List<TimelineHit> hits)
        {
            var entries = new List<TimelineEntry>(hits != null ? hits.Count : 0);
            if (hits != null)
            {
                for (var i = 0; i < hits.Count; i++)
                    entries.Add(hits[i].Entry);
            }

            return entries;
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
