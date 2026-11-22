using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-186: words from the 2026-11-22 log are read as faces, not dropped to neutral.</summary>
    public sealed class EmotionAliasTests
    {
        [TestCase("conflicted", Emotion.Concerned)]
        [TestCase("torn", Emotion.Concerned)]
        [TestCase("hesitant", Emotion.Concerned)]
        [TestCase("guilty", Emotion.Sad)]
        [TestCase("Ashamed", Emotion.Sad)]
        [TestCase("remorseful", Emotion.Sad)]
        [TestCase("sorry", Emotion.Sad)]
        public void TheWord_IsAFace(string word, Emotion expected)
        {
            Emotion emotion;
            Assert.IsTrue(EmotionNames.TryParse(word, out emotion), word);
            Assert.AreEqual(expected, emotion, word);
        }

        [Test]
        public void TheLogTags_AreReadable_AndKeepTheirLandingAndHeat()
        {
            // As the model wrote them in Jussi's log.
            var conflicted = AnimationCueParser.Parse("<animation emotion=\"conflicted\" intensity=\"0.75\" gesture=\"cross_arms\" landed=\"flirted\" heat=\"warm\">\nHm.");
            Assert.AreEqual(CueParseStatus.Parsed, conflicted.Status);
            Assert.AreEqual(Emotion.Concerned, conflicted.Cue.Emotion);
            Assert.AreEqual(LandingEvent.Flirted, conflicted.Cue.Landed);
            Assert.AreEqual(DesireHeat.Warm, conflicted.Cue.Heat);

            var guilty = AnimationCueParser.Parse("<animation emotion=\"guilty\" landed=\"apologized\" heat=\"warm\">\nSorry.");
            Assert.AreEqual(CueParseStatus.Parsed, guilty.Status);
            Assert.AreEqual(Emotion.Sad, guilty.Cue.Emotion);
        }
    }
}
