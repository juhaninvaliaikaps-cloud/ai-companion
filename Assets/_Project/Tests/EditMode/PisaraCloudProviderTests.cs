using System;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-126: DeepSeek's thinking mode must be off for Pisara's JSON reading.</summary>
    public sealed class PisaraCloudProviderTests
    {
        [Test]
        public void DeepSeekEndpoint_DisablesThinking()
        {
            var transport = new RecordingTransport();
            var provider = PisaraCloudProvider.Create(
                "https://api.deepseek.com",
                "deepseek-v4-pro",
                () => "test-key",
                transport);

            provider.StreamChat(Request(), _ => { }, CancellationToken.None).GetAwaiter().GetResult();

            StringAssert.Contains("\"thinking\":{\"type\":\"disabled\"}", transport.Last.Body);
        }

        [Test]
        public void OtherEndpoint_DoesNotSendTheDeepSeekThinkingField()
        {
            var transport = new RecordingTransport();
            var provider = PisaraCloudProvider.Create(
                "https://api.x.ai/v1",
                "grok-4.7",
                () => "test-key",
                transport);

            provider.StreamChat(Request(), _ => { }, CancellationToken.None).GetAwaiter().GetResult();

            StringAssert.DoesNotContain("\"thinking\"", transport.Last.Body);
        }

        static ChatRequest Request()
        {
            return new ChatRequest
            {
                SystemPrompt = "Return JSON.",
                Messages = new System.Collections.Generic.List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, "read her"),
                },
                Temperature = 0.5f,
                MaxTokens = 2200,
                TimeoutSeconds = 30f,
            };
        }

        sealed class RecordingTransport : IChatTransport
        {
            public ChatHttpRequest Last;

            public Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken)
            {
                Last = request;
                return Task.FromResult(ChatHttpResult.Http(200, "data: [DONE]\n\n"));
            }
        }
    }
}
