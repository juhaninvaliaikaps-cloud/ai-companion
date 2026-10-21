using AICompanion.Avatar;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class EmotionFaceTests
    {
        [Test]
        public void EmotionPose_DrivesSmileFrownAndBrows()
        {
            var happy = EmotionFace.EmotionPose(Emotion.Happy, 1f);
            var sad = EmotionFace.EmotionPose(Emotion.Sad, 1f);
            var surprised = EmotionFace.EmotionPose(Emotion.Surprised, 1f);
            var thinking = EmotionFace.EmotionPose(Emotion.Thinking, 1f);
            var neutral = EmotionFace.EmotionPose(Emotion.Neutral, 1f);

            Assert.AreEqual(1f, happy.Joy, 0.0001f);
            Assert.Greater(happy.Fun, 0f);
            Assert.AreEqual(0f, happy.Sorrow, 0.0001f);
            Assert.AreEqual(0.9f, sad.Sorrow, 0.0001f);
            Assert.AreEqual(0.5f, sad.Angry, 0.0001f);
            Assert.AreEqual(1f, surprised.Surprised, 0.0001f);
            Assert.Greater(surprised.Aa, 0f);
            Assert.Greater(thinking.Ih, 0f);
            Assert.AreEqual(0f, neutral.Joy, 0.0001f);
            Assert.AreEqual(0f, neutral.Aa, 0.0001f);
            Assert.AreEqual(0f, EmotionFace.EmotionPose(Emotion.Happy, 0f).Joy, 0.0001f);
            Assert.AreEqual(1f, EmotionFace.EmotionPose(Emotion.Happy, 3f).Joy, 0.0001f);

            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("mouthSmileLeft"));
            Assert.AreEqual(ExpressionPreset.Sorrow, ExpressionPresetMap.Classify("mouthFrownRight"));
            Assert.AreEqual(ExpressionPreset.Angry, ExpressionPresetMap.Classify("browDownLeft"));
            Assert.AreEqual(ExpressionPreset.Surprised, ExpressionPresetMap.Classify("browInnerUp"));
            Assert.AreEqual(ExpressionPreset.Surprised, ExpressionPresetMap.Classify("browOuterUpRight"));
            Assert.AreEqual(ExpressionPreset.Surprised, ExpressionPresetMap.Classify("eyeWideLeft"));
            Assert.AreEqual(ExpressionPreset.Surprised, ExpressionPresetMap.Classify("surprised"));
            Assert.AreEqual(ExpressionPreset.Aa, ExpressionPresetMap.Classify("jawOpen"));
        }

        [Test]
        public void TalkPose_OpensTheJawAndStaysQuietWhenAmountIsZero()
        {
            var quarter = 0.25f / EmotionFace.TalkRateHz;
            var atZero = EmotionFace.TalkPose(0f, 1f);
            var open = EmotionFace.TalkPose(quarter, 1f);

            Assert.Greater(open.Aa, atZero.Aa);
            Assert.Greater(open.Aa, 0.8f);
            Assert.AreEqual(0f, EmotionFace.TalkPose(quarter, 0f).Aa, 0.0001f);
        }

        [Test]
        public void CombineAndApproach_BlendEmotionIntoTalkAndAcrossEmotions()
        {
            var happy = EmotionFace.EmotionPose(Emotion.Happy, 1f);
            var sad = EmotionFace.EmotionPose(Emotion.Sad, 1f);
            var talk = EmotionFace.TalkPose(0.25f / EmotionFace.TalkRateHz, 1f);
            var both = EmotionFace.Combine(happy, talk);

            Assert.AreEqual(1f, both.Joy, 0.0001f);
            Assert.Greater(both.Aa, 0.8f);

            var surprised = EmotionFace.Combine(EmotionFace.EmotionPose(Emotion.Surprised, 1f), talk);
            Assert.AreEqual(1f, surprised.Aa, 0.0001f);
            Assert.AreEqual(1f, surprised.Surprised, 0.0001f);

            var mid = EmotionFace.Approach(happy, sad, 0.15f, 0.3f);
            Assert.AreEqual(0.5f, mid.Joy, 0.0001f);
            Assert.AreEqual(0.45f, mid.Sorrow, 0.0001f);
            Assert.AreEqual(0.25f, mid.Angry, 0.0001f);

            var done = EmotionFace.Approach(happy, sad, 1f, 0.3f);
            Assert.AreEqual(0f, done.Joy, 0.0001f);
            Assert.AreEqual(0.9f, done.Sorrow, 0.0001f);
        }

        [Test]
        public void Collect_SkipsMissingPresetsAndWritesZeroWhenThePoseReleases()
        {
            var available = new bool[16];
            available[(int)ExpressionPreset.Joy] = true;
            available[(int)ExpressionPreset.Sorrow] = true;
            var writes = new FaceWrite[8];

            var count = EmotionFace.Collect(EmotionFace.EmotionPose(Emotion.Sad, 1f), available, writes);

            Assert.AreEqual(2, count);
            Assert.AreEqual(ExpressionPreset.Joy, writes[0].Preset);
            Assert.AreEqual(0f, writes[0].UnityWeight, 0.01f);
            Assert.AreEqual(ExpressionPreset.Sorrow, writes[1].Preset);
            Assert.AreEqual(90f, writes[1].UnityWeight, 0.01f);
            Assert.AreEqual(0, EmotionFace.Collect(EmotionFace.EmotionPose(Emotion.Happy, 1f), null, writes));
        }
    }
}
