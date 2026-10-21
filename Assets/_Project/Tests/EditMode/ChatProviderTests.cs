using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using AICompanion.AI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class ChatProviderTests
    {
        [Test]
        public void SseParser_JoinsSplitChunks()
        {
            var events = Parse("data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"", "}}]}\n\n");

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(SseKind.Data, events[0].Kind);
            Assert.IsTrue(ChatCompletionJson.TryReadDeltaContent(events[0].Data, out var text));
            Assert.AreEqual("Hel", text);
        }

        [Test]
        public void SseParser_JoinsMultiLineDataAndStopsAtDone()
        {
            var events = Parse("data: {\"a\":\ndata: 1}\n\ndata: [DONE]\n\n");

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(SseKind.Data, events[0].Kind);
            Assert.AreEqual("{\"a\":\n1}", events[0].Data);
            Assert.AreEqual(SseKind.Done, events[1].Kind);
        }

        [Test]
        public void SseParser_MarksALineWithNoField()
        {
            var events = Parse("this is not sse\n\n");

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(SseKind.Malformed, events[0].Kind);
        }

        [Test]
        public void SseParser_IgnoresCommentsAndKeepsTheFollowingEvent()
        {
            var events = Parse(": keep-alive\ndata: {\"content\":\"Hi\"}\n\n");

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(SseKind.Data, events[0].Kind);
            Assert.IsTrue(ChatCompletionJson.TryReadDeltaContent(events[0].Data, out var text));
            Assert.AreEqual("Hi", text);
        }

        [Test]
        public void DeltaContent_SkipsReasoningAndReadsEscapes()
        {
            Assert.IsFalse(ChatCompletionJson.TryReadDeltaContent("{\"delta\":{\"reasoning_content\":\"The\"}}", out _));
            Assert.IsFalse(ChatCompletionJson.TryReadDeltaContent("{\"delta\":{\"content\":null}}", out _));
            Assert.IsTrue(ChatCompletionJson.TryReadDeltaContent("{\"delta\":{\"content\":\"a\\\"b\\n\\u0041\"}}", out var text));
            Assert.AreEqual("a\"b\nA", text);
        }

        [Test]
        public void Build_EscapesTheUserTextAndNamesTheModel()
        {
            var json = ChatCompletionJson.Build(
                "grok-4.7",
                0.7f,
                32,
                "be brief",
                new[] { new ChatMessage(ChatRole.User, "say \"hi\"") });

            StringAssert.Contains("\"model\":\"grok-4.7\"", json);
            StringAssert.Contains("\"stream\":true", json);
            StringAssert.Contains("\"temperature\":0.7", json);
            StringAssert.Contains("\"max_completion_tokens\":32", json);
            StringAssert.Contains("say \\\"hi\\\"", json);
            Assert.AreEqual("https://api.x.ai/v1/chat/completions", ChatEndpoints.CompletionsUrl("https://api.x.ai/v1/"));
            Assert.AreEqual("https://api.x.ai/v1/chat/completions", ChatEndpoints.CompletionsUrl("https://api.x.ai/v1/chat/completions"));
        }

        [Test]
        public void Errors_MapStatusCodesToFriendlyMessages()
        {
            var invalid = ChatErrors.FromTransport("Grok", ChatHttpResult.Ok(401), "", false, false);
            var limited = ChatErrors.FromTransport("Grok", ChatHttpResult.Ok(429), "", false, false);
            var down = ChatErrors.FromTransport("Grok", ChatHttpResult.Ok(503), "", false, false);
            var offline = ChatErrors.FromTransport("Grok", ChatHttpResult.Network(), "", false, false);
            var slow = ChatErrors.FromTransport("Grok", ChatHttpResult.Timeout(), "", false, false);
            var stopped = ChatErrors.FromTransport("Grok", ChatHttpResult.Cancel(), "", false, false);
            var garbled = ChatErrors.FromTransport("Grok", ChatHttpResult.Ok(200), "", false, true);

            Assert.AreEqual(ChatErrorKind.InvalidApiKey, invalid.Error);
            Assert.AreEqual("Invalid API key", invalid.UserMessage);
            Assert.AreEqual("Rate limited, try again shortly", limited.UserMessage);
            Assert.AreEqual("Couldn't reach Grok", down.UserMessage);
            Assert.AreEqual("Couldn't reach Grok", offline.UserMessage);
            Assert.AreEqual("Grok took too long to answer", slow.UserMessage);
            Assert.AreEqual("Request cancelled", stopped.UserMessage);
            Assert.AreEqual(ChatErrorKind.Malformed, garbled.Error);
            Assert.IsFalse(invalid.UserMessage.Contains("secret"));
        }

        [Test]
        public void Errors_ReportStatusAndScrubSecrets()
        {
            const string secret = "xai-SuperSecretKey1";
            var billing = ChatErrors.FromTransport(
                "Grok",
                ChatHttpResult.Http(403, "{\"code\":\"permission-denied\",\"error\":\"No credits. Bearer " + secret + "\"}"),
                "",
                false,
                false);
            Assert.AreEqual(ChatErrors.BillingHint, billing.UserMessage);
            StringAssert.StartsWith("Chat error: HTTP 403 from Grok: ", billing.LogMessage);
            StringAssert.Contains("No credits", billing.LogMessage);
            Assert.IsFalse(billing.LogMessage.Contains(secret));
            Assert.IsFalse(billing.UserMessage.Contains(secret));
            Assert.IsFalse(billing.LogMessage.Contains("Authorization"));

            var invalidKey = ChatErrors.FromTransport(
                "Grok",
                ChatHttpResult.Http(400, "{\"error\":\"Incorrect API key provided: " + secret + "\"}"),
                "",
                false,
                false);
            Assert.AreEqual("Invalid API key", invalidKey.UserMessage);
            StringAssert.Contains("HTTP 400", invalidKey.LogMessage);
            Assert.IsFalse(invalidKey.LogMessage.Contains(secret));

            var missingModel = ChatErrors.FromTransport(
                "Grok",
                ChatHttpResult.Http(404, "{\"error\":\"The model does not exist\"}"),
                "",
                false,
                false,
                "grok-4.7");
            Assert.AreEqual("Model grok-4.7 not available for this key; change ChatSettings.model", missingModel.UserMessage);

            var namedInBody = ChatErrors.FromTransport(
                "Grok",
                ChatHttpResult.Http(400, "{\"error\":{\"message\":\"unknown model\"}}"),
                "",
                false,
                false,
                "demo-model");
            Assert.AreEqual("Model demo-model not available for this key; change ChatSettings.model", namedInBody.UserMessage);
            StringAssert.Contains("unknown model", namedInBody.LogMessage);

            var plain = new string('b', 600);
            var rejected = ChatErrors.FromTransport("Grok", ChatHttpResult.Http(422, plain), "", false, false);
            const string screenPrefix = "Grok rejected the request (HTTP 422): ";
            const string logPrefix = "Chat error: HTTP 422 from Grok: ";
            StringAssert.StartsWith(screenPrefix, rejected.UserMessage);
            Assert.AreEqual(ChatErrors.ScreenBodyLimit, rejected.UserMessage.Length - screenPrefix.Length);
            StringAssert.StartsWith(logPrefix, rejected.LogMessage);
            Assert.AreEqual(new string('b', ChatErrors.LogBodyLimit), rejected.LogMessage.Substring(logPrefix.Length));

            var offline = ChatErrors.FromTransport(
                "Grok",
                ChatHttpResult.Network("Cannot connect to destination host Bearer " + secret),
                "",
                false,
                false);
            Assert.AreEqual("Couldn't reach Grok", offline.UserMessage);
            StringAssert.Contains("Cannot connect to destination host", offline.LogMessage);
            Assert.IsFalse(offline.LogMessage.Contains(secret));
            Assert.AreEqual("see Bearer [redacted] and [redacted].", ChatErrors.Scrub("see Bearer " + secret + " and " + secret + "."));
        }

        [Test]
        public void Registry_SelectsByIdAndKeepsTheFirstRegistration()
        {
            var registry = new ChatProviderRegistry();
            var grok = new StubProvider("grok");
            var local = new StubProvider("local");
            var again = new StubProvider("Grok");
            registry.Register(grok);
            registry.Register(local);
            registry.Register(again);

            Assert.IsTrue(registry.TryGet("grok", out var found));
            Assert.AreSame(grok, found);
            Assert.IsTrue(registry.TryGet("LOCAL", out var localFound));
            Assert.AreSame(local, localFound);
            Assert.IsFalse(registry.TryGet("missing", out _));
            Assert.IsFalse(registry.TryGet("  ", out _));
        }

        [Test]
        public void KeyResolution_PrefersTheEnvironmentThenTheFirstReadableFile()
        {
            var protector = new PlainSecretProtector();
            var file = SecretFileFormat.Write("file-key", false);
            var second = SecretFileFormat.Write("other-key", false);
            var reversed = SecretFileFormat.Write("yek-tset", true);

            Assert.IsTrue(ApiKeyResolver.TryResolveValues("  env-key  ", new[] { file }, protector, out var fromEnv));
            Assert.AreEqual("env-key", fromEnv);

            Assert.IsTrue(ApiKeyResolver.TryResolveValues("   ", new[] { "not json", file, second }, protector, out var fromFile));
            Assert.AreEqual("file-key", fromFile);

            Assert.IsFalse(ApiKeyResolver.TryResolveValues(null, new[] { "{\"protected\":true,\"xaiApiKey\":\"abc\"}" }, protector, out _));
            Assert.IsTrue(ApiKeyResolver.TryResolveValues(null, new[] { reversed }, new ReverseProtector(), out var unprotected));
            Assert.AreEqual("test-key", unprotected);
            Assert.IsFalse(ApiKeyResolver.TryResolveValues(null, new string[0], protector, out _));
        }

        [Test]
        public void KeyFile_RoundTripsOutsideTheRepository()
        {
            var directory = Path.Combine(Path.GetTempPath(), "project-leia-key-test-" + System.Guid.NewGuid().ToString("N"));
            var first = Path.Combine(directory, "a", "secrets.json");
            var second = Path.Combine(directory, "b", "secrets.json");
            try
            {
                ApiKeyFile.Save(first, "test-key-not-real", new PlainSecretProtector());
                ApiKeyFile.Save(second, "second-key", new ReverseProtector());
                Assert.IsTrue(ApiKeyFile.TryReadKey(first, new PlainSecretProtector(), out var plain));
                Assert.AreEqual("test-key-not-real", plain);
                Assert.IsTrue(ApiKeyResolver.TryResolveFiles(_ => null, new[] { first, second }, new PlainSecretProtector(), out var resolved));
                Assert.AreEqual("test-key-not-real", resolved);

                var root = Path.Combine("app-data");
                Assert.AreEqual(Path.Combine(root, "ProjectLeia", "secrets.json"), ApiKeyLocations.AppDataPath(root));
                Assert.AreEqual(ApiKeyLocations.AppDataPath("a"), ApiKeyLocations.PreferredWritePath("a", "b"));
                Assert.AreEqual(ApiKeyLocations.PersistentPath("b"), ApiKeyLocations.PreferredWritePath("", "b"));
                Assert.IsNull(ApiKeyLocations.AppDataPath(null));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void GrokProvider_StreamsDeltasThroughAFakeTransport()
        {
            var transport = new ScriptedTransport
            {
                Chunks = new[]
                {
                    "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n",
                    "data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"skip\",\"content\":\"lo\"}}]}\n\n",
                    "data: [DONE]\n\n"
                }
            };
            var provider = Grok(transport, "test-key");
            var deltas = new List<string>();
            var request = new ChatRequest
            {
                SystemPrompt = "be brief",
                Temperature = 0.7f,
                MaxTokens = 32
            };
            request.Messages.Add(new ChatMessage(ChatRole.User, "hello"));

            var result = provider.StreamChat(request, deltas.Add, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            Assert.AreEqual("Hello", result.Text);
            CollectionAssert.AreEqual(new[] { "Hel", "lo" }, deltas);
            Assert.AreEqual(1, transport.Calls);
            Assert.AreEqual("https://api.x.ai/v1/chat/completions", transport.Last.Url);
            StringAssert.Contains("\"model\":\"grok-4.7\"", transport.Last.Body);
            Assert.IsFalse(transport.Last.Body.Contains("test-key"));
            Assert.AreEqual("Bearer test-key", Header(transport.Last, "Authorization"));
        }

        [Test]
        public void GrokProvider_MapsTransportFailuresWithoutEchoingTheKey()
        {
            var missing = new ScriptedTransport();
            var missingResult = Grok(missing, "  ").StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(ChatErrorKind.MissingApiKey, missingResult.Error);
            Assert.AreEqual("No API key is set", missingResult.UserMessage);
            Assert.AreEqual(0, missing.Calls);

            var denied = new ScriptedTransport { Result = ChatHttpResult.Ok(401) };
            var deniedResult = Grok(denied, "test-key").StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual("Invalid API key", deniedResult.UserMessage);
            Assert.IsFalse(deniedResult.UserMessage.Contains("test-key"));

            var limited = new ScriptedTransport { Result = ChatHttpResult.Ok(429) };
            Assert.AreEqual("Rate limited, try again shortly", Grok(limited, "test-key").StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult().UserMessage);

            var offline = new ScriptedTransport { Result = ChatHttpResult.Network() };
            Assert.AreEqual("Couldn't reach Grok", Grok(offline, "test-key").StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult().UserMessage);

            var slow = new ScriptedTransport { Result = ChatHttpResult.Timeout() };
            Assert.AreEqual(ChatErrorKind.Timeout, Grok(slow, "test-key").StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult().Error);

            const string secret = "xai-SuperSecretKey1";
            var deniedBilling = new ScriptedTransport
            {
                Result = ChatHttpResult.Http(403, "{\"error\":\"Team has no credits. Bearer " + secret + "\"}")
            };
            var billing = Grok(deniedBilling, secret).StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(ChatErrors.BillingHint, billing.UserMessage);
            StringAssert.Contains("HTTP 403", billing.LogMessage);
            StringAssert.Contains("Team has no credits", billing.LogMessage);
            Assert.IsFalse(billing.LogMessage.Contains(secret));
            Assert.IsFalse(billing.UserMessage.Contains(secret));
            Assert.IsFalse(deniedBilling.Last.Body.Contains(secret));
            Assert.AreEqual("Bearer " + secret, Header(deniedBilling.Last, "Authorization"));
        }

        [Test]
        public void Factory_UsesTheSelectedProviderWithoutACallerSwitch()
        {
            var settings = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                settings.providerId = ChatDefaults.LocalId;
                settings.localBaseUrl = "http://127.0.0.1:9/v1";
                settings.localModel = "demo-model";
                var transport = new ScriptedTransport
                {
                    Chunks = new[] { "data: [DONE]\n\n" }
                };
                var registry = ChatProviderFactory.Create(settings, new FixedKey("test-key"), transport);
                Assert.IsTrue(registry.TryGet(settings.providerId, out var provider));
                Assert.AreEqual(ChatDefaults.LocalId, provider.ProviderId);
                Assert.IsTrue(provider.IsConfigured);

                var result = provider.StreamChat(new ChatRequest(), null, CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsTrue(result.Ok);
                Assert.AreEqual("http://127.0.0.1:9/v1/chat/completions", transport.Last.Url);
                StringAssert.Contains("\"model\":\"demo-model\"", transport.Last.Body);
                Assert.IsNull(Header(transport.Last, "Authorization"));

                settings.providerId = ChatDefaults.GrokId;
                Assert.IsTrue(registry.TryGet(settings.providerId, out var grok));
                Assert.AreEqual(ChatDefaults.GrokId, grok.ProviderId);
                Assert.IsTrue(grok.IsConfigured);
                Assert.AreNotSame(provider, grok);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Utf8ChunkDecoder_KeepsASplitCharacter()
        {
            var decoder = new Utf8ChunkDecoder();
            var bytes = Encoding.UTF8.GetBytes("é");

            Assert.AreEqual(string.Empty, decoder.Take(new[] { bytes[0] }, 1));
            Assert.AreEqual("é", decoder.Take(new[] { bytes[1] }, 1));
        }

        static OpenAiCompatibleChatProvider Grok(ScriptedTransport transport, string key)
        {
            return new OpenAiCompatibleChatProvider(new OpenAiProviderConfig
            {
                ProviderId = ChatDefaults.GrokId,
                DisplayName = "Grok",
                RequiresKey = true,
                BaseUrl = () => ChatDefaults.BaseUrl,
                Model = () => ChatDefaults.Model,
                TimeoutSeconds = () => 30f,
                ApiKey = () => key,
                Transport = transport
            });
        }

        static List<SseEvent> Parse(params string[] chunks)
        {
            var parser = new SseParser();
            var events = new List<SseEvent>();
            for (var i = 0; i < chunks.Length; i++)
                parser.Push(chunks[i], events);
            parser.Flush(events);
            return events;
        }

        static string Header(ChatHttpRequest request, string name)
        {
            if (request == null)
                return null;
            for (var i = 0; i < request.Headers.Count; i++)
            {
                if (request.Headers[i].Key == name)
                    return request.Headers[i].Value;
            }

            return null;
        }

        sealed class ScriptedTransport : IChatTransport
        {
            public int Calls;
            public ChatHttpRequest Last;
            public ChatHttpResult Result = ChatHttpResult.Ok(200);
            public string[] Chunks = new string[0];

            public System.Threading.Tasks.Task<ChatHttpResult> PostAsync(ChatHttpRequest request, System.Action<string> onBodyChunk, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                if (cancellationToken.IsCancellationRequested)
                    return System.Threading.Tasks.Task.FromResult(ChatHttpResult.Cancel());
                if (!Result.TimedOut && !Result.Cancelled && !Result.NetworkFailed && Result.StatusCode >= 200 && Result.StatusCode < 300 && Chunks != null && onBodyChunk != null)
                {
                    for (var i = 0; i < Chunks.Length; i++)
                        onBodyChunk(Chunks[i]);
                }

                return System.Threading.Tasks.Task.FromResult(Result);
            }
        }

        sealed class StubProvider : IChatProvider
        {
            public StubProvider(string id)
            {
                ProviderId = id;
            }

            public string ProviderId { get; }
            public string DisplayName => ProviderId;
            public bool IsConfigured => true;

            public System.Threading.Tasks.Task<ChatResult> StreamChat(ChatRequest request, System.Action<string> onDelta, CancellationToken cancellationToken)
            {
                return System.Threading.Tasks.Task.FromResult(ChatResult.Success(string.Empty));
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
                return Reverse(plain);
            }

            public bool TryUnprotect(string payload, out string plain)
            {
                plain = Reverse(payload);
                return !string.IsNullOrEmpty(plain);
            }

            static string Reverse(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return string.Empty;
                var chars = value.ToCharArray();
                System.Array.Reverse(chars);
                return new string(chars);
            }
        }
    }
}
