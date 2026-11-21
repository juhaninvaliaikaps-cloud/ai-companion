using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-152: what the Persona Studio needs from chat: her first greeting in an empty chat,
    /// a sample line in her voice, and a test chat that saves nothing.
    /// </summary>
    public sealed partial class ChatPanel
    {
        public const float TestChatTimeoutSeconds = 120f;
        public const int TestChatMaxTokens = 300;
        public const string TestChatNoModel = "No chat model is ready. Set one up on the AI tab.";

        /// <summary>Shows her greeting when the chat is empty. Not spoken, so a launch stays quiet.</summary>
        public bool ShowGreeting(string text)
        {
            if (!_started)
                StartCompanion();
            if (_conversation == null || _conversation.IsBusy || _conversation.Lines.Count > 0 || string.IsNullOrWhiteSpace(text))
                return false;
            _conversation.AddAssistant(text.Trim());
            RefreshTranscript(true);
            return true;
        }

        /// <summary>Speaks a line in the active persona's voice without adding it to the chat.</summary>
        public bool SpeakSample(string text)
        {
            if (!_alive || string.IsNullOrWhiteSpace(text) || settings == null)
                return false;
            var privacy = GetComponent<IPrivacyMode>();
            var keepsLocal = IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
            BeginSpeech(keepsLocal, keepsLocal ? ChatDefaults.LocalId : settings.providerId);
            BindSpeech();
            if (_speech == null)
                return false;
            _speech.Append(text.Trim());
            _speech.CompleteUtterance();
            return true;
        }

        /// <summary>
        /// One test-chat reply for a draft persona: the chat model under the usual privacy rule
        /// (private mode and 18+ stay local), no memory, nothing added to the chat or history.
        /// </summary>
        public async Task<ChatResult> RequestTestReply(string systemPrompt, List<ChatMessage> messages, CancellationToken token)
        {
            if (_registry == null || settings == null)
                return new ChatResult(false, string.Empty, ChatErrorKind.None, TestChatNoModel, "test chat: no registry");
            var privacy = GetComponent<IPrivacyMode>();
            var keepsLocal = IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
            var providerId = PrivateChatRouting.ProviderId(keepsLocal, settings.providerId, settings.privateEndpointType);
            IChatProvider provider;
            if (!_registry.TryGet(providerId, out provider) || provider == null || !provider.IsConfigured || !PisaraReader.MayUse(provider, keepsLocal))
                return new ChatResult(false, string.Empty, ChatErrorKind.None, TestChatNoModel, "test chat: no provider");

            var request = new ChatRequest
            {
                SystemPrompt = (systemPrompt ?? string.Empty) + "\n" + IntimateBoundaries.PromptLine(false),
                Messages = messages ?? new List<ChatMessage>(),
                Temperature = settings.temperature,
                MaxTokens = TestChatMaxTokens,
                TimeoutSeconds = TestChatTimeoutSeconds,
                NoMemory = true,
                BlockCloud = keepsLocal,
            };
            try
            {
                return await provider.StreamChat(request, _ => { }, token);
            }
            catch (OperationCanceledException)
            {
                return new ChatResult(false, string.Empty, ChatErrorKind.Cancelled, string.Empty, "test chat cancelled");
            }
            catch (Exception ex)
            {
                return new ChatResult(false, string.Empty, ChatErrorKind.Unreachable, "The test chat failed.", ex.GetType().Name);
            }
        }
    }
}
