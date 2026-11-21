using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-149: screen looks on any cloud AI API whose model reads pictures, not only xAI.</summary>
    public sealed class CloudVisionTests
    {
        sealed class Recorder : IChatTransport
        {
            readonly string _reply;
            public int Calls;
            public string Url = string.Empty;
            public string Body = string.Empty;

            public Recorder(string reply)
            {
                _reply = reply;
            }

            public Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken)
            {
                Calls++;
                Url = request.Url;
                Body = request.Body ?? string.Empty;
                if (onBodyChunk != null)
                    onBodyChunk(_reply);
                return Task.FromResult(ChatHttpResult.Ok(200));
            }
        }

        static ChatImageAttachment Jpeg()
        {
            return new ChatImageAttachment { JpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 }, Width = 2, Height = 2 };
        }

        static ChatRequest Look(ChatImageAttachment image)
        {
            return new ChatRequest
            {
                SystemPrompt = "You are Lola.",
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "What do you see?") },
                MaxTokens = 80,
                Image = image,
            };
        }

        static CloudChatProvider Cloud(Recorder transport, string baseUrl, string model)
        {
            return new CloudChatProvider(() => baseUrl, () => model, () => 30f, () => "key", transport);
        }

        [TearDown]
        public void TearDown()
        {
            VisionCapability.Reset();
        }

        [TestCase("deepseek-flash", true)]
        [TestCase("deepseek-v4-flash-vision-exp", true)]
        [TestCase("deepseek-chat", false)]
        [TestCase("deepseek-reasoner", false)]
        [TestCase("grok-4.7", true)]
        [TestCase("grok-4.7-vision", false)]
        [TestCase("grok-2", false)]
        [TestCase("claude-sonnet-5-5", true)]
        [TestCase("claude-3-haiku-20240307", true)]
        [TestCase("claude-instant-1.2", false)]
        [TestCase("gpt-4o-mini", true)]
        [TestCase("openai/gpt-4o", true)]
        [TestCase("gpt-3.5-turbo", false)]
        [TestCase("o3-mini", false)]
        [TestCase("o4-mini", true)]
        [TestCase("gemini-2.5-flash", true)]
        [TestCase("qwen2.5-vl-72b-instruct", true)]
        [TestCase("qwen3-235b", false)]
        [TestCase("meta-llama/llama-4-scout", true)]
        [TestCase("", false)]
        public void TheKnownVisionModels(string model, bool reads)
        {
            Assert.AreEqual(reads, VisionCapability.CloudModelAcceptsImages(model), model);
        }

        [Test]
        public void TheSetting_OverridesTheList()
        {
            VisionCapability.Configure("on");
            Assert.IsTrue(VisionCapability.CloudModelAcceptsImages("my-own-vision-model"));
            Assert.IsFalse(VisionCapability.CloudModelAcceptsImages(" "), "still needs a model");

            VisionCapability.Configure("off");
            Assert.IsFalse(VisionCapability.CloudModelAcceptsImages("grok-4.7"));

            VisionCapability.Configure("LOUD");
            Assert.AreEqual(VisionCapability.ModeAuto, VisionCapability.Mode);
            Assert.IsTrue(VisionCapability.CloudModelAcceptsImages("deepseek-flash"));
        }

        [Test]
        public void DeepSeek_GetsTheImage_AsAChatCompletionsDataUrl()
        {
            var transport = new Recorder("{\"id\":\"1\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"A boss fight! Go go go!\",\"reasoning_content\":null}}]}");
            var image = Jpeg();

            var result = Cloud(transport, "https://api.deepseek.com/v1", "deepseek-flash").StreamChat(Look(image), null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok, result.UserMessage);
            Assert.AreEqual("A boss fight! Go go go!", result.Text);
            Assert.AreEqual("https://api.deepseek.com/v1/chat/completions", transport.Url);
            StringAssert.Contains("\"type\":\"image_url\"", transport.Body);
            StringAssert.Contains("data:image/jpeg;base64," + Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 }), transport.Body);
            StringAssert.Contains("\"thinking\":{\"type\":\"disabled\"}", transport.Body);
            StringAssert.Contains("\"stream\":false", transport.Body);
            Assert.IsNull(image.JpegBytes, "the image is wiped");
        }

        [Test]
        public void DeepSeeksTextModel_GetsNoImage()
        {
            var transport = new Recorder("{}");
            var image = Jpeg();
            var result = Cloud(transport, "https://api.deepseek.com/v1", "deepseek-chat").StreamChat(Look(image), null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(VisionCapability.ModelRefusal, result.UserMessage);
            Assert.AreEqual(0, transport.Calls, "nothing was sent");
            Assert.IsNull(image.JpegBytes);
        }

        [Test]
        public void Xai_KeepsItsResponsesApi()
        {
            var transport = new Recorder("{\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"Nice view.\"}]}]}");
            var result = Cloud(transport, "https://api.x.ai/v1", "grok-4.7").StreamChat(Look(Jpeg()), null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result.Ok, result.UserMessage);
            Assert.AreEqual("https://api.x.ai/v1/responses", transport.Url);
            StringAssert.Contains("\"input_image\"", transport.Body);
        }

        [Test]
        public void Claude_GetsABase64ImageBlock()
        {
            var reply = "event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"role\":\"assistant\"}}\n\n"
                        + "event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n"
                        + "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"I see a menu.\"}}\n\n"
                        + "event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n";
            var transport = new Recorder(reply);
            var image = Jpeg();
            var result = Cloud(transport, "https://api.anthropic.com/v1", "claude-haiku-5-5").StreamChat(Look(image), null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok, result.UserMessage);
            Assert.AreEqual("https://api.anthropic.com/v1/messages", transport.Url);
            StringAssert.Contains("{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/jpeg\",\"data\":\"", transport.Body);
            StringAssert.Contains("\"text\":\"What do you see?\"", transport.Body);
            Assert.IsNull(image.JpegBytes, "the image is wiped");
        }

        [Test]
        public void APrivateRequest_NeverSendsTheImage_EvenToAVisionModel()
        {
            var transport = new Recorder("{}");
            var image = Jpeg();
            var request = Look(image);
            request.BlockCloud = true;
            var result = Cloud(transport, "https://api.deepseek.com/v1", "deepseek-flash").StreamChat(request, null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(0, transport.Calls);
            Assert.IsNull(image.JpegBytes);
        }

        [Test]
        public void TheSetting_DefaultsToAuto_AndRoundTrips()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.AreEqual(VisionCapability.ModeAuto, fresh.CloudVision);
            fresh.CloudVision = "on";
            Assert.AreEqual("on", CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None).CloudVision);
            fresh.CloudVision = "whatever";
            Assert.AreEqual(VisionCapability.ModeAuto, CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None).CloudVision);
        }
    }
}
