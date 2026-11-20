using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-134: the dated relationship timeline is local, deduplicated and persona-scoped.</summary>
    public sealed class RelationshipTimelineTests
    {
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
                    // The test cleanup must not hide the assertion result.
                }
            }

            _cleanup.Clear();
        }

        string TempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "happi-timeline-" + Guid.NewGuid().ToString("N") + ".json");
            _cleanup.Add(path);
            return path;
        }

        [Test]
        public void AddSession_WritesDatedEntries_AndReloadKeepsThem()
        {
            var path = TempFile();
            var timeline = RelationshipTimeline.Load(path);
            var now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

            var added = timeline.AddSession(new[]
            {
                "You told her about your sister and she promised to ask again.",
                "You both laughed about the broken coffee machine.",
            }, false, now);

            Assert.AreEqual(2, added);
            Assert.AreEqual(2, timeline.Count);
            var reloaded = RelationshipTimeline.Load(path);
            Assert.AreEqual(2, reloaded.Count);
            Assert.AreEqual("You told her about your sister and she promised to ask again.", reloaded.Page(1)[0].summary);
            Assert.AreEqual(4, reloaded.Page(1)[0].importance);
            Assert.AreEqual(now.ToString("o"), reloaded.Page(1)[0].happenedAtUtc);
        }

        [Test]
        public void AddSession_SkipsDuplicateSummaries()
        {
            var path = TempFile();
            var timeline = RelationshipTimeline.Load(path);
            var now = DateTime.UtcNow;
            Assert.AreEqual(1, timeline.AddSession(new[] { "A promise about the weekend." }, false, now));
            Assert.AreEqual(0, timeline.AddSession(new[] { "A promise about the weekend." }, false, now));
            Assert.AreEqual(1, timeline.Count);
        }

        [Test]
        public void Page_NewestFirst_AndClear()
        {
            var path = TempFile();
            var timeline = RelationshipTimeline.Load(path);
            timeline.AddSession(new[] { "The user's sister is called Anna." }, false, new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc));
            timeline.AddSession(new[] { "The user promised to visit on Sunday." }, true, new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc));

            var page = timeline.Page(10);
            Assert.AreEqual(2, page.Count);
            Assert.AreEqual("The user promised to visit on Sunday.", page[0].summary);
            Assert.IsTrue(page[0].isPrivate);
            Assert.AreEqual("The user's sister is called Anna.", page[1].summary);
            Assert.AreEqual(2, timeline.Clear());
            Assert.AreEqual(0, RelationshipTimeline.Load(path).Count);
        }

        [Test]
        public void AddSession_DropsSceneState_AndKeepsTheDurablePart()
        {
            var path = TempFile();
            var timeline = RelationshipTimeline.Load(path);
            var added = timeline.AddSession(new[]
            {
                "She trembled when he touched her neck.",
                "The user's sister is called Anna.",
                "The room went quiet after the kiss.",
                "She does not want her neck touched without warning.",
            }, false, DateTime.UtcNow);

            Assert.AreEqual(2, added);
            var page = timeline.Page(10);
            Assert.AreEqual(2, page.Count);
            var kinds = new List<string>();
            for (var i = 0; i < page.Count; i++)
                kinds.Add(page[i].kind);
            CollectionAssert.Contains(kinds, MemoryDurability.Boundary);
            CollectionAssert.Contains(kinds, MemoryDurability.Fact);
            for (var i = 0; i < page.Count; i++)
                Assert.IsFalse(string.IsNullOrEmpty(page[i].why));
        }

        [Test]
        public void AddSession_SkipsAnEntryAlreadyCoveredByAStoredFact()
        {
            var path = TempFile();
            var memory = MemoryStore.Load(Path.Combine(Path.GetTempPath(), "memory-" + Guid.NewGuid().ToString("N") + ".json"));
            _cleanup.Add(memory.Path);
            memory.AddFact("The user's sister is called Anna.", false);
            var timeline = RelationshipTimeline.Load(path);

            var added = timeline.AddSession(new[] { "The user's sister is called Anna." }, false, DateTime.UtcNow, memory);
            Assert.AreEqual(0, added);
            Assert.AreEqual(0, timeline.Count);
        }

        [Test]
        public void AddSession_MergesParaphrasesIntoOneEntry()
        {
            var path = TempFile();
            var timeline = RelationshipTimeline.Load(path);
            timeline.AddSession(new[] { "The user's sister is called Anna." }, false, DateTime.UtcNow);
            var added = timeline.AddSession(new[] { "The user's sister Anna was mentioned." }, false, DateTime.UtcNow);

            Assert.AreEqual(0, added);
            Assert.AreEqual(1, timeline.Count);
            Assert.AreEqual(2, timeline.Page(1)[0].evidenceCount);
            Assert.IsFalse(string.IsNullOrEmpty(timeline.Page(1)[0].lastSeenAtUtc));
        }

        [Test]
        public void TimelinePath_IsBesideTheMatchingMemoryFile()
        {
            var memory = Path.Combine(Path.GetTempPath(), "memory-custom-6.json");
            Assert.AreEqual(
                Path.Combine(Path.GetTempPath(), "timeline-custom-6.json"),
                MemoryPaths.TimelineFileForMemoryPath(memory));
            Assert.AreEqual(
                Path.Combine(Path.GetTempPath(), "timeline.json"),
                MemoryPaths.TimelineFileForMemoryPath(Path.Combine(Path.GetTempPath(), "memory.json")));
        }

        [Test]
        public void InventoryTreatsTimelineAsPrivateEncryptedData()
        {
            Assert.IsTrue(LocalDataInventory.IsTimelineName("timeline.json"));
            Assert.IsTrue(LocalDataInventory.IsTimelineName("timeline-custom-6.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("timeline-custom-6.json"));
            Assert.IsFalse(LocalDataInventory.IsTimelineName("memory-custom-6.json"));
        }
    }
}
