using System;
using AICompanion.AI;

namespace AICompanion.Voice
{
    /// <summary>
    /// Where a reply is allowed to go. Private mode is a local or user-provided server, never a commercial cloud provider.
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

        /// <summary>
        /// Screen images are a cloud vision call. Private mode keeps them on the machine.
        /// </summary>
        public static bool AllowsCloudVision(bool privateMode)
        {
            return !privateMode;
        }

        public static string ChatProviderId(bool privateMode, string selected, string privateEndpointType = null)
        {
            return PrivateChatRouting.ProviderId(privateMode, selected, privateEndpointType);
        }
    }
}
