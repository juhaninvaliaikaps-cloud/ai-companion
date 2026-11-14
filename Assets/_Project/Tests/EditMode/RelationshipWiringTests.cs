using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-118 section 4 and the new threads: the wiring around the relationship and world threads.</summary>
    public sealed class RelationshipWiringTests
    {
        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-relwire-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "memory.json");
            PisaraSwitches.Reset();
            RelationshipNow.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PisaraSwitches.Reset();
            RelationshipNow.Reset();
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
            public string Reply;
            public ChatRequest Last;
            public string ProviderId { get { return ChatDefaults.LocalId; } }
            public string DisplayName { get { return "local"; } }
            public bool IsConfigured { get { return true; } }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Last = request;
                return Task.FromResult(ChatResult.Success(Reply));
            }
        }

        static AnimationCue Cue(string tag)
        {
            return AnimationCueParser.Parse(tag + "Hello.").Cue;
        }

        // ---- The landing, once per reply ----------------------------------------------------------

        [Test]
        public void Reply_LandsThroughTheMath_AndGivesTheMoodCardAReason()
        {
            var cue = Cue("<animation emotion=\"happy\" intensity=\"0.7\" landed=\"complimented\" />");
            Assert.AreEqual(LandingEvent.Complimented, cue.Landed);

            var change = RelationshipNow.Land(cue, 20f, 60f);
            var expected = RelationshipMath.Land(LandingEvent.Complimented, 0.7f, 20f, 60f);
            Assert.AreEqual(expected.Affection, change.Affection, 0.0001f, "the relationship math is used as it is");
            Assert.AreEqual(expected.Familiarity, change.Familiarity, 0.0001f);
            Assert.Greater(change.Affection, 20f);

            var history = new RelationshipHistory();
            Assert.IsNotEmpty(history.Record(change), "a landed message has a reason");
            StringAssert.Contains("Recently:", RelationshipCard.Describe(change.Affection, change.Familiarity, history, true));
        }

        [Test]
        public void UnreadableEmotion_StillLands_AndANeutralReplyIsStillAnExchange()
        {
            var odd = Cue("<animation emotion=\"zzz\" landed=\"insulted\" />");
            Assert.AreEqual(LandingEvent.Insulted, odd.Landed, "the landing is applied even when the emotion was unreadable");
            Assert.Less(RelationshipNow.Land(odd, 0f, 0f).Affection, 0f);

            var none = AnimationCueParser.NeutralCue;
            var change = RelationshipNow.Land(none, 10f, 30f);
            Assert.AreEqual(10f, change.Affection, 0.0001f, "a reply without a landing does not move affection");
            Assert.Greater(change.Familiarity, 30f, "but every exchange is time spent knowing each other");
        }

        [Test]
        public void ReportReply_ReachesTheListenerOnce()
        {
            var heard = 0;
            Action<AnimationCue> listener = cue => heard++;
            RelationshipNow.ReplyFinished += listener;
            try
            {
                RelationshipNow.ReportReply(AnimationCueParser.NeutralCue);
            }
            finally
            {
                RelationshipNow.ReplyFinished -= listener;
            }

            Assert.AreEqual(1, heard);
            RelationshipNow.ReportReply(AnimationCueParser.NeutralCue);
            Assert.AreEqual(1, heard, "without a listener nothing happens");
        }

        [Test]
        public void LandedAttribute_NeverShowsInTheChatText()
        {
            var filter = new CueStreamFilter();
            filter.Push("<animation emotion=\"happy\" landed=\"teased\" />Oh, you think so?");
            filter.Flush();
            Assert.AreEqual("Oh, you think so?", filter.Text.Trim());
            StringAssert.DoesNotContain("landed", filter.Text);
        }

        // ---- The prompt -----------------------------------------------------------------------------

        [Test]
        public void PromptLine_GoesAfterTheMoodLine_AndIsEmptyWithoutOne()
        {
            Assert.AreEqual("Persona.", RelationshipNow.Append("Persona.\n"));

            var sheet = new SheetRelationship { role = RelationshipRoles.Family, label = "stepsister" };
            var line = RelationshipPrompt.Line(sheet, new SheetLanding(), 25f, 80f);
            RelationshipNow.SetPromptLine(line);
            Assert.AreEqual("Persona.\n\n" + line, RelationshipNow.Append("Persona."));
            StringAssert.StartsWith("Your relationship with the user: stepsister (family)", RelationshipNow.PromptLine);

            RelationshipNow.Reset();
            Assert.AreEqual(string.Empty, RelationshipNow.PromptLine);
        }

        [Test]
        public void CueInstruction_AsksForLanded_OnlyWhileRelationshipChangesAreOn()
        {
            var director = new UnityEngine.GameObject("director").AddComponent<AICompanion.AnimationDirector.AnimationDirector>();
            try
            {
                StringAssert.Contains(LandingEvents.Instruction, director.SystemPromptSuffix);
                PisaraSwitches.Configure(false, true, true);
                StringAssert.DoesNotContain("landed=", director.SystemPromptSuffix);
                StringAssert.Contains(AnimationCueParser.Instruction, director.SystemPromptSuffix, "the cue instruction itself stays");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(director.gameObject);
            }
        }

        // ---- Familiarity from time and start values ------------------------------------------------

        [Test]
        public void ChatTime_GrowsFamiliarity_InSmallSteps()
        {
            var familiarity = 10f;
            for (var i = 0; i < 600; i++)
                familiarity = RelationshipMath.FamiliarityAfter(familiarity, 1d / 60d);
            Assert.Greater(familiarity, 10f, "ten seconds of chatting count");
            Assert.AreEqual(RelationshipMath.FamiliarityAfter(10f, 10d), familiarity, 0.01f, "frame by frame is the same as all at once");
        }

        static float ByClock(float start, int steps, double stepSeconds)
        {
            var clock = new FamiliarityClock();
            var familiarity = start;
            for (var i = 0; i < steps; i++)
            {
                if (clock.Add(stepSeconds))
                    familiarity = clock.Apply(familiarity);
            }

            // The rest goes in before a save.
            return clock.Apply(familiarity);
        }

        [TestCase(50f)]
        [TestCase(80f)]
        [TestCase(90f)]
        public void FamiliarityClock_3600OneFrameSteps_MatchOne3600SecondStep(float start)
        {
            var exact = RelationshipMath.FamiliarityAfter(start, 3600d);
            Assert.Greater(exact, start, "an hour of chatting does count");
            Assert.AreEqual(exact, ByClock(start, 3600, 1d), 0.05f, "3600 steps of one second");
            Assert.AreEqual(exact, ByClock(start, 3600 * 60, 1d / 60d), 0.05f, "an hour at 60 fps: the gain no longer rounds away");
            Assert.AreEqual(exact, ByClock(start, 3600 * 30, 1d / 30d), 0.05f, "and at 30 fps the same");
        }

        [Test]
        public void FamiliarityClock_AppliesEveryFiveSeconds_AndOnFlush()
        {
            var clock = new FamiliarityClock();
            Assert.IsFalse(clock.Add(4.9d));
            Assert.IsTrue(clock.Add(0.2d), "5 s gathered");
            Assert.AreEqual(5.1d, clock.Pending, 1e-9);

            var after = clock.Apply(90f);
            Assert.Greater(after, 90f);
            Assert.AreEqual(0d, clock.Pending, "used up");
            Assert.AreEqual(after, clock.Apply(after), "nothing pending: unchanged");

            clock.Add(double.NaN);
            clock.Add(-3d);
            Assert.AreEqual(0d, clock.Pending, "bad time is ignored");
            clock.Add(2d);
            clock.Clear();
            Assert.AreEqual(0d, clock.Pending);
        }

        [Test]
        public void StartValues_ComeFromTheSheet_InsideWhatTheRoleAllows()
        {
            float affection;
            float familiarity;
            RelationshipMath.Start(new SheetRelationship { role = RelationshipRoles.Family, affection = 40f, familiarity = 80f }, out affection, out familiarity);
            Assert.AreEqual(40f, affection, 0.01f);
            Assert.AreEqual(80f, familiarity, 0.01f);
        }

        // ---- The keyword fallback --------------------------------------------------------------------

        [Test]
        public void Keywords_BestPresetAt06_SecondOnlyAboveTheThreshold()
        {
            var none = PisaraKeywords.Temperament("You are a companion.");
            Assert.IsNull(none, "no trait words: no keyword guess");

            var shy = PisaraKeywords.Temperament("You are shy, timid and bashful, and you blush easily.");
            Assert.IsNotNull(shy);
            Assert.AreEqual(PisaraKeywords.BestWeight, shy.shy, 0.001f);
            var reading = PersonaTemperamentBlend.From(shy);
            Assert.AreEqual(PersonaTemperaments.ShyId, reading.IsBlend ? PersonaTemperaments.ShyId : reading.Temperament.Id, "0.6 clears the blend's confidence");

            var result = TemperamentKeywordScorer.Score("You are shy, timid and bashful, and you blush easily.");
            foreach (var id in PersonaTemperaments.Ids)
            {
                if (id == PersonaTemperaments.DefaultId || id == result.BestId)
                    continue;
                var weight = shy.Weight(id);
                Assert.IsTrue(weight == 0f || (weight == PisaraKeywords.SecondWeight && result.Score(id) > PisaraKeywords.SecondMinScore), id);
            }
        }

        // ---- New threads at session end ---------------------------------------------------------------

        static SessionCapture Capture(FakeProvider provider, MemoryStore store, bool askThread)
        {
            return Task.Run(() => SessionMemories.CaptureAsync(
                provider, "User: hi\nMira: hey", "Mira", 6, false, false, store, 30f, CancellationToken.None, askThread)).Result;
        }

        const string ReplyWithThread =
            "The user told Mira about their new job.\n" +
            "{\"newThread\": {\"text\": \"Mira signed up for a figure drawing class.\", \"goodOutcome\": \"She loves it.\", \"badOutcome\": \"She drops out.\"}}";

        [Test]
        public void Session_StoresANewThread_AndNeverItsJsonAsAMemory()
        {
            var store = MemoryStore.Load(_path);
            var provider = new FakeProvider { Reply = ReplyWithThread };
            var capture = Capture(provider, store, true);

            StringAssert.Contains(WorldThreads.Instruction, provider.Last.SystemPrompt);
            Assert.AreEqual(1, capture.Memories.Count);
            Assert.AreEqual("The user told Mira about their new job.", capture.Memories[0]);
            Assert.IsNotNull(capture.Thread);
            Assert.IsTrue(capture.Thread.isThread);
            Assert.AreEqual(MemoryOrigins.Life, capture.Thread.origin);
            Assert.AreEqual("She drops out.", capture.Thread.badOutcome);
            foreach (var entry in store.Entries)
                StringAssert.DoesNotContain("{", entry.text);
            Assert.AreEqual(" (her life)", MemoryViewerModel.OriginLabel(capture.Thread));
        }

        [Test]
        public void Session_ReadsABareThreadObjectToo()
        {
            var thread = SessionMemories.ParseThread("Line one.\n```json\n{\"text\": \"A job interview on Monday.\", \"goodOutcome\": \"She gets it.\", \"badOutcome\": \"She does not.\"}\n```");
            Assert.IsNotNull(thread);
            Assert.AreEqual("A job interview on Monday.", thread.text);
            Assert.IsNull(SessionMemories.ParseThread("Just one memory line."));
            Assert.AreEqual(1, SessionMemories.Parse("The user got a cat named Miso.\n```json\n{\"text\": \"x\"}\n```").Count);
        }

        [Test]
        public void Session_NoThreadWhenNotAskedOrSwitchedOff()
        {
            var store = MemoryStore.Load(_path);
            var notAsked = new FakeProvider { Reply = ReplyWithThread };
            var capture = Capture(notAsked, store, false);
            Assert.IsNull(capture.Thread);
            StringAssert.DoesNotContain("newThread", notAsked.Last.SystemPrompt);

            PisaraSwitches.Configure(true, false, true);
            Assert.IsNull(Capture(new FakeProvider { Reply = ReplyWithThread }, MemoryStore.Load(Path.Combine(_dir, "b.json")), true).Thread, "Life while you are away off");
            PisaraSwitches.Configure(true, true, false);
            Assert.IsNull(Capture(new FakeProvider { Reply = ReplyWithThread }, MemoryStore.Load(Path.Combine(_dir, "c.json")), true).Thread, "Her own world off");
        }
    }
}
