using AICompanion.AI;
using AICompanion.Settings;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;

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
    }
}
