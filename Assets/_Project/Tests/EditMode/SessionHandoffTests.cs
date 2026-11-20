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
