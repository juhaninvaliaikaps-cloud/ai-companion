using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-141: a stored fact the user corrects is replaced, not kept next to the new one.
    /// "My sister moved to Oulu" updates "The user's sister lives in Tampere".
    /// </summary>
    public sealed class FactCorrectionTests
    {
        string _dir;
        MemoryStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-fact-corrections-" + Guid.NewGuid().ToString("N"));
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

        int Capture(IChatProvider provider, string user, string reply, bool privateMode = false)
        {
            return MemoryCapture.CaptureAsync(provider, user, reply, privateMode, _store).GetAwaiter().GetResult();
        }

        [Test]
        public void ACorrection_ReplacesTheOldFact_AndKeepsWhatItSaid()
        {
            _store.AddFact("The user's sister lives in Tampere", false);
            var model = new Recording(ChatDefaults.GrokId, "[\"update f1: The user's sister moved to Oulu\"]");

            Capture(model, "My sister moved to Oulu last month", "Oh, that's a big move!");

            Assert.AreEqual(1, _store.Count, "no second, contradicting fact");
            Assert.AreEqual("The user's sister moved to Oulu", _store.Entries[0].text);
            Assert.AreEqual("The user's sister lives in Tampere", _store.Entries[0].previousText);
            Assert.IsNotEmpty(_store.Entries[0].updatedAtUtc);
            StringAssert.Contains("[f1] The user's sister lives in Tampere", model.LastRequest.SystemPrompt);
            Assert.IsTrue(model.LastRequest.NoMemory, "the request carries its own list");
        }

        [Test]
        public void ACorrectionAboutSomethingElse_IsRefused()
        {
            _store.AddFact("The user's sister lives in Tampere", false);
            var model = new Recording(ChatDefaults.GrokId, "[\"update f1: The user likes green tea\"]");

            Capture(model, "My sister moved, and I like green tea now", "Nice!");

            Assert.AreEqual("The user's sister lives in Tampere", _store.Entries[0].text);
        }

        [Test]
        public void AnUnknownId_ChangesNothing_AndIsNotStoredAsAFact()
        {
            _store.AddFact("The user's sister lives in Tampere", false);
            var model = new Recording(ChatDefaults.GrokId, "[\"update f7: The user's sister moved to Oulu\"]");

            Capture(model, "My sister moved to Oulu", "Oh!");

            Assert.AreEqual(1, _store.Count);
            Assert.AreEqual("The user's sister lives in Tampere", _store.Entries[0].text);
        }

        [Test]
        public void ACorrectionToAnExistingSentence_RemovesTheOldRow()
        {
            _store.AddFact("The user's sister lives in Tampere", false);
            _store.AddFact("The user's sister moved to Oulu", false);
            var model = new Recording(ChatDefaults.GrokId, "[\"update f1: The user's sister moved to Oulu\"]");

            // The Tampere fact matches more words (sister, live, Tampere), so it is f1.
            Capture(model, "My sister does not live in Tampere anymore", "Got it.");

            Assert.AreEqual(1, _store.Count);
            Assert.AreEqual("The user's sister moved to Oulu", _store.Entries[0].text);
        }

        [Test]
        public void APrivateFact_IsNeverListedToTheCloud()
        {
            _store.AddFact("The user's sister lives in Tampere", true);
            var cloud = new Recording(ChatDefaults.GrokId, "[]");

            Capture(cloud, "My sister moved to Oulu", "Oh!");

            Assert.IsFalse(cloud.LastRequest.SystemPrompt.Contains("Tampere"));
        }

        [Test]
        public void ACorrectionInPrivateMode_MakesTheFactPrivate()
        {
            _store.AddFact("The user's sister lives in Tampere", false);
            var local = new Recording(ChatDefaults.LocalId, "[\"update f1: The user's sister moved to Oulu\"]");

            Capture(local, "My sister moved to Oulu", "Oh!", privateMode: true);

            Assert.IsTrue(_store.Entries[0].isPrivate);
        }

        [Test]
        public void HerOwnLife_IsNotListed()
        {
            _store.AddEntry(new MemoryEntry { text = "Her sister lives in Tampere", origin = MemoryOrigins.Seeded });
            var model = new Recording(ChatDefaults.GrokId, "[]");

            Capture(model, "Does your sister still live in Tampere?", "She does!");

            Assert.IsFalse(model.LastRequest.SystemPrompt.Contains("[f1]"));
            Assert.AreEqual(MemoryFactExtractor.SystemPrompt, model.LastRequest.SystemPrompt);
        }

        [Test]
        public void TheIdentityFact_IsAlwaysListed_SoANameChangeCanCorrectIt()
        {
            _store.AddFact("The user's name is Jussi", false, true);
            var model = new Recording(ChatDefaults.GrokId, "[\"update f1: The user's name is Juhani, Jussi for short\"]");

            Capture(model, "Actually, call me Juhani", "Juhani it is.");

            Assert.AreEqual("The user's name is Juhani, Jussi for short", _store.Entries[0].text);
            Assert.IsTrue(_store.Entries[0].isCore);
        }

        [Test]
        public void Parse_LeavesCorrectionsOutOfTheNewFacts()
        {
            var facts = MemoryFactExtractor.Parse("[\"update f1: The user's sister moved to Oulu\", \"The user has a cat\"]");

            Assert.AreEqual(1, facts.Count);
            Assert.AreEqual("The user has a cat", facts[0]);
            var updates = MemoryFactExtractor.ParseUpdates("[\"Update [f2]: The user works at Nokia\", \"update f2: again\", \"update f9: x y z\"]", 3);
            Assert.AreEqual(1, updates.Count);
            Assert.AreEqual(2, updates[0].Index);
            Assert.AreEqual("The user works at Nokia", updates[0].Text);
        }

        sealed class Recording : IChatProvider
        {
            readonly string _reply;

            public Recording(string providerId, string reply)
            {
                ProviderId = providerId;
                _reply = reply;
            }

            public string ProviderId { get; }
            public string DisplayName { get { return "Recording"; } }
            public bool IsConfigured { get { return true; } }
            public ChatRequest LastRequest { get; private set; }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(ChatResult.Success(_reply));
            }
        }
    }
}
