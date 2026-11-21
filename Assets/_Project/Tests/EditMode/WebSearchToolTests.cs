using System.IO;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-29: the keyless search parser keeps titles and sources without HTML noise.</summary>
    public sealed class WebSearchToolTests
    {
        [Test]
        public void Parse_ReadsCurrentNewsItems()
        {
            const string xml = "<rss><channel>" +
                "<item><title>Counter-Strike 2 patch notes &amp; changes</title>" +
                "<link>https://news.google.com/rss/articles/abc</link>" +
                "<description>&lt;a href=\"x\"&gt;What changed in the latest update.&lt;/a&gt;</description>" +
                "<source url=\"https://example.com/cs2\">Example Gaming</source></item>" +
                "<item><title>Second story</title><link>https://example.com/second</link>" +
                "<description>More context.</description></item>" +
                "</channel></rss>";

            var response = WebSearchTool.Parse("CS2 patch", xml);

            Assert.IsTrue(response.Ok);
            Assert.AreEqual(2, response.Results.Count);
            Assert.AreEqual("Counter-Strike 2 patch notes & changes", response.Results[0].Title);
            Assert.AreEqual("https://example.com/cs2", response.Results[0].Url);
            Assert.AreEqual("Example Gaming", response.Results[0].Source);
            StringAssert.Contains("latest update", response.Results[0].Snippet);
        }

        [Test]
        public void EmptySearch_IsAnError()
        {
            var response = WebSearchTool.Parse("nothing", string.Empty);
            Assert.IsFalse(response.Ok);
            StringAssert.Contains("no results", response.Error);
        }

        [Test]
        public void SearchResults_DoNotCarryIntoTheNextMessage()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/ChatPanel.cs"));
            var send = source.IndexOf("TrySend(cleaned);", System.StringComparison.Ordinal);
            Assert.Greater(send, 0);
            var clear = source.IndexOf("_webSearchContext = null;", send, System.StringComparison.Ordinal);
            Assert.Greater(clear, send);
            Assert.Less(clear - send, 200, "The results are cleared right after the send.");
        }
    }
}
