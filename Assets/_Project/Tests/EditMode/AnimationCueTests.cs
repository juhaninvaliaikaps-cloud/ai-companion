using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class AnimationCueTests
    {
        [Test]
        public void Constructor_ClampsIntensityAndKeepsFields()
        {
            var cue = new AnimationCue(Emotion.Happy, 2f, "wave");

            Assert.AreEqual(Emotion.Happy, cue.Emotion);
            Assert.AreEqual(1f, cue.Intensity);
            Assert.AreEqual("wave", cue.Gesture);
        }

        [Test]
        public void ClampIntensity_TreatsNaNAsZero()
        {
            Assert.AreEqual(0f, AnimationCue.ClampIntensity(float.NaN), 0.0001f);
            Assert.AreEqual(1f, AnimationCue.ClampIntensity(float.PositiveInfinity), 0.0001f);
        }
    }
}
