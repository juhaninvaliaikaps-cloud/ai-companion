using System.IO;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Settings;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AICompanion.Tests.EditMode
{
    public class ActiveUseTests
    {
        const string ExampleLocalModel = "hf.co/mradermacher/Rocinante-X-12B-v1-Heretic-Uncensored-i1-GGUF:Q4_K_M";

        [Test]
        public void SettingsScroll_IsFastEnoughForAWheelAndATouchpad()
        {
            Assert.AreEqual(80f, SettingsScroll.Sensitivity);
            Assert.GreaterOrEqual(SettingsScroll.Sensitivity, 40f);
        }

        [Test]
        public void ActiveLabel_NamesTheModelThatActuallyRuns()
        {
            Assert.AreEqual("Grok · grok-4.7", ActiveChatLabel.Format(ChatDefaults.GrokId, false, "grok-4.7", "llama3.2"));
            Assert.AreEqual("Local · " + ExampleLocalModel, ActiveChatLabel.Format(ChatDefaults.LocalId, false, "grok-4.7", ExampleLocalModel));
            Assert.AreEqual("Local · llama3.2 · private", ActiveChatLabel.Format(ChatDefaults.GrokId, true, "grok-4.7", "llama3.2"));
            Assert.AreEqual("Local · llama3.2", ActiveChatLabel.Format("  LOCAL  ", false, "grok-4.7", "  llama3.2  "));
            Assert.AreEqual("Grok · " + ChatDefaults.Model, ActiveChatLabel.Format(ChatDefaults.GrokId, false, "  ", null));
        }

        [Test]
        public void VoiceRoute_UsesPiperForLocalAndPrivateAndCloudForGrok()
        {
            Assert.AreEqual(VoiceRoute.Cloud, VoiceRoute.ProviderFor(false, ChatDefaults.GrokId));
            Assert.AreEqual(VoiceRoute.Local, VoiceRoute.ProviderFor(false, ChatDefaults.LocalId));
            Assert.AreEqual(VoiceRoute.Local, VoiceRoute.ProviderFor(false, "LOCAL"));
            Assert.AreEqual(VoiceRoute.Local, VoiceRoute.ProviderFor(true, ChatDefaults.GrokId));
            Assert.AreEqual(VoiceRoute.Local, VoiceRoute.ProviderFor(true, null));
            Assert.IsTrue(VoiceRoute.UsesCloudTts(false, ChatDefaults.GrokId));
            Assert.IsFalse(VoiceRoute.UsesCloudTts(false, ChatDefaults.LocalId));
            Assert.IsFalse(VoiceRoute.UsesCloudTts(true, ChatDefaults.GrokId));
        }

        [Test]
        public void VoiceRoute_PrivateGrokSpeaksWithPiperAndNotCloudTts()
        {
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            var voice = ScriptableObject.CreateInstance<VoiceSettings>();
            try
            {
                chat.providerId = OutboundPrivacy.ChatProviderId(true, ChatDefaults.GrokId);
                voice.enabled = true;
                voice.provider = VoiceRoute.ProviderFor(true, ChatDefaults.GrokId);
                voice.allowCloudTtsForLocalReplies = true;

                Assert.AreEqual(ChatDefaults.LocalId, chat.providerId);
                Assert.AreEqual(PiperTtsProvider.Id, voice.provider);
                Assert.IsTrue(SpeechPolicy.ShouldSpeak(chat, voice, false, true));
                Assert.IsFalse(VoiceRoute.UsesCloudTts(true, ChatDefaults.GrokId));

                chat.providerId = ChatDefaults.GrokId;
                voice.provider = VoiceRoute.ProviderFor(false, ChatDefaults.GrokId);
                Assert.AreEqual(VoiceRoute.Cloud, voice.provider);
                Assert.IsTrue(SpeechPolicy.ShouldSpeak(chat, voice, false, false));
                Assert.IsFalse(SpeechPolicy.ShouldSpeak(chat, voice, false, true));
            }
            finally
            {
                Object.DestroyImmediate(chat);
                Object.DestroyImmediate(voice);
            }
        }

        [Test]
        public void Utterance_LocalAndPrivateStayOnPiperEvenWhenTheVoiceFileIsBlank()
        {
            var voice = ScriptableObject.CreateInstance<VoiceSettings>();
            try
            {
                voice.voiceId = "eve";
                voice.provider = VoiceRoute.Cloud;
                voice.allowCloudTtsForLocalReplies = true;
                voice.piperVoice = string.Empty;
                voice.piperExe = string.Empty;

                var local = VoiceUtterance.Decide(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.LocalId,
                    Model = ExampleLocalModel,
                    Private = false
                }, voice, _ => false);
                Assert.IsFalse(local.UsesCloudTts);
                Assert.AreEqual(PiperTtsProvider.Id, local.EngineId);
                Assert.AreEqual(VoiceUtterance.DefaultPiperVoiceId, local.Voice);
                StringAssert.Contains("provider=Local", local.LogLine());
                StringAssert.Contains("engine=Piper", local.LogLine());
                StringAssert.Contains("model=" + ExampleLocalModel, local.LogLine());
                StringAssert.DoesNotContain("engine=xAI", local.LogLine());

                var forced = VoiceUtterance.Decide(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.GrokId,
                    Model = "llama3.2",
                    Private = true
                }, voice, _ => false);
                Assert.IsTrue(forced.Private);
                Assert.AreEqual("Piper", forced.Engine);
                Assert.IsFalse(forced.UsesCloudTts);
                Assert.AreEqual("Voice route: provider=Local model=llama3.2 private=true engine=Piper voice=" + VoiceUtterance.DefaultPiperVoiceId, forced.LogLine());

                var root = Path.Combine(Path.GetTempPath(), "leia-piper-default");
                var onnx = Path.Combine(root, VoiceUtterance.DefaultPiperVoiceId + ".onnx");
                voice.piperExe = Path.Combine(root, "piper.exe");
                var resolved = VoiceUtterance.Decide(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.LocalId,
                    Model = "llama3.2",
                    Private = false
                }, voice, path => string.Equals(path, onnx, System.StringComparison.OrdinalIgnoreCase));
                Assert.AreEqual(onnx, resolved.PiperPath);
                Assert.AreEqual(VoiceUtterance.DefaultPiperVoiceId, resolved.Voice);
            }
            finally
            {
                Object.DestroyImmediate(voice);
            }
        }

        [Test]
        public void Speaker_SwitchesEngineWhenTheReplyOriginChanges()
        {
            var host = new GameObject("voice-route");
            var speaker = host.AddComponent<VoiceSpeaker>();
            try
            {
                speaker.StartCompanion();
                var grok = "Voice route: provider=Grok model=grok-4.7 private=false engine=xAI voice=eve";
                LogAssert.Expect(LogType.Log, grok);
                speaker.BeginUtterance(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.GrokId,
                    Model = "grok-4.7",
                    Private = false
                });
                Assert.AreEqual(VoiceRoute.Cloud, speaker.ActiveEngineId);
                Assert.AreEqual("eve", speaker.RuntimeVoice.voiceId);

                var local = "Voice route: provider=Local model=llama3.2 private=true engine=Piper voice=" + VoiceUtterance.DefaultPiperVoiceId;
                LogAssert.Expect(LogType.Log, local);
                speaker.BeginUtterance(new SpeechOrigin
                {
                    ChatProviderId = ChatDefaults.GrokId,
                    Model = "llama3.2",
                    Private = true
                });
                Assert.AreEqual(PiperTtsProvider.Id, speaker.ActiveEngineId);
                Assert.AreEqual(PiperTtsProvider.Id, speaker.RuntimeVoice.provider);
                Assert.AreNotEqual(VoiceRoute.Cloud, speaker.ActiveEngineId);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
