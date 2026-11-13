using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Settings;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class PrivateEndpointTests
    {
        [Test]
        public void Settings_RoundTripTheCustomEndpoint_AndTheKeyIsNotInJson()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.PrivateMode = true;
            settings.PrivateEndpointType = PrivateEndpointTypes.Custom;
            settings.PrivateBaseUrl = "https://gpu.example.com/v1";
            settings.PrivateModel = "qwen2.5-7b-instruct";
            const string key = "sk-private-roundtrip-test";

            var json = CompanionSettingsJson.Write(settings);
            Assert.IsFalse(json.Contains(key), "the API key must never be in settings.json");
            Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(json));
            StringAssert.Contains("\"privateEndpointType\": \"custom\"", json);
            StringAssert.Contains("\"privateBaseUrl\": \"https://gpu.example.com/v1\"", json);

            var loaded = CompanionSettingsJson.Parse(json, SettingsLegacyPrefs.None);
            Assert.AreEqual(PrivateEndpointTypes.Custom, loaded.PrivateEndpointType);
            Assert.AreEqual("https://gpu.example.com/v1", loaded.PrivateBaseUrl);
            Assert.AreEqual("qwen2.5-7b-instruct", loaded.PrivateModel);
        }

        [Test]
        public void PrivateKeyFile_IsEncryptedAndReadable()
        {
            var directory = Path.Combine(Path.GetTempPath(), "leia-private-key-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "private-api-key.json");
            try
            {
                var protector = new ReverseProtector();
                PrivateApiKeyStore.Save(path, "sk-custom-secret", protector);
                var raw = File.ReadAllText(path);
                Assert.IsFalse(raw.Contains("sk-custom-secret"), "the key must be protected on disk");
                StringAssert.Contains("\"privateApiKey\"", raw);

                Assert.IsTrue(PrivateApiKeyStore.TryRead(path, protector, out var key));
                Assert.AreEqual("sk-custom-secret", key);
            }
            finally
            {
                try { Directory.Delete(directory, true); } catch (Exception) { }
            }
        }

        [Test]
        public void CustomProvider_RefusesA3xxRedirect_WithoutReplayingToken()
        {
            const string key = "sk-redirect-secret";
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                chat.privateEndpointType = PrivateEndpointTypes.Custom;
                chat.privateBaseUrl = "https://gpu.example.com/v1";
                chat.privateModel = "demo-model";
                var transport = new RecordingTransport
                {
                    Result = ChatHttpResult.Http(302, "redirect")
                };
                var registry = ChatProviderFactory.Create(chat, new FixedKey("cloud-key"), transport, null, new FixedKey(key));
                Assert.IsTrue(registry.TryGet(ChatDefaults.CustomId, out var custom));

                var result = custom.StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult();

                Assert.AreEqual(ChatErrorKind.Malformed, result.Error);
                StringAssert.Contains("tried to redirect", result.UserMessage);
                Assert.AreEqual(1, transport.Calls, "a redirect must not be followed or replayed");
                Assert.IsFalse(transport.Last.FollowRedirects, "custom POSTs must disable Unity redirects");
                Assert.AreEqual("https://gpu.example.com/v1/chat/completions", transport.Last.Url);
                Assert.AreEqual("Bearer " + key, Header(transport.Last, "Authorization"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chat);
            }
        }

        [Test]
        public void ChatProviderId_CustomServerIsPrivateAndCloudIsNot()
        {
            Assert.AreEqual(ChatDefaults.CustomId, OutboundPrivacy.ChatProviderId(true, ChatDefaults.GrokId, PrivateEndpointTypes.Custom));
            Assert.AreEqual(ChatDefaults.LocalId, OutboundPrivacy.ChatProviderId(true, ChatDefaults.GrokId, PrivateEndpointTypes.Local));
            Assert.AreEqual(ChatDefaults.GrokId, OutboundPrivacy.ChatProviderId(false, ChatDefaults.GrokId, PrivateEndpointTypes.Custom));
            Assert.AreEqual(ChatDefaults.GrokId, OutboundPrivacy.ChatProviderId(false, ChatDefaults.CustomId, PrivateEndpointTypes.Custom));
            Assert.AreEqual("Custom · qwen2.5 · private", ActiveChatLabel.Format(ChatDefaults.CustomId, true, "grok-4.7", "llama3.2", "qwen2.5"));
        }

        [TestCase(false, PrivateEndpointTypes.Custom, ChatDefaults.LocalId)]
        [TestCase(false, PrivateEndpointTypes.Local, ChatDefaults.LocalId)]
        [TestCase(true, PrivateEndpointTypes.Custom, ChatDefaults.CustomId)]
        [TestCase(true, PrivateEndpointTypes.Local, ChatDefaults.LocalId)]
        public void ObservationProviderId_NeverUsesCloudGrok(bool privateMode, string endpointType, string expected)
        {
            Assert.AreEqual(expected, PrivateChatRouting.ObservationProviderId(privateMode, endpointType));
        }

        [Test]
        public void CommercialHosts_AreRefusedAsPrivateEndpoints()
        {
            foreach (var host in PrivateEndpointRules.CommercialHosts)
            {
                var error = PrivateEndpointRules.ValidateAddress("https://" + host + "/v1");
                Assert.IsNotNull(error, host);
                StringAssert.Contains("commercial cloud provider", error, host);
            }

            Assert.IsNotNull(PrivateEndpointRules.ValidateAddress("HTTPS://API.OPENAI.COM./v1"));
            Assert.IsNull(PrivateEndpointRules.ValidateAddress("https://api.openai.com.evil.test/v1"));
            Assert.IsNotNull(PrivateEndpointRules.ValidateAddress("https://foo.openai.azure.com/v1"));
            Assert.IsNotNull(PrivateEndpointRules.ValidateAddress("https://auth.anthropic.com/v1"));
            Assert.IsNull(PrivateEndpointRules.ValidateAddress("https://gpu.example.com/v1"));
            Assert.IsNull(PrivateEndpointRules.ValidateAddress("https://api.mycompany.test/v1"));
            Assert.IsNull(PrivateEndpointRules.ValidateAddress("http://127.0.0.1:8000/v1"));
            Assert.IsNull(PrivateEndpointRules.ValidateAddress("http://localhost:8000/v1"));
        }

        [Test]
        public void PrivateProbeStatus_RedactsAnEchoedKey()
        {
            const string key = "sk-private-echo";
            var status = SettingsPanel.PrivateProbeStatus(
                401,
                "Unauthorized",
                "{\"error\":\"Incorrect API key " + key + "\"}",
                key,
                null);

            Assert.IsFalse(status.Contains(key));
            StringAssert.Contains("[redacted]", status);
        }

        [Test]
        public void CustomProvider_ScrubsAnEchoedKeyFromMessages()
        {
            const string key = "sk-custom-echo";
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                chat.privateEndpointType = PrivateEndpointTypes.Custom;
                chat.privateBaseUrl = "https://gpu.example.com/v1";
                chat.privateModel = "demo-model";
                var transport = new RecordingTransport
                {
                    Result = ChatHttpResult.Http(401, "{\"error\":\"Incorrect API key " + key + "\"}")
                };
                var registry = ChatProviderFactory.Create(chat, new FixedKey("cloud-key"), transport, null, new FixedKey(key));
                Assert.IsTrue(registry.TryGet(ChatDefaults.CustomId, out var custom));

                var result = custom.StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult();

                Assert.AreEqual(ChatErrorKind.InvalidApiKey, result.Error);
                Assert.IsFalse(result.UserMessage.Contains(key), result.UserMessage);
                Assert.IsFalse(result.LogMessage.Contains(key), result.LogMessage);
                StringAssert.Contains("[redacted]", result.LogMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chat);
            }
        }

        [Test]
        public void CommercialEndpoint_IsRejectedBeforeAnyRequest()
        {
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                chat.privateEndpointType = PrivateEndpointTypes.Custom;
                chat.privateBaseUrl = "https://api.openai.com/v1";
                chat.privateModel = "gpt-4o";
                var transport = new RecordingTransport();
                var registry = ChatProviderFactory.Create(chat, new FixedKey("cloud-key"), transport);
                Assert.IsTrue(registry.TryGet(ChatDefaults.CustomId, out var custom));

                var result = custom.StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult();

                Assert.AreEqual(ChatErrorKind.Malformed, result.Error);
                StringAssert.Contains("commercial cloud provider", result.UserMessage);
                Assert.AreEqual(0, transport.Calls, "a commercial host must be refused before any request");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chat);
            }
        }

        [Test]
        public void PlainHttp_IsWarnedOnlyOutsideLoopback()
        {
            StringAssert.Contains("plain HTTP", PrivateEndpointRules.HttpWarning("http://gpu.example.com/v1"));
            Assert.AreEqual(string.Empty, PrivateEndpointRules.HttpWarning("https://gpu.example.com/v1"));
            Assert.AreEqual(string.Empty, PrivateEndpointRules.HttpWarning("http://127.0.0.1:8000/v1"));
            Assert.AreEqual(string.Empty, PrivateEndpointRules.HttpWarning("http://localhost:8000/v1"));
            Assert.AreEqual(string.Empty, PrivateEndpointRules.HttpWarning("http://[::1]:8000/v1"));
        }

        [Test]
        public void UnreachableCustomServer_DoesNotFallBackToCloud()
        {
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                chat.privateEndpointType = PrivateEndpointTypes.Custom;
                chat.privateBaseUrl = "http://127.0.0.1:9/v1";
                chat.privateModel = "demo-model";
                var transport = new RecordingTransport();
                var registry = ChatProviderFactory.Create(
                    chat,
                    new FixedKey("xai-cloud-key"),
                    transport,
                    null,
                    new FixedKey("custom-server-key"));

                Assert.IsTrue(registry.TryGet(ChatDefaults.CustomId, out var custom));
                var result = custom.StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult();

                Assert.AreEqual(ChatErrorKind.Unreachable, result.Error);
                Assert.AreEqual(1, transport.Calls);
                Assert.AreEqual("http://127.0.0.1:9/v1/chat/completions", transport.Last.Url);
                Assert.IsFalse(transport.Last.Url.Contains("api.x.ai"), "an unreachable custom server must not call xAI");
                Assert.AreEqual("Bearer custom-server-key", Header(transport.Last, "Authorization"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chat);
            }
        }

        [Test]
        public void VoiceUtterance_NamesTheCustomProviderWhileStayingLocal()
        {
            var plan = VoiceUtterance.Decide(new SpeechOrigin
            {
                ChatProviderId = ChatDefaults.CustomId,
                Model = "qwen2.5-7b",
                Private = true
            }, null, null);

            Assert.IsTrue(plan.Local);
            Assert.AreEqual("Custom", plan.ProviderLabel);
            Assert.AreEqual("qwen2.5-7b", plan.Model);
            StringAssert.Contains("provider=Custom", plan.LogLine());
        }

        [Test]
        public void IntimateGate_IsOnForLocalOrCustomPrivateMode_AndOffForCloud()
        {
            IntimateExpressions.Reset();
            try
            {
                IntimateExpressions.Configure(true, true, true);
                Assert.IsTrue(IntimateExpressions.Active, "local or custom endpoint in Private mode may show the gated set");
                Assert.AreEqual(ChatDefaults.CustomId, OutboundPrivacy.ChatProviderId(false, ChatDefaults.GrokId, PrivateEndpointTypes.Custom), "an open intimate gate must keep the custom route local");
                Assert.AreNotEqual(ChatDefaults.GrokId, OutboundPrivacy.ChatProviderId(false, "grok", "custom"));

                IntimateExpressions.Configure(true, false, true);
                Assert.IsFalse(IntimateExpressions.Active, "the cloud path must not activate the gated set");

                IntimateExpressions.Configure(false, true, true);
                Assert.IsFalse(IntimateExpressions.Active, "the setting must also be on");
            }
            finally
            {
                IntimateExpressions.Reset();
            }
        }

        static string Header(ChatHttpRequest request, string name)
        {
            if (request == null)
                return null;
            for (var i = 0; i < request.Headers.Count; i++)
            {
                if (string.Equals(request.Headers[i].Key, name, StringComparison.OrdinalIgnoreCase))
                    return request.Headers[i].Value;
            }

            return null;
        }

        sealed class RecordingTransport : IChatTransport
        {
            public int Calls;
            public ChatHttpRequest Last;
            public ChatHttpResult Result = ChatHttpResult.Network();

            public Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                return Task.FromResult(Result);
            }
        }

        sealed class FixedKey : IApiKeySource
        {
            readonly string _value;

            public FixedKey(string value)
            {
                _value = value;
            }

            public bool TryGet(out string key)
            {
                key = _value;
                return !string.IsNullOrWhiteSpace(_value);
            }
        }

        sealed class ReverseProtector : ISecretProtector
        {
            public bool Protects => true;

            public string Protect(string plain)
            {
                var chars = (plain ?? string.Empty).ToCharArray();
                Array.Reverse(chars);
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(new string(chars)));
            }

            public bool TryUnprotect(string payload, out string plain)
            {
                plain = null;
                try
                {
                    var raw = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                    var chars = raw.ToCharArray();
                    Array.Reverse(chars);
                    plain = new string(chars);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
