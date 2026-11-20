using AICompanion.Memory;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-134: scene beats are not relationship memory; durable facts and boundaries are.</summary>
    public sealed class MemoryDurabilityTests
    {
        [Test]
        public void SceneBeats_AreNotDurable()
        {
            Assert.IsFalse(MemoryDurability.IsDurable("She trembled when he touched her neck."));
            Assert.IsFalse(MemoryDurability.IsDurable("The room went quiet after the kiss."));
            Assert.IsFalse(MemoryDurability.IsDurable("She blushed and looked away."));
            Assert.IsFalse(MemoryDurability.IsDurable("Her heart raced in that moment."));
        }

        [Test]
        public void DurableFactsAndBoundaries_Survive()
        {
            Assert.IsTrue(MemoryDurability.IsDurable("The user's sister is called Anna."));
            Assert.IsTrue(MemoryDurability.IsDurable("The user promised to visit on Sunday."));
            Assert.IsTrue(MemoryDurability.IsDurable("She does not want her neck touched without warning."));
            Assert.IsTrue(MemoryDurability.IsDurable("She is afraid of being left."));
        }

        // JUH-134 review, bug 4: a scene word inside a lasting line must not drop it.
        [TestCase("The user's father suddenly died last year.")]
        [TestCase("She hates long silences.")]
        [TestCase("She loves it when the user smiles.")]
        [TestCase("The user looked at apartments in Tampere.")]
        [TestCase("The user felt nervous about the job interview on Monday.")]
        public void ASceneWordInALastingLine_IsKept(string line)
        {
            Assert.IsTrue(MemoryDurability.IsDurable(line), line);
        }

        [TestCase("The user smiled at her warmly.")]
        [TestCase("She blushed when the user said she was pretty.")]
        [TestCase("She sighed and looked at the user.")]
        [TestCase("She smiled softly at the user's joke.")]
        public void ABeatThatOnlyMentionsSpeech_IsStillDropped(string line)
        {
            Assert.IsFalse(MemoryDurability.IsDurable(line), line);
        }

        [Test]
        public void VagueSummaries_AreNotDurable()
        {
            Assert.IsFalse(MemoryDurability.IsDurable("The user confessed to loving something deeply personal and vulnerable during an intense sexual encounter."));
            Assert.IsFalse(MemoryDurability.IsDurable("They shared a deeply personal moment."));
        }

        [Test]
        public void KindsAndReasons_AreSet()
        {
            Assert.AreEqual(MemoryDurability.Promise, MemoryDurability.KindOf("The user promised to visit on Sunday."));
            Assert.AreEqual(MemoryDurability.Boundary, MemoryDurability.KindOf("She does not want her neck touched without warning."));
            Assert.AreEqual(MemoryDurability.Preference, MemoryDurability.KindOf("The user loves strong coffee."));
            Assert.IsFalse(string.IsNullOrEmpty(MemoryDurability.Why(MemoryDurability.Promise)));
        }

        [Test]
        public void SimilarSummaries_AreRecognised()
        {
            Assert.IsTrue(MemoryDurability.Similar(
                "The user's sister is called Anna.",
                "The user's sister, Anna, was mentioned."));
            Assert.IsFalse(MemoryDurability.Similar(
                "The user's sister is called Anna.",
                "The user promised to visit on Sunday."));
        }
    }
}
