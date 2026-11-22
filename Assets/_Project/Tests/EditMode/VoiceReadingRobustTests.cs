using AICompanion.AI;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-178: the everyday voice reading survives a reply the local model did not finish.</summary>
    public sealed class VoiceReadingRobustTests
    {
        [Test]
        public void AFinishedObject_IsReturnedUnchanged()
        {
            Assert.AreEqual("{\"a\": \"x\"}", JsonRepair.CloseTruncated("Sure! {\"a\": \"x\"} hope that helps"));
        }

        [Test]
        public void AnUnfinishedObject_KeepsWhatWasComplete()
        {
            Assert.AreEqual("{\"summary\": \"Blunt.\", \"register\": \"casual\"}",
                JsonRepair.CloseTruncated("{\"summary\": \"Blunt.\", \"register\": \"casual\", \"rhythm\": \"short sen"));
            Assert.AreEqual("{\"a\": \"x\", \"habits\": [\"one\", \"two\"]}",
                JsonRepair.CloseTruncated("{\"a\": \"x\", \"habits\": [\"one\", \"two\", \"thr"));
            Assert.AreEqual("{\"a\": \"x\"}", JsonRepair.CloseTruncated("{\"a\": \"x\", \"hab"), "a half-written key is dropped");
            Assert.AreEqual("{\"lines\": {\"content\": [\"Hi.\"]}}", JsonRepair.CloseTruncated("{\"lines\": {\"content\": [\"Hi.\", \"Yo"));
            Assert.IsNull(JsonRepair.CloseTruncated("{\"summ"));
            Assert.IsNull(JsonRepair.CloseTruncated("no json"));
        }

        [Test]
        public void TheSheet_ReadsFromAnUnfinishedReply()
        {
            var sheet = VoiceSheet.Parse("```json\n{\"summary\": \"Clipped and commanding.\", \"register\": \"casual\", \"habits\": [\"gives orders\", \"calls you rookie\"], \"never\": [\"beg");
            Assert.IsNotNull(sheet);
            Assert.AreEqual("Clipped and commanding.", sheet.summary);
            Assert.AreEqual(2, sheet.habits.Length);
            Assert.AreEqual(0, sheet.never.Length, "the unfinished list is dropped");
        }

        [Test]
        public void TheEverydayReading_NoLongerAsksForLines_TheRefreshWritesThem()
        {
            StringAssert.DoesNotContain("\"lines\"", VoiceReader.EverydaySystemPrompt);
            StringAssert.Contains("\"lines\"", VoiceReader.IntimateSystemPrompt);
            Assert.IsTrue(VoicePool.NeedsRefresh(new VoiceSheet().lines.Get("content"), null), "an empty mood is refreshed");
        }

        [Test]
        public void AnUnreadableReply_SaysWhyWithoutItsText()
        {
            StringAssert.Contains("unfinished", VoiceReader.BadReplyDetail("{\"summary\": \"secret words"));
            StringAssert.DoesNotContain("secret words", VoiceReader.BadReplyDetail("{\"summary\": \"secret words"));
            StringAssert.Contains("no JSON", VoiceReader.BadReplyDetail("I can't do that."));
            StringAssert.StartsWith(VoiceReader.BadReply, VoiceReader.BadReplyDetail(""));
            Assert.IsFalse(ReadRetry.IsPassing(VoiceReader.BadReplyDetail("x")), "the reader itself asks once more instead");
        }
    }
}
