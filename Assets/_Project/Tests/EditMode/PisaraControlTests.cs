using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-117 ("Pisara") control: the per-part switches, the user's relationship correction,
    /// the recent reasons under Mood, and new threads in her world.
    /// </summary>
    public class PisaraControlTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 14, 18, 0, 0, DateTimeKind.Utc);

        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            PisaraSwitches.Reset();
            _dir = Path.Combine(Path.GetTempPath(), "happi-pisara-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "memory.json");
        }

        [TearDown]
        public void TearDown()
        {
            PisaraSwitches.Reset();
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        // --- Switches

        [Test]
        public void Switches_AreOnByDefault_AndConfigureSetsEach()
        {
            Assert.IsTrue(PisaraSwitches.Relationship);
            Assert.IsTrue(PisaraSwitches.AwayEvents);
            Assert.IsTrue(PisaraSwitches.World);

            PisaraSwitches.Configure(false, true, false);
            Assert.IsFalse(PisaraSwitches.Relationship);
            Assert.IsTrue(PisaraSwitches.AwayEvents);
            Assert.IsFalse(PisaraSwitches.World);
        }

        [Test]
        public void Settings_SwitchesDefaultOn_AndRoundTrip()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.IsTrue(fresh.PisaraRelationship && fresh.PisaraAwayEvents && fresh.PisaraWorld);

            var old = CompanionSettingsJson.Parse("{\n  \"version\": 1,\n  \"showMoodDebug\": true\n}\n", SettingsLegacyPrefs.None);
            Assert.IsTrue(old.PisaraRelationship && old.PisaraAwayEvents && old.PisaraWorld, "an older file keeps every part on");

            fresh.PisaraRelationship = false;
            fresh.PisaraWorld = false;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None);
            Assert.IsFalse(back.PisaraRelationship);
            Assert.IsTrue(back.PisaraAwayEvents);
            Assert.IsFalse(back.PisaraWorld);
        }

        // --- Correction

        static CharacterSheet ReadAsFriend()
        {
            var sheet = CharacterSheet.Neutral("She is my stepsister.");
            sheet.source = CharacterSheet.SourceModel;
            sheet.relationship.role = RelationshipRoles.CloseFriend;
            sheet.relationship.label = "friend";
            sheet.relationship.affection = 30f;
            sheet.relationship.familiarity = 60f;
            sheet.relationship.sharedHistory = true;
            return sheet;
        }

        [Test]
        public void Correction_SetsAndLocksTheRelationship()
        {
            var sheet = ReadAsFriend();
            PisaraCorrections.SetRelationship(sheet, RelationshipRoles.Family, "stepsister", 20f, 75f, true);

            Assert.IsTrue(sheet.relationshipLocked);
            Assert.AreEqual(RelationshipRoles.Family, sheet.relationship.role);
            Assert.AreEqual("stepsister", sheet.relationship.label);
            Assert.AreEqual(20f, sheet.relationship.affection, 0.001f);
            Assert.AreEqual(75f, sheet.relationship.familiarity, 0.001f);
            Assert.AreEqual(PisaraCorrections.CorrectedNote, PisaraCorrections.Note(sheet));
        }

        [Test]
        public void Correction_KeepsTheRoleLimits()
        {
            var sheet = ReadAsFriend();
            PisaraCorrections.SetRelationship(sheet, RelationshipRoles.Family, "sister", 500f, 10f, true);
            Assert.AreEqual(100f, sheet.relationship.affection, 0.001f);
            Assert.AreEqual(RelationshipMath.RoleFamiliarityFloor(RelationshipRoles.Family), sheet.relationship.familiarity, 0.001f,
                "family knows her at least this well");

            PisaraCorrections.SetRelationship(sheet, RelationshipRoles.Stranger, string.Empty, 0f, 90f, true);
            Assert.LessOrEqual(sheet.relationship.familiarity, 15f, "a stranger stays a stranger");
            Assert.IsFalse(sheet.relationship.sharedHistory, "a stranger never shares a past");
        }

        [Test]
        public void Correction_SurvivesANewReading()
        {
            var corrected = ReadAsFriend();
            PisaraCorrections.SetRelationship(corrected, RelationshipRoles.Family, "stepsister", 20f, 75f, true);

            var fresh = ReadAsFriend();
            fresh.temperament.cheerful = 0.9f;
            PisaraCorrections.CarryOver(corrected, fresh);

            Assert.IsTrue(fresh.relationshipLocked);
            Assert.AreEqual(RelationshipRoles.Family, fresh.relationship.role);
            Assert.AreEqual("stepsister", fresh.relationship.label);
            Assert.AreEqual(0.9f, fresh.temperament.cheerful, 0.001f, "the rest of the new reading is kept");
        }

        [Test]
        public void Correction_IsNotCarriedOnceUnlocked()
        {
            var corrected = ReadAsFriend();
            PisaraCorrections.SetRelationship(corrected, RelationshipRoles.Family, "stepsister", 20f, 75f, true);
            PisaraCorrections.Unlock(corrected);

            var fresh = ReadAsFriend();
            PisaraCorrections.CarryOver(corrected, fresh);
            Assert.IsFalse(fresh.relationshipLocked);
            Assert.AreEqual(RelationshipRoles.CloseFriend, fresh.relationship.role);
            Assert.AreEqual(string.Empty, PisaraCorrections.Note(fresh));
        }

        [Test]
        public void Lock_IsSaved_ButTheModelCannotSetIt()
        {
            var sheet = ReadAsFriend();
            PisaraCorrections.SetRelationship(sheet, RelationshipRoles.Family, "stepsister", 20f, 75f, true);
            var saved = CharacterSheet.FromJson(sheet.ToJson());
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.relationshipLocked, "a saved correction is still the user's after a restart");

            var parsed = CharacterSheet.Parse("{ \"relationship\": { \"role\": \"friend\" }, \"relationshipLocked\": true }");
            Assert.IsNotNull(parsed);
            Assert.IsFalse(parsed.relationshipLocked, "only the user locks a relationship");
        }

        [Test]
        public void NextRole_CyclesAndWraps()
        {
            Assert.AreEqual(RelationshipRoles.All[1], PisaraCorrections.NextRole(RelationshipRoles.All[0]));
            Assert.AreEqual(RelationshipRoles.All[0], PisaraCorrections.NextRole(RelationshipRoles.All[RelationshipRoles.All.Length - 1]));
            Assert.AreEqual(RelationshipRoles.Acquaintance, PisaraCorrections.NextRole("nonsense"), "unknown is a stranger, then the next one");
        }

        // --- Recent reasons

        [Test]
        public void History_RecordsReasons_NewestFirst_AndSkipsNeutral()
        {
            var history = new RelationshipHistory();
            var insult = RelationshipMath.Land(LandingEvent.Insulted, 0.5f, 0f, 0f);
            var neutral = RelationshipMath.Land(LandingEvent.Neutral, 0.5f, 0f, 0f);
            var compliment = RelationshipMath.Land(LandingEvent.Complimented, 0.5f, 0f, 0f);

            StringAssert.StartsWith("An insult stung: affection -", history.Record(insult));
            Assert.AreEqual(string.Empty, history.Record(neutral));
            history.Record(compliment);

            Assert.AreEqual(2, history.Count);
            StringAssert.StartsWith("A compliment landed well", history.Line(0));
            StringAssert.StartsWith("An insult stung", history.Line(1));
            Assert.AreEqual(string.Empty, history.Line(2));
        }

        [Test]
        public void History_KeepsOnlyTheLastFew()
        {
            var history = new RelationshipHistory();
            for (var i = 0; i < RelationshipHistory.Capacity + 3; i++)
                history.Record(RelationshipMath.Land(LandingEvent.Thanked, 0.5f, 0f, 0f));
            history.Record(RelationshipMath.Land(LandingEvent.Dismissed, 0.5f, 0f, 0f));

            Assert.AreEqual(RelationshipHistory.Capacity, history.Count);
            StringAssert.StartsWith("She felt brushed off", history.Line(0));
            history.Clear();
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void MoodCard_ShowsStandingAndReasons()
        {
            var history = new RelationshipHistory();
            history.Record(RelationshipMath.Land(LandingEvent.Insulted, 0.5f, 0f, 0f));

            var text = RelationshipCard.Describe(-30f, 5f, history, true);
            StringAssert.Contains("she is at odds with you", text);
            StringAssert.Contains("you have only just met", text);
            StringAssert.Contains("Recently:", text);
            StringAssert.Contains("An insult stung", text);

            var off = RelationshipCard.Describe(-30f, 5f, history, false);
            StringAssert.Contains("switched off", off);
            StringAssert.DoesNotContain("Recently:", off);

            StringAssert.DoesNotContain("Recently:", RelationshipCard.Describe(0f, 0f, new RelationshipHistory(), true));
        }

        [Test]
        public void MoodCard_PhrasesFollowThePromptBands()
        {
            Assert.AreEqual("she can't stand you right now", RelationshipCard.Feeling(-80f));
            Assert.AreEqual("she has no strong feelings about you yet", RelationshipCard.Feeling(0f));
            Assert.AreEqual("she likes you", RelationshipCard.Feeling(40f));
            Assert.AreEqual("she adores you", RelationshipCard.Feeling(90f));
            Assert.AreEqual("you know each other very well", RelationshipCard.Knowing(80f));
        }

        // --- New threads

        const string Reply = "Summary done.\n```json\n{ \"summary\": \"We talked about boots.\", \"newThread\": "
            + "{ \"text\": \"She signed up for a pottery class on Tuesday.\", \"goodOutcome\": \"Her first bowl came out fine.\", "
            + "\"badOutcome\": \"The bowl collapsed on the wheel.\" } }\n```";

        [Test]
        public void NewThread_IsReadFromTheSessionEndReply()
        {
            var thread = WorldThreads.TryParse(Reply);
            Assert.IsNotNull(thread);
            Assert.AreEqual("She signed up for a pottery class on Tuesday.", thread.text);
            Assert.AreEqual("The bowl collapsed on the wheel.", thread.badOutcome);

            Assert.IsNull(WorldThreads.TryParse("{ \"summary\": \"nothing new\" }"), "no thread is fine");
            Assert.IsNull(WorldThreads.TryParse("{ \"newThread\": { \"text\": \"Only a text.\" } }"), "a thread needs an outcome");
            Assert.IsNull(WorldThreads.TryParse("not json"));
            Assert.IsNull(WorldThreads.TryParse(null));
        }

        [Test]
        public void NewThread_IsStoredAsHerLife()
        {
            var store = MemoryStore.Load(_path);
            var entry = WorldThreads.Add(store, WorldThreads.TryParse(Reply), Now, false);

            Assert.IsNotNull(entry);
            Assert.AreEqual(MemoryOrigins.Life, entry.origin);
            Assert.IsTrue(entry.isThread);
            Assert.AreEqual(MemoryHorizons.Long, entry.horizon);
            Assert.AreEqual(1, WorldThreads.OpenCount(MemoryStore.Load(_path)), "it is saved");
            Assert.IsNull(WorldThreads.Add(store, WorldThreads.TryParse(Reply), Now, false), "the same thread is not added twice");
        }

        [Test]
        public void NewThread_StopsAtMaxOpen()
        {
            var store = MemoryStore.Load(_path);
            for (var i = 0; i < WorldThreads.MaxOpen; i++)
            {
                var added = WorldThreads.Add(store, new NewThread { text = "Thread " + i + ".", goodOutcome = "It worked out." }, Now, false);
                Assert.IsNotNull(added);
            }

            Assert.IsNull(WorldThreads.Add(store, new NewThread { text = "One too many.", goodOutcome = "Fine." }, Now, false));
            Assert.AreEqual(WorldThreads.MaxOpen, WorldThreads.OpenCount(store));
        }

        [Test]
        public void NewThread_RespectsTheSwitches()
        {
            var store = MemoryStore.Load(_path);
            var thread = new NewThread { text = "A job interview on Monday.", goodOutcome = "She got the job." };

            PisaraSwitches.Configure(true, false, true);
            Assert.IsNull(WorldThreads.Add(store, thread, Now, false), "life while away is off");
            PisaraSwitches.Configure(true, true, false);
            Assert.IsNull(WorldThreads.Add(store, thread, Now, false), "her own world is off");
            Assert.AreEqual(0, store.Count);

            PisaraSwitches.Reset();
            Assert.IsNotNull(WorldThreads.Add(store, thread, Now, true));
            Assert.IsTrue(store.Entries[0].isPrivate, "a thread from a private session stays private");
        }
    }
}
