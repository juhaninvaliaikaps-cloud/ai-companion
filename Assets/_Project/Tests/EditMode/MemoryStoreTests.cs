using System;
using System.IO;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class MemoryStoreTests
    {
        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ai-companion-memory-" + Guid.NewGuid().ToString("N"));
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

        [Test]
        public void AddFact_WritesAFileAndReloadsIt()
        {
            var store = MemoryStore.Load(_path);
            var entry = store.AddFact("Likes coffee with oat milk", privateMode: false);

            Assert.IsNotNull(entry);
            Assert.IsTrue(File.Exists(_path));

            var reloaded = MemoryStore.Load(_path);
            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual("Likes coffee with oat milk", reloaded.Entries[0].text);
            Assert.AreEqual(MemoryKind.Fact, reloaded.Entries[0].kind);
            Assert.IsFalse(reloaded.Entries[0].isPrivate);
        }

        [Test]
        public void Search_FindsByKeywordAndIgnoresStopWords()
        {
            var store = MemoryStore.Load(_path);
            store.AddFact("Works as a game developer", privateMode: false);
            store.AddFact("Likes coffee with oat milk", privateMode: false);

            var hits = store.Search("what coffee do you like");

            Assert.AreEqual(1, hits.Count);
            Assert.IsTrue(hits[0].text.Contains("coffee"));
            Assert.AreEqual(0, store.Search("the and what when").Count);
            Assert.AreEqual(0, store.Search(string.Empty).Count);
        }

        [Test]
        public void Search_ExcludesPrivateMemoriesWhenPrivateIsNotAllowed()
        {
            var store = MemoryStore.Load(_path);
            store.AddFact("Public fact about cats", privateMode: false);
            store.AddFact("Private fact about cats", privateMode: true);

            var cloudHits = store.Search("cats", includePrivate: false);
            var localHits = store.Search("cats", includePrivate: true);

            Assert.AreEqual(1, cloudHits.Count);
            Assert.IsFalse(cloudHits[0].isPrivate);
            Assert.AreEqual(2, localHits.Count);
        }

        [Test]
        public void Search_PrefersSummariesOnATiedToken()
        {
            var store = MemoryStore.Load(_path);
            store.AddFact("talked about the mountain trip", privateMode: false);
            store.AddSummary("long talk about the mountain trip", privateMode: false);

            var hits = store.Search("mountain");

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(MemoryKind.Summary, hits[0].kind, "A summary should outrank a fact for the same keyword.");
        }

        [Test]
        public void Delete_RemovesTheEntryAndPersists()
        {
            var store = MemoryStore.Load(_path);
            var entry = store.AddFact("Temporary fact", privateMode: false);

            Assert.IsTrue(store.Delete(entry.id));
            Assert.IsFalse(store.Delete(entry.id));
            Assert.AreEqual(0, MemoryStore.Load(_path).Count);
        }

        [Test]
        public void Load_MissingFile_IsAnEmptyStore()
        {
            var store = MemoryStore.Load(Path.Combine(_dir, "missing.json"));

            Assert.AreEqual(0, store.Count);
            Assert.IsEmpty(store.Search("anything"));
        }

        [Test]
        public void Search_CapsTheNumberOfResults()
        {
            var store = MemoryStore.Load(_path);
            for (var i = 0; i < 12; i++)
                store.AddFact("parrot fact " + i, privateMode: false);

            Assert.AreEqual(MemoryStore.MaxSearchResults, store.Search("parrot").Count);
            Assert.AreEqual(3, store.Search("parrot", 3).Count);
        }

        [Test]
        public void Add_IsIdempotentForTheSameText()
        {
            var store = MemoryStore.Load(_path);
            var first = store.AddFact("The user's name is Jussi", privateMode: false);
            var second = store.AddFact("  the user's name is jussi  ", privateMode: false);

            Assert.AreEqual(1, store.Count);
            Assert.AreEqual(first.id, second.id);
        }

        [Test]
        public void Add_UpgradesAnExistingEntryToCore()
        {
            var store = MemoryStore.Load(_path);
            store.AddFact("The user's name is Jussi", privateMode: false);

            store.AddFact("The user's name is Jussi", privateMode: false, core: true);

            Assert.IsTrue(store.Entries[0].isCore);
        }

        [Test]
        public void SearchFor_AlwaysIncludesCoreFacts()
        {
            var store = MemoryStore.Load(_path);
            store.AddFact("Likes coffee with oat milk", privateMode: false);
            store.AddFact("The user's name is Jussi", privateMode: false, core: true);

            var hits = store.SearchFor("what is the weather like");

            Assert.IsTrue(hits.Count >= 1);
            Assert.IsTrue(hits[0].isCore);
            StringAssert.Contains("Jussi", hits[0].text);
        }

        [Test]
        public void SearchFor_FallsBackToRecentEntriesWhenNothingMatches()
        {
            var store = MemoryStore.Load(_path);
            store.AddFact("Likes coffee with oat milk", privateMode: false);

            var hits = store.SearchFor("zzz");

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual("Likes coffee with oat milk", hits[0].text);
        }
    }
}
