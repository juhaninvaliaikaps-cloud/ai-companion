using System;
using AICompanion.AI;

namespace AICompanion.Voice
{
    /// <summary>
    /// Voice follows the chat that will actually run. Grok uses the cloud voice.
    /// Local chat and private mode use Piper or Kokoro, so that text never goes to cloud TTS.
    /// An open intimate gate also counts as private here (JUH-112).
    /// </summary>
    public static class VoiceRoute
    {
        public const string Cloud = "xai";
        public const string Local = PiperTtsProvider.Id;

        public static string ProviderFor(bool privateMode, string chatProviderId, string baseUrl = null)
        {
            var chat = OutboundPrivacy.ChatProviderId(privateMode, chatProviderId);
            if (string.Equals(chat, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase))
                return Local;
            // A non-xAI OpenAI-compatible endpoint (DeepSeek, OpenRouter, Groq) has no xAI
            // TTS. Do not send its replies to a cloud voice that cannot authenticate.
            if (!HasXaiVoice(baseUrl))
                return Local;
            return Cloud;
        }

        public static bool UsesCloudTts(bool privateMode, string chatProviderId, string baseUrl = null)
        {
            return string.Equals(ProviderFor(privateMode, chatProviderId, baseUrl), Cloud, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Empty keeps the historical xAI default. Only an xAI host gets xAI TTS.</summary>
        public static bool HasXaiVoice(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return true;
            Uri uri;
            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out uri))
                return false;
            var host = uri.Host ?? string.Empty;
            return host.Equals("x.ai", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".x.ai", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True for every engine that keeps audio on this machine. Piper and Kokoro are both
        /// local; only the cloud engine may be blocked for local or private replies.
        /// </summary>
        public static bool IsLocalEngine(string providerId)
        {
            var id = providerId == null ? string.Empty : providerId.Trim();
            if (id.Length == 0)
                return false;

            return string.Equals(id, PiperTtsProvider.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, KokoroTtsProvider.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
