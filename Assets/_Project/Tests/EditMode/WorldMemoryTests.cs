using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-117 ("Pisara"): her world in memory. Seeding from the sheet, short-term fading and
    /// promotion, a thread resolving while the user is away, and the private flag.
    /// </summary>
    public class WorldMemoryTests
    {
        static readonly DateTime Met = new DateTime(2026, 11, 13, 20, 0, 0, DateTimeKind.Utc);

        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-world-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "memory.json");
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

        static CharacterSheet Sheet(string prompt = "prompt one")
        {
            var sheet = new CharacterSheet { promptHash = CharacterSheet.HashPrompt(prompt) };
            sheet.world.backstory = new List<string> { "She studies design.", "She has a cat named Miso." };
            sheet.world.lastWeek = new List<SheetWeekEntry>
            {
                new SheetWeekEntry { daysAgo = 2, text = "Failed a quiz." },
                new SheetWeekEntry { daysAgo = 0, text = "Bought new boots." },
            };
            sheet.world.openThreads = new List<SheetThread>
            {
                new SheetThread { text = "Portfolio review on Friday.", goodOutcome = "It went well.", badOutcome = "It went badly." },
                new SheetThread { text = "Waiting for a reply from her sister.", goodOutcome = "Her sister wrote back.", badOutcome = string.Empty },
            };
            sheet.world.rightNow = "Sketching at her desk, headphones on.";
            return sheet;
        }

        static MemoryEntry Find(MemoryStore store, string text)
        {
            for (var i = 0; i < store.Entries.Count; i++)
            {
                if (store.Entries[i].text == text)
                    return store.Entries[i];
            }

            return null;
        }

        static int Count(MemoryStore store, Predicate<MemoryEntry> match)
        {
            var count = 0;
            for (var i = 0; i < store.Entries.Count; i++)
            {
                if (match(store.Entries[i]))
                    count++;
            }

            return count;
        }

        // ---- old files -----------------------------------------------------------------------

        [Test]
        public void OldEntries_LoadAsOrdinaryLongTermLivedMemories()
        {
            var store = MemoryStore.Load(_path);
            var fact = store.AddFact("Likes coffee with oat milk", false);
            Assert.AreEqual(MemoryOrigins.Lived, fact.origin);
            Assert.AreEqual(MemoryHorizons.Long, fact.horizon);
            Assert.IsFalse(fact.IsShortTerm);
            Assert.IsFalse(fact.isThread);
            Assert.AreEqual(string.Empty, fact.happenedAtUtc);
            Assert.AreEqual(string.Empty, store.SeededFromHash);

            var again = MemoryStore.Load(_path);
            Assert.AreEqual(1, again.Count);
            Assert.AreEqual(string.Empty, again.SeededFromHash);
            Assert.AreEqual(string.Empty, WorldMemory.When(again.Entries[0], Met));
        }

        // ---- seeding -------------------------------------------------------------------------

        [Test]
        public void Seed_WritesBackstoryTheLastWeekAndThreads_Once()
        {
            var store = MemoryStore.Load(_path);
            store.AddFact("The user's name is Jussi", false, true);
            var sheet = Sheet();

            Assert.IsTrue(WorldMemory.NeedsSeeding(store, sheet));
            Assert.AreEqual(6, WorldMemory.Seed(store, sheet, Met, false));
            Assert.AreEqual(7, store.Count);
            Assert.AreEqual(sheet.promptHash, store.SeededFromHash);

            var cat = Find(store, "She has a cat named Miso.");
            Assert.AreEqual(MemoryOrigins.Seeded, cat.origin);
            Assert.IsFalse(cat.IsShortTerm);
            Assert.IsFalse(cat.isPrivate);

            var quiz = Find(store, "Failed a quiz.");
            Assert.IsTrue(quiz.IsShortTerm);
            Assert.AreEqual("2 days ago", WorldMemory.When(quiz, Met));
            Assert.AreEqual("today", WorldMemory.When(Find(store, "Bought new boots."), Met));
            Assert.AreEqual("yesterday", WorldMemory.When(Find(store, "Bought new boots."), Met.AddDays(1)));

            var review = Find(store, "Portfolio review on Friday.");
            Assert.IsTrue(review.isThread);
            Assert.AreEqual("It went badly.", review.badOutcome);

            // Already in: nothing happens, also after a reload.
            Assert.IsFalse(WorldMemory.NeedsSeeding(store, sheet));
            Assert.AreEqual(0, WorldMemory.Seed(store, sheet, Met, false));
            var reloaded = MemoryStore.Load(_path);
            Assert.AreEqual(7, reloaded.Count);
            Assert.AreEqual(0, WorldMemory.Seed(reloaded, Sheet(), Met, false));
            Assert.IsTrue(Find(reloaded, "Portfolio review on Friday.").isThread);
        }

        [Test]
        public void Seed_FromAChangedPrompt_ReplacesTheOldWorld_AndKeepsLivedAndAwayMemories()
        {
            var store = MemoryStore.Load(_path);
            store.AddFact("The user likes rain", false);
            WorldMemory.Seed(store, Sheet(), Met, false);
            var away = WorldMemory.ResolveThread(store, WorldMemory.MinAwaySeconds, 0.1, Met.AddHours(5), true);
            Assert.IsTrue(away.Happened);

            var changed = Sheet("prompt two");
            changed.world.backstory = new List<string> { "She is a baker." };
            changed.world.lastWeek = new List<SheetWeekEntry>();
            changed.world.openThreads = new List<SheetThread>();
            Assert.IsTrue(WorldMemory.NeedsSeeding(store, changed));
            Assert.AreEqual(1, WorldMemory.Seed(store, changed, Met.AddDays(1), false));

            Assert.IsNull(Find(store, "She studies design."));
            Assert.IsNull(Find(store, "Waiting for a reply from her sister."));
            Assert.IsNotNull(Find(store, "She is a baker."));
            Assert.IsNotNull(Find(store, "The user likes rain"));
            Assert.IsNotNull(Find(store, away.Text));
            Assert.AreEqual(changed.promptHash, store.SeededFromHash);
        }

        [Test]
        public void Seed_KeepsThePrivateFlag_SoNothingPrivateReachesACloudPrompt()
        {
            var store = MemoryStore.Load(_path);
            WorldMemory.Seed(store, Sheet(), Met, true);
            Assert.AreEqual(store.Count, Count(store, entry => entry.isPrivate));
            Assert.AreEqual(0, store.SearchFor("design cat quiz boots", 8, false).Count);
            Assert.Greater(store.SearchFor("design cat quiz boots", 8, true).Count, 0);

            // A private thread does not resolve into a session that may use the cloud.
            Assert.IsFalse(WorldMemory.ResolveThread(store, 1e6, 0.1, Met, false).Happened);
            var local = WorldMemory.ResolveThread(store, 1e6, 0.1, Met, true);
            Assert.IsTrue(local.Happened);
            Assert.IsTrue(Find(store, local.Text).isPrivate);
        }

        [Test]
        public void Seed_NeedsAPromptHash_SoAResolvedThreadNeverComesBack()
        {
            var store = MemoryStore.Load(_path);
            var unhashed = Sheet();
            unhashed.promptHash = string.Empty;
            Assert.IsFalse(WorldMemory.NeedsSeeding(store, unhashed));
            Assert.AreEqual(0, WorldMemory.Seed(store, unhashed, Met, false));
            Assert.AreEqual(0, store.Count);

            // With a hash: seeded once, a thread resolves, and seeding again does not undo it.
            var sheet = Sheet();
            WorldMemory.Seed(store, sheet, Met, false);
            var away = WorldMemory.ResolveThread(store, 1e6, 0.1, Met, true);
            Assert.IsTrue(away.Happened);
            Assert.AreEqual(0, WorldMemory.Seed(MemoryStore.Load(_path), Sheet(), Met, false));
            Assert.IsNull(Find(MemoryStore.Load(_path), "Portfolio review on Friday."));
        }

        [Test]
        public void DeleteAll_ForgetsTheUser_AndHerWorldIsSeededFreshFromTheSheet()
        {
            var store = MemoryStore.Load(_path);
            var sheet = Sheet();
            WorldMemory.Seed(store, sheet, Met, false);
            store.AddFact("The user's name is Jussi", false, true);
            WorldMemory.ResolveThread(store, 1e6, 0.1, Met, true);

            Assert.Greater(store.DeleteAll(), 0);
            Assert.AreEqual(0, store.Count);
            Assert.AreEqual(string.Empty, store.SeededFromHash);
            Assert.AreEqual(string.Empty, MemoryStore.Load(_path).SeededFromHash, "saved");

            // The next start seeds her world again, threads open again, and nothing about the user.
            var reloaded = MemoryStore.Load(_path);
            Assert.IsTrue(WorldMemory.NeedsSeeding(reloaded, sheet));
            Assert.AreEqual(6, WorldMemory.Seed(reloaded, sheet, Met.AddDays(3), false));
            Assert.IsTrue(Find(reloaded, "Portfolio review on Friday.").isThread);
            Assert.IsNull(Find(reloaded, "The user's name is Jussi"));
        }

        [Test]
        public void Seed_IgnoresNulls_AndFirstSceneOpensMidScene()
        {
            Assert.AreEqual(0, WorldMemory.Seed(null, Sheet(), Met, false));
            Assert.AreEqual(0, WorldMemory.Seed(MemoryStore.Load(_path), null, Met, false));
            Assert.IsFalse(WorldMemory.NeedsSeeding(null, null));

            Assert.AreEqual(
                "This is the first time the user talks to you. You were in the middle of this: Sketching at her desk, headphones on. Start from there, in your own words.",
                WorldMemory.FirstScene(Sheet()));
            Assert.AreEqual(string.Empty, WorldMemory.FirstScene(null));
            Assert.AreEqual(string.Empty, WorldMemory.FirstScene(new CharacterSheet()));
        }

        // ---- promotion and fading ------------------------------------------------------------

        [Test]
        public void ShortTermMemories_FadeAfterTwoWeeks_UnlessTheyKeepComingUp()
        {
            var store = MemoryStore.Load(_path);
            var kept = WorldMemory.AddShortTerm(store, "We talked about her portfolio.", Met, false);
            var lost = WorldMemory.AddShortTerm(store, "It rained all evening.", Met, false);
            var fact = store.AddFact("Likes coffee", false);
            Assert.IsTrue(kept.IsShortTerm);
            Assert.AreEqual(MemoryKind.Summary, kept.kind);

            // One recall is not enough; two promote it.
            Assert.AreEqual(0, WorldMemory.MarkRecalled(store, new[] { kept, fact }));
            Assert.IsTrue(kept.IsShortTerm);
            Assert.AreEqual(1, WorldMemory.MarkRecalled(store, new[] { kept, null }));
            Assert.IsFalse(kept.IsShortTerm);
            Assert.AreEqual(0, fact.recalls, "long-term entries are not counted");

            Assert.AreEqual(0, WorldMemory.Fade(store, Met.AddDays(WorldMemory.ShortLifeDays)));
            Assert.AreEqual(1, WorldMemory.Fade(store, Met.AddDays(WorldMemory.ShortLifeDays + 1)));
            Assert.IsNull(Find(store, "It rained all evening."));
            Assert.IsNotNull(Find(store, "We talked about her portfolio."));
            Assert.IsNotNull(Find(store, "Likes coffee"));
            Assert.AreEqual(0, WorldMemory.Fade(store, Met.AddDays(400)));
            Assert.AreEqual(0, WorldMemory.Fade(null, Met));
            Assert.AreEqual(0, WorldMemory.MarkRecalled(store, null));

            // The promotion and the fading are on disk.
            var reloaded = MemoryStore.Load(_path);
            Assert.AreEqual(2, reloaded.Count);
            Assert.IsFalse(Find(reloaded, "We talked about her portfolio.").IsShortTerm);
        }

        [Test]
        public void Fade_CountsFromWhenItWasStored_AndNeverTakesThreadsOrCoreFacts()
        {
            var store = MemoryStore.Load(_path);
            var sheet = Sheet();
            sheet.world.lastWeek = new List<SheetWeekEntry> { new SheetWeekEntry { daysAgo = 14, text = "Moved desks." } };
            WorldMemory.Seed(store, sheet, DateTime.UtcNow, false);
            var core = store.AddEntry(new MemoryEntry { text = "Core short", horizon = MemoryHorizons.Short, isCore = true, createdAtUtc = Met.AddDays(-100).ToString("o") });

            // "Moved desks" happened 14 days before they met, but was stored just now.
            Assert.AreEqual(0, WorldMemory.Fade(store, DateTime.UtcNow.AddDays(1)));
            Assert.IsNotNull(Find(store, "Moved desks."));

            Assert.AreEqual(1, WorldMemory.Fade(store, DateTime.UtcNow.AddDays(WorldMemory.ShortLifeDays + 2)));
            Assert.IsNull(Find(store, "Moved desks."));
            Assert.IsNotNull(Find(store, "Portfolio review on Friday."));
            Assert.IsNotNull(core);
            Assert.IsNotNull(Find(store, "Core short"));
        }

        // ---- away ----------------------------------------------------------------------------

        [Test]
        public void Away_ResolvesOneThread_GoodOrBad_AndNudgesHerMood()
        {
            var store = MemoryStore.Load(_path);
            WorldMemory.Seed(store, Sheet(), Met, false);
            var back = Met.AddHours(6);

            // Not away long enough.
            Assert.IsFalse(WorldMemory.ResolveThread(store, WorldMemory.MinAwaySeconds - 1, 0.1, back, true).Happened);
            Assert.IsFalse(WorldMemory.ResolveThread(store, double.NaN, 0.1, back, true).Happened);
            Assert.IsFalse(WorldMemory.ResolveThread(null, 1e6, 0.1, back, true).Happened);

            var bad = WorldMemory.ResolveThread(store, WorldMemory.MinAwaySeconds, 0.9, back, true);
            Assert.IsTrue(bad.Happened);
            Assert.IsFalse(bad.WentWell);
            Assert.AreEqual("Portfolio review on Friday. It went badly.", bad.Text);
            StringAssert.Contains(bad.Text, bad.PromptLine);
            Assert.IsFalse(bad.PromptLine.Contains("\n"));
            Assert.AreEqual(CompanionMood.Sad, bad.MoodPushes[0].Mood);

            Assert.IsNull(Find(store, "Portfolio review on Friday."), "the thread is closed");
            var memory = Find(store, bad.Text);
            Assert.AreEqual(MemoryOrigins.Away, memory.origin);
            Assert.IsTrue(memory.IsShortTerm);
            Assert.IsFalse(memory.isThread);
            Assert.AreEqual("today", WorldMemory.When(memory, back));

            // The next thread has only a good outcome written, so a bad roll still takes it.
            var good = WorldMemory.ResolveThread(store, 1e6, 0.99, back, true);
            Assert.IsTrue(good.WentWell);
            Assert.AreEqual("Waiting for a reply from her sister. Her sister wrote back.", good.Text);
            Assert.AreEqual(CompanionMood.Happy, good.MoodPushes[0].Mood);

            // No threads left.
            Assert.IsFalse(WorldMemory.ResolveThread(store, 1e6, 0.1, back, true).Happened);
            Assert.AreEqual(0, Count(MemoryStore.Load(_path), entry => entry.isThread));
        }

        [Test]
        public void Away_GoodChance_SplitsTheRoll_AndNeverPushesIntimate()
        {
            foreach (var roll in new[] { 0.0, 0.59, 0.6, 1.0 })
            {
                var store = MemoryStore.Load(Path.Combine(_dir, "m" + roll + ".json"));
                WorldMemory.Seed(store, Sheet(), Met, false);
                var away = WorldMemory.ResolveThread(store, 1e6, roll, Met, true);
                Assert.AreEqual(roll < WorldMemory.GoodChance, away.WentWell, roll.ToString());
                foreach (var push in away.MoodPushes)
                    Assert.AreNotEqual(CompanionMood.Intimate, push.Mood);
            }
        }

        // ---- the prompt ----------------------------------------------------------------------

        [Test]
        public void Prompt_PlacesDatedMemoriesInTime()
        {
            var store = MemoryStore.Load(_path);
            WorldMemory.Seed(store, Sheet(), Met, false);
            var text = MemoryPrompt.Build(new[] { Find(store, "Failed a quiz."), Find(store, "She studies design."), null }, Met.AddDays(1));
            Assert.AreEqual(MemoryPrompt.Header + "\n- (3 days ago) Failed a quiz.\n- She studies design.", text);
            Assert.AreEqual(string.Empty, MemoryPrompt.Build(null, Met));

            var old = new MemoryEntry { id = "x", text = "Long ago.", happenedAtUtc = Met.AddDays(-200).ToString("o") };
            Assert.AreEqual(string.Empty, WorldMemory.When(old, Met));
            var future = new MemoryEntry { id = "y", text = "Later.", happenedAtUtc = Met.AddDays(2).ToString("o") };
            Assert.AreEqual(string.Empty, WorldMemory.When(future, Met));
            Assert.AreEqual(string.Empty, WorldMemory.When(new MemoryEntry { happenedAtUtc = "not a date" }, Met));
            Assert.AreEqual(string.Empty, WorldMemory.When(null, Met));
        }
    }
}
