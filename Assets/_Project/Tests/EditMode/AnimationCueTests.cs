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
            Assert.AreEqual(0f, cue.Direction, 0.0001f);
        }

        [Test]
        public void Constructor_DirectionIsASideOrUnspecified()
        {
            Assert.AreEqual(-1f, new AnimationCue(Emotion.Neutral, 0.5f, "walk", -0.2f).Direction, 0.0001f);
            Assert.AreEqual(1f, new AnimationCue(Emotion.Neutral, 0.5f, "walk", 4f).Direction, 0.0001f);
            Assert.AreEqual(0f, new AnimationCue(Emotion.Neutral, 0.5f, "walk", float.NaN).Direction, 0.0001f);
        }

        [Test]
        public void ClampIntensity_TreatsNaNAsZero()
        {
            Assert.AreEqual(0f, AnimationCue.ClampIntensity(float.NaN), 0.0001f);
            Assert.AreEqual(1f, AnimationCue.ClampIntensity(float.PositiveInfinity), 0.0001f);
        }
    }
}
