using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-126: Claude's non-streamed Messages API and the cloud provider factory.</summary>
    public sealed class AnthropicChatProviderTests
    {
        const string SheetReply =
            "{\"temperament\":{\"calm\":0.1,\"cheerful\":0.2,\"shy\":0.0,\"tsundere\":0.9,\"moody\":0.3,\"reason\":\"sharp\"}," +
            "\"relationship\":{\"role\":\"friend\",\"label\":\"friend\",\"affection\":10,\"familiarity\":20,\"sharedHistory\":false,\"reason\":\"met\"}," +
            "\"world\":{\"backstory\":[],\"lastWeek\":[],\"openThreads\":[],\"rightNow\":\"\"}," +
            "\"landing\":{\"touchy\":[],\"winsHerOver\":[]}}";

        static AnthropicChatProvider Provider(RecordingTransport transport, string baseUrl = "https://api.anthropic.com/v1/")
        {
            return new AnthropicChatProvider(new AnthropicProviderConfig
            {
                ProviderId = "pisara",
                DisplayName = "Pisara cloud",
                BaseUrl = () => baseUrl,
                Model = () => "claude-opus-5-5",
                ApiKey = () => "claude-key",
                TimeoutSeconds = () => 30f,
                Transport = transport,
            });
        }

        [Test]
        public void Request_UsesNativeMessagesHeadersAndGateClosedSystemPrompt()
        {
            var transport = new RecordingTransport(Wrap(SheetReply));
            var provider = Provider(transport);
            var request = PisaraReader.BuildRequest("Mira", "Her description", false, false);

            var result = provider.StreamChat(request, _ => { }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            StringAssert.Contains("sharp", result.Text);
            Assert.AreEqual("https://api.anthropic.com/v1/messages", transport.Last.Url);
            Assert.AreEqual("claude-key", Header(transport.Last, "x-api-key"));
            Assert.AreEqual(AnthropicChatProvider.AnthropicVersion, Header(transport.Last, "anthropic-version"));
            StringAssert.Contains("\"model\":\"claude-opus-5-5\"", transport.Last.Body);
            StringAssert.Contains("\"system\":", transport.Last.Body);
            StringAssert.Contains(IntimateBoundaries.GateClosedRule, transport.Last.Body);
            StringAssert.Contains("Her description", transport.Last.Body);
        }

        [Test]
        public void NonStreamedReply_ParsesThroughPisaraReader()
        {
            var transport = new RecordingTransport(Wrap(SheetReply), callback: false);
            var provider = Provider(transport);

            var result = Task.Run(() => PisaraReader.ReadAsync(
                provider,
                "Mira",
                "Her description",
                false,
                false,
                null,
                CancellationToken.None,
                30f)).Result;

            Assert.IsTrue(result.FromModel);
            Assert.AreEqual(0.9f, result.Sheet.temperament.tsundere, 0.001f);
            Assert.AreEqual(CharacterSheet.SourceModel, result.Sheet.source);
            Assert.AreEqual(1, transport.Calls);
        }

        [Test]
        public void MessagesUrl_HandlesBaseUrlAndFullPath()
        {
            Assert.AreEqual("https://api.anthropic.com/v1/messages", AnthropicChatProvider.MessagesUrl("https://api.anthropic.com/v1/"));
            Assert.AreEqual("https://api.anthropic.com/v1/messages", AnthropicChatProvider.MessagesUrl("https://api.anthropic.com/v1/messages"));
            Assert.IsTrue(AnthropicChatProvider.IsAnthropic("https://api.anthropic.com/v1/"));
            Assert.IsFalse(AnthropicChatProvider.IsAnthropic("https://api.x.ai/v1"));
        }

        [Test]
        public void Factory_SelectsAnthropicOnlyForAnthropicHosts()
        {
            var anthropic = PisaraCloudProvider.Create(
                "https://api.anthropic.com/v1/", "claude-opus-5-5", new FixedSource("key"), new RecordingTransport("{}"));
            var openai = PisaraCloudProvider.Create(
                "https://api.x.ai/v1", "grok-4.7", new FixedSource("key"), new RecordingTransport("{}"));

            Assert.IsInstanceOf<AnthropicChatProvider>(anthropic);
            Assert.IsInstanceOf<OpenAiCompatibleChatProvider>(openai);
        }

        static string Wrap(string text)
        {
            var sb = new System.Text.StringBuilder("{\"content\":[{\"type\":\"text\",\"text\":");
            JsonText.AppendString(sb, text);
            sb.Append("}]}");
            return sb.ToString();
        }

        static string Header(ChatHttpRequest request, string name)
        {
            for (var i = 0; i < request.Headers.Count; i++)
            {
                if (string.Equals(request.Headers[i].Key, name, StringComparison.OrdinalIgnoreCase))
                    return request.Headers[i].Value;
            }

            return null;
        }

        sealed class RecordingTransport : IChatTransport
        {
            readonly string _reply;
            readonly bool _callback;

            public RecordingTransport(string reply, bool callback = true)
            {
                _reply = reply;
                _callback = callback;
            }

            public ChatHttpRequest Last;
            public int Calls;

            public Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                if (_callback && onBodyChunk != null)
                    onBodyChunk(_reply);
                return Task.FromResult(ChatHttpResult.Http(200, _reply));
            }
        }

        sealed class FixedSource : IApiKeySource
        {
            readonly string _key;

            public FixedSource(string key)
            {
                _key = key;
            }

            public bool TryGet(out string key)
            {
                key = _key;
                return !string.IsNullOrWhiteSpace(key);
            }
        }
    }
}
