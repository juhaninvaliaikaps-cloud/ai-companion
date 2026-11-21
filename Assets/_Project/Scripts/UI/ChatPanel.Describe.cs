using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-154: "Describe her from the picture" on the cloud AI API, like
    /// <see cref="RequestGameLine"/> with an image. It never runs in Private mode or with the 18+
    /// gate open, and it adds nothing to the conversation, memory or history.
    /// </summary>
    public sealed partial class ChatPanel
    {
        public const string DescribeRefusedPrivate = "In Private mode or with 18+ open, her picture is never sent to a cloud model.";
        public const string DescribeNoVision = "The cloud model doesn't read pictures.";

        public async Task<ChatResult> RequestPictureDescription(byte[] jpeg, int width, int height, string prompt, float timeoutSeconds, CancellationToken token)
        {
            var image = new ChatImageAttachment { JpegBytes = jpeg, Width = width, Height = height };
            var privacy = GetComponent<IPrivacyMode>();
            if (IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode))
            {
                image.Wipe();
                return new ChatResult(false, string.Empty, ChatErrorKind.None, DescribeRefusedPrivate, "describe refused: private");
            }

            IChatProvider provider;
            if (_registry == null || !_registry.TryGet(ChatDefaults.GrokId, out provider) || provider == null || !provider.IsConfigured)
            {
                image.Wipe();
                return new ChatResult(false, string.Empty, ChatErrorKind.MissingApiKey, GameLineNoCloud, "describe: no cloud");
            }

            if (!CloudSeesImages || !VisionCapability.AcceptsPayload(image))
            {
                image.Wipe();
                return new ChatResult(false, string.Empty, ChatErrorKind.None, DescribeNoVision, "describe: cloud can't see");
            }

            var request = new ChatRequest
            {
                Image = image,
                SystemPrompt = (prompt ?? string.Empty) + "\n" + IntimateBoundaries.PromptLine(false),
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "Describe her.") },
                Temperature = 0.4f,
                MaxTokens = 220,
                TimeoutSeconds = timeoutSeconds,
                NoMemory = true,
            };
            try
            {
                return await provider.StreamChat(request, _ => { }, token);
            }
            catch (OperationCanceledException)
            {
                return new ChatResult(false, string.Empty, ChatErrorKind.Cancelled, string.Empty, "describe cancelled");
            }
            catch (Exception ex)
            {
                return new ChatResult(false, string.Empty, ChatErrorKind.Unreachable, string.Empty, "describe failed: " + ex.GetType().Name);
            }
            finally
            {
                image.Wipe();
            }
        }
    }
}
