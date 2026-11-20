using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-134: a session that misses the four-second quit window survives to the next start.</summary>
    public sealed class SessionHandoffTests
    {
        readonly List<string> _cleanup = new List<string>();
        readonly List<string> _folders = new List<string>();

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
                }
            }

            _cleanup.Clear();
            for (var i = 0; i < _folders.Count; i++)
            {
                try
                {
                    if (Directory.Exists(_folders[i]))
                        Directory.Delete(_folders[i], true);
                }
                catch (Exception)
                {
                }
            }

            _folders.Clear();
        }

        string TempFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "happi-handoffs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _folders.Add(folder);
            return folder;
        }

        string TempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "happi-handoff-" + Guid.NewGuid().ToString("N") + ".json");
            _cleanup.Add(path);
            return path;
        }

        [Test]
        public void Save_LoadAndClear_RoundTrip()
        {
            var handoffPath = TempFile();
            var memoryPath = TempFile();
            var store = MemoryStore.Load(memoryPath);

            var pending = SessionHandoff.SaveAt(
                handoffPath, "User: hello\nLola: hi", "Lola", "local", store,
                "quitting", 6, true, false);

            Assert.IsNotNull(pending);
            var loaded = SessionHandoff.LoadAt(handoffPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(pending.id, loaded.id);
            Assert.AreEqual("quitting", loaded.why);
            Assert.AreEqual(6, loaded.userMessages);
            Assert.IsTrue(loaded.keepsLocal);

            SessionHandoff.ClearAt(loaded, handoffPath);
            Assert.IsNull(SessionHandoff.LoadAt(handoffPath));
        }

        // JUH-134 review: one file per session, so the next session's checkpoint cannot
        // overwrite a session whose summary is still running.
        [Test]
        public void TwoSessions_KeepTheirOwnFiles_AndLoadOldestFirst()
        {
            var folder = TempFolder();
            var store = MemoryStore.Load(Path.Combine(folder, "memory.json"));

            var a = SessionHandoff.SaveAt(SessionHandoff.PathFor(folder, "sessionA"), "User: about my sister\nLola: tell me", "Lola", "local", store,
                "a long pause", 6, true, false, "sessionA");
            System.Threading.Thread.Sleep(5);
            var b = SessionHandoff.SaveAt(SessionHandoff.PathFor(folder, "sessionB"), "User: new topic\nLola: ok", "Lola", "local", store,
                "a rolling checkpoint", 4, true, false, "sessionB");

            var all = SessionHandoff.LoadAllIn(folder);
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(a.id, all[0].id);
            Assert.AreEqual("sessionA", all[0].session);
            Assert.AreEqual(b.id, all[1].id);

            SessionHandoff.Clear(all[0]);
            var left = SessionHandoff.LoadAllIn(folder);
            Assert.AreEqual(1, left.Count);
            Assert.AreEqual("sessionB", left[0].session);
        }

        [Test]
        public void TheSameSession_ReplacesItsCheckpoint_AndAnOldClearLeavesTheNewerSave()
        {
            var folder = TempFolder();
            var store = MemoryStore.Load(Path.Combine(folder, "memory.json"));
            var path = SessionHandoff.PathFor(folder, "sessionA");

            var checkpoint = SessionHandoff.SaveAt(path, "User: one\nLola: two", "Lola", "local", store, "a rolling checkpoint", 4, true, false, "sessionA");
            var final = SessionHandoff.SaveAt(path, "User: one\nLola: two\nUser: three", "Lola", "local", store, "a long pause", 5, true, false, "sessionA");

            Assert.AreEqual(1, SessionHandoff.LoadAllIn(folder).Count);
            SessionHandoff.Clear(checkpoint);
            Assert.AreEqual(final.id, SessionHandoff.LoadAllIn(folder)[0].id, "a stale id does not delete the newer save");
        }

        [Test]
        public void TheOldSingleSlot_IsStillFinished()
        {
            var folder = TempFolder();
            var store = MemoryStore.Load(Path.Combine(folder, "memory.json"));
            SessionHandoff.SaveAt(Path.Combine(folder, SessionHandoff.FileName), "User: hello\nLola: hi", "Lola", "local", store, "quitting", 6, false, false);

            var all = SessionHandoff.LoadAllIn(folder);
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(string.Empty, all[0].session);
        }

        [Test]
        public void ASessionId_CannotLeaveTheFolder()
        {
            var folder = TempFolder();
            Assert.AreEqual(Path.Combine(folder, "pending-session-etcpasswd.json"), SessionHandoff.PathFor(folder, "../etc/passwd"));
            Assert.AreEqual(Path.Combine(folder, SessionHandoff.FileName), SessionHandoff.PathFor(folder, ""));
            Assert.IsTrue(AICompanion.Core.LocalDataInventory.IsPendingSessionName("pending-session-0d42af9a.json"));
            Assert.IsTrue(AICompanion.Core.LocalDataInventory.IsPendingSessionName("pending-session.json"));
            Assert.IsFalse(AICompanion.Core.LocalDataInventory.IsPendingSessionName("pending-sessions.txt"));
        }

        [Test]
        public void InvalidTranscript_IsNotSaved()
        {
            var handoffPath = TempFile();
            var memoryPath = TempFile();
            var store = MemoryStore.Load(memoryPath);
            Assert.IsNull(SessionHandoff.SaveAt(handoffPath, "", "Lola", "local", store, "quitting", 6, false, false));
        }
    }
}
