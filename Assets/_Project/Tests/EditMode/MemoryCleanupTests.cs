using System;
using System.IO;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-134: cleanup merges only very close duplicates and keeps a backup.</summary>
    public sealed class MemoryCleanupTests
    {
        string _dir;
        MemoryStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-memory-clean-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _store = MemoryStore.Load(Path.Combine(_dir, "memory.json"));
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
        public void NearDuplicateFacts_AreMerged_AndABackupIsWritten()
        {
            _store.AddFact("The user's sister is called Anna.", false);
            _store.AddFact("The user's sister Anna was mentioned.", false);

            Assert.AreEqual(1, _store.ConsolidateSimilar());
            Assert.AreEqual(1, _store.Count);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "memory.json.before-cleanup")));
        }

        [Test]
        public void DifferentFacts_AreNotMerged()
        {
            _store.AddFact("The user's sister is called Anna.", false);
            _store.AddFact("The user promised to visit on Sunday.", false);

            Assert.AreEqual(0, _store.ConsolidateSimilar());
            Assert.AreEqual(2, _store.Count);
        }
    }
}
