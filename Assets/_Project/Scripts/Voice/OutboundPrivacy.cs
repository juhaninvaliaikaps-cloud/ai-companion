using System;
using AICompanion.AI;

namespace AICompanion.Voice
{
    /// <summary>
    /// Where a reply is allowed to go. Private mode is the local Ollama path only.
    /// Cloud TTS, the voice agent, and Uthana must check this before any request.
    /// The default is private mode off, which keeps today's public Grok path.
    /// </summary>
    public static class OutboundPrivacy
    {
        public static bool AllowsCloudChat(bool privateMode)
        {
            return !privateMode;
        }

        public static bool AllowsCloudTts(bool privateMode)
        {
            return !privateMode;
        }

        public static bool AllowsVoiceAgent(bool privateMode)
        {
            return !privateMode;
        }

        public static bool AllowsUthana(bool privateMode)
        {
            return !privateMode;
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
