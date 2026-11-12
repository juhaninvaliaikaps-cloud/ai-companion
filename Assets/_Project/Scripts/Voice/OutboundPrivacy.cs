using System;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.Voice
{
    /// <summary>
    /// Where a reply is allowed to go. Private mode is a local or user-provided server, never a commercial cloud provider.
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

        public static string ChatProviderId(bool privateMode, string selected, string privateEndpointType = null)
        {
            return PrivateChatRouting.ProviderId(StaysLocal(privateMode), selected, privateEndpointType);
        }
    }
}
