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
    }
}
