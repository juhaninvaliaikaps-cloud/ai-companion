using System;
using System.IO;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-128: a non-xAI base URL stays on a local engine, and Chatterbox is one of those engines.
    /// </summary>
    public sealed class ChatterboxVoiceRouteTests
    {
        [Test]
        public void HasXaiVoice_OnlyAcceptsAnXaiHost()
        {
            Assert.IsTrue(VoiceRoute.HasXaiVoice(null));
            Assert.IsTrue(VoiceRoute.HasXaiVoice(string.Empty));
            Assert.IsTrue(VoiceRoute.HasXaiVoice("   "));
            Assert.IsTrue(VoiceRoute.HasXaiVoice(ChatDefaults.BaseUrl));
            Assert.IsTrue(VoiceRoute.HasXaiVoice("https://API.X.AI/v1"));
            Assert.IsTrue(VoiceRoute.HasXaiVoice("https://foo.x.ai/v1"));
            Assert.IsFalse(VoiceRoute.HasXaiVoice("https://api.deepseek.com/v1"));
            Assert.IsFalse(VoiceRoute.HasXaiVoice("https://x.ai.evil.example/v1"));
            Assert.IsFalse(VoiceRoute.HasXaiVoice("https://notx.ai/v1"));
            Assert.IsFalse(VoiceRoute.HasXaiVoice("not a url"));
            Assert.IsFalse(VoiceRoute.HasXaiVoice("http://127.0.0.1:11434/v1"));
        }

        [Test]
        public void ProviderFor_UsesCloudTtsOnlyForAnXaiChat()
        {
            Assert.AreEqual(VoiceRoute.Cloud, VoiceRoute.ProviderFor(false, ChatDefaults.GrokId, null));
            Assert.AreEqual(VoiceRoute.Cloud, VoiceRoute.ProviderFor(false, ChatDefaults.GrokId, ChatDefaults.BaseUrl));
            Assert.IsTrue(VoiceRoute.UsesCloudTts(false, ChatDefaults.GrokId, "https://api.x.ai/v1"));

            Assert.AreEqual(VoiceRoute.Local, VoiceRoute.ProviderFor(false, ChatDefaults.GrokId, "https://api.deepseek.com/v1"));
            Assert.IsFalse(VoiceRoute.UsesCloudTts(false, ChatDefaults.GrokId, "https://api.deepseek.com/v1"));

            Assert.AreEqual(VoiceRoute.Local, VoiceRoute.ProviderFor(true, ChatDefaults.GrokId, ChatDefaults.BaseUrl));
            Assert.IsFalse(VoiceRoute.UsesCloudTts(true, ChatDefaults.GrokId, "https://api.x.ai/v1"));

            Assert.AreEqual(VoiceRoute.Local, VoiceRoute.ProviderFor(false, ChatDefaults.LocalId, ChatDefaults.BaseUrl));
            Assert.IsFalse(VoiceRoute.UsesCloudTts(false, ChatDefaults.LocalId, ChatDefaults.BaseUrl));
        }

        [Test]
        public void Chatterbox_IsALocalEngine()
        {
            Assert.IsTrue(VoiceRoute.IsLocalEngine(ChatterboxTtsProvider.Id));
            Assert.IsTrue(VoiceRoute.IsLocalEngine("CHATTERBOX"));
            Assert.IsTrue(VoiceRoute.IsLocalEngine(" chatterbox "));
            Assert.IsFalse(VoiceRoute.IsLocalEngine(VoiceRoute.Cloud));
        }

        [Test]
        public void Decide_SpeaksChatterboxForANonXaiReplyAndKeepsTheFolderName()
        {
            IntimateExpressions.Reset();
            var voices = Path.Combine(Path.GetTempPath(), "happi-cb-route-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(voices);
            var voice = ScriptableObject.CreateInstance<VoiceSettings>();
            try
            {
                voice.provider = ChatterboxTtsProvider.Id;
                voice.chatterboxPython = "python";
                voice.chatterboxScript = "script.py";
                voice.chatterboxVoices = voices;
                voice.chatterboxVoice = "Af_Bella";
                voice.voiceId = "eve";
                voice.allowCloudTtsForLocalReplies = true;

                var plan = VoiceUtterance.Decide(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.GrokId,
                    Model = "deepseek-chat",
                    Private = false,
                    BaseUrl = "https://api.deepseek.com/v1"
                }, voice, path => path == "python" || path == "script.py");

                Assert.IsTrue(plan.Local);
                Assert.IsFalse(plan.UsesCloudTts);
                Assert.AreEqual("Chatterbox", plan.Engine);
                Assert.AreEqual(ChatterboxTtsProvider.Id, plan.EngineId);
                Assert.AreEqual("Af_Bella", plan.Voice);
                var line = VoiceUtterance.RequestLine(plan.EngineId, voice, 4);
                StringAssert.Contains("engine=Chatterbox", line);
                StringAssert.Contains("voice=Af_Bella", line);
                StringAssert.Contains("local)", line);
                StringAssert.DoesNotContain("cloud)", line);

                var cloud = VoiceUtterance.Decide(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.GrokId,
                    Model = "grok-4.7",
                    Private = false,
                    BaseUrl = ChatDefaults.BaseUrl
                }, voice, path => path == "python" || path == "script.py");
                Assert.IsFalse(cloud.Local);
                Assert.IsTrue(cloud.UsesCloudTts);
                Assert.AreEqual(VoiceRoute.Cloud, cloud.EngineId);
                Assert.AreEqual("eve", cloud.Voice);
            }
            finally
            {
                IntimateExpressions.Reset();
                UnityEngine.Object.DestroyImmediate(voice);
                try
                {
                    if (Directory.Exists(voices))
                        Directory.Delete(voices, true);
                }
                catch (IOException)
                {
                }
            }
        }

        [Test]
        public void Decide_PrivateAndNonXaiRepliesStayOffCloudTts()
        {
            IntimateExpressions.Reset();
            var voice = ScriptableObject.CreateInstance<VoiceSettings>();
            try
            {
                voice.provider = VoiceRoute.Cloud;
                voice.voiceId = "eve";
                voice.allowCloudTtsForLocalReplies = true;

                var privateReply = VoiceUtterance.Decide(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.GrokId,
                    Model = "grok-4.7",
                    Private = true,
                    BaseUrl = ChatDefaults.BaseUrl
                }, voice, _ => false);
                Assert.IsTrue(privateReply.Private);
                Assert.IsTrue(privateReply.Local);
                Assert.IsFalse(privateReply.UsesCloudTts);
                Assert.AreEqual(PiperTtsProvider.Id, privateReply.EngineId);

                var otherHost = VoiceUtterance.Decide(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.GrokId,
                    Model = "deepseek-chat",
                    Private = false,
                    BaseUrl = "https://api.deepseek.com/v1"
                }, voice, _ => false);
                Assert.IsFalse(otherHost.Private);
                Assert.IsTrue(otherHost.Local);
                Assert.IsFalse(otherHost.UsesCloudTts);
                Assert.AreEqual(PiperTtsProvider.Id, otherHost.EngineId);
                Assert.AreEqual("Piper", otherHost.Engine);
            }
            finally
            {
                IntimateExpressions.Reset();
                UnityEngine.Object.DestroyImmediate(voice);
            }
        }
    }
}
