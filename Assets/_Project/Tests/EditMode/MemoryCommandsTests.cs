using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-134: explicit remember requests are local commands, not model instructions.</summary>
    public sealed class MemoryCommandsTests
    {
        [Test]
        public void RememberRequests_ExtractTheDurableSentence()
        {
            string memory;
            Assert.IsTrue(MemoryCommands.TryExtract("Remember that my sister is Anna.", out memory));
            Assert.AreEqual("my sister is Anna", memory);
            Assert.IsTrue(MemoryCommands.TryExtract("Muista että pidän kahvista.", out memory));
            Assert.AreEqual("pidän kahvista", memory);
        }

        [Test]
        public void Questions_AreNotRememberCommands()
        {
            string memory;
            Assert.IsFalse(MemoryCommands.TryExtract("Do you remember my sister?", out memory));
            Assert.IsFalse(MemoryCommands.TryExtract("Remember?", out memory));
        }
    }
}
