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

            var request = new ChatRequest
            {
                SystemPrompt = MemoryFactExtractor.SystemPrompt,
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, exchange) },
                Temperature = Temperature,
                MaxTokens = MaxTokens,
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
    }
}
