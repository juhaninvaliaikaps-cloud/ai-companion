using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-134: when her reply narrates clothing coming off, the video must not stay clothed.
    /// Review fix (Jussi, 2026-11-20): her reply decides, so a refusal keeps her dressed.
    /// </summary>
    public sealed class ClothingIntentTests
    {
        [TestCase("*She pulls off her dress and lets it fall.*")]
        [TestCase("She slowly unbuttons her blouse.")]
        [TestCase("*slides her skirt off* There.")]
        [TestCase("Okay... *she takes off her shirt*")]
        [TestCase("*Hän riisuu paitansa hitaasti.*")]
        [TestCase("*Hän vetää mekon pois päältään.*")]
        [TestCase("Fine, I'll do it. <animation reaction=\"shy\"/> *She removes her bra.*")]
        public void HerReplyRemovingClothes_IsDetected(string reply)
        {
            Assert.IsTrue(ClothingIntent.ReplyRemovesClothing(reply));
        }

        [TestCase("No. *She pulls her shirt back down and keeps it on.*")]
        [TestCase("*She shakes her head.* I won't take off my shirt.")]
        [TestCase("Not yet. *She keeps her dress on.*")]
        [TestCase("Should I take off my jacket?")]
        [TestCase("*She stops you from pulling off her top.*")]
        [TestCase("En riisu paitaa.")]
        [TestCase("*Hän pitää mekon päällä.*")]
        public void ARefusalOrAQuestion_KeepsHerDressed(string reply)
        {
            Assert.IsFalse(ClothingIntent.ReplyRemovesClothing(reply));
        }

        [TestCase("Please stop removing the files from my laptop.")]
        [TestCase("I'm going to the top of the hill, then off to work.")]
        [TestCase("Laitan sen pois ja avaan ikkunan.")]
        [TestCase("You look nice in that dress.")]
        [TestCase("")]
        public void EverydayText_IsNotAStrip(string reply)
        {
            Assert.IsFalse(ClothingIntent.ReplyRemovesClothing(reply));
        }

        [Test]
        public void OnlyTheSentenceThatRemoves_Counts()
        {
            // A refusal in one sentence does not cancel another that really happens.
            Assert.IsTrue(ClothingIntent.ReplyRemovesClothing("I said no to the jacket earlier. *Now she pulls off her dress.*"));
        }
    }
}
