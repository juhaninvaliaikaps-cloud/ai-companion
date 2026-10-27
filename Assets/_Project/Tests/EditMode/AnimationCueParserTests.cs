using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class AnimationCueParserTests
    {
        [Test]
        public void Parse_NoTag_ReturnsTextAndNoCue()
        {
            var result = AnimationCueParser.Parse("  Hello there.  ");

            Assert.AreEqual("  Hello there.  ", result.Text);
            Assert.AreEqual(CueParseStatus.None, result.Status);
            Assert.IsFalse(result.HasCue);
            Assert.AreEqual(Emotion.Neutral, result.Cue.Emotion);
        }

        [Test]
        public void Parse_StripsTagAndReadsAttributes()
        {
            var result = AnimationCueParser.Parse("Nice to see you!\n<animation emotion=\"happy\" intensity=\"0.8\" gesture=\"wave\" />");

            Assert.AreEqual("Nice to see you!", result.Text);
            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual(Emotion.Happy, result.Cue.Emotion);
            Assert.AreEqual(0.8f, result.Cue.Intensity, 0.0001f);
            Assert.AreEqual("wave", result.Cue.Gesture);
        }

        [Test]
        public void Parse_KeepsAnUnknownGestureForTheDirector()
        {
            var result = AnimationCueParser.Parse("Sure.\n<animation emotion=\"neutral\" gesture=\"moonwalk\" />");

            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual("moonwalk", result.Cue.Gesture);
        }

        [Test]
        public void Parse_UnknownEmotion_IsInvalidAndNeutral()
        {
            var result = AnimationCueParser.Parse("Hi.\n<animation emotion=\"furious\" intensity=\"0.9\" />");

            Assert.AreEqual("Hi.", result.Text);
            Assert.AreEqual(CueParseStatus.Invalid, result.Status);
            Assert.AreEqual(Emotion.Neutral, result.Cue.Emotion);
            Assert.AreEqual(AnimationCueParser.NeutralIntensity, result.Cue.Intensity, 0.0001f);
        }

        [Test]
        public void Parse_MalformedTag_IsStrippedFromTheText()
        {
            var result = AnimationCueParser.Parse("Here you go.\n<animation emotion=\"happy\"");

            Assert.AreEqual("Here you go.", result.Text);
            Assert.AreEqual(CueParseStatus.Invalid, result.Status);
        }

        [Test]
        public void Parse_TagWithoutSelfClosingSlash_IsInvalid()
        {
            var result = AnimationCueParser.Parse("Hello\n<animation emotion=\"happy\">");

            Assert.AreEqual("Hello", result.Text);
            Assert.AreEqual(CueParseStatus.Invalid, result.Status);
        }

        [Test]
        public void Parse_ClampsIntensityAndSanitizesGesture()
        {
            var result = AnimationCueParser.Parse("Oh!\n<animation emotion=\"surprised\" intensity=\"2.5\" gesture=\"WAVE!!\" />");

            Assert.AreEqual(1f, result.Cue.Intensity, 0.0001f);
            Assert.AreEqual("wave", result.Cue.Gesture);
        }

        [Test]
        public void Parse_UsesDefaultsWhenAttributesAreMissing()
        {
            var result = AnimationCueParser.Parse("Hello\n<animation />");

            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual(Emotion.Neutral, result.Cue.Emotion);
            Assert.AreEqual(AnimationCueParser.DefaultIntensity, result.Cue.Intensity, 0.0001f);
            Assert.AreEqual(string.Empty, result.Cue.Gesture);
        }

        [Test]
        public void Parse_KeepsTextAfterAClosedTag()
        {
            var result = AnimationCueParser.Parse("Hello\n<animation emotion=\"happy\" intensity=\"0.8\" />\nthere");

            Assert.AreEqual("Hello\nthere", result.Text);
            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual(Emotion.Happy, result.Cue.Emotion);
            Assert.AreEqual(0.8f, result.Cue.Intensity, 0.0001f);
        }

        [Test]
        public void Parse_DoesNotTreatALongerWordAsACueTag()
        {
            var result = AnimationCueParser.Parse("See the <animations> list");

            Assert.AreEqual("See the <animations> list", result.Text);
            Assert.AreEqual(CueParseStatus.None, result.Status);
        }

        [Test]
        public void Parse_ReadsSingleQuotedAttributes()
        {
            var result = AnimationCueParser.Parse("Ok.\n<animation emotion='sad' intensity='0.25' gesture='nod' />");

            Assert.AreEqual("Ok.", result.Text);
            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual(Emotion.Sad, result.Cue.Emotion);
            Assert.AreEqual(0.25f, result.Cue.Intensity, 0.0001f);
            Assert.AreEqual("nod", result.Cue.Gesture);
        }

        [Test]
        public void Parse_NaNIntensity_IsInvalid()
        {
            var result = AnimationCueParser.Parse("Hi.\n<animation emotion=\"happy\" intensity=\"NaN\" />");

            Assert.AreEqual("Hi.", result.Text);
            Assert.AreEqual(CueParseStatus.Invalid, result.Status);
            Assert.AreEqual(Emotion.Neutral, result.Cue.Emotion);
            Assert.AreEqual(AnimationCueParser.NeutralIntensity, result.Cue.Intensity, 0.0001f);
        }

        [Test]
        public void Parse_LastClosedTagWinsAndEarlierTagsAreStripped()
        {
            var result = AnimationCueParser.Parse(
                "Hi <animation emotion=\"happy\" /> there <animation emotion=\"sad\" gesture=\"shrug\" />");

            Assert.AreEqual("Hi there", result.Text);
            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual(Emotion.Sad, result.Cue.Emotion);
            Assert.AreEqual("shrug", result.Cue.Gesture);
        }
    }
}
