using AICompanion.Core;
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

        [Test]
        public void NeutralBeforeSpeechKeepsTheExpression()
        {
            var held = string.Empty;
            var quiet = 0f;

            Assert.AreEqual("sad", PortraitHold.ResolveUtterance("sad", ref held, false, 0.1f, ref quiet));
            Assert.AreEqual("sad", PortraitHold.ResolveUtterance("neutral", ref held, false, 0.4f, ref quiet));
            Assert.AreEqual("sad", held);
        }

        [Test]
        public void ExpressionHoldsForTheWholeUtteranceThenReleases()
        {
            var held = "sad";
            var quiet = 0f;

            Assert.AreEqual("sad", PortraitHold.ResolveUtterance("neutral", ref held, true, 4f, ref quiet));
            Assert.AreEqual("sad", held);
            Assert.AreEqual(0f, quiet, 0.001f);

            Assert.AreEqual("sad", PortraitHold.ResolveUtterance("neutral", ref held, false, 0.5f, ref quiet));
            Assert.AreEqual("neutral", PortraitHold.ResolveUtterance("neutral", ref held, false, PortraitHold.ReleaseSeconds, ref quiet));
            Assert.AreEqual(string.Empty, held);
        }

        [Test]
        public void PreviewHoldsEachExpressionUntilACueOrSpeech()
        {
            var preview = new PortraitPreview();
            preview = preview.Tick(keyDown: true, hint: "sad", emotion: Emotion.Neutral, utterance: false);
            Assert.IsTrue(preview.Active);
            Assert.AreEqual("sad", preview.Expression);

            preview = preview.Tick(keyDown: false, hint: "sad", emotion: Emotion.Neutral, utterance: false);
            Assert.IsTrue(preview.Active);
            Assert.AreEqual("sad", preview.Expression);

            preview = preview.Tick(keyDown: true, hint: string.Empty, emotion: Emotion.Happy, utterance: false);
            Assert.AreEqual("neutral", preview.Expression);

            preview = preview.Tick(keyDown: true, hint: "embarrassed", emotion: Emotion.Neutral, utterance: false);
            preview = preview.Tick(keyDown: false, hint: "shy", emotion: Emotion.Neutral, utterance: false);
            Assert.IsFalse(preview.Active);

            preview = preview.Tick(keyDown: true, hint: "sad", emotion: Emotion.Sad, utterance: false);
            preview = preview.Tick(keyDown: false, hint: "sad", emotion: Emotion.Sad, utterance: true);
            Assert.IsFalse(preview.Active);
        }
    }
}
