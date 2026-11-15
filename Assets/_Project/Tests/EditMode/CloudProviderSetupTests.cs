using AICompanion.AI;
using AICompanion.Settings;
using AICompanion.Voice;
using NUnit.Framework;

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
    }
}
