using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-134: a direct clothing-removal action must not leave the video clothed.</summary>
    public sealed class ClothingIntentTests
    {
        [Test]
        public void RemovalActions_AreDetected()
        {
            Assert.IsTrue(ClothingIntent.RemovesClothing("I take off your shirt."));
            Assert.IsTrue(ClothingIntent.RemovesClothing("She pulls off her dress."));
            Assert.IsTrue(ClothingIntent.RemovesClothing("riisu paitasi"));
            Assert.IsTrue(ClothingIntent.RemovesClothing("remove her clothes"));
        }

        [Test]
        public void NegatedOrUnrelatedText_IsNotDetected()
        {
            Assert.IsFalse(ClothingIntent.RemovesClothing("Do not take off your shirt."));
            Assert.IsFalse(ClothingIntent.RemovesClothing("You look nice in that dress."));
            Assert.IsFalse(ClothingIntent.RemovesClothing("Let's talk about something else."));
        }
    }
}
