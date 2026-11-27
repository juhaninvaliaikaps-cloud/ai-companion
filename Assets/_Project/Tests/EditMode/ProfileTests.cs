using System;
using System.Collections.Generic;
using System.Globalization;
using AICompanion.Core;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-121: the data behind the Profile panel (Feelings, Personality, Her world, Memories).</summary>
    public class ProfileTests
    {
        [TestCase("2026-11-24T05:00:00Z", true)]
        [TestCase("", false)]
        [TestCase("not a date", false)]
        public void TimelineRowsKeepDatesAndKindsSeparateFromTheSummary(string rawDate, bool validDate)
        {
            var entry = new TimelineEntry { happenedAtUtc = rawDate, kind = "event", summary = "A summary [with brackets]." };
            var row = ProfileTimelineRow.From(entry);
            var expected = validDate
                ? DateTime.Parse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime()
                    .ToString("d.M.yyyy", CultureInfo.InvariantCulture)
                : string.Empty;

            Assert.That(row.When, Is.EqualTo(expected));
            Assert.That(row.Kind, Is.EqualTo(MemoryDurability.KindLabel(entry.kind)));
            Assert.That(row.Text, Is.EqualTo(entry.summary));
        }

        static readonly DateTime Now = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

        // ---- Feelings (option A) -------------------------------------------------------------

        [Test]
        public void Feelings_ShowTheNumbersBandsAndMarker()
        {
            var moods = new[] { CompanionMood.Happy, CompanionMood.Annoyed, CompanionMood.Content };
            var strengths = new[] { 0.62f, 0.2f, 0f };
            var f = ProfileFeelings.Build(40f, 80f, CompanionMood.Happy, moods, strengths, null, true);

            Assert.AreEqual("+40 · she likes you", f.AffectionValue);
            Assert.AreEqual(0.7f, f.AffectionMarker, 0.001f);
            Assert.AreEqual("80 · you know each other very well", f.FamiliarityValue);
            Assert.AreEqual(0.8f, f.FamiliarityFill, 0.001f);
            Assert.AreEqual("Mood now: Happy", f.MoodTitle);
            Assert.AreEqual(CompanionMood.Happy, f.CurrentMood);
            Assert.AreEqual("She likes you", f.AffectionDescription);
            Assert.AreEqual("You know each other very well", f.FamiliarityDescription);
            Assert.AreEqual(2, f.Moods.Count, "an empty slot is not shown");
            Assert.AreEqual("Happy", f.Moods[0].Label);
            Assert.AreEqual(CompanionMood.Happy, f.Moods[0].Mood);
            Assert.AreEqual(CompanionMood.Annoyed, f.Moods[1].Mood);
            Assert.AreEqual("62%", f.Moods[0].Value);
            Assert.AreEqual("Nothing has moved yet this session.", f.ReasonsNote);
        }

        [Test]
        public void Feelings_ClampAndPlaceTheMarkerAtTheEnds()
        {
            Assert.AreEqual(0f, ProfileFeelings.Build(-500f, 0f, CompanionMood.Content, null, null, null, true).AffectionMarker, 0.001f);
            Assert.AreEqual(1f, ProfileFeelings.Build(500f, 0f, CompanionMood.Content, null, null, null, true).AffectionMarker, 0.001f);
            Assert.AreEqual("-30 · she is at odds with you", ProfileFeelings.Build(-30f, 0f, CompanionMood.Content, null, null, null, true).AffectionValue);
        }

        [Test]
        public void Feelings_ListTheRecentReasons_OrSayTheyAreOff()
        {
            var history = new RelationshipHistory();
            history.Record(RelationshipMath.Land(LandingEvent.Insulted, 0.5f, 0f, 0f));
            history.Record(RelationshipMath.Land(LandingEvent.Complimented, 0.5f, 0f, 0f));

            var on = ProfileFeelings.Build(0f, 0f, CompanionMood.Content, null, null, history, true);
            Assert.AreEqual(2, on.Reasons.Count);
            CollectionAssert.AreEqual(new[] { 1, -1 }, on.ReasonDirections);
            StringAssert.StartsWith("A compliment landed well", on.Reasons[0], "newest first");
            Assert.AreEqual(string.Empty, on.ReasonsNote);

            var off = ProfileFeelings.Build(0f, 0f, CompanionMood.Content, null, null, history, false);
            Assert.AreEqual(0, off.Reasons.Count);
            Assert.IsEmpty(off.ReasonDirections);
            StringAssert.Contains("switched off", off.ReasonsNote);
        }

        [Test]
        public void Feelings_PublishesMoodAndGateChangesButSkipsStrengthOnlyUpdates()
        {
            var shown = CompanionMoodNow.Shown;
            var strength = CompanionMoodNow.Strength;
            var enabled = IntimateExpressions.Enabled;
            var privateMode = IntimateExpressions.PrivateMode;
            var hasFolder = IntimateExpressions.FolderPresent;
            var changes = new List<CompanionMood>();
            void OnChanged() => changes.Add(CompanionMoodNow.Effective);
            IntimateExpressions.Configure(false, false, false);
            CompanionMoodNow.Reset();
            CompanionMoodNow.DisplayChanged += OnChanged;
            try
            {
                CompanionMoodNow.Set(CompanionMood.Happy, 0.7f);
                CompanionMoodNow.Set(CompanionMood.Happy, 0.8f);
                IntimateExpressions.Configure(true, true, true);
                CompanionMoodNow.Set(CompanionMood.Intimate, 0.9f);
                IntimateExpressions.Configure(true, false, true);
                CollectionAssert.AreEqual(new[]
                {
                    CompanionMood.Happy, CompanionMood.Happy, CompanionMood.Intimate, CompanionMood.Content,
                }, changes);
            }
            finally
            {
                CompanionMoodNow.DisplayChanged -= OnChanged;
                IntimateExpressions.Configure(enabled, privateMode, hasFolder);
                CompanionMoodNow.Set(shown, strength);
            }
        }

        // ---- Personality ---------------------------------------------------------------------

        [Test]
        public void Personality_TakesTheFiveAxesFromTheSheet()
        {
            var sheet = CharacterSheet.Neutral("A bratty stepsister.");
            sheet.temperament.tsundere = 0.9f;
            sheet.temperament.cheerful = 0.55f;
            sheet.temperament.calm = 1.7f;
            sheet.temperament.reason = "Sharp-tongued but caring.";

            var p = ProfilePersonality.Build(sheet, "Temperament: Auto (Tsundere 62%, Cheerful 38%)", false, 50f);
            Assert.IsTrue(p.HasReading);
            Assert.AreEqual(5, p.Axes.Length);
            Assert.AreEqual("Calm", ProfilePersonality.AxisNames[0]);
            Assert.AreEqual(1f, p.Axes[0], 0.001f, "clamped to 1");
            Assert.AreEqual(0.55f, p.Axes[1], 0.001f);
            Assert.AreEqual(0.9f, p.Axes[3], 0.001f);
            Assert.AreEqual("Sharp-tongued but caring.", p.Reason);
            Assert.AreEqual("Temperament: Auto (Tsundere 62%, Cheerful 38%)", p.Title);
            Assert.IsFalse(p.ShowInnocence, "nothing about innocence while 18+ is off");
        }

        [Test]
        public void Personality_WithoutAReading_SaysSo()
        {
            var p = ProfilePersonality.Build(null, string.Empty, false, 50f);
            Assert.IsFalse(p.HasReading);
            Assert.AreEqual("Temperament", p.Title);
            StringAssert.Contains("not been read", p.Reason);
        }

        [Test]
        public void Personality_ShowsInnocenceOnlyWithTheGateOpen()
        {
            var p = ProfilePersonality.Build(null, string.Empty, true, 30f);
            Assert.IsTrue(p.ShowInnocence);
            Assert.AreEqual(30f, p.Innocence);
            Assert.AreEqual("30 · innocent", p.InnocenceValue);
        }

        [Test]
        public void Header_SaysWhoSheIsToYou()
        {
            Assert.AreEqual("Stepsister (family) · you share a past", ProfileHeader.RoleLine(new SheetRelationship { role = RelationshipRoles.Family, label = "stepsister", sharedHistory = true }));
            Assert.AreEqual("Stranger", ProfileHeader.RoleLine(new SheetRelationship { role = RelationshipRoles.Stranger, sharedHistory = true }), "a stranger never shares a past");
            Assert.AreEqual("Close friend · you share a past", ProfileHeader.RoleLine(new SheetRelationship { role = RelationshipRoles.CloseFriend, label = "close friend", sharedHistory = true }));
            Assert.AreEqual(string.Empty, ProfileHeader.RoleLine(null));
        }

        // ---- Her world -----------------------------------------------------------------------

        static MemoryEntry Entry(string id, string text, string origin, int horizon, double daysAgo = -1, bool thread = false)
        {
            return new MemoryEntry
            {
                id = id,
                text = text,
                origin = origin,
                horizon = horizon,
                isThread = thread,
                happenedAtUtc = daysAgo < 0 ? string.Empty : Now.AddDays(-daysAgo).ToString("o", CultureInfo.InvariantCulture),
            };
        }

        [Test]
        public void World_SortsHerLifeIntoItsSections()
        {
            var entries = new List<MemoryEntry>
            {
                Entry("a", "Studies graphic design.", MemoryOrigins.Seeded, MemoryHorizons.Long),
                Entry("b", "Has a ferret named Pixel.", MemoryOrigins.Seeded, MemoryHorizons.Long),
                Entry("c", "Went to the library.", MemoryOrigins.Seeded, MemoryHorizons.Short, 5),
                Entry("d", "Designed a new logo.", MemoryOrigins.Seeded, MemoryHorizons.Short, 0),
                Entry("e", "Waiting for her project grade.", MemoryOrigins.Seeded, MemoryHorizons.Long, -1, true),
                Entry("f", "Signed up for pottery.", MemoryOrigins.Life, MemoryHorizons.Long, -1, true),
                Entry("g", "Her design project got an A.", MemoryOrigins.Away, MemoryHorizons.Long, 1),
                Entry("h", "The user likes jazz.", MemoryOrigins.Lived, MemoryHorizons.Long),
            };
            var sheet = CharacterSheet.Neutral("x");
            sheet.world.rightNow = "Designing a poster.";

            var w = ProfileWorld.Build(entries, sheet, Now);
            Assert.AreEqual("Designing a poster.", w.RightNow);
            CollectionAssert.AreEqual(new[] { "Studies graphic design.", "Has a ferret named Pixel." }, w.Backstory);
            CollectionAssert.AreEqual(new[] { "Waiting for her project grade.", "Signed up for pottery." }, w.Waiting);
            Assert.AreEqual(1, w.WhileAway.Count);
            Assert.AreEqual("yesterday", w.WhileAway[0].When);
            Assert.AreEqual(2, w.Week.Count);
            Assert.AreEqual("today", w.Week[0].When, "newest first");
            Assert.AreEqual("Designed a new logo.", w.Week[0].Text);
            Assert.AreEqual("5 days ago", w.Week[1].When);
            Assert.IsFalse(w.IsEmpty);
        }

        [Test]
        public void World_IsEmptyWithoutASheetOrMemories()
        {
            Assert.IsTrue(ProfileWorld.Build(null, null, Now).IsEmpty);
            Assert.IsTrue(ProfileWorld.Build(new List<MemoryEntry> { Entry("h", "The user likes jazz.", MemoryOrigins.Lived, MemoryHorizons.Long) }, null, Now).IsEmpty,
                "what she remembers about you is not her world");
        }

        [Test]
        public void World_AnOldOrUndatedDayIsEarlier()
        {
            var w = ProfileWorld.Build(new List<MemoryEntry> { Entry("c", "A long time ago.", MemoryOrigins.Seeded, MemoryHorizons.Short, 400) }, null, Now);
            Assert.AreEqual(ProfileWorld.Earlier, w.Week[0].When);
        }

        // ---- Memories ------------------------------------------------------------------------

        [Test]
        public void Memories_AreOnlyAboutYou_NewestFirst()
        {
            var entries = new List<MemoryEntry>
            {
                Entry("a", "Studies graphic design.", MemoryOrigins.Seeded, MemoryHorizons.Long),
                Entry("h1", "The user likes jazz.", MemoryOrigins.Lived, MemoryHorizons.Long),
                Entry("h2", "The user has a cat.", MemoryOrigins.Lived, MemoryHorizons.Short),
                Entry("g", "Her project got an A.", MemoryOrigins.Away, MemoryHorizons.Long, 1),
            };
            int total;
            var list = ProfileMemories.AboutYou(entries, 8, out total);
            Assert.AreEqual(2, total);
            CollectionAssert.AreEqual(new[] { "The user has a cat.", "The user likes jazz." }, list);

            ProfileMemories.AboutYou(entries, 1, out total);
            Assert.AreEqual(2, total, "the total counts all of them");
            Assert.AreEqual("She remembers 2 things about you.", ProfileMemories.Summary(2));
            Assert.AreEqual("She remembers 1 thing about you.", ProfileMemories.Summary(1));
            StringAssert.Contains("anything", ProfileMemories.Summary(0));
        }
    }
}
