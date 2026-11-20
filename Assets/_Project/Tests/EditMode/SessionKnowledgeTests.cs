using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-141: the session summary sees what she already remembers, corrects an entry instead
    /// of adding a contradicting one, closes promises that were kept, and writes real dates.
    /// </summary>
    public sealed class SessionKnowledgeTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);
        readonly List<string> _cleanup = new List<string>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _cleanup.Count; i++)
            {
                try
                {
                    if (File.Exists(_cleanup[i]))
                        File.Delete(_cleanup[i]);
                }
                catch (Exception)
                {
                    // Cleanup must not hide the assertion result.
                }
            }

            _cleanup.Clear();
        }

        RelationshipTimeline Timeline()
        {
            var path = Path.Combine(Path.GetTempPath(), "happi-knowledge-" + Guid.NewGuid().ToString("N") + ".json");
            _cleanup.Add(path);
            var timeline = RelationshipTimeline.Load(path);
            timeline.AddSession(new[]
            {
                "The user's sister Anna lives in Tampere.",
                "The user promised to take her to the cinema next weekend.",
                "The user's dog Ruffe turned ten years old.",
            }, false, Now.AddDays(-10));
            timeline.AddSession(new[] { "The user told her a private story about his first love." }, true, Now.AddDays(-5));
            return timeline;
        }

        static TimelineEntry Find(RelationshipTimeline timeline, string start)
        {
            foreach (var entry in timeline.Entries)
            {
                if (entry.summary.StartsWith(start, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        // ---- What the summary is shown --------------------------------------------------------

        [Test]
        public void Known_IsTheRelatedEntries_ThenOpenPromises()
        {
            var timeline = Timeline();
            var known = SessionKnowledge.For(timeline.Entries, "User: Anna moved to Helsinki last month.\nHer: Oh really?", true, Now);
            Assert.GreaterOrEqual(known.Count, 2);
            Assert.AreEqual("m1", known[0].Label);
            StringAssert.StartsWith("The user's sister Anna", known[0].Entry.summary, "the entry the talk is about comes first");
            var promise = false;
            foreach (var k in known)
                promise |= k.Entry.kind == MemoryDurability.Promise;
            Assert.IsTrue(promise, "an open promise is shown even when the talk did not name it");
        }

        [Test]
        public void Known_ACloudSummaryIsNeverShownPrivateEntries()
        {
            var timeline = Timeline();
            var cloud = SessionKnowledge.For(timeline.Entries, "User: remember my first love story?", false, Now);
            foreach (var k in cloud)
                Assert.IsFalse(k.Entry.isPrivate);
            var local = SessionKnowledge.For(timeline.Entries, "User: remember my first love story?", true, Now);
            var sawPrivate = false;
            foreach (var k in local)
                sawPrivate |= k.Entry.isPrivate;
            Assert.IsTrue(sawPrivate);
        }

        [Test]
        public void Known_IgnoresTheAlreadyRaisedRule()
        {
            var timeline = Timeline();
            Find(timeline, "The user's sister").lastRecalledAtUtc = Now.AddMinutes(-1).ToString("o");
            var known = SessionKnowledge.For(timeline.Entries, "User: Anna is fine.", true, Now);
            StringAssert.StartsWith("The user's sister Anna", known[0].Entry.summary, "the summary must see what the talk was about");
        }

        [Test]
        public void PromptBlock_HasTodayIdsAndTheRules()
        {
            var timeline = Timeline();
            var known = SessionKnowledge.For(timeline.Entries, "User: Anna moved.", true, Now);
            var block = SessionKnowledge.PromptBlock(known, new DateTime(2026, 11, 20));
            StringAssert.Contains("Today is Friday 20.11.2026", block);
            StringAssert.Contains("on Sunday 22.11.2026", block);
            StringAssert.Contains("[m1] (", block);
            StringAssert.Contains("update m1: ", block);
            StringAssert.Contains("resolved m2", block);

            var empty = SessionKnowledge.PromptBlock(new List<KnownMemory>(), new DateTime(2026, 11, 20));
            StringAssert.Contains("Today is Friday", empty);
            Assert.IsFalse(empty.Contains("already remembers"));
        }

        // ---- What the summary answers -----------------------------------------------------------

        [Test]
        public void Changes_AreParsed_AndNeverStoredAsNewMemories()
        {
            var timeline = Timeline();
            var known = SessionKnowledge.For(timeline.Entries, "User: Anna moved to Helsinki. We went to the cinema yesterday.", true, Now);
            var labelOf = new Dictionary<string, string>();
            foreach (var k in known)
                labelOf[k.Entry.summary.Substring(0, 12)] = k.Label;
            var sister = labelOf["The user's s"];
            var promise = labelOf["The user pro"];

            var reply = "[fact|3|90] The user started a new job at a bakery.\n"
                        + "- Update " + sister.ToUpperInvariant() + ": The user's sister Anna moved from Tampere to Helsinki.\n"
                        + "resolved " + promise + ".\n"
                        + "update m99: The user's sister moved to the moon.\n";
            var updates = new List<MemoryUpdate>();
            var resolved = new List<string>();
            SessionKnowledge.ParseChanges(reply, known, updates, resolved);

            Assert.AreEqual(1, updates.Count, "m99 was never shown, so it is ignored");
            Assert.AreEqual(Find(timeline, "The user's sister").id, updates[0].EntryId);
            Assert.AreEqual("The user's sister Anna moved from Tampere to Helsinki.", updates[0].Memory.Text);
            Assert.AreEqual(1, resolved.Count);
            Assert.AreEqual(Find(timeline, "The user promised").id, resolved[0]);

            var memories = SessionMemories.ParseDetailed(reply);
            Assert.AreEqual(1, memories.Count, "only the bakery line is a new memory");
            StringAssert.Contains("bakery", memories[0].Text);
        }

        [Test]
        public void ApplySession_CorrectsInsteadOfDuplicating_AndClosesThePromise()
        {
            var timeline = Timeline();
            var sister = Find(timeline, "The user's sister");
            var promise = Find(timeline, "The user promised");
            var count = timeline.Count;
            var capture = new SessionCapture();
            capture.Updates.Add(new MemoryUpdate
            {
                EntryId = sister.id,
                Memory = SessionMemories.ParseLineForUpdate("The user's sister Anna moved from Tampere to Helsinki."),
            });
            capture.Resolved.Add(promise.id);

            var result = timeline.ApplySession(capture, false, Now);

            Assert.AreEqual(1, result.Updated);
            Assert.AreEqual(1, result.Resolved);
            Assert.AreEqual(count, timeline.Count, "a correction is not a new entry");
            Assert.AreEqual("The user's sister Anna moved from Tampere to Helsinki.", sister.summary);
            Assert.AreEqual("The user's sister Anna lives in Tampere.", sister.previousSummary);
            Assert.AreEqual(2, sister.evidenceCount);
            Assert.IsFalse(string.IsNullOrEmpty(sister.updatedAtUtc));
            Assert.IsTrue(promise.resolved);
            Assert.IsFalse(string.IsNullOrEmpty(promise.resolvedAtUtc));

            var reloaded = RelationshipTimeline.Load(timeline.Path);
            Assert.AreEqual("The user's sister Anna moved from Tampere to Helsinki.", Find(reloaded, "The user's sister").summary);
            Assert.AreEqual(0, reloaded.Search("cinema next weekend", 3, true).Count, "a kept promise is not recalled any more");
        }

        [Test]
        public void ApplySession_RejectsAVagueCorrection_AndAPrivateOneMakesTheEntryPrivate()
        {
            var timeline = Timeline();
            var dog = Find(timeline, "The user's dog");
            var capture = new SessionCapture();
            capture.Updates.Add(new MemoryUpdate { EntryId = dog.id, Memory = SessionMemories.ParseLineForUpdate("The user shared something deeply personal.") });
            Assert.AreEqual(0, timeline.ApplySession(capture, false, Now).Updated);
            Assert.AreEqual("The user's dog Ruffe turned ten years old.", dog.summary, "a vague line never replaces a concrete one");

            var privateFix = new SessionCapture();
            privateFix.Updates.Add(new MemoryUpdate { EntryId = dog.id, Memory = SessionMemories.ParseLineForUpdate("The user's dog Ruffe turned eleven years old.") });
            Assert.AreEqual(1, timeline.ApplySession(privateFix, true, Now).Updated);
            Assert.IsTrue(dog.isPrivate, "a correction from a private session keeps the entry private from now on");
        }

        [Test]
        public void ApplySession_AnUnknownIdChangesNothing()
        {
            var timeline = Timeline();
            var capture = new SessionCapture();
            capture.Resolved.Add("no-such-id");
            capture.Updates.Add(new MemoryUpdate { EntryId = "no-such-id", Memory = SessionMemories.ParseLineForUpdate("The user's cat is called Mirri.") });
            var result = timeline.ApplySession(capture, false, Now);
            Assert.AreEqual(0, result.Updated);
            Assert.AreEqual(0, result.Resolved);
        }

        [Test]
        public void BuildRequest_CarriesTheKnowledgeBlock()
        {
            var request = SessionMemories.BuildRequest("User: hi", "Mira", true, false, false, "Today is Friday 20.11.2026.");
            StringAssert.Contains("Today is Friday 20.11.2026.", request.SystemPrompt);
            Assert.IsTrue(request.NoMemory);
            Assert.IsTrue(request.BlockCloud);
        }
    }
}
