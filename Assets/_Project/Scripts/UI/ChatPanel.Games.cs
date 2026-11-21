using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using Director = AICompanion.AnimationDirector.AnimationDirector;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-47: game companion lines in the chat. The game controller decides what to say;
    /// this asks the cloud AI API for a line with her persona, or shows and speaks a line.
    /// </summary>
    public sealed partial class ChatPanel
    {
        public const string GameLineRefusedPrivate = "Private mode or 18+ is on, so nothing about the game was sent.";
        public const string GameLineNoCloud = "No cloud AI API is set up.";

        /// <summary>True when a game line may go into the chat now: she isn't answering the user.</summary>
        public bool CanSayGameLine
        {
            get { return _alive && _conversation != null && !_conversation.IsBusy; }
        }

        /// <summary>True when the cloud AI API slot has a key and a model.</summary>
        public bool CloudReady
        {
            get
            {
                IChatProvider provider;
                return _registry != null && _registry.TryGet(ChatDefaults.GrokId, out provider) && provider != null && provider.IsConfigured;
            }
        }

        /// <summary>
        /// One cloud request for a game line. The system text is her persona prompt, then
        /// <paramref name="gameText"/> (her game style and the fair-play rules), then the
        /// closed 18+ gate rule. It never runs in private mode or with the 18+ gate open, and
        /// it adds nothing to the conversation, memory or history.
        /// </summary>
        public async Task<ChatResult> RequestGameLine(string gameText, string turn, int maxTokens, float temperature, float timeoutSeconds, CancellationToken token)
        {
            var privacy = GetComponent<IPrivacyMode>();
            if (IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode))
                return new ChatResult(false, string.Empty, ChatErrorKind.None, GameLineRefusedPrivate, "game line refused: private");

            IChatProvider provider;
            if (_registry == null || !_registry.TryGet(ChatDefaults.GrokId, out provider) || provider == null || !provider.IsConfigured)
                return new ChatResult(false, string.Empty, ChatErrorKind.MissingApiKey, GameLineNoCloud, "game line: no cloud");

            var request = new ChatRequest
            {
                SystemPrompt = PersonaPrompt(false) + "\n\n" + (gameText ?? string.Empty) + "\n" + IntimateBoundaries.PromptLine(false),
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, turn ?? string.Empty) },
                Temperature = temperature,
                MaxTokens = maxTokens,
                TimeoutSeconds = timeoutSeconds,
                NoMemory = true,
            };
            try
            {
                return await provider.StreamChat(request, _ => { }, token);
            }
            catch (OperationCanceledException)
            {
                return new ChatResult(false, string.Empty, ChatErrorKind.Cancelled, string.Empty, "game line cancelled");
            }
            catch (Exception ex)
            {
                return new ChatResult(false, string.Empty, ChatErrorKind.Unreachable, string.Empty, "game line failed: " + ex.GetType().Name);
            }
        }

        /// <summary>
        /// Shows and speaks a game line with her face and gesture. A model line uses the cloud
        /// voice route (xAI voice only for an xAI host); a pre-written line always uses the
        /// local voice, so nothing about the game leaves this PC.
        /// </summary>
        public bool SayGameLine(string text, AnimationCue cue, bool fromModel)
        {
            if (!CanSayGameLine || string.IsNullOrWhiteSpace(text))
                return false;

            var privacy = GetComponent<IPrivacyMode>();
            var keepsLocal = IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
            var cloud = fromModel && !keepsLocal;
            BeginSpeech(!cloud, cloud ? ChatDefaults.GrokId : ChatDefaults.LocalId);
            _conversation.AddAssistant(text);
            var director = GetComponent<Director>();
            if (director != null)
                director.Play(cue);
            BindSpeech();
            if (_speech != null)
            {
                _speech.Append(text);
                _speech.CompleteUtterance();
            }

            RefreshTranscript(true);
            return true;
        }
    }
}
