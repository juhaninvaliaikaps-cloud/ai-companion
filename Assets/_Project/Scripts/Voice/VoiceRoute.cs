using System;
using AICompanion.AI;

namespace AICompanion.Voice
{
    /// <summary>
    /// Voice follows the chat that will actually run. Grok uses the cloud voice.
    /// Local chat and private mode use Piper, so that text never goes to cloud TTS.
    /// </summary>
    public static class VoiceRoute
    {
        public const string Cloud = "xai";
        public const string Local = PiperTtsProvider.Id;

        public static string ProviderFor(bool privateMode, string chatProviderId)
        {
            var chat = OutboundPrivacy.ChatProviderId(privateMode, chatProviderId);
            if (string.Equals(chat, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase))
                return Local;
            return Cloud;
        }

        public static bool UsesCloudTts(bool privateMode, string chatProviderId)
        {
            return string.Equals(ProviderFor(privateMode, chatProviderId), Cloud, StringComparison.OrdinalIgnoreCase);
        }
    }
}
