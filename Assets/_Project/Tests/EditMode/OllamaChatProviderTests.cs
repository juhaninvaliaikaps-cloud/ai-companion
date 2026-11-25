using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>The native Ollama path exists to keep num_ctx above Ollama's 4096 default.</summary>
    public sealed class OllamaChatProviderTests
    {
        [Test]
        public void StreamChat_BuildsNativeChatAndReadsJsonLines()
        {
            var transport = new FakeTransport
            {
                Chunks = new[]
                {
                    "{\"message\":{\"role\":\"assistant\",\"content\":\"Hel\"},\"done\":false}\n",
                    "{\"message\":{\"role\":\"assistant\",\"content\":\"lo\"},\"done\":true}\n",
                },
            };
            var provider = new OllamaChatProvider(new OllamaProviderConfig
            {
                BaseUrl = () => "http://127.0.0.1:11434/v1",
                Model = () => "demo",
                TimeoutSeconds = () => 30f,
                Transport = transport,
                ContextTokens = 8192,
            });
            var deltas = new List<string>();

            var result = provider.StreamChat(
                new ChatRequest
                {
                    SystemPrompt = "persona",
                    Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "hi") },
                    Temperature = 0.7f,
                    MaxTokens = 64,
                },
                deltas.Add,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            Assert.AreEqual("Hello", result.Text);
            CollectionAssert.AreEqual(new[] { "Hel", "lo" }, deltas);
            Assert.AreEqual("http://127.0.0.1:11434/api/chat", transport.Last.Url);
            StringAssert.Contains("\"think\":false", transport.Last.Body);
            StringAssert.Contains("\"num_ctx\":8192", transport.Last.Body);
            StringAssert.Contains("\"role\":\"system\"", transport.Last.Body);
            StringAssert.Contains("\"content\":\"persona\"", transport.Last.Body);
            StringAssert.Contains("\"role\":\"user\"", transport.Last.Body);
        }

        [Test]
        public void ChatUrl_AcceptsBaseAndV1()
        {
            Assert.AreEqual("http://127.0.0.1:11434/api/chat", OllamaChatJson.ChatUrl("http://127.0.0.1:11434"));
            Assert.AreEqual("http://127.0.0.1:11434/api/chat", OllamaChatJson.ChatUrl("http://127.0.0.1:11434/v1"));
        }

        sealed class FakeTransport : IChatTransport
        {
            public ChatHttpRequest Last;
            public string[] Chunks = Array.Empty<string>();

            public Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken)
            {
                Last = request;
                for (var i = 0; i < Chunks.Length; i++)
                    onBodyChunk?.Invoke(Chunks[i]);
                return Task.FromResult(ChatHttpResult.Ok(200));
            }
        }
    }
}
