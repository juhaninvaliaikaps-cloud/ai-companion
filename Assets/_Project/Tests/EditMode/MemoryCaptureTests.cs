using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Memory;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AICompanion.Tests.EditMode
{
    public class MemoryCaptureTests
    {
        string _dir;
        MemoryStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ai-companion-memory-" + Guid.NewGuid().ToString("N"));
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
        public void Parse_ReadsAFencedJsonArray()
        {
            var facts = MemoryFactExtractor.Parse("Here you go:\n```json\n[\"The user's name is Jussi\", \"Likes coffee\"]\n```");

            Assert.AreEqual(2, facts.Count);
            Assert.AreEqual("The user's name is Jussi", facts[0]);
            Assert.AreEqual("Likes coffee", facts[1]);
        }

        [Test]
        public void Parse_RejectsTextWithoutAnArray()
        {
            Assert.IsEmpty(MemoryFactExtractor.Parse("I could not find any facts."));
            Assert.IsEmpty(MemoryFactExtractor.Parse("[not quoted]"));
            Assert.IsEmpty(MemoryFactExtractor.Parse(string.Empty));
        }

        [Test]
        public void IsCoreFact_MatchesNamesButNotPreferences()
        {
            Assert.IsTrue(MemoryFactExtractor.IsCoreFact("The user's name is Jussi"));
            Assert.IsTrue(MemoryFactExtractor.IsCoreFact("Call me Jussi"));
            Assert.IsFalse(MemoryFactExtractor.IsCoreFact("Likes coffee with oat milk"));
        }

        [Test]
        public void Capture_StoresTheNameAndASecondSessionRemembersIt()
        {
            var extraction = new ScriptedProvider(ChatDefaults.GrokId, "[\"The user's name is Jussi\"]");
            LogAssert.Expect(UnityEngine.LogType.Log, "Memory: saved 1 fact(s)");

            var saved = MemoryCapture.CaptureAsync(extraction, "my name is Jussi", "Nice to meet you, Jussi.", false, _store)
                .GetAwaiter().GetResult();

            Assert.AreEqual(1, saved);
            Assert.AreEqual(1, _store.Count);
            Assert.IsTrue(_store.Entries[0].isCore, "A name must be stored as a core fact.");

            var chat = new MemoryChatProvider(new ScriptedProvider(ChatDefaults.GrokId, "Hi Jussi!"), _store);
            var enriched = chat.Enrich(ChatRequestFor("what is my name?"));

            StringAssert.Contains("Jussi", enriched.SystemPrompt);
        }

        [Test]
        public void Capture_DeduplicatesRepeatedFacts()
        {
            var first = new ScriptedProvider(ChatDefaults.GrokId, "[\"The user's name is Jussi\"]");
            LogAssert.Expect(UnityEngine.LogType.Log, "Memory: saved 1 fact(s)");
            MemoryCapture.CaptureAsync(first, "my name is Jussi", "ok", false, _store).GetAwaiter().GetResult();

            var second = new ScriptedProvider(ChatDefaults.GrokId, "[\"The user's name is Jussi\"]");
            var saved = MemoryCapture.CaptureAsync(second, "I said my name is Jussi", "ok", false, _store)
                .GetAwaiter().GetResult();

            Assert.AreEqual(0, saved);
            Assert.AreEqual(1, _store.Count);
        }

        [Test]
        public void Capture_RefusesToSendPrivateModeTextToACloudProvider()
        {
            var cloud = new ScriptedProvider(ChatDefaults.GrokId, "[\"Should never be used\"]");

            var saved = MemoryCapture.CaptureAsync(cloud, "my name is Jussi", "ok", privateMode: true, store: _store)
                .GetAwaiter().GetResult();

            Assert.AreEqual(0, saved);
            Assert.AreEqual(0, cloud.Calls);
            Assert.AreEqual(0, _store.Count);
        }

        [Test]
        public void Capture_KeepsPrivateFactsLocal()
        {
            var local = new ScriptedProvider(ChatDefaults.LocalId, "[\"The user's name is Jussi\"]");
            LogAssert.Expect(UnityEngine.LogType.Log, "Memory: saved 1 fact(s)");

            var saved = MemoryCapture.CaptureAsync(local, "my name is Jussi", "ok", privateMode: true, store: _store)
                .GetAwaiter().GetResult();

            Assert.AreEqual(1, saved);
            Assert.IsTrue(_store.Entries[0].isPrivate);

            var cloudPrompt = new MemoryChatProvider(new ScriptedProvider(ChatDefaults.GrokId, "x"), _store)
                .Enrich(ChatRequestFor("what is my name?")).SystemPrompt;
            var localPrompt = new MemoryChatProvider(new ScriptedProvider(ChatDefaults.LocalId, "x"), _store)
                .Enrich(ChatRequestFor("what is my name?")).SystemPrompt;

            Assert.IsFalse(cloudPrompt.Contains("Jussi"));
            StringAssert.Contains("Jussi", localPrompt);
        }

        static ChatRequest ChatRequestFor(string userText)
        {
            return new ChatRequest
            {
                SystemPrompt = "base prompt",
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, userText) },
            };
        }

        sealed class ScriptedProvider : IChatProvider
        {
            readonly string _reply;

            public ScriptedProvider(string providerId, string reply)
            {
                ProviderId = providerId;
                _reply = reply;
            }

            public string ProviderId { get; }

            public string DisplayName
            {
                get { return "Scripted"; }
            }

            public bool IsConfigured
            {
                get { return true; }
            }

            public int Calls { get; private set; }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Calls++;
                if (onDelta != null)
                    onDelta(_reply);

                return Task.FromResult(ChatResult.Success(_reply));
            }
        }
    }
}
