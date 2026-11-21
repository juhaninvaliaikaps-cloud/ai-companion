using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-143: one cloud slot for any cloud API. The base URL decides: Claude's streamed
    /// Messages API for api.anthropic.com, OpenAI-compatible for everything else. The id stays
    /// "grok" so saved settings keep working.
    /// </summary>
    public sealed class CloudChatProviderTests
    {
        const string Claude = "https://api.anthropic.com/v1";

        static CloudChatProvider Cloud(FakeTransport transport, string baseUrl, string model = "claude-sonnet-5-5", string key = "cloud-key")
        {
            return new CloudChatProvider(() => baseUrl, () => model, () => 30f, () => key, transport);
        }

        static ChatRequest Hello()
        {
            return new ChatRequest
            {
                SystemPrompt = "You are Lola.",
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "Hi!") },
                Temperature = 0.7f,
                MaxTokens = 256,
            };
        }

        static string Claudes(params string[] parts)
        {
            var sb = new StringBuilder();
            sb.Append("event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"role\":\"assistant\"}}\n\n");
            sb.Append("event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n");
            for (var i = 0; i < parts.Length; i++)
            {
                sb.Append("event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"")
                    .Append(parts[i]).Append("\"}}\n\n");
            }

            sb.Append("event: ping\ndata: {\"type\":\"ping\"}\n\n");
            sb.Append("event: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\n");
            sb.Append("event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n");
            return sb.ToString();
        }

        [TestCase("https://api.anthropic.com/v1", CloudApiKind.Anthropic)]
        [TestCase("https://api.deepseek.com/v1", CloudApiKind.DeepSeek)]
        [TestCase("https://api.x.ai/v1", CloudApiKind.OpenAiCompatible)]
        [TestCase("https://openrouter.ai/api/v1", CloudApiKind.OpenAiCompatible)]
        [TestCase("", CloudApiKind.OpenAiCompatible)]
        public void TheBaseUrl_DecidesTheApi(string baseUrl, CloudApiKind expected)
        {
            Assert.AreEqual(expected, CloudChatProvider.KindOf(baseUrl));
        }

        [Test]
        public void Claude_StreamsTheReply_PieceByPiece()
        {
            var transport = new FakeTransport(Claudes("Hey", " you", "!"), splitEvery: 37);
            var deltas = new List<string>();

            var result = Cloud(transport, Claude).StreamChat(Hello(), deltas.Add, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok, result.UserMessage);
            Assert.AreEqual("Hey you!", result.Text);
            CollectionAssert.AreEqual(new[] { "Hey", " you", "!" }, deltas, "each delta passed on as it arrives");
            Assert.AreEqual("https://api.anthropic.com/v1/messages", transport.Last.Url);
            Assert.AreEqual("cloud-key", Header(transport.Last, "x-api-key"));
            Assert.AreEqual(AnthropicChatProvider.AnthropicVersion, Header(transport.Last, "anthropic-version"));
            Assert.IsNull(Header(transport.Last, "Authorization"), "no Bearer token to Anthropic");
            StringAssert.Contains("\"stream\":true", transport.Last.Body);
            StringAssert.Contains("\"system\":\"You are Lola.\"", transport.Last.Body);
            StringAssert.Contains("\"model\":\"claude-sonnet-5-5\"", transport.Last.Body);
        }

        [Test]
        public void Xai_UsesTheOpenAiCompatibleStream_WithABearerToken()
        {
            var transport = new FakeTransport("data: {\"choices\":[{\"delta\":{\"content\":\"Hi there\"}}]}\n\ndata: [DONE]\n\n");

            var result = Cloud(transport, "https://api.x.ai/v1", "grok-4.7").StreamChat(Hello(), null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok, result.UserMessage);
            Assert.AreEqual("Hi there", result.Text);
            Assert.AreEqual("https://api.x.ai/v1/chat/completions", transport.Last.Url);
            Assert.AreEqual("Bearer cloud-key", Header(transport.Last, "Authorization"));
            Assert.IsFalse(transport.Last.Body.Contains("\"thinking\""));
        }

        [Test]
        public void DeepSeek_TurnsItsThinkingOff()
        {
            var transport = new FakeTransport("data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\ndata: [DONE]\n\n");

            Cloud(transport, "https://api.deepseek.com/v1", "deepseek-chat").StreamChat(Hello(), null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("https://api.deepseek.com/v1/chat/completions", transport.Last.Url);
            StringAssert.Contains("\"thinking\":{\"type\":\"disabled\"}", transport.Last.Body);
        }

        [Test]
        public void AWindowImage_GoesOnlyToAModelThatReadsPictures()
        {
            // JUH-149: any API may get a picture, but only for a model that reads pictures.
            var transport = new FakeTransport(Claudes("x"));
            var request = Hello();
            request.Image = new ChatImageAttachment { JpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x10 }, Width = 1, Height = 1 };

            var result = Cloud(transport, "https://api.deepseek.com/v1", "deepseek-chat").StreamChat(request, null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(VisionCapability.ModelRefusal, result.UserMessage);
            Assert.IsNull(request.Image, "the image is wiped");
            Assert.AreEqual(0, transport.Calls, "nothing was sent");
        }

        [Test]
        public void ARequestThatMustStayLocal_IsRefusedBeforeAnything()
        {
            var transport = new FakeTransport(Claudes("x"));
            var request = Hello();
            request.BlockCloud = true;

            var result = Cloud(transport, Claude).StreamChat(request, null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(CloudChatProvider.BlockedRefusal, result.UserMessage);
            Assert.AreEqual(0, transport.Calls);
        }

        [Test]
        public void AClaudeKeyThatIsRejected_SaysSo()
        {
            var transport = new FakeTransport("{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}", status: 401);

            var result = Cloud(transport, Claude).StreamChat(Hello(), null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(ChatErrorKind.InvalidApiKey, result.Error);
        }

        [Test]
        public void AnOverloadedClaude_MidStream_WithNoText_IsAFailure()
        {
            var transport = new FakeTransport("event: error\ndata: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}\n\n");

            var result = Cloud(transport, Claude).StreamChat(Hello(), null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(ChatErrorKind.Unreachable, result.Error);
        }

        [Test]
        public void NoKey_MeansNotConfigured()
        {
            Assert.IsFalse(Cloud(new FakeTransport(""), Claude, key: "  ").IsConfigured);
            Assert.IsTrue(Cloud(new FakeTransport(""), Claude).IsConfigured);
        }

        [Test]
        public void TheRegistry_PutsTheCloudSlotUnderTheOldId()
        {
            var settings = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                settings.baseUrl = Claude;
                settings.model = "claude-sonnet-5-5";
                var transport = new FakeTransport(Claudes("Hi"));
                var registry = ChatProviderFactory.Create(settings, new Key("cloud-key"), transport);

                Assert.IsTrue(registry.TryGet(ChatDefaults.GrokId, out var cloud));
                Assert.IsInstanceOf<CloudChatProvider>(cloud);
                Assert.AreEqual(ChatDefaults.CloudShortName, cloud.DisplayName);
                var result = cloud.StreamChat(Hello(), null, CancellationToken.None).GetAwaiter().GetResult();
                Assert.AreEqual("Hi", result.Text);
                Assert.AreEqual("https://api.anthropic.com/v1/messages", transport.Last.Url);

                settings.baseUrl = "https://api.x.ai/v1";
                cloud.StreamChat(Hello(), null, CancellationToken.None).GetAwaiter().GetResult();
                Assert.AreEqual("https://api.x.ai/v1/chat/completions", transport.Last.Url, "a changed base URL applies at once");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void TheConnectionTest_SendsClaudesHeaders_ToClaude()
        {
            var claude = ConnectionProbe.Plan(false, ChatDefaults.GrokId, Claude, ChatDefaults.LocalBaseUrl);
            var claudeHeaders = ConnectionProbe.AuthHeaders(claude, "k");
            Assert.AreEqual("https://api.anthropic.com/v1/models", claude.Url);
            Assert.AreEqual("x-api-key", claudeHeaders[0].Key);
            Assert.AreEqual("anthropic-version", claudeHeaders[1].Key);

            var xai = ConnectionProbe.Plan(false, ChatDefaults.GrokId, "https://api.x.ai/v1", ChatDefaults.LocalBaseUrl);
            var xaiHeaders = ConnectionProbe.AuthHeaders(xai, "k");
            Assert.AreEqual(1, xaiHeaders.Count);
            Assert.AreEqual("Bearer k", xaiHeaders[0].Value);

            var local = ConnectionProbe.Plan(true, ChatDefaults.GrokId, Claude, ChatDefaults.LocalBaseUrl);
            Assert.AreEqual(0, ConnectionProbe.AuthHeaders(local, "k").Count, "private mode tests the local model, with no key");
        }

        [Test]
        public void AScreenLook_WithACloudApiThatCannotSee_UsesTheLocalVisionModel()
        {
            var input = new AICompanion.ScreenLook.ScreenLookInput
            {
                FeatureEnabled = true,
                CloudConsent = true,
                AllowCloud = true,
                EffectiveProviderId = ChatDefaults.GrokId,
                CloudModel = "deepseek-chat",
                Target = new AICompanion.ScreenLook.WindowTarget { Hwnd = 42, ProcessId = 7, Title = "Notes", ClassName = "Notepad", IsAlive = true, Width = 800, Height = 600 },
            };

            Assert.AreEqual(VisionCapability.ModelRefusal, AICompanion.ScreenLook.ScreenLookPolicy.Evaluate(input).Message, "no local vision model: refused, nothing sent");

            input.LocalVisionModel = "qwen2.5vl:7b";
            var local = AICompanion.ScreenLook.ScreenLookPolicy.Evaluate(input);
            Assert.AreEqual(AICompanion.ScreenLook.ScreenLookRoute.Local, local.Route);
            Assert.IsTrue(local.AllowSend);
        }

        [Test]
        public void TheActiveLabel_SaysCloudAi()
        {
            Assert.AreEqual("Cloud AI · claude-sonnet-5-5", ActiveChatLabel.Format(ChatDefaults.GrokId, false, "claude-sonnet-5-5", "llama3.2"));
            Assert.AreEqual("Cloud AI API (Grok, Claude, DeepSeek etc.)", ChatDefaults.CloudName);
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

        sealed class Key : IApiKeySource
        {
            readonly string _value;

            public Key(string value)
            {
                _value = value;
            }

            public bool TryGet(out string key)
            {
                key = _value;
                return !string.IsNullOrEmpty(key);
            }
        }

        /// <summary>Delivers a body in small chunks (so lines are split), with one status.</summary>
        sealed class FakeTransport : IChatTransport
        {
            readonly string _body;
            readonly int _status;
            readonly int _splitEvery;

            public FakeTransport(string body, int status = 200, int splitEvery = 0)
            {
                _body = body ?? string.Empty;
                _status = status;
                _splitEvery = splitEvery;
            }

            public int Calls;
            public ChatHttpRequest Last;

            public Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                if (_status >= 400)
                    return Task.FromResult(ChatHttpResult.Http(_status, _body));
                if (onBodyChunk != null)
                {
                    if (_splitEvery <= 0)
                    {
                        onBodyChunk(_body);
                    }
                    else
                    {
                        for (var i = 0; i < _body.Length; i += _splitEvery)
                            onBodyChunk(_body.Substring(i, Math.Min(_splitEvery, _body.Length - i)));
                    }
                }

                return Task.FromResult(ChatHttpResult.Ok(_status));
            }
        }
    }
}
