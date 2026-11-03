using AICompanion.Avatar;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class ExpressionHoldTests
    {
        [Test]
        public void StaysUpThroughTheGapBeforeSheSpeaks()
        {
            var hold = new ExpressionHold();
            hold.Apply(Emotion.Sad, 0.7f, "sad", speaking: false);

            Assert.IsFalse(hold.Tick(5f, speaking: false));

            Assert.IsTrue(hold.Active);
            Assert.AreEqual(Emotion.Sad, hold.Emotion);
            Assert.AreEqual(0.7f, hold.Intensity, 0.0001f);
            Assert.AreEqual("sad", hold.Portrait);
        }

        [Test]
        public void SpeechKeepsTheExpressionAndTheTailWaitsUntilItStops()
        {
            var hold = new ExpressionHold();
            hold.Apply(Emotion.Sad, 0.7f, "sad", speaking: false);

            Assert.IsFalse(hold.Tick(1f, speaking: true));
            Assert.AreEqual(Emotion.Sad, hold.Emotion);
            Assert.IsFalse(hold.Tick(ExpressionHold.ReleaseSeconds - 0.05f, speaking: false));
            Assert.IsTrue(hold.Active);

            Assert.IsTrue(hold.Tick(0.05f, speaking: false));
            Assert.IsFalse(hold.Active);
            Assert.AreEqual(Emotion.Neutral, hold.Emotion);
            Assert.AreEqual(string.Empty, hold.Portrait);
        }

        [Test]
        public void AGapInsideSpeechDoesNotRelease()
        {
            var hold = new ExpressionHold();
            hold.Apply(Emotion.Surprised, 0.9f, string.Empty, speaking: true);

            Assert.IsFalse(hold.Tick(0.3f, speaking: false));
            Assert.IsFalse(hold.Tick(0.2f, speaking: true));
            Assert.IsFalse(hold.Tick(ExpressionHold.ReleaseSeconds - 0.05f, speaking: false));
            Assert.AreEqual(Emotion.Surprised, hold.Emotion);
        }

        [Test]
        public void ANewCueReplacesImmediatelyAndWaitsForItsOwnSpeech()
        {
            var hold = new ExpressionHold();
            hold.Apply(Emotion.Sad, 1f, "sad", speaking: true);
            hold.Tick(0.2f, speaking: false);

            hold.Apply(Emotion.Happy, 0.8f, string.Empty, speaking: false);

            Assert.AreEqual(Emotion.Happy, hold.Emotion);
            Assert.AreEqual(string.Empty, hold.Portrait);
            Assert.IsFalse(hold.Tick(ExpressionHold.ReleaseSeconds + 1f, speaking: false));
            Assert.IsTrue(hold.Active);
        }

        [Test]
        public void ACueThatArrivesWhileSheIsTalkingUsesThatUtterance()
        {
            var hold = new ExpressionHold();
            hold.Apply(Emotion.Sad, 1f, "sad", speaking: true);
            hold.Apply(Emotion.Embarrassed, 0.6f, "embarrassed", speaking: true);

            Assert.AreEqual(Emotion.Embarrassed, hold.Emotion);
            Assert.AreEqual("embarrassed", hold.Portrait);
            Assert.IsFalse(hold.Tick(0f, speaking: false));
            Assert.IsTrue(hold.Tick(ExpressionHold.ReleaseSeconds, speaking: false));
            Assert.IsFalse(hold.Active);
        }

        [Test]
        public void ANeutralCueClearsImmediately()
        {
            var hold = new ExpressionHold();
            hold.Apply(Emotion.Embarrassed, 0.6f, "embarrassed", speaking: true);

            hold.Apply(Emotion.Neutral, 1f, "neutral", speaking: true);

            Assert.IsFalse(hold.Active);
            Assert.AreEqual(Emotion.Neutral, hold.Emotion);
            Assert.IsFalse(hold.Tick(0f, speaking: true));
        }

        [Test]
        public void APortraitOnlyNameHoldsEvenWhenTheFaceEmotionIsNeutral()
        {
            var hold = new ExpressionHold();
            hold.Apply(Emotion.Neutral, 0.3f, " sleepy ", speaking: false);

            Assert.IsTrue(hold.Active);
            Assert.AreEqual(Emotion.Neutral, hold.Emotion);
            Assert.AreEqual("sleepy", hold.Portrait);
            Assert.IsFalse(hold.Tick(3f, speaking: false));
        }
    }
}
