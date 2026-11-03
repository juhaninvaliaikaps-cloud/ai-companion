using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class LooseCueTagTests
    {
        [Test]
        public void AReplyWithALooseTagKeepsItsWords()
        {
            var result = AnimationCueParser.Parse("Hello! How are you today?\n<emotion=\"happy\" intensity=\"0.8\" gesture=\"wave_hello\" />");

            StringAssert.Contains("Hello! How are you today?", result.Text);
            StringAssert.DoesNotContain("emotion=", result.Text);
            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual(Emotion.Happy, result.Cue.Emotion);
            Assert.AreEqual("wave_hello", result.Cue.Gesture);
        }

        [Test]
        public void ALooseTagOnItsOwnLeavesNoText()
        {
            var result = AnimationCueParser.Parse("<emotion=\"happy\" gesture=\"wave_hello\" />");

            Assert.AreEqual(string.Empty, result.Text);
            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual("wave_hello", result.Cue.Gesture);
        }

        [Test]
        public void ALooseTagWithOnlyAnEmotionIsStillACue()
        {
            var result = AnimationCueParser.Parse("Oh no.\n<emotion=\"sad\" intensity=\"0.7\" />");

            StringAssert.Contains("Oh no.", result.Text);
            Assert.AreEqual(Emotion.Sad, result.Cue.Emotion);
        }

        [Test]
        public void OrdinaryMarkupIsLeftAlone()
        {
            var result = AnimationCueParser.Parse("Use <b>bold</b> and <i>italics</i> please.");

            Assert.AreEqual("Use <b>bold</b> and <i>italics</i> please.", result.Text);
            Assert.AreEqual(CueParseStatus.None, result.Status);
        }

        [Test]
        public void AStreamingLooseTagIsHeldBack()
        {
            var partial = AnimationCueParser.VisiblePrefix("Hi <emot");
            Assert.AreEqual("Hi", partial.TrimEnd());
            Assert.IsFalse(partial.Contains("<"));

            var longer = AnimationCueParser.VisiblePrefix("Hi <emotion=\"happy\"");
            Assert.AreEqual("Hi", longer.TrimEnd());
            Assert.IsFalse(longer.Contains("<"));
        }

        [Test]
        public void AFinishedLooseTagIsRemovedWhileStreaming()
        {
            Assert.AreEqual("Hi", AnimationCueParser.VisiblePrefix("Hi <emotion=\"happy\" />"));
        }
    }
}
