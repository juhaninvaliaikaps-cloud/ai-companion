using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.UI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class MemoryChatProviderTests
    {
        string _dir;
        string _memoryPath;
        MemoryStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ai-companion-memory-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _memoryPath = Path.Combine(_dir, "memory.json");
            _store = MemoryStore.Load(_memoryPath);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void RecallTargets_RecheckThePersonaAndCurrentPrivacyAndDisappearAfterDeletion()
        {
            _store.AddFact("Prefers oat milk", privateMode: true);
            var entry = _store.Entries[0];
            var link = new ChatMemoryLink { id = entry.id, store = "memory.json", privateEntry = false, phrase = "oat milk" };
            Assert.IsNull(MemoryRuntime.ResolveLink(_store, link, false), "stored privacy is authoritative");
            Assert.AreEqual(entry.text, MemoryRuntime.ResolveLink(_store, link, true));
            link.store = "memory-someone-else.json";
            Assert.IsNull(MemoryRuntime.ResolveLink(_store, link, true));
            link.store = "memory.json";
            Assert.IsTrue(_store.Delete(entry.id));
            Assert.IsNull(MemoryRuntime.ResolveLink(_store, link, true));
        }

        [Test]
        public async Task RibbonRecall_OffersOnlyEligibleSourcesAndPersistsAnExactPhraseWithoutSpeakingMetadata()
        {
            _store.AddFact("Likes coffee with oat milk", privateMode: false);
            _store.AddFact("Prefers private almond milk", privateMode: true);
            var inner = new FakeProvider(ChatDefaults.GrokId)
            {
                Deltas = new[] { "I picked up oat milk.", "\n<anim", "ation emotion=\"happy\" memory=\"r1|oat milk\" memory2=\"r2|private almond milk\"/>" }
            };
            var provider = new MemoryChatProvider(inner, _store);
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation) { IncludeMemoryLinks = true };
            var spoken = string.Empty;
            exchange.SpokenDelta = part => spoken += part;

            var result = await exchange.Run(provider, "coffee milk?", "Be kind.", .5f, 128, CancellationToken.None);

            Assert.IsTrue(result.Ok);
            StringAssert.Contains("[r1] Likes coffee with oat milk", inner.LastRequest.SystemPrompt);
            StringAssert.DoesNotContain("private almond milk", inner.LastRequest.SystemPrompt);
            Assert.AreEqual("I picked up oat milk.", result.Text);
            Assert.AreEqual(result.Text, spoken.Trim());
            Assert.AreEqual("oat milk", conversation.Lines[1].MemoryLinks[0].phrase);
            Assert.AreEqual(1, conversation.Lines[1].MemoryLinks.Count);
            StringAssert.DoesNotContain("oat milk", exchange.LastCueTag);

            var restored = new ChatConversation();
            ChatHistoryCodec.ReadInto(ChatHistoryCodec.Write(conversation.Lines), restored);
            Assert.AreEqual(conversation.Lines[1].MemoryLinks[0].id, restored.Lines[1].MemoryLinks[0].id);
            Assert.AreEqual(result.Text, restored.Lines[1].Text);
            Assert.AreEqual(result.Text, restored.BuildRequest("base", .5f, 128).Messages[1].Content);
        }

        [Test]
        public void RecallVerifier_RejectsUnknownAliasesUnrelatedAndAmbiguousPhrases()
        {
            var sources = new ChatMemorySources("memory-happi.json");
            sources.Add("a", "Likes coffee with oat milk", false, false);
            sources.Add("b", "Promised to visit the museum", true, false);
            Assert.IsNull(sources.Verify("oat milk twice, oat milk", "r1|oat milk", null));
            Assert.IsNull(sources.Verify("I remember your museum", "r1|your museum", "r9|museum"));
            Assert.IsNull(sources.Verify("an oatmeal cookie", "r1|oat", null));
            var links = sources.Verify("I brought oat milk to the museum.", "r1|oat milk", "r2|museum");
            Assert.AreEqual(2, links.Length);
            Assert.IsTrue(links[1].timeline);
        }

        [Test]
        public void Enrich_AppendsRetrievedMemoriesToThePrompt()
        {
            _store.AddFact("Likes coffee with oat milk", privateMode: false);
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.GrokId), _store);

            var enriched = provider.Enrich(Request("Do you remember my coffee order?", "You are a companion."));

            StringAssert.Contains("You are a companion.", enriched.SystemPrompt);
            StringAssert.Contains(MemoryPrompt.Header, enriched.SystemPrompt);
            StringAssert.Contains("Likes coffee with oat milk", enriched.SystemPrompt);
        }

        [Test]
        public void Enrich_KeepsPrivateMemoriesAwayFromTheCloudProvider()
        {
            _store.AddFact("Secret private cat fact", privateMode: true);
            var cloud = new MemoryChatProvider(new FakeProvider(ChatDefaults.GrokId), _store);
            var local = new MemoryChatProvider(new FakeProvider(ChatDefaults.LocalId), _store);
            var request = Request("tell me about the private cat fact", string.Empty);

            var cloudRequest = cloud.Enrich(request);
            var localRequest = local.Enrich(request);

            Assert.IsFalse(cloudRequest.SystemPrompt.Contains("Secret private cat fact"));
            StringAssert.Contains("Secret private cat fact", localRequest.SystemPrompt);
        }

        [Test]
        public void Enrich_LeavesMessagesAndSamplingUntouched()
        {
            _store.AddFact("Likes coffee", privateMode: false);
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.GrokId), _store);
            var request = Request("coffee?", "prompt");
            request.Temperature = 0.25f;
            request.MaxTokens = 123;
            request.Model = "grok-test";

            var enriched = provider.Enrich(request);

            Assert.AreEqual(request.Messages.Count, enriched.Messages.Count);
            Assert.AreEqual(request.Messages[0].Content, enriched.Messages[0].Content);
            Assert.AreEqual(0.25f, enriched.Temperature);
            Assert.AreEqual(123, enriched.MaxTokens);
            Assert.AreEqual("grok-test", enriched.Model);
        }

        [Test]
        public void Enrich_KeepsEveryRequestFieldItDoesNotChange()
        {
            _store.AddFact("Likes coffee", privateMode: false);
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.GrokId), _store);
            var image = new ChatImageAttachment
            {
                JpegBytes = new byte[] { 1, 2, 3 },
                Width = 64,
                Height = 32,
                Generation = 7,
            };
            var request = Request("coffee?", "prompt");
            request.Model = "grok-test";
            request.Temperature = 0.25f;
            request.MaxTokens = 123;
            request.TimeoutSeconds = 37f;
            request.BlockCloud = true;
            request.NoMemory = false;
            request.Image = image;

            var enriched = provider.Enrich(request);

            Assert.AreSame(request.Messages, enriched.Messages);
            Assert.AreSame(image, enriched.Image);
            Assert.AreEqual("grok-test", enriched.Model);
            Assert.AreEqual(0.25f, enriched.Temperature);
            Assert.AreEqual(123, enriched.MaxTokens);
            Assert.AreEqual(37f, enriched.TimeoutSeconds);
            Assert.IsTrue(enriched.BlockCloud);
            Assert.IsFalse(enriched.NoMemory);
        }

        [Test]
        public void Enrich_WithoutMatch_ReturnsTheSameRequest()
        {
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.GrokId), _store);
            var request = Request("nothing remembered here", "prompt");

            Assert.AreSame(request, provider.Enrich(request));
        }

        [Test]
        public void StreamChat_SendsTheEnrichedRequestToTheInnerProvider()
        {
            _store.AddFact("Has a dog named Pixel", privateMode: false);
            var fake = new FakeProvider(ChatDefaults.GrokId);
            var provider = new MemoryChatProvider(fake, _store);
            var pieces = new List<string>();

            var result = provider.StreamChat(Request("what is my dog called?", "base"), pieces.Add, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            Assert.IsNotNull(fake.LastRequest);
            StringAssert.Contains("Has a dog named Pixel", fake.LastRequest.SystemPrompt);
            Assert.AreEqual(1, pieces.Count);
        }

        [Test]
        public void Enrich_ReadsTheStoreAgainOnTheNextRequest()
        {
            var first = MemoryStore.Load(Path.Combine(_dir, "first.json"));
            first.AddFact("Has a dog named Pixel", privateMode: false);
            var second = MemoryStore.Load(Path.Combine(_dir, "second.json"));
            var current = first;
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.GrokId), () => current, () => null);

            var remembered = provider.Enrich(Request("what is my dog called?", "base"));
            StringAssert.Contains("Has a dog named Pixel", remembered.SystemPrompt);

            current = second;
            var wiped = provider.Enrich(Request("what is my dog called?", "base"));
            Assert.AreEqual("base", wiped.SystemPrompt);
        }

        [Test]
        public void Decorator_WrapsEveryProviderItIsGiven()
        {
            var decorator = new MemoryChatDecorator(_store);
            var inner = new FakeProvider(ChatDefaults.LocalId);

            var wrapped = decorator.Wrap(inner);

            Assert.IsInstanceOf<MemoryChatProvider>(wrapped);
            Assert.AreEqual(ChatDefaults.LocalId, wrapped.ProviderId);
            Assert.IsNull(decorator.Wrap(null));
        }

        [Test]
        public void Personality_SurvivesAReload()
        {
            var path = Path.Combine(_dir, "personality.json");
            var personality = PersonalityStore.Load(path);
            personality.SetPersona("Leia", 0.75f);
            personality.SetMood("cheerful");

            var reloaded = PersonalityStore.Load(path);

            Assert.AreEqual("Leia", reloaded.Current.personaName);
            Assert.AreEqual("cheerful", reloaded.Current.mood);
            Assert.AreEqual(0.75f, reloaded.Current.warmth, 0.001f);
            Assert.IsFalse(string.IsNullOrEmpty(reloaded.Current.updatedUtc));
        }

        [Test]
        public void Enrich_AddsThePersistedPersona()
        {
            var personality = PersonalityStore.Load(Path.Combine(_dir, "personality.json"));
            personality.SetPersona("Leia", 0.5f);
            personality.SetMood("curious");
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.GrokId), _store, personality);

            var enriched = provider.Enrich(Request("hello", "base prompt"));

            StringAssert.Contains("Leia", enriched.SystemPrompt);
            StringAssert.Contains("curious", enriched.SystemPrompt);
        }

        [Test]
        public void Enrich_AppendsRelevantTimelineEntriesAndMarksThem()
        {
            var timeline = RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_memoryPath));
            timeline.AddSession(new[] { "The user promised to visit on Sunday." }, false, DateTime.UtcNow);
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.LocalId), _store);

            var enriched = provider.Enrich(Request("Did I promise to visit on Sunday?", "base"));

            StringAssert.Contains(MemoryPrompt.TimelineHeader, enriched.SystemPrompt);
            StringAssert.Contains("The user promised to visit on Sunday.", enriched.SystemPrompt);
            StringAssert.Contains("[promise]", enriched.SystemPrompt);
            Assert.AreEqual(1, RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_memoryPath)).Page(1)[0].recallCount);
        }

        [Test]
        public void Enrich_KeepsAJustRaisedEntryWhileTheTalkIsAboutIt_TaggedNotToRepeat()
        {
            // JUH-141: asked again about the same thing, she still knows it (no amnesia), but the
            // entry is tagged so she does not bring it up again unprompted (no verbal tic).
            var timeline = RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_memoryPath));
            timeline.AddSession(new[] { "The user promised to visit on Sunday." }, false, DateTime.UtcNow);
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.LocalId), _store);
            var request = Request("Did I promise to visit on Sunday?", "base");

            var first = provider.Enrich(request);
            var second = provider.Enrich(request);

            StringAssert.Contains("The user promised to visit on Sunday.", first.SystemPrompt);
            Assert.IsFalse(first.SystemPrompt.Contains(MemoryPrompt.AlreadyRaisedNote));
            StringAssert.Contains("The user promised to visit on Sunday.", second.SystemPrompt);
            StringAssert.Contains(MemoryPrompt.AlreadyRaisedNote, second.SystemPrompt);
        }

        [Test]
        public void Enrich_DoesNotBringBackAJustRaisedEntryOnAPassingWord()
        {
            var timeline = RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_memoryPath));
            timeline.AddSession(new[] { "The user promised to visit on Sunday." }, false, DateTime.UtcNow);
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.LocalId), _store);

            provider.Enrich(Request("Did I promise to visit on Sunday?", "base"));
            var passing = provider.Enrich(Request("What a lovely Sunday", "base"));

            Assert.IsFalse(passing.SystemPrompt.Contains(MemoryPrompt.TimelineHeader), "one shared word is not the same topic");
        }

        [Test]
        public void Enrich_UsesTheMessageBeforeForAFollowUp()
        {
            var timeline = RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_memoryPath));
            timeline.AddSession(new[] { "The user planned a trip to Lapland in March." }, false, DateTime.UtcNow);
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.LocalId), _store);
            var request = new ChatRequest
            {
                SystemPrompt = "base",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, "I can't wait for Lapland"),
                    new ChatMessage(ChatRole.Assistant, "Me neither!"),
                    new ChatMessage(ChatRole.User, "Do you remember what I told you?"),
                },
            };

            StringAssert.Contains("Lapland", provider.Enrich(request).SystemPrompt);
        }

        [Test]
        public void Enrich_KeepsPrivateTimelineEntriesAwayFromTheCloud()
        {
            var timeline = RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_memoryPath));
            timeline.AddSession(new[] { "The user's private family story is important." }, true, DateTime.UtcNow);
            var cloud = new MemoryChatProvider(new FakeProvider(ChatDefaults.GrokId), _store);
            var local = new MemoryChatProvider(new FakeProvider(ChatDefaults.LocalId), _store);
            var request = Request("Tell me the private family story", "base");

            Assert.IsFalse(cloud.Enrich(request).SystemPrompt.Contains("private family story"));
            StringAssert.Contains("private family story", local.Enrich(request).SystemPrompt);
        }

        [Test]
        public void Enrich_LeavesTheTimelineOutOfTheFactExtraction()
        {
            // JUH-134 review, bug 1: the extractor's "user" message is the whole exchange.
            var timeline = RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_memoryPath));
            timeline.AddSession(new[] { "The user promised to visit on Sunday." }, false, DateTime.UtcNow);
            var provider = new MemoryChatProvider(new FakeProvider(ChatDefaults.LocalId), _store);
            var exchange = MemoryFactExtractor.BuildExchangeText("Did I promise to visit on Sunday?", "You promised to visit on Sunday!");

            var enriched = provider.Enrich(Request(exchange, MemoryFactExtractor.SystemPrompt));

            Assert.IsFalse(enriched.SystemPrompt.Contains(MemoryPrompt.TimelineHeader), "no shared history in the extractor's prompt");
            Assert.AreEqual(0, RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_memoryPath)).Page(1)[0].recallCount,
                "the extraction does not restart the recall cooldown");
        }

        static ChatRequest Request(string userText, string systemPrompt)
        {
            return new ChatRequest
            {
                SystemPrompt = systemPrompt,
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, userText) },
            };
        }

        sealed class FakeProvider : IChatProvider
        {
            public string[] Deltas = { "ok" };
            public FakeProvider(string providerId)
            {
                ProviderId = providerId;
            }

            public string ProviderId { get; }

            public string DisplayName
            {
                get { return "Fake"; }
            }

            public bool IsConfigured
            {
                get { return true; }
            }

            public ChatRequest LastRequest { get; private set; }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                LastRequest = request;
                if (onDelta != null)
                    foreach (var delta in Deltas)
                        onDelta(delta);
                return Task.FromResult(ChatResult.Success(string.Concat(Deltas)));
            }
        }
    }
}
