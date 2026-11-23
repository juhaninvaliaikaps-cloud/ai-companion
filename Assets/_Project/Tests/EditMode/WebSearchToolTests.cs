using System;
using System.IO;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-29: the keyless search parser keeps titles and sources without HTML noise.</summary>
    public sealed class WebSearchToolTests
    {
        [TearDown]
        public void TearDown()
        {
            WebSearchTool.ResetLookupSlot();
        }

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

        [Test]
        public void BackgroundPatchLookup_DoesNotCoolDownOrReplaceUserSearch()
        {
            var now = DateTime.UtcNow;
            var user = new WebSearchResponse { Ok = true, Query = "my cat" };
            var started = WebSearchTool.Claim(false, "my cat", now);
            Assert.IsFalse(started.Refused);
            WebSearchTool.Remember(false, "my cat", user, now);

            var background = WebSearchTool.Claim(true, "Arknights: Endfield latest patch notes what changed meta", now.AddSeconds(1));
            Assert.IsFalse(background.Refused);
            WebSearchTool.Remember(true, "Arknights: Endfield latest patch notes what changed meta",
                new WebSearchResponse { Ok = true, Query = "patch" }, now.AddSeconds(1));

            var again = WebSearchTool.Claim(false, "my cat", now.AddSeconds(2));
            Assert.IsNotNull(again.Cached);
            Assert.IsTrue(again.Cached.FromCache);
            Assert.AreEqual("my cat", again.Cached.Query);

            WebSearchTool.ResetLookupSlot();
            WebSearchTool.Claim(true, "Arknights: Endfield latest patch notes what changed meta", now);
            var typed = WebSearchTool.Claim(false, "hello", now.AddSeconds(1));
            Assert.IsFalse(typed.Refused);
            Assert.IsNull(typed.Cached);
        }
    }
}
