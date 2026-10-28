using System;
using AICompanion.AI;

namespace AICompanion.Voice
{
    /// <summary>
    /// Decides whether reply text may leave the machine.
    /// The local chat provider is the adult-content path, so cloud TTS stays off
    /// unless the voice asset explicitly allows it.
    /// </summary>
    public static class SpeechPolicy
    {
        public static bool ShouldSpeak(ChatSettings chat, VoiceSettings voice, bool muted)
        {
            return ShouldSpeak(chat, voice, muted, false);
        }

        /// <summary>
        /// Private mode never sends reply text to cloud TTS, even when the voice
        /// asset would allow cloud speech for a local model.
        /// </summary>
        public static bool ShouldSpeak(ChatSettings chat, VoiceSettings voice, bool muted, bool privateMode)
        {
            if (privateMode || !OutboundPrivacy.AllowsCloudTts(privateMode))
                return false;
            if (voice == null || !voice.enabled || muted)
                return false;

            var provider = chat != null && chat.providerId != null ? chat.providerId.Trim() : string.Empty;
            if (provider.Length > 0 && string.Equals(provider, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase))
                return voice.allowCloudTtsForLocalReplies;
            return true;
        }
    }
}
