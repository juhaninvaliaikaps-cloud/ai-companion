using System;
using System.Collections.Generic;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-141: the timeline retrieval, measured. A fixed timeline and the questions a user
    /// would really ask; each probe names the entry that must come first, or that nothing may
    /// come back. Tuning the ranking later (weights, BM25, embeddings) has to keep these green.
    /// </summary>
    public sealed class TimelineSearchTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

        static TimelineEntry Entry(string id, string summary, string kind, int importance = 3, int daysAgo = 3,
            int evidence = 1, string source = "session", bool isPrivate = false, int confidence = 60)
        {
            return new TimelineEntry
            {
                id = id,
                summary = summary,
                title = summary,
                kind = kind,
                importance = importance,
                confidence = confidence,
                evidenceCount = evidence,
                source = source,
                isPrivate = isPrivate,
                happenedAtUtc = Now.AddDays(-daysAgo).ToString("o"),
                createdAtUtc = Now.AddDays(-daysAgo).ToString("o"),
            };
        }

        static List<TimelineEntry> Timeline()
        {
            return new List<TimelineEntry>
            {
                Entry("father", "The user told her his father is in hospital after a heart attack.", MemoryDurability.Event, 5, 12),
                Entry("sister", "The user's sister Anna moved to Helsinki for a new job.", MemoryDurability.Fact, 3, 20),
                Entry("interview", "The user was nervous about a job interview at Nokia on Friday.", MemoryDurability.OpenThread, 4, 2),
                Entry("promise", "The user promised to take her to the cinema next weekend.", MemoryDurability.Promise, 4, 5),
                Entry("pickles", "The user hates pickles.", MemoryDurability.Preference, 2, 30),
                Entry("boundary", "She does not want to be called childish; it hurts her.", MemoryDurability.Boundary, 5, 40),
                Entry("dog", "The user's dog Ruffe turned ten years old.", MemoryDurability.Event, 2, 8),
                Entry("trip", "The user planned a trip to Lapland in March.", MemoryDurability.Event, 3, 15),
                Entry("isa", "Käyttäjän isä asuu Tampereella.", MemoryDurability.Fact, 3, 10),
                Entry("secret", "The user told her a private story about his first love.", MemoryDurability.Event, 4, 6, isPrivate: true),
            };
        }

        static string Top(string latest, string previous = "", bool includePrivate = true)
        {
            var hits = TimelineSearch.Rank(Timeline(), latest, previous, includePrivate, Now);
            return hits.Count == 0 ? "(none)" : hits[0].Entry.id;
        }

        // ---- Acceptance probes --------------------------------------------------------------

        [TestCase("How is your dad doing?", "father")]
        [TestCase("Is my father still in the hospital?", "father")]
        [TestCase("I'm so nervous about the interview tomorrow", "interview")]
        [TestCase("What did I promise you?", "promise")]
        [TestCase("Should we go to the cinema?", "promise")]
        [TestCase("Want a pickle sandwich?", "pickles")]
        [TestCase("You're being so childish", "boundary")]
        [TestCase("Guess how old Ruffe is now", "dog")]
        [TestCase("Have you ever been to Lapland?", "trip")]
        [TestCase("Muistatko missä isäni asuu?", "isa")]
        public void Probe_FindsTheRightEntryFirst(string question, string expected)
        {
            Assert.AreEqual(expected, Top(question));
        }

        [TestCase("Hi! How are you today?")]
        [TestCase("That was about what you said, right?")]
        [TestCase("ok")]
        [TestCase("")]
        public void SmallTalk_FindsNothing(string question)
        {
            Assert.AreEqual("(none)", Top(question), "common words must not pull up a memory");
        }

        [Test]
        public void AConcept_FindsTheMember_ButADirectWordWins()
        {
            Assert.AreEqual("sister", Top("Tell me about my family in Helsinki"));
            // "family" alone reaches the family entries through the concept.
            var hits = TimelineSearch.Rank(Timeline(), "How is my family?", string.Empty, true, Now);
            var ids = new List<string>();
            foreach (var hit in hits)
                ids.Add(hit.Entry.id);
            CollectionAssert.Contains(ids, "father");
            CollectionAssert.Contains(ids, "sister");
        }

        [Test]
        public void TheMessageBefore_GivesAFollowUpItsTopic()
        {
            Assert.AreEqual("(none)", Top("Do you remember what I told you?"));
            Assert.AreEqual("trip", Top("Do you remember what I told you?", "I can't wait for Lapland"));
        }

        [Test]
        public void PrivateEntries_NeverReachACloudProvider()
        {
            Assert.AreEqual("secret", Top("Remember my first love?", "", true));
            Assert.AreNotEqual("secret", Top("Remember my first love?", "", false));
        }

        [Test]
        public void WholeWords_WorkDoesNotMatchHomework()
        {
            var hits = TimelineSearch.Rank(new List<TimelineEntry> { Entry("hw", "The user finished his homework early.", MemoryDurability.Event) },
                "How was work?", string.Empty, true, Now);
            Assert.AreEqual(0, hits.Count);
        }

        [Test]
        public void ShortConceptWords_AreSearched()
        {
            Assert.AreEqual("sister", Top("Did Anna like the new job?"));
            Assert.Contains("#anger", TimelineSearch.Words("I was so mad"));
            Assert.Contains("#joy", TimelineSearch.Words("pure joy"));
        }

        [Test]
        public void ResolvedEntries_AreNotRecalled()
        {
            var timeline = Timeline();
            timeline[3].resolved = true;
            Assert.AreNotEqual("promise", TimelineSearch.Rank(timeline, "What did I promise you?", "", true, Now).Count > 0
                ? TimelineSearch.Rank(timeline, "What did I promise you?", "", true, Now)[0].Entry.id
                : "(none)");
        }

        // ---- Already raised: no tic, no amnesia ----------------------------------------------

        [Test]
        public void JustRaised_StaysWhileTheTalkIsAboutIt_TaggedAsRaised()
        {
            var timeline = Timeline();
            timeline[0].lastRecalledAtUtc = Now.AddMinutes(-2).ToString("o");
            var hits = TimelineSearch.Rank(timeline, "My father is still in hospital, they said his heart is weak", "", true, Now);
            Assert.AreEqual("father", hits[0].Entry.id);
            Assert.IsTrue(hits[0].AlreadyRaised);
        }

        [Test]
        public void JustRaised_APassingWordDoesNotBringItBack()
        {
            var timeline = Timeline();
            timeline[0].lastRecalledAtUtc = Now.AddMinutes(-2).ToString("o");
            Assert.AreEqual("(none)", TimelineSearch.Rank(timeline, "The hospital food here is awful", "", true, Now).Count == 0
                ? "(none)"
                : TimelineSearch.Rank(timeline, "The hospital food here is awful", "", true, Now)[0].Entry.id);
        }

        [Test]
        public void RaisedLongAgo_IsNewAgain()
        {
            var timeline = Timeline();
            timeline[0].lastRecalledAtUtc = Now.AddHours(-3).ToString("o");
            var hits = TimelineSearch.Rank(timeline, "The hospital food here is awful", "", true, Now);
            Assert.AreEqual("father", hits[0].Entry.id);
            Assert.IsFalse(hits[0].AlreadyRaised);
        }

        // ---- Trust ------------------------------------------------------------------------

        [Test]
        public void Repetition_AndTheExplicitCommand_CountMoreThanSelfReportedConfidence()
        {
            var once = Entry("a", "x", MemoryDurability.Event);
            var often = Entry("b", "x", MemoryDurability.Event, evidence: 4);
            var asked = Entry("c", "x", MemoryDurability.Event, source: "explicit");
            var boasted = Entry("d", "x", MemoryDurability.Event, confidence: 100);
            Assert.Greater(TimelineSearch.Trust(often), TimelineSearch.Trust(once));
            Assert.Greater(TimelineSearch.Trust(asked), TimelineSearch.Trust(once));
            Assert.Greater(TimelineSearch.Trust(often), TimelineSearch.Trust(boasted), "three more mentions beat a model saying 100");
            Assert.LessOrEqual(TimelineSearch.Trust(boasted) / TimelineSearch.Trust(once), 1.1f);
        }

        // ---- Words ------------------------------------------------------------------------

        [Test]
        public void Stemming_MakesAWordMeetItself()
        {
            Assert.AreEqual(TimelineSearch.Stem("promise", false), TimelineSearch.Stem("promised", false));
            Assert.AreEqual(TimelineSearch.Stem("visit", false), TimelineSearch.Stem("visited", false));
            Assert.AreEqual(TimelineSearch.Stem("sister", false), TimelineSearch.Stem("sisters", false));
            Assert.AreEqual(TimelineSearch.Stem("family", false), TimelineSearch.Stem("families", false));
            Assert.AreEqual(TimelineSearch.Stem("sisko", true), TimelineSearch.Stem("siskosta", true));
            Assert.AreEqual(TimelineSearch.Stem("sisko", true), TimelineSearch.Stem("siskon", true));
        }

        [Test]
        public void Language_IsGuessedPerText()
        {
            Assert.IsTrue(TimelineSearch.LooksFinnish("muistatko mitä sanoin"));
            Assert.IsTrue(TimelineSearch.LooksFinnish("isä"));
            Assert.IsFalse(TimelineSearch.LooksFinnish("call me on sunday"));
            Assert.IsFalse(TimelineSearch.LooksFinnish("the user promised to visit"));
        }
    }
}
