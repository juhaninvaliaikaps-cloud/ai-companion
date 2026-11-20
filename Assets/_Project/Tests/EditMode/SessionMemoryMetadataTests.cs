using System.Collections.Generic;
using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-134: the model may supply type, importance and confidence; fallback stays valid.</summary>
    public sealed class SessionMemoryMetadataTests
    {
        [Test]
        public void TaggedLine_IsParsedWithoutLeakingTheTagIntoTheMemory()
        {
            var list = SessionMemories.ParseDetailed("[promise|5|95] The user promised to visit on Sunday.");
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("The user promised to visit on Sunday.", list[0].Text);
            Assert.AreEqual(MemoryDurability.Promise, list[0].Kind);
            Assert.AreEqual(5, list[0].Importance);
            Assert.AreEqual(95, list[0].Confidence);
            Assert.IsTrue(list[0].HasMetadata);
        }

        [Test]
        public void UntaggedLine_FallsBackWithoutMetadata()
        {
            var list = SessionMemories.ParseDetailed("The user's sister is called Anna.");
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("The user's sister is called Anna.", list[0].Text);
            Assert.IsFalse(list[0].HasMetadata);
        }

        [Test]
        public void InvalidTag_DoesNotBreakTheLine()
        {
            var list = SessionMemories.ParseDetailed("[not-a-type|99|x] The user's sister is called Anna.");
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("The user's sister is called Anna.", list[0].Text);
            Assert.IsFalse(list[0].HasMetadata);
        }
    }
}
