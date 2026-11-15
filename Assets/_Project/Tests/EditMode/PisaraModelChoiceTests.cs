using System;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-126: the Pisara cloud chooser and its privacy boundaries.</summary>
    public sealed class PisaraModelChoiceTests
    {
        static CompanionSettings ChatSettings()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.ProviderId = ChatDefaults.GrokId;
            settings.Model = "chat-model";
            settings.BaseUrl = "https://chat.example/v1";
            return settings;
        }

        [Test]
        public void Off_UsesTheChatPath()
        {
            var choice = PisaraModelChoice.For(ChatSettings(), false, false, true, true);

            Assert.IsFalse(choice.UseCloud);
            Assert.AreEqual(ChatDefaults.GrokId, choice.ProviderId);
            Assert.AreEqual("chat-model", choice.Model);
            Assert.IsFalse(choice.GateClosed);
            StringAssert.Contains("off", choice.Reason);
        }

        [Test]
        public void On_NotPrivate_UsesCloudWithTheGateClosed()
        {
            var settings = ChatSettings();
            settings.PisaraCloud = true;
            settings.PisaraCloudBaseUrl = "https://api.anthropic.com/v1/";
            settings.PisaraCloudModel = "claude-opus-5-5";

            var closed = PisaraModelChoice.For(settings, false, false, true, false);
            Assert.IsTrue(closed.UseCloud);
            Assert.AreEqual(PisaraModelChoice.CloudProviderId, closed.ProviderId);
            Assert.AreEqual("https://api.anthropic.com/v1/", closed.BaseUrl);
            Assert.AreEqual("claude-opus-5-5", closed.Model);
            Assert.IsTrue(closed.GateClosed);
            Assert.AreEqual(PisaraKeySource.Pisara, closed.KeySource);

            var open = PisaraModelChoice.For(settings, false, true, true, false);
            Assert.IsTrue(open.UseCloud);
            Assert.IsTrue(open.GateClosed, "the cloud request is always built gate-closed");
        }

        [Test]
        public void On_Private_UsesCloudWithTheGateClosed()
        {
            var settings = ChatSettings();
            settings.PisaraCloud = true;
            settings.PisaraCloudBaseUrl = "https://api.anthropic.com/v1/";
            settings.PisaraCloudModel = "claude-opus-5-5";

            var choice = PisaraModelChoice.For(settings, true, true, true, false);
            Assert.IsTrue(choice.UseCloud);
            Assert.IsTrue(choice.GateClosed);
            Assert.AreEqual(PisaraKeySource.Pisara, choice.KeySource);
        }

        [Test]
        public void On_NoKey_UsesChatPathWithAReason()
        {
            var settings = ChatSettings();
            settings.PisaraCloud = true;

            var choice = PisaraModelChoice.For(settings, false, false, false, false);

            Assert.IsFalse(choice.UseCloud);
            Assert.AreEqual(ChatDefaults.GrokId, choice.ProviderId);
            StringAssert.Contains("key", choice.Reason);
        }

        [Test]
        public void EmptyBaseAndModel_FallBackToTheGrokChatValues()
        {
            var settings = ChatSettings();
            settings.PisaraCloud = true;
            settings.PisaraCloudBaseUrl = string.Empty;
            settings.PisaraCloudModel = string.Empty;

            var choice = PisaraModelChoice.For(settings, false, false, false, true);

            Assert.AreEqual(settings.BaseUrl, choice.BaseUrl);
            Assert.AreEqual(settings.Model, choice.Model);
            Assert.AreEqual(PisaraKeySource.Grok, choice.KeySource);
        }

        [Test]
        public void AnthropicUrl_WithOnlyTheGrokKey_DoesNotCallTheCloud()
        {
            var settings = ChatSettings();
            settings.PisaraCloud = true;
            settings.PisaraCloudBaseUrl = "https://api.anthropic.com/v1/";
            settings.PisaraCloudModel = "claude-opus-5-5";

            var choice = PisaraModelChoice.For(settings, false, false, false, true);

            Assert.IsFalse(choice.UseCloud, "the Grok key must not be sent to Anthropic");
            Assert.AreEqual(PisaraKeySource.None, choice.KeySource);
            Assert.AreEqual("Set a Pisara API key for this service", choice.Reason);
        }

        [Test]
        public void GrokHost_WithOnlyTheGrokKey_UsesTheCloud()
        {
            var settings = ChatSettings();
            settings.PisaraCloud = true;
            settings.PisaraCloudBaseUrl = settings.BaseUrl;
            settings.PisaraCloudModel = "grok-pisara-test";

            var choice = PisaraModelChoice.For(settings, false, false, false, true);

            Assert.IsTrue(choice.UseCloud);
            Assert.AreEqual(PisaraKeySource.Grok, choice.KeySource);
        }

        [Test]
        public void NonGrokUrl_WithAnEmptyModel_AsksForAModel()
        {
            var settings = ChatSettings();
            settings.PisaraCloud = true;
            settings.PisaraCloudBaseUrl = "https://api.anthropic.com/v1/";
            settings.PisaraCloudModel = string.Empty;

            var choice = PisaraModelChoice.For(settings, false, false, true, false);

            Assert.IsFalse(choice.UseCloud);
            Assert.AreEqual("Set a Pisara model for this service", choice.Reason);
        }

        [Test]
        public void CloudChoice_DoesNotChangeInnocenceReader()
        {
            var settings = ChatSettings();
            settings.PisaraCloud = true;
            settings.PisaraCloudModel = "claude-opus-5-5";
            var choice = PisaraModelChoice.For(settings, false, true, true, false);
            Assert.IsTrue(choice.UseCloud);

            var cloud = new FakeProvider(ChatDefaults.GrokId);
            var result = InnocenceReader.ReadAsync(cloud, "Mira", "prompt", true, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(InnocenceReader.CloudRefused, result.Failure);
            Assert.AreEqual(0, cloud.Calls, "InnocenceReader must refuse before any call");
        }

        [Test]
        public void OldSettingsWithoutPisaraKeys_LoadTheDefaults()
        {
            var parsed = CompanionSettingsJson.Parse("{\"model\":\"chat-model\"}", SettingsLegacyPrefs.None);

            Assert.IsFalse(parsed.PisaraCloud);
            Assert.AreEqual(string.Empty, parsed.PisaraCloudBaseUrl);
            Assert.AreEqual(string.Empty, parsed.PisaraCloudModel);
        }

        [Test]
        public void PisaraCloudSettings_RoundTrip()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.PisaraCloud = true;
            settings.PisaraCloudBaseUrl = "https://api.anthropic.com/v1/";
            settings.PisaraCloudModel = "claude-opus-5-5";

            var parsed = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);

            Assert.IsTrue(parsed.PisaraCloud);
            Assert.AreEqual("https://api.anthropic.com/v1/", parsed.PisaraCloudBaseUrl);
            Assert.AreEqual("claude-opus-5-5", parsed.PisaraCloudModel);
        }

        [Test]
        public void PisaraApiKeyFileIsSecret()
        {
            Assert.IsTrue(LocalDataInventory.IsSecretFile("pisara-api-key.json"));
        }

        sealed class FakeProvider : IChatProvider
        {
            public FakeProvider(string id)
            {
                ProviderId = id;
            }

            public string ProviderId { get; }
            public string DisplayName => "Fake";
            public bool IsConfigured => true;
            public int Calls { get; private set; }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(ChatResult.Success("{}"));
            }
        }
    }
}
