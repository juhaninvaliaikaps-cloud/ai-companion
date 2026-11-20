using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>
    /// Writes durable facts after an exchange. The extraction call goes to the same
    /// provider that answered the user, so private mode (which routes chat to the local
    /// model) also keeps extraction local. If private mode is on and the provider is
    /// not local, nothing is sent at all.
    /// </summary>
    public static class MemoryCapture
    {
        public const int MaxTokens = 220;
        public const float Temperature = 0.2f;
        public const int MinExchangeCharacters = 12;

        /// <summary>
        /// Returns how many new facts were stored. Duplicates are skipped by the store,
        /// so repeating a name three times leaves one row.
        /// </summary>
        public static async Task<int> CaptureAsync(
            IChatProvider provider,
            string userText,
            string assistantText,
            bool privateMode,
            MemoryStore store)
        {
            if (provider == null || store == null || string.IsNullOrWhiteSpace(userText))
                return 0;

            var local = string.Equals(provider.ProviderId, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase);
            if (privateMode && !local)
                return 0;

            var exchange = MemoryFactExtractor.BuildExchangeText(userText, assistantText);
            if (exchange.Length < MinExchangeCharacters)
                return 0;

            // JUH-141: the stored facts this exchange is about, so a change ("my sister moved to
            // Oulu") corrects the old fact instead of living next to it. The request carries its
            // own list, so it skips the memory layer. Private facts only go to the local model.
            var known = KnownFacts(store, userText, assistantText, local);
            var request = new ChatRequest
            {
                SystemPrompt = MemoryFactExtractor.PromptWith(known),
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, exchange) },
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                NoMemory = true,
            };

            ChatResult result;
            try
            {
                result = await provider.StreamChat(request, _ => { }, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Memory: extraction failed (" + e.GetType().Name + ")");
                return 0;
            }

            if (result.Ok == false)
                return 0;

            var corrected = 0;
            var updates = MemoryFactExtractor.ParseUpdates(result.Text, known.Count);
            for (var i = 0; i < updates.Count; i++)
            {
                var fact = known[updates[i].Index - 1];
                if (store.Correct(fact.id, updates[i].Text, privateMode, MemoryFactExtractor.IsCoreFact(updates[i].Text), DateTime.UtcNow))
                    corrected++;
            }

            if (corrected > 0)
                Debug.Log("Memory: corrected " + corrected + " fact(s)");

            var facts = MemoryFactExtractor.Parse(result.Text);
            var saved = 0;
            for (var i = 0; i < facts.Count; i++)
            {
                var before = store.Count;
                store.Add(facts[i], MemoryKind.Fact, privateMode, MemoryFactExtractor.IsCoreFact(facts[i]));
                if (store.Count > before)
                    saved++;
            }

            if (saved > 0)
                Debug.Log("Memory: saved " + saved + " fact(s)");

            return saved;
        }

        /// <summary>
        /// The user's own stored facts (not her life, not session summaries) that fit this
        /// exchange, best first: identity facts, then the ones the user's words are about,
        /// ranked like the timeline (BM25, stemming, Finnish). At most
        /// <see cref="MemoryFactExtractor.MaxKnownFacts"/>.
        /// </summary>
        public static List<MemoryEntry> KnownFacts(MemoryStore store, string userText, string assistantText, bool includePrivate)
        {
            var known = new List<MemoryEntry>(MemoryFactExtractor.MaxKnownFacts);
            if (store == null)
                return known;

            var candidates = new List<TimelineEntry>();
            var byId = new Dictionary<string, MemoryEntry>(StringComparer.Ordinal);
            var entries = store.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsValid || entry.kind != MemoryKind.Fact || entry.origin != MemoryOrigins.Lived)
                    continue;
                if (entry.isPrivate && !includePrivate)
                    continue;
                if (entry.isCore && known.Count < 2)
                {
                    known.Add(entry);
                    continue;
                }

                byId[entry.id] = entry;
                candidates.Add(new TimelineEntry
                {
                    id = entry.id, summary = entry.text, title = string.Empty, kind = MemoryDurability.KindOf(entry.text),
                    importance = 3, confidence = 100, evidenceCount = 1, createdAtUtc = entry.createdAtUtc,
                    happenedAtUtc = entry.createdAtUtc, isPrivate = entry.isPrivate,
                });
            }

            var hits = TimelineSearch.Rank(candidates, userText, assistantText, true, DateTime.UtcNow,
                MemoryFactExtractor.MaxKnownFacts - known.Count);
            for (var i = 0; i < hits.Count && known.Count < MemoryFactExtractor.MaxKnownFacts; i++)
            {
                MemoryEntry entry;
                if (byId.TryGetValue(hits[i].Entry.id, out entry))
                    known.Add(entry);
            }

            return known;
        }
    }
}
