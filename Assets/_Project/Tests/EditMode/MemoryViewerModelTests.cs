using System;
using System.Collections.Generic;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class MemoryViewerModelTests
    {
        [Test]
        public void Summary_CountsCoreAndPrivateEntries()
        {
            var entries = new List<MemoryEntry>
            {
                Entry("Likes coffee", core: false, isPrivate: false, createdAtUtc: "2026-10-28T10:00:00Z"),
                Entry("The user's name is Jussi", core: true, isPrivate: false, createdAtUtc: "2026-10-28T10:01:00Z"),
                Entry("Private fact", core: false, isPrivate: true, createdAtUtc: "2026-10-28T10:02:00Z"),
            };

            var summary = MemoryViewerModel.Summary(entries);

            StringAssert.Contains("3 memories", summary);
            StringAssert.Contains("1 core", summary);
            StringAssert.Contains("1 private", summary);
        }

        [Test]
        public void Summary_EmptyStoreSaysNothingRemembered()
        {
            StringAssert.Contains("No memories", MemoryViewerModel.Summary(new List<MemoryEntry>()));
            StringAssert.Contains("No memories", MemoryViewerModel.Summary(null));
        }

        [Test]
        public void Page_PutsCoreFirstThenNewest()
        {
            var entries = new List<MemoryEntry>
            {
                Entry("older fact", false, false, "2026-10-28T09:00:00Z"),
                Entry("newer fact", false, false, "2026-10-28T11:00:00Z"),
                Entry("core fact", true, false, "2026-10-27T09:00:00Z"),
            };

            var page = MemoryViewerModel.Page(entries, 3);

            Assert.AreEqual("core fact", page[0].text);
            Assert.AreEqual("newer fact", page[1].text);
            Assert.AreEqual("older fact", page[2].text);
        }

        [Test]
        public void Page_CapsTheNumberOfRows()
        {
            var entries = new List<MemoryEntry>();
            for (var i = 0; i < 12; i++)
                entries.Add(Entry("fact " + i, false, false, "2026-10-28T10:0" + (i % 10) + ":00Z"));

            Assert.AreEqual(MemoryViewerModel.DefaultPageSize, MemoryViewerModel.Page(entries).Count);
            Assert.AreEqual(3, MemoryViewerModel.Page(entries, 3).Count);
        }

        [Test]
        public void Label_TruncatesAndAddsBadges()
        {
            var longText = new string('x', 120);
            var label = MemoryViewerModel.Label(Entry(longText, core: true, isPrivate: false, createdAtUtc: "2026-10-28T10:00:00Z"));

            StringAssert.StartsWith("Delete: ", label);
            StringAssert.Contains("(core)", label);
            Assert.LessOrEqual(label.Length, "Delete: ".Length + MemoryViewerModel.MaxLabelLength + " (core)".Length);

            var privateLabel = MemoryViewerModel.Label(Entry("secret", false, true, "2026-10-28T10:00:00Z"));
            StringAssert.Contains("(private)", privateLabel);
        }

        static MemoryEntry Entry(string text, bool core, bool isPrivate, string createdAtUtc)
        {
            return new MemoryEntry
            {
                id = Guid.NewGuid().ToString("N"),
                text = text,
                kind = MemoryKind.Fact,
                createdAtUtc = createdAtUtc,
                isCore = core,
                isPrivate = isPrivate,
            };
        }
    }
}
