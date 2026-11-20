using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-141: the smarter memory search. Meaning vectors from the local nomic-embed-text find
    /// "my old man" → "his father"; with the switch off, or the model missing, it is the word
    /// search, unchanged. Never anything but Ollama on this PC.
    /// </summary>
    public sealed class SmarterMemorySearchTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);
        const string LocalOllama = "http://127.0.0.1:11434/v1";

        string _dir;

        [SetUp]
        public void SetUp()
        {
            MemoryEmbeddings.Reset();
            _dir = Path.Combine(Path.GetTempPath(), "happi-embeddings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            MemoryEmbeddings.Reset();
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        static TimelineEntry Entry(string id, string summary, int importance = 3)
        {
            return new TimelineEntry
            {
                id = id, summary = summary, title = summary, kind = MemoryDurability.Event, importance = importance,
                confidence = 60, evidenceCount = 1, source = "session",
                happenedAtUtc = Now.AddDays(-3).ToString("o"), createdAtUtc = Now.AddDays(-3).ToString("o"),
            };
        }

        static List<TimelineEntry> Two()
        {
            return new List<TimelineEntry>
            {
                Entry("father", "The user told her his father is in hospital after a heart attack.", 5),
                Entry("pickles", "The user hates pickles.", 2),
            };
        }

        static Func<TimelineEntry, float> Scores(Dictionary<string, float> byId)
        {
            return entry =>
            {
                float value;
                return byId.TryGetValue(entry.id, out value) ? value : float.NaN;
            };
        }

        // ---- The ranking ---------------------------------------------------------------------

        [Test]
        public void ACloseMeaning_IsFound_WithoutASharedWord()
        {
            const string question = "How is my old man holding up?";
            Assert.AreEqual(0, TimelineSearch.Rank(Two(), question, "", true, Now).Count, "words alone find nothing");

            var hits = TimelineSearch.Rank(Two(), question, "", true, Now, 3, null,
                Scores(new Dictionary<string, float> { { "father", 0.80f }, { "pickles", 0.30f } }));

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual("father", hits[0].Entry.id);
        }

        [Test]
        public void AWeakMeaning_AloneFindsNothing()
        {
            var hits = TimelineSearch.Rank(Two(), "How is my old man holding up?", "", true, Now, 3, null,
                Scores(new Dictionary<string, float> { { "father", 0.62f } }));

            Assert.AreEqual(0, hits.Count, "below SemanticOnly a meaning match needs a word too");
        }

        [Test]
        public void Meaning_BreaksATieBetweenWordMatches()
        {
            var timeline = new List<TimelineEntry>
            {
                Entry("work", "The user had a long day at work."),
                Entry("burnout", "The user said work is draining him and he cannot sleep."),
            };
            var hits = TimelineSearch.Rank(timeline, "Work is killing me lately", "", true, Now, 3, null,
                Scores(new Dictionary<string, float> { { "work", 0.58f }, { "burnout", 0.82f } }));

            Assert.AreEqual("burnout", hits[0].Entry.id);
        }

        [Test]
        public void UnknownMeaning_IsTheWordSearchUnchanged()
        {
            var words = TimelineSearch.Rank(Two(), "Is my father still in the hospital?", "", true, Now);
            var withNaN = TimelineSearch.Rank(Two(), "Is my father still in the hospital?", "", true, Now, 3, null, e => float.NaN);

            Assert.AreEqual(words.Count, withNaN.Count);
            Assert.AreEqual(words[0].Entry.id, withNaN[0].Entry.id);
            Assert.AreEqual(words[0].Score, withNaN[0].Score, 1e-5f);
        }

        [Test]
        public void AJustRaisedEntry_StaysWhileTheMeaningIsClose()
        {
            var timeline = Two();
            timeline[0].lastRecalledAtUtc = Now.AddMinutes(-2).ToString("o");

            Assert.AreEqual(0, TimelineSearch.Rank(timeline, "The hospital food here is awful", "", true, Now).Count);
            var hits = TimelineSearch.Rank(timeline, "The hospital food here is awful", "", true, Now, 3, null,
                Scores(new Dictionary<string, float> { { "father", 0.85f } }));
            Assert.AreEqual("father", hits[0].Entry.id);
            Assert.IsTrue(hits[0].AlreadyRaised);
        }

        // ---- The vectors ---------------------------------------------------------------------

        [Test]
        public void TheIndex_ForgetsAVectorWhenTheSentenceChanged()
        {
            var index = new EmbeddingIndex();
            var entry = Entry("sister", "The user's sister lives in Tampere.");
            index.Put(entry, new[] { 3f, 4f });

            float[] vector;
            Assert.IsTrue(index.TryGet(entry, out vector));
            Assert.AreEqual(0.6f, vector[0], 1e-5f, "stored normalized");
            entry.summary = "The user's sister moved to Oulu.";
            Assert.IsFalse(index.TryGet(entry, out vector), "a corrected entry is embedded again");
            Assert.AreEqual(1, index.Missing(new[] { entry }, 10).Count);
        }

        [Test]
        public void Missing_IsNewestFirst_AndSkipsClosedEntries()
        {
            var old = Entry("old", "An old memory.");
            var closed = Entry("closed", "A kept promise.");
            closed.resolved = true;
            var fresh = Entry("new", "A new memory.");

            var missing = new EmbeddingIndex().Missing(new[] { old, closed, fresh }, 10);

            Assert.AreEqual(2, missing.Count);
            Assert.AreEqual("new", missing[0].id);
        }

        [Test]
        public void TheTexts_CarryNomicsTaskPrefixes()
        {
            StringAssert.StartsWith("search_document: ", EmbeddingIndex.DocumentText(Entry("a", "The user hates pickles.")));
            Assert.AreEqual("search_query: I can't wait for Lapland what did I tell you?",
                EmbeddingIndex.QueryText("what did I tell you?", "I can't wait for Lapland"));
            Assert.AreEqual(0f, EmbeddingIndex.Cosine(new[] { 1f, 0f }, new[] { 0f, 1f }), 1e-6f);
            Assert.IsTrue(float.IsNaN(EmbeddingIndex.Cosine(new[] { 1f }, new[] { 1f, 0f })));
        }

        // ---- The local model -----------------------------------------------------------------

        [TestCase("http://127.0.0.1:11434/v1", true)]
        [TestCase("http://localhost:11434", true)]
        [TestCase("http://127.0.0.1:1234/v1", false)]
        [TestCase("http://192.168.1.20:11434", false)]
        [TestCase("https://api.x.ai/v1", false)]
        [TestCase("", false)]
        public void OnlyOllamaOnThisPc_IsUsed(string baseUrl, bool expected)
        {
            Assert.AreEqual(expected, OllamaEmbedder.IsLocalOllama(baseUrl));
        }

        [Test]
        public void TheRequest_IsOllamasEmbedEndpoint_WithNomic()
        {
            var transport = new FakeOllama(text => new[] { 1f, 0f });
            var embedder = new OllamaEmbedder(transport, () => LocalOllama);

            var vectors = embedder.EmbedAsync(new[] { "search_query: hello", "search_document: hi" }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(2, vectors.Length);
            Assert.AreEqual("http://127.0.0.1:11434/api/embed", transport.LastUrl);
            StringAssert.Contains("\"model\":\"nomic-embed-text\"", transport.LastBody);
            StringAssert.Contains("\"input\":[\"search_query: hello\",\"search_document: hi\"]", transport.LastBody);
        }

        [Test]
        public void ACloudAddress_IsNeverCalled()
        {
            var transport = new FakeOllama(text => new[] { 1f });
            var embedder = new OllamaEmbedder(transport, () => "https://api.x.ai/v1");

            Assert.IsNull(embedder.EmbedAsync(new[] { "search_query: secret" }, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(0, transport.Calls);
        }

        [Test]
        public void AMissingModel_SaysHowToInstallIt_AndIsNotAskedAgainAtOnce()
        {
            var transport = new FakeOllama(text => new[] { 1f }) { Status = 404 };
            var embedder = new OllamaEmbedder(transport, () => LocalOllama);

            Assert.IsNull(embedder.EmbedAsync(new[] { "search_query: hi" }, CancellationToken.None).GetAwaiter().GetResult());
            StringAssert.Contains(OllamaEmbedder.PullHint, embedder.LastProblem);
            Assert.IsFalse(embedder.Ready(DateTime.UtcNow));
            Assert.IsNull(embedder.EmbedAsync(new[] { "search_query: hi" }, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(1, transport.Calls);
        }

        [Test]
        public void Parse_ReadsOllamasReply_AndRefusesAnythingElse()
        {
            var vectors = OllamaEmbedder.Parse("{\"model\":\"nomic-embed-text\",\"embeddings\":[[0.1,-0.2,3e-2],[1,0,0]]}");
            Assert.AreEqual(2, vectors.Length);
            Assert.AreEqual(-0.2f, vectors[0][1], 1e-6f);
            Assert.AreEqual(0.03f, vectors[0][2], 1e-6f);
            Assert.IsNull(OllamaEmbedder.Parse("{\"error\":\"model not found\"}"));
            Assert.IsNull(OllamaEmbedder.Parse("{\"embeddings\":[[0.1,0.2],[0.3]]}"), "lengths differ");
            Assert.IsNull(OllamaEmbedder.Parse(""));
        }

        // ---- End to end ----------------------------------------------------------------------

        [Test]
        public void AChatRequest_FindsTheFather_ByMeaning_WhenTheSwitchIsOn()
        {
            var store = MemoryStore.Load(Path.Combine(_dir, "memory.json"));
            var timeline = RelationshipTimeline.ForStore(store);
            timeline.AddSession(new[] { "The user told her his father is in hospital after a heart attack." }, false, DateTime.UtcNow);
            timeline.AddSession(new[] { "The user planned a trip to Lapland in March." }, false, DateTime.UtcNow);
            // A tiny pretend meaning space: [family/health, travel].
            MemoryEmbeddings.UseEmbedder(new OllamaEmbedder(new FakeOllama(text =>
                text.Contains("father") || text.Contains("old man") ? new[] { 1f, 0.1f }
                : text.Contains("Lapland") ? new[] { 0.1f, 1f } : new[] { 0.5f, 0.5f }), () => LocalOllama));
            var provider = new MemoryChatProvider(new Echo(), store);
            var request = new ChatRequest
            {
                SystemPrompt = "base",
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "How is my old man holding up?") },
            };

            var off = provider.EnrichAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(off.SystemPrompt.Contains("father"), "switch off: words only");

            MemoryEmbeddings.Configure(true);
            var on = provider.EnrichAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            StringAssert.Contains("his father is in hospital", on.SystemPrompt);
            Assert.IsFalse(on.SystemPrompt.Contains("Lapland"));
        }

        [Test]
        public void TheModelMissing_LeavesTheWordSearch()
        {
            var store = MemoryStore.Load(Path.Combine(_dir, "memory.json"));
            RelationshipTimeline.ForStore(store).AddSession(new[] { "The user told her his father is in hospital after a heart attack." }, false, DateTime.UtcNow);
            MemoryEmbeddings.UseEmbedder(new OllamaEmbedder(new FakeOllama(text => new[] { 1f }) { Status = 404 }, () => LocalOllama));
            MemoryEmbeddings.Configure(true);
            var provider = new MemoryChatProvider(new Echo(), store);
            var request = new ChatRequest
            {
                SystemPrompt = "base",
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "Is my father still in the hospital?") },
            };

            StringAssert.Contains("his father is in hospital", provider.EnrichAsync(request, CancellationToken.None).GetAwaiter().GetResult().SystemPrompt);
        }

        // ---- Fakes ---------------------------------------------------------------------------

        sealed class FakeOllama : IChatTransport
        {
            static readonly Regex Input = new Regex("\"(search_(?:query|document): (?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);
            readonly Func<string, float[]> _vector;

            public FakeOllama(Func<string, float[]> vector)
            {
                _vector = vector;
            }

            public int Status = 200;
            public int Calls;
            public string LastUrl;
            public string LastBody;

            public Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken)
            {
                Calls++;
                LastUrl = request.Url;
                LastBody = request.Body;
                if (Status != 200)
                    return Task.FromResult(ChatHttpResult.Http(Status, "{\"error\":\"model not found\"}"));

                var json = new StringBuilder("{\"embeddings\":[");
                var first = true;
                foreach (Match match in Input.Matches(request.Body))
                {
                    if (!first)
                        json.Append(',');
                    first = false;
                    var vector = _vector(match.Groups[1].Value);
                    json.Append('[');
                    for (var i = 0; i < vector.Length; i++)
                        json.Append(i > 0 ? "," : string.Empty).Append(vector[i].ToString("R", CultureInfo.InvariantCulture));
                    json.Append(']');
                }

                json.Append("]}");
                onBodyChunk?.Invoke(json.ToString());
                return Task.FromResult(ChatHttpResult.Ok(200));
            }
        }

        sealed class Echo : IChatProvider
        {
            public string ProviderId { get { return ChatDefaults.LocalId; } }
            public string DisplayName { get { return "Echo"; } }
            public bool IsConfigured { get { return true; } }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                return Task.FromResult(ChatResult.Success("ok"));
            }
        }
    }
}
