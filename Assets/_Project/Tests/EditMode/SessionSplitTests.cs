using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.UI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-188: public and private lines are summarised separately; intimate memory stays.</summary>
    public sealed class SessionSplitTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-split-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
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

        sealed class FakeProvider : IChatProvider
        {
            public string ProviderId => ChatDefaults.LocalId;
            public string DisplayName => "fake";
            public bool IsConfigured => true;
            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
                => Task.FromResult(ChatResult.Success("- The user and Mira talked about the garden."));
        }

        [Test]
        public void PublicAndPrivateLines_AreSeparated()
        {
            var conversation = new ChatConversation();
            conversation.ImportLine(ChatLineKind.User, "public question", false);
            conversation.ImportLine(ChatLineKind.Assistant, "public answer", false);
            conversation.ImportLine(ChatLineKind.User, "private question", true);
            conversation.ImportLine(ChatLineKind.Assistant, "private answer", true);

            var publicMessages = new List<ChatMessage>();
            var privateMessages = new List<ChatMessage>();
            SessionLineFilter.AppendPublic(publicMessages, conversation.Lines, 0);
            SessionLineFilter.AppendPrivate(privateMessages, conversation.Lines, 0);

            Assert.AreEqual(2, publicMessages.Count);
            Assert.AreEqual(2, privateMessages.Count);
            StringAssert.Contains("public question", publicMessages[0].Content);
            StringAssert.Contains("private question", privateMessages[0].Content);
            Assert.IsFalse(publicMessages.Exists(m => m.Content.Contains("private")));
            Assert.IsFalse(privateMessages.Exists(m => m.Content.Contains("public")));
        }

        [Test]
        public void IntimateMoment_LeavesAGenericPrivateMemory()
        {
            var store = MemoryStore.Load(Path.Combine(_dir, "memory.json"));
            var capture = SessionMemories.CaptureAsync(new FakeProvider(), "User: hello\nMira: hi",
                "Mira", 4, true, true, store, 2f, CancellationToken.None, false, null, "intimate")
                .GetAwaiter().GetResult();

            Assert.IsTrue(capture.Answered);
            MemoryEntry marker = null;
            for (var i = 0; i < store.Entries.Count; i++)
            {
                if (store.Entries[i].text == "The user and Mira were intimate.")
                    marker = store.Entries[i];
            }

            Assert.IsNotNull(marker);
            Assert.IsTrue(marker.isPrivate);
            Assert.AreEqual("intimate", marker.moment);
        }

        [Test]
        public void MomentKind_RoundTripsThroughHandoff()
        {
            var store = MemoryStore.Load(Path.Combine(_dir, "memory.json"));
            var pendingPath = Path.Combine(_dir, "pending.json");
            var pending = SessionHandoff.SaveAt(pendingPath, "User: hello", "Mira", ChatDefaults.LocalId,
                store, "test", 4, true, true, "session", "intimate");

            Assert.IsNotNull(pending);
            var loaded = SessionHandoff.LoadAt(pendingPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("intimate", loaded.momentKind);
        }
    }
}
