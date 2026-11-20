using System;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-141: the memory block says whose memory each line is.</summary>
    public sealed class MemoryOwnershipTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

        static MemoryEntry Entry(string text, string origin)
        {
            return new MemoryEntry { id = Guid.NewGuid().ToString("N"), text = text, origin = origin };
        }

        [Test]
        public void AMixedList_GetsTwoHeadings_SoTheUsersDislikeIsNotHers()
        {
            var text = MemoryPrompt.Build(new[]
            {
                Entry("The user hates pickles.", MemoryOrigins.Lived),
                Entry("She grew up in Oulu.", MemoryOrigins.Seeded),
                Entry("Her exam went badly while the user was away.", MemoryOrigins.Away),
            }, Now);

            var about = text.IndexOf(MemoryPrompt.AboutUserHeader, StringComparison.Ordinal);
            var own = text.IndexOf(MemoryPrompt.OwnLifeHeader, StringComparison.Ordinal);
            Assert.Greater(about, 0);
            Assert.Greater(own, about);
            var pickles = text.IndexOf("pickles", StringComparison.Ordinal);
            Assert.That(pickles, Is.GreaterThan(about).And.LessThan(own), "the user's dislike sits under the user");
            Assert.Greater(text.IndexOf("Oulu", StringComparison.Ordinal), own);
            Assert.Greater(text.IndexOf("exam", StringComparison.Ordinal), own);
        }

        [Test]
        public void AListOfOneKind_StaysFlat()
        {
            var text = MemoryPrompt.Build(new[] { Entry("The user hates pickles.", MemoryOrigins.Lived) }, Now);
            Assert.AreEqual(MemoryPrompt.Header + "\n- The user hates pickles.", text);
            Assert.IsFalse(MemoryPrompt.Build(new[] { Entry("She grew up in Oulu.", MemoryOrigins.Seeded) }, Now).Contains(MemoryPrompt.OwnLifeHeader));
        }

        [Test]
        public void HerOwnLife_IsSeededAwayAndLife()
        {
            Assert.IsTrue(MemoryPrompt.IsHerOwnLife(Entry("x", MemoryOrigins.Seeded)));
            Assert.IsTrue(MemoryPrompt.IsHerOwnLife(Entry("x", MemoryOrigins.Away)));
            Assert.IsTrue(MemoryPrompt.IsHerOwnLife(Entry("x", MemoryOrigins.Life)));
            Assert.IsFalse(MemoryPrompt.IsHerOwnLife(Entry("x", MemoryOrigins.Lived)));
            Assert.IsFalse(MemoryPrompt.IsHerOwnLife(null));
        }
    }
}
