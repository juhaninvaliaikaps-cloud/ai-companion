using System;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.Voice
{
    /// <summary>
    /// Where a reply is allowed to go. Private mode is the local Ollama path only.
    /// Cloud TTS, the voice agent, and Uthana must check this before any request.
    /// The default is private mode off, which keeps today's public Grok path.
    /// While the intimate gate is open everything stays local too, even if the private flag
    /// handed in here is stale (JUH-112).
    /// </summary>
    public static class OutboundPrivacy
    {
        static bool StaysLocal(bool privateMode)
        {
            return IntimateExpressions.KeepsLocal(privateMode);
        }

        public static bool AllowsCloudChat(bool privateMode)
        {
            return !StaysLocal(privateMode);
        }

        public static bool AllowsCloudTts(bool privateMode)
        {
            return !StaysLocal(privateMode);
        }

        public static bool AllowsVoiceAgent(bool privateMode)
        {
            return !StaysLocal(privateMode);
        }

        public static bool AllowsUthana(bool privateMode)
        {
            return !StaysLocal(privateMode);
        }

        /// <summary>
        /// Screen images are a cloud vision call. Private mode keeps them on the machine.
        /// </summary>
        public static bool AllowsCloudVision(bool privateMode)
        {
            return !StaysLocal(privateMode);
        }

        public static string ChatProviderId(bool privateMode, string selected)
        {
            if (!AllowsCloudChat(privateMode))
                return ChatDefaults.LocalId;

            if (string.IsNullOrWhiteSpace(selected))
                return ChatDefaults.GrokId;

            var id = selected.Trim();
            if (string.Equals(id, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase))
                return ChatDefaults.LocalId;
            if (string.Equals(id, ChatDefaults.GrokId, StringComparison.OrdinalIgnoreCase))
                return ChatDefaults.GrokId;
            return ChatDefaults.GrokId;
        }
    }
}
