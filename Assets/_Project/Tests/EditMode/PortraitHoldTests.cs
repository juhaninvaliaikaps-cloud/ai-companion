using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class PortraitHoldTests
    {
        [Test]
        public void NonNeutralExpressionIsRemembered()
        {
            var held = string.Empty;

            var resolved = PortraitHold.Resolve("embarrassed", ref held, speaking: false);

            Assert.AreEqual("embarrassed", resolved);
            Assert.AreEqual("embarrassed", held);
        }

        [Test]
        public void NeutralDuringSpeechKeepsTheBlush()
        {
            var held = "embarrassed";

            var resolved = PortraitHold.Resolve("neutral", ref held, speaking: true);

            Assert.AreEqual("embarrassed", resolved);
        }

        [Test]
        public void NeutralOutsideSpeechClearsTheHold()
        {
            var held = "embarrassed";

            var resolved = PortraitHold.Resolve("neutral", ref held, speaking: false);

            Assert.AreEqual("neutral", resolved);
            Assert.AreEqual(string.Empty, held);
        }

        [Test]
        public void NeutralSpeechWithoutAHoldStaysNeutral()
        {
            var held = string.Empty;

            var resolved = PortraitHold.Resolve("neutral", ref held, speaking: true);

            Assert.AreEqual("neutral", resolved);
        }

        [Test]
        public void NewExpressionReplacesTheHold()
        {
            var held = "embarrassed";

            var resolved = PortraitHold.Resolve("happy", ref held, speaking: true);

            Assert.AreEqual("happy", resolved);
            Assert.AreEqual("happy", held);
        }
    }
}
