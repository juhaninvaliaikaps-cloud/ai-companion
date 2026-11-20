using System;
using System.IO;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-134 review: one timeline instance per persona, re-read when something else wrote the file.</summary>
    public sealed class SharedTimelineTests
    {
        string _dir;
        MemoryStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-shared-timeline-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _store = MemoryStore.Load(Path.Combine(_dir, "memory-lola.json"));
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
        public void ForStore_ReturnsTheSameInstance_WhileTheFileIsUnchanged()
        {
            var first = RelationshipTimeline.ForStore(_store);
            first.AddSession(new[] { "The user promised to visit on Sunday." }, false, DateTime.UtcNow);

            Assert.AreSame(first, RelationshipTimeline.ForStore(_store));
        }

        [Test]
        public void TwoRequests_CanNoLongerSaveOverANewSession()
        {
            // Before: a request loaded its own copy, the session ended and added an entry, then
            // the request's MarkRecalled saved its older copy over it.
            var request = RelationshipTimeline.ForStore(_store);
            request.AddSession(new[] { "The user planned a trip to Lapland in March." }, false, DateTime.UtcNow);

            var session = RelationshipTimeline.ForStore(_store);
            session.AddSession(new[] { "The user's sister moved to Oulu last spring." }, false, DateTime.UtcNow);
            request.MarkRecalled(request.Page(1));

            Assert.AreEqual(2, RelationshipTimeline.Load(RelationshipTimeline.ForStore(_store).Path).Count);
        }

        [Test]
        public void AFileWrittenElsewhere_IsReadAgain()
        {
            var shared = RelationshipTimeline.ForStore(_store);
            Assert.AreEqual(0, shared.Count);

            var other = RelationshipTimeline.Load(MemoryPaths.TimelineFileForMemoryPath(_store.Path));
            other.AddSession(new[] { "The user's father suddenly died last year." }, false, DateTime.UtcNow);

            Assert.AreEqual(1, RelationshipTimeline.ForStore(_store).Count);
        }

        [Test]
        public void ADeletedFile_StartsEmpty()
        {
            var shared = RelationshipTimeline.ForStore(_store);
            shared.AddSession(new[] { "The user promised to visit on Sunday." }, false, DateTime.UtcNow);
            File.Delete(shared.Path);

            Assert.AreEqual(0, RelationshipTimeline.ForStore(_store).Count);
        }
    }
}
