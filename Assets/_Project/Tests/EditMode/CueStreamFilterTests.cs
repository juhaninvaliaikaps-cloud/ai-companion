using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class CueStreamFilterTests
    {
        [Test]
        public void Push_ReleasesPlainTextImmediately()
        {
            var filter = new CueStreamFilter();

            Assert.AreEqual("Hello there", filter.Push("Hello there"));
        }

        [Test]
        public void Push_HoldsBackAPartialTag()
        {
            var filter = new CueStreamFilter();

            Assert.AreEqual("Hi ", filter.Push("Hi <ani"));
            Assert.AreEqual(string.Empty, filter.Push("mation emotion=\"happy\" />"));
        }

        [Test]
        public void Push_ReleasesALessThanThatCannotBeATag()
        {
            var filter = new CueStreamFilter();

            Assert.AreEqual("2 < 3", filter.Push("2 < 3"));
        }

        [Test]
        public void Flush_ReleasesTheVisibleTailAndParsesTheCue()
        {
            var filter = new CueStreamFilter();
            filter.Push("Nice!\n<animation emotion=\"happy\" gesture=\"nod\" />");

            Assert.AreEqual(string.Empty, filter.Flush());
            Assert.AreEqual("Nice!", filter.Text);
            Assert.AreEqual(CueParseStatus.Parsed, filter.Status);
            Assert.AreEqual(Emotion.Happy, filter.Cue.Emotion);
            Assert.AreEqual("nod", filter.Cue.Gesture);
        }

        [Test]
        public void Flush_ReleasesAHeldBackPrefixThatWasNotATag()
        {
            var filter = new CueStreamFilter();
            filter.Push("Two <ani");

            var rest = filter.Flush();

            Assert.AreEqual("<ani", rest);
            Assert.AreEqual("Two <ani", filter.Text);
            Assert.AreEqual(CueParseStatus.None, filter.Status);
        }

        [Test]
        public void Push_TagSplitAcrossChunksNeverReachesTheDelta()
        {
            var filter = new CueStreamFilter();
            var shown = filter.Push("Hey <anim");
            shown += filter.Push("ation emotion=\"sad\" inten");
            shown += filter.Push("sity=\"0.4\" />");
            shown += filter.Flush();

            StringAssert.DoesNotContain("<", shown);
            StringAssert.DoesNotContain("emotion", shown);
            Assert.AreEqual("Hey", filter.Text);
            Assert.AreEqual(CueParseStatus.Parsed, filter.Status);
            Assert.AreEqual(Emotion.Sad, filter.Cue.Emotion);
            Assert.AreEqual(0.4f, filter.Cue.Intensity, 0.0001f);
        }

        [Test]
        public void Push_LaterLessThanDoesNotReleaseAnEarlierTag()
        {
            var filter = new CueStreamFilter();
            var shown = filter.Push("Hi <animation emotion=\"happy\" gesture=\"wave\" />");
            shown += filter.Push(" <");
            shown += filter.Flush();

            StringAssert.DoesNotContain("animation", shown);
            StringAssert.DoesNotContain("wave", shown);
            Assert.AreEqual(CueParseStatus.Parsed, filter.Status);
            Assert.AreEqual(Emotion.Happy, filter.Cue.Emotion);
            Assert.AreEqual("wave", filter.Cue.Gesture);
            StringAssert.StartsWith("Hi", filter.Text);
        }

        [Test]
        public void Push_ReleasesTextAfterAClosedTagWithoutTheTag()
        {
            var filter = new CueStreamFilter();

            var shown = filter.Push("Hello <animation emotion=\"surprised\" /> there");

            StringAssert.DoesNotContain("<", shown);
            StringAssert.Contains("there", shown);
            Assert.AreEqual(string.Empty, filter.Flush());
            Assert.AreEqual("Hello there", filter.Text);
            Assert.AreEqual(Emotion.Surprised, filter.Cue.Emotion);
        }

        [Test]
        public void Flush_StripsAnUnclosedTagAndDoesNotLeakIt()
        {
            var filter = new CueStreamFilter();
            var shown = filter.Push("Here you go.\n<animation emotion=\"ha");
            shown += filter.Flush();

            StringAssert.DoesNotContain("<", shown);
            Assert.AreEqual("Here you go.", filter.Text);
            Assert.AreEqual(CueParseStatus.Invalid, filter.Status);
            Assert.AreEqual(Emotion.Neutral, filter.Cue.Emotion);
        }
    }
}
