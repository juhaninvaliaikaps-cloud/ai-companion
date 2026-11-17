using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.Settings;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-135: custom cloud endpoints use their own URL and never force xAI TTS.</summary>
    public sealed class CloudProviderSetupTests
    {
        [Test]
        public void ConnectionProbe_UsesTheConfiguredBaseUrl()
        {
            var plan = ConnectionProbe.Plan(false, ChatDefaults.GrokId, "https://api.deepseek.com", ChatDefaults.LocalBaseUrl);

            Assert.IsTrue(plan.Allowed);
            Assert.IsTrue(plan.SendAuthorization);
            Assert.AreEqual("https://api.deepseek.com/models", plan.Url);
            Assert.IsFalse(plan.Url.Contains("x.ai"));
        }

        [Test]
        public void NonXaiCloudChat_FallsBackToLocalVoice()
        {
            Assert.IsFalse(VoiceRoute.UsesCloudTts(false, ChatDefaults.GrokId, "https://api.deepseek.com"));
            Assert.IsTrue(VoiceRoute.UsesCloudTts(false, ChatDefaults.GrokId, "https://api.x.ai/v1"));
            Assert.IsTrue(VoiceRoute.HasXaiVoice(null));
            Assert.IsFalse(VoiceRoute.HasXaiVoice("https://api.deepseek.com"));
        }

        [Test]
        public void DefaultXaiEndpoint_StillUsesCloudVoice()
        {
            Assert.IsTrue(VoiceRoute.UsesCloudTts(false, ChatDefaults.GrokId, ChatDefaults.BaseUrl));
            Assert.AreEqual(VoiceRoute.Cloud, VoiceRoute.ProviderFor(false, ChatDefaults.GrokId, ChatDefaults.BaseUrl));
        }

        [TestCase("https://api.deepseek.com")]
        [TestCase("https://openrouter.ai/api/v1")]
        [TestCase("https://api.groq.com/openai/v1")]
        public void SettingsApply_NonXaiCloud_WritesLocalVoice(string baseUrl)
        {
            var applied = ApplyVoice(baseUrl, VoiceRoute.Cloud);

            Assert.AreEqual(PiperTtsProvider.Id, applied.TtsProvider);
            Assert.AreEqual(PiperTtsProvider.Id, applied.VoiceProvider);
            Assert.AreEqual(PiperTtsProvider.Id, applied.EngineId);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("https://api.x.ai/v1")]
        public void SettingsApply_XaiOrEmptyBaseUrl_KeepsCloudVoice(string baseUrl)
        {
            var applied = ApplyVoice(baseUrl, VoiceRoute.Cloud);

            Assert.AreEqual(VoiceRoute.Cloud, applied.TtsProvider);
            Assert.AreEqual(VoiceRoute.Cloud, applied.VoiceProvider);
            Assert.AreEqual(VoiceRoute.Cloud, applied.EngineId);
        }

        [Test]
        public void SettingsApply_ExplicitPiperOnDeepSeek_StaysPiper()
        {
            var applied = ApplyVoice("https://api.deepseek.com", PiperTtsProvider.Id);

            Assert.AreEqual(PiperTtsProvider.Id, applied.TtsProvider);
            Assert.AreEqual(PiperTtsProvider.Id, applied.VoiceProvider);
            Assert.AreEqual(PiperTtsProvider.Id, applied.EngineId);
        }

        static AppliedVoice ApplyVoice(string baseUrl, string ttsProvider)
        {
            var settings = CompanionSettings.CreateDefault();
            settings.PrivateMode = false;
            settings.ProviderId = ChatDefaults.GrokId;
            settings.BaseUrl = baseUrl;
            settings.TtsProvider = ttsProvider;

            var host = new GameObject("voice-route-apply");
            var speaker = host.AddComponent<VoiceSpeaker>();
            VoiceSettings voice = null;
            ChatSettings chat = null;
            try
            {
                SettingsApplicator.Apply(settings, speaker);
                voice = speaker.RuntimeVoice;
                chat = speaker.RuntimeChat;
                return new AppliedVoice(
                    settings.TtsProvider,
                    voice != null ? voice.provider : null,
                    speaker.ActiveEngineId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (voice != null)
                    UnityEngine.Object.DestroyImmediate(voice);
                if (chat != null)
                    UnityEngine.Object.DestroyImmediate(chat);
                IntimateExpressions.Reset();
                PisaraSwitches.Reset();
                NewUiSwitch.Reset();
                MemoryRuntime.Discard();
            }
        }

        readonly struct AppliedVoice
        {
            public AppliedVoice(string ttsProvider, string voiceProvider, string engineId)
            {
                TtsProvider = ttsProvider;
                VoiceProvider = voiceProvider;
                EngineId = engineId;
            }

            public string TtsProvider { get; }
            public string VoiceProvider { get; }
            public string EngineId { get; }
        }
    }
}
