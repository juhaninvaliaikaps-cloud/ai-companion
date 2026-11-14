using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-118 sections 2 and 3: the wiring around <see cref="WorldMemory"/>.</summary>
    public sealed class WorldWiringTests
    {
        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-wiring-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "memory.json");
            WorldPromptNow.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            WorldPromptNow.Reset();
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        sealed class FakeProvider : IChatProvider
        {
            public string Id = ChatDefaults.LocalId;
            public bool Configured = true;
            public string Reply = "- The user told Mira about their new job.\n2. Mira promised to show the user her portfolio.";
            public bool Hang;
            public int Calls;
            public ChatRequest Last;

            public string ProviderId { get { return Id; } }
            public string DisplayName { get { return Id; } }
            public bool IsConfigured { get { return Configured; } }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                if (Hang)
                    return new TaskCompletionSource<ChatResult>().Task;
                return Task.FromResult(ChatResult.Success(Reply));
            }
        }

        static CharacterSheet Sheet(bool sharedHistory)
        {
            var sheet = new CharacterSheet();
            sheet.source = CharacterSheet.SourceModel;
            sheet.promptHash = CharacterSheet.HashPrompt("You are Mira.");
            sheet.relationship.role = sharedHistory ? RelationshipRoles.Family : RelationshipRoles.Stranger;
            sheet.relationship.familiarity = sharedHistory ? 80f : 0f;
            sheet.relationship.sharedHistory = sharedHistory;
            sheet.world.backstory.Add("She studies graphic design.");
            sheet.world.backstory.Add("She steals the user's snacks.");
            sheet.world.backstory.Add("Your old room is next to hers.");
            sheet.world.lastWeek.Add(new SheetWeekEntry { daysAgo = 2, text = "She failed a quiz." });
            sheet.world.lastWeek.Add(new SheetWeekEntry { daysAgo = 3, text = "She argued with the user about a game." });
            sheet.world.openThreads.Add(new SheetThread { text = "Portfolio review on Friday.", goodOutcome = "It went well.", badOutcome = "It went badly." });
            sheet.world.openThreads.Add(new SheetThread { text = "A birthday gift.", goodOutcome = "The user loves it.", badOutcome = "She finds nothing." });
            sheet.world.rightNow = "Sketching at her desk.";
            return sheet;
        }

        // ---- The shared-history guard ------------------------------------------------------------

        [Test]
        public void MentionsUser_WholeWordsOnly()
        {
            foreach (var text in new[] { "She steals the user's snacks.", "Helped the User move.", "Your room is next to hers.", "She misses you." })
                Assert.IsTrue(WorldGuard.MentionsUser(text), text);
            foreach (var text in new[] { "", "She loves youth hostels.", "A superuser account.", "She studies design.", "Library users annoy her." })
                Assert.IsFalse(WorldGuard.MentionsUser(text), text);
        }

        [Test]
        public void Guard_WithoutASharedPast_DropsEveryLineAboutTheUser()
        {
            var sheet = Sheet(false);
            var dropped = new List<string>();
            var safe = WorldGuard.ForSeeding(sheet, dropped);

            Assert.AreEqual(1, safe.world.backstory.Count);
            Assert.AreEqual(1, safe.world.lastWeek.Count);
            Assert.AreEqual(1, safe.world.openThreads.Count, "a thread whose outcome mentions the user goes too");
            Assert.AreEqual(4, dropped.Count);
            Assert.AreEqual(sheet.promptHash, safe.promptHash, "the copy keeps the hash, so it can be seeded");
            Assert.AreEqual(3, sheet.world.backstory.Count, "the cached sheet is not changed");

            var store = MemoryStore.Load(_path);
            Assert.AreEqual(3, WorldMemory.Seed(store, safe, DateTime.UtcNow, false));
            foreach (var entry in store.Entries)
                Assert.IsFalse(WorldGuard.MentionsUser(entry.text), entry.text);
            Assert.IsFalse(WorldMemory.NeedsSeeding(store, sheet), "seeded once for this sheet");
        }

        [Test]
        public void Guard_WithASharedPast_KeepsEverything()
        {
            var safe = WorldGuard.ForSeeding(Sheet(true));
            Assert.AreEqual(3, safe.world.backstory.Count);
            Assert.AreEqual(2, safe.world.lastWeek.Count);
            Assert.AreEqual(2, safe.world.openThreads.Count);
            Assert.IsNull(WorldGuard.ForSeeding(null));
        }

        [Test]
        public void FirstScene_OnlyFromAModelSheet_AndNotWithTheUserInIt()
        {
            StringAssert.Contains("Sketching at her desk.", WorldGuard.FirstScene(Sheet(false)));

            var fallback = Sheet(false);
            fallback.source = CharacterSheet.SourceDefault;
            Assert.AreEqual(string.Empty, WorldGuard.FirstScene(fallback));

            var withUser = Sheet(false);
            withUser.world.rightNow = "Playing a game with the user.";
            Assert.AreEqual(string.Empty, WorldGuard.FirstScene(withUser));
            withUser.relationship.sharedHistory = true;
            StringAssert.Contains("Playing a game", WorldGuard.FirstScene(withUser), "with a shared past the user may be in it");
            StringAssert.DoesNotContain("first time", WorldGuard.FirstScene(withUser), "an old friend has talked to the user before");

            var noStop = Sheet(false);
            noStop.world.rightNow = "Reading by the window";
            StringAssert.Contains("Reading by the window. Start from there", WorldGuard.FirstScene(noStop));
            Assert.AreEqual(string.Empty, WorldGuard.FirstScene(null));
        }

        [Test]
        public void Guard_EndsLinesWithAFullStop_SoJoinedTextReads()
        {
            Assert.AreEqual("She reads.", WorldGuard.Sentence(" She reads "));
            Assert.AreEqual("Is it Friday?", WorldGuard.Sentence("Is it Friday?"));
            Assert.AreEqual("It went well!", WorldGuard.Sentence("It went well!"));
            Assert.AreEqual(string.Empty, WorldGuard.Sentence("  "));

            var sheet = Sheet(true);
            sheet.world.openThreads[0].text = "Waiting for the review";
            sheet.world.openThreads[0].badOutcome = "It went badly";
            var store = MemoryStore.Load(_path);
            WorldMemory.Seed(store, WorldGuard.ForSeeding(sheet), DateTime.UtcNow, false);
            var away = WorldMemory.ResolveThread(store, 5 * 3600, 0.9, DateTime.UtcNow, true);
            Assert.AreEqual("Waiting for the review. It went badly.", away.Text);
        }

        // ---- The prompt hand-off -----------------------------------------------------------------

        [Test]
        public void WorldPrompt_SceneOnlyOnTheFirstMessage_AwayOnce()
        {
            Assert.AreEqual("Persona.", WorldPromptNow.Append("Persona.\n", true), "nothing set: unchanged");

            WorldPromptNow.SetFirstScene("SCENE");
            WorldPromptNow.SetAway("AWAY");
            Assert.AreEqual("Persona.\n\nSCENE\n\nAWAY", WorldPromptNow.Append("Persona.", true));
            Assert.AreEqual("Persona.\n\nAWAY", WorldPromptNow.Append("Persona.", false), "no scene after the first message");
            Assert.AreEqual("Persona.\n\nAWAY", WorldPromptNow.Append("Persona.", false), "the away line stays until a reply finished");

            WorldPromptNow.ReplyFinished();
            Assert.AreEqual("Persona.", WorldPromptNow.Append("Persona.", false), "used once");
            Assert.AreEqual("Persona.\n\nSCENE", WorldPromptNow.Append("Persona.", true), "the scene waits for an empty conversation");
            Assert.AreEqual("SCENE", WorldPromptNow.Append(null, true));

            WorldPromptNow.SetHerName(" Mira ");
            Assert.AreEqual("Mira", WorldPromptNow.HerName);
            WorldPromptNow.Reset();
            Assert.AreEqual(string.Empty, WorldPromptNow.FirstScene + WorldPromptNow.Away + WorldPromptNow.HerName);
        }

        // ---- Memories in the prompt --------------------------------------------------------------

        [Test]
        public void Enrich_DatesMemories_CountsRecalls_AndPromotesAfterTwo()
        {
            var store = MemoryStore.Load(_path);
            var entry = WorldMemory.AddShortTerm(store, "The user adopted a cat named Miso.", DateTime.UtcNow.AddDays(-3), false);
            var provider = new MemoryChatProvider(new FakeProvider(), store);
            var request = new ChatRequest { SystemPrompt = "Persona." };
            request.Messages.Add(new ChatMessage(ChatRole.User, "How is my cat Miso?"));

            var enriched = provider.Enrich(request);
            StringAssert.Contains("(3 days ago) The user adopted a cat named Miso.", enriched.SystemPrompt);
            Assert.AreEqual(1, entry.recalls);
            Assert.IsTrue(entry.IsShortTerm);

            provider.Enrich(request);
            Assert.AreEqual(2, entry.recalls);
            Assert.IsFalse(entry.IsShortTerm, "came up twice: long-term now");
        }

        [Test]
        public void Enrich_TheExtractionAndSessionRequests_DoNotCountAsRecalls()
        {
            var store = MemoryStore.Load(_path);
            var entry = WorldMemory.AddShortTerm(store, "The user adopted a cat named Miso.", DateTime.UtcNow, false);
            var provider = new MemoryChatProvider(new FakeProvider(), store);

            var extraction = new ChatRequest { SystemPrompt = MemoryFactExtractor.SystemPrompt };
            extraction.Messages.Add(new ChatMessage(ChatRole.User, "User: my cat Miso is asleep."));
            provider.Enrich(extraction);
            Assert.AreEqual(0, entry.recalls, "fact extraction is not her recalling it");

            var session = SessionMemories.BuildRequest("User: my cat Miso is asleep.", "Mira", false, false);
            Assert.AreSame(session, provider.Enrich(session), "the session request gets no memory block");
            Assert.AreEqual(0, entry.recalls);
        }

        // ---- Session memories --------------------------------------------------------------------

        static List<string> Capture(FakeProvider provider, MemoryStore store, int userMessages, bool keepsLocal, float timeout = 30f)
        {
            return Task.Run(() => SessionMemories.CaptureAsync(
                provider, "User: hi\nMira: hey", "Mira", userMessages, keepsLocal, false, store, timeout, CancellationToken.None)).Result.Memories;
        }

        [Test]
        public void Session_StoresOneToThreeShortTermMemories()
        {
            var store = MemoryStore.Load(_path);
            var provider = new FakeProvider();
            var stored = Capture(provider, store, 4, false);

            Assert.AreEqual(1, provider.Calls, "one request per session");
            Assert.AreEqual(2, stored.Count);
            Assert.AreEqual("The user told Mira about their new job.", stored[0]);
            Assert.AreEqual("Mira promised to show the user her portfolio.", stored[1]);
            Assert.AreEqual(2, store.Count);
            foreach (var entry in store.Entries)
            {
                Assert.IsTrue(entry.IsShortTerm);
                Assert.AreEqual(MemoryOrigins.Lived, entry.origin);
                Assert.IsFalse(entry.isPrivate);
            }

            StringAssert.Contains(IntimateBoundaries.ConsentRule, provider.Last.SystemPrompt, "IntimateBoundaries stays in every prompt");
            Assert.IsTrue(provider.Last.NoMemory);
            StringAssert.Contains("User: hi", provider.Last.Messages[0].Content);
        }

        [Test]
        public void Session_ShortSessionsAreSkipped()
        {
            var store = MemoryStore.Load(_path);
            var provider = new FakeProvider();
            Assert.AreEqual(0, Capture(provider, store, 3, false).Count);
            Assert.AreEqual(0, provider.Calls, "fewer than 4 user messages: no request at all");
            Assert.IsFalse(SessionMemories.Qualifies(3));
            Assert.IsTrue(SessionMemories.Qualifies(4));
        }

        [Test]
        public void Session_Private_NeverCallsACloudModel_AndStoresPrivately()
        {
            var store = MemoryStore.Load(_path);
            var cloud = new FakeProvider { Id = ChatDefaults.GrokId };
            Assert.AreEqual(0, Capture(cloud, store, 6, true).Count);
            Assert.AreEqual(0, cloud.Calls, "nothing may be sent to the cloud for a private session");
            Assert.AreEqual(0, store.Count);

            var local = new FakeProvider();
            Assert.AreEqual(2, Capture(local, store, 6, true).Count);
            Assert.IsTrue(local.Last.BlockCloud);
            foreach (var entry in store.Entries)
                Assert.IsTrue(entry.isPrivate, "a private session's memories never go into a cloud prompt");
        }

        [Test]
        public void Session_ThatWasPrivate_StaysLocalAndPrivate_AfterPrivateModeIsTurnedOff()
        {
            // 1. Messages sent in Private mode (or with the 18+ gate open). 2. Private mode is turned
            // off, maybe another message. 3. The session ends (quit, or 10 minutes idle).
            var privacy = new SessionPrivacy();
            privacy.Note(true);
            privacy.Note(false);
            var keepsLocal = privacy.KeepsLocal(false);
            Assert.IsTrue(keepsLocal, "the session was private at one point, so it is private at its end");

            // The routing at session end picks the local model, not the cloud chat provider...
            Assert.AreEqual(ChatDefaults.LocalId, PrivateChatRouting.ProviderId(keepsLocal, ChatDefaults.GrokId, PrivateEndpointTypes.Local));
            Assert.AreEqual(ChatDefaults.CustomId, PrivateChatRouting.ProviderId(keepsLocal, ChatDefaults.GrokId, PrivateEndpointTypes.Custom));

            // ...a cloud provider would be refused before anything is sent...
            var store = MemoryStore.Load(_path);
            var cloud = new FakeProvider { Id = ChatDefaults.GrokId };
            Assert.AreEqual(0, Capture(cloud, store, 6, keepsLocal).Count);
            Assert.AreEqual(0, cloud.Calls, "the private transcript never reaches a cloud model");

            // ...and what the local model writes is stored as private.
            var local = new FakeProvider();
            Assert.AreEqual(2, Capture(local, store, 6, keepsLocal).Count);
            Assert.IsTrue(local.Last.BlockCloud);
            foreach (var entry in store.Entries)
                Assert.IsTrue(entry.isPrivate);

            // A new session starts clean, and turning Private mode on at the end still counts.
            privacy.Reset();
            Assert.IsFalse(privacy.KeepsLocal(false));
            Assert.IsTrue(privacy.KeepsLocal(true));
        }

        [Test]
        public void Session_ATimeoutOrNoModel_StoresNothing()
        {
            var store = MemoryStore.Load(_path);
            Assert.AreEqual(0, Capture(new FakeProvider { Hang = true }, store, 6, false, 1f).Count, "quit never waits for a model that hangs");
            Assert.AreEqual(0, Capture(new FakeProvider { Configured = false }, store, 6, false).Count);
            Assert.AreEqual(0, Task.Run(() => SessionMemories.CaptureAsync(null, "x", "Mira", 6, false, false, store, 5f, CancellationToken.None)).Result.Memories.Count);
            Assert.AreEqual(0, store.Count);
            Assert.LessOrEqual(SessionMemories.QuitTimeoutSeconds, 5f);
        }

        [Test]
        public void Session_ParseKeepsAtMostThreeCleanLines()
        {
            var lines = SessionMemories.Parse("Here are the memories:\n1. The user got a new job at a bakery.\n\n* \"Mira teased the user about their singing.\"\n- ok\n3) The user promised to bring cake.\n- The user went to bed late.\n<emotion=happy>");
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual("The user got a new job at a bakery.", lines[0]);
            Assert.AreEqual("Mira teased the user about their singing.", lines[1]);
            Assert.AreEqual("The user promised to bring cake.", lines[2]);
            Assert.AreEqual(0, SessionMemories.Parse(null).Count);
            Assert.AreEqual(0, SessionMemories.Parse("   ").Count);
        }

        [Test]
        public void Session_TranscriptKeepsTheNewestLines()
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "ignored"),
                new ChatMessage(ChatRole.User, "hello"),
                new ChatMessage(ChatRole.Assistant, "hey you"),
            };
            Assert.AreEqual("User: hello\nMira: hey you", SessionMemories.BuildTranscript(messages, "Mira"));
            Assert.AreEqual("User: hello\nCompanion: hey you", SessionMemories.BuildTranscript(messages, ""));

            var many = new List<ChatMessage>();
            for (var i = 0; i < 200; i++)
                many.Add(new ChatMessage(ChatRole.User, "message number " + i + " " + new string('x', 80)));
            var text = SessionMemories.BuildTranscript(many, "Mira");
            Assert.LessOrEqual(text.Length, SessionMemories.MaxTranscriptCharacters + 200);
            StringAssert.Contains("message number 199", text);
            StringAssert.DoesNotContain("message number 0 ", text);
            Assert.AreEqual(string.Empty, SessionMemories.BuildTranscript(null, "Mira"));
        }

        // ---- The viewer and the time away -----------------------------------------------------------

        [Test]
        public void Viewer_LabelsHerLifeAndAwayMemories()
        {
            var lived = new MemoryEntry { text = "The user likes tea." };
            var seeded = new MemoryEntry { text = "She studies design.", origin = MemoryOrigins.Seeded };
            var away = new MemoryEntry { text = "Portfolio review. It went well.", origin = MemoryOrigins.Away, isPrivate = true };

            Assert.AreEqual("Delete: The user likes tea.", MemoryViewerModel.Label(lived));
            Assert.AreEqual("Delete: She studies design. (her life)", MemoryViewerModel.Label(seeded));
            Assert.AreEqual("Delete: Portfolio review. It went well. (while you were away) (private)", MemoryViewerModel.Label(away));
        }

        [Test]
        public void MoodFile_TellsWhenSheWasLastActive()
        {
            var saved = new DateTime(2026, 11, 13, 20, 0, 0, DateTimeKind.Utc);
            var json = new CompanionMoodState().ToJson(saved);
            Assert.AreEqual(saved, CompanionMoodState.SavedAtUtc(json));
            Assert.IsNull(CompanionMoodState.SavedAtUtc("not json"));
            Assert.IsNull(CompanionMoodState.SavedAtUtc(null));

            var path = Path.Combine(_dir, "mood-mira.json");
            Assert.IsNull(CompanionMoodStore.SavedAtUtc(path), "no file: she has not been here before");
            Assert.IsTrue(CompanionMoodStore.Save(path, new CompanionMoodState(), saved));
            Assert.AreEqual(saved, CompanionMoodStore.SavedAtUtc(path));
        }

        [Test]
        public void AwayThread_ResolvesAfterFourHours_AndAStartOverSeedsAgain()
        {
            var store = MemoryStore.Load(_path);
            var sheet = Sheet(true);
            WorldMemory.Seed(store, WorldGuard.ForSeeding(sheet), DateTime.UtcNow, false);

            Assert.IsFalse(WorldMemory.ResolveThread(store, 3600, 0.1, DateTime.UtcNow, true).Happened, "one hour away: nothing");
            var away = WorldMemory.ResolveThread(store, 5 * 3600, 0.1, DateTime.UtcNow, true);
            Assert.IsTrue(away.Happened);
            Assert.IsTrue(away.WentWell);
            StringAssert.Contains("It went well.", away.Text);
            StringAssert.Contains(away.Text, away.PromptLine);

            // Start over: everything is forgotten, and the same cached sheet seeds her world again.
            store.DeleteAll();
            Assert.IsTrue(WorldMemory.NeedsSeeding(store, sheet));
            Assert.AreEqual(7, WorldMemory.Seed(store, WorldGuard.ForSeeding(sheet), DateTime.UtcNow, false));
        }
    }
}
