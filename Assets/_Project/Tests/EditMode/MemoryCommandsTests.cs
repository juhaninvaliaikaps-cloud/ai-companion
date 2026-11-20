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
            Assert.IsTrue(MemoryCommands.TryExtract("Remember to ask me about the exam on Friday", out memory));
            Assert.AreEqual("to ask me about the exam on Friday", memory);
            Assert.IsTrue(MemoryCommands.TryExtract("Don't forget my birthday is May 3.", out memory));
            Assert.AreEqual("my birthday is May 3", memory);
            Assert.IsTrue(MemoryCommands.TryExtract("Remember this: meetings are on Mondays", out memory));
            Assert.AreEqual("meetings are on Mondays", memory);
        }

        [Test]
        public void Questions_AreNotRememberCommands()
        {
            string memory;
            Assert.IsFalse(MemoryCommands.TryExtract("Do you remember my sister?", out memory));
            Assert.IsFalse(MemoryCommands.TryExtract("Remember?", out memory));
        }

        // JUH-134 review, bug 3: a recollection is not a fact to store.
        [TestCase("Remember when we went to the beach, that was fun")]
        [TestCase("Remember that time we got lost in Tampere")]
        [TestCase("remember the night we watched the stars")]
        [TestCase("Remember how you laughed")]
        [TestCase("Don't forget me")]
        [TestCase("Don't forget about us, okay")]
        public void Recollections_AreNotStored(string text)
        {
            string memory;
            Assert.IsFalse(MemoryCommands.TryExtract(text, out memory), text);
        }

        // JUH-134 review, bug 2: "I" in a stored line must stay the user's.
        [Test]
        public void TheStoredLine_SaysTheUserAsked()
        {
            string memory;
            Assert.IsTrue(MemoryCommands.TryExtract("Remember that I hate pickles.", out memory));
            var stored = MemoryCommands.Stored(memory);
            Assert.AreEqual("The user asked you to remember: \"I hate pickles\"", stored);
            StringAssert.DoesNotStartWith("I ", stored);
        }
    }
}
