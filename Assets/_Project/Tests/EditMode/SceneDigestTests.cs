using System.Text;
using AICompanion.UI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-129: a long scene keeps its place, time and clothes after it leaves the chat window.</summary>
    public sealed class SceneDigestTests
    {
        const int Window = ChatConversation.DefaultMaxMessages;
        const int Chars = ChatConversation.DefaultMaxCharacters;

        [Test]
        public void ItIsDue_OnlyOnceTheSessionNoLongerFits_AndEveryFewMessages()
        {
            var digest = new SceneDigest();

            Assert.IsFalse(digest.IsDue(9, 30, 9000, Window, Chars), "too few user messages yet");
            Assert.IsFalse(digest.IsDue(10, 12, 3000, Window, Chars), "the model still sees all of it");
            Assert.IsTrue(digest.IsDue(10, 20, 3000, Window, Chars), "more messages than the window");
            Assert.IsTrue(digest.IsDue(10, 10, 5000, Window, Chars), "more characters than the window");

            digest.Begin(10);
            Assert.IsFalse(digest.IsDue(25, 50, 9000, Window, Chars), "one at a time");
            digest.Finish(digest.Generation, "They are in her kitchen at night.", true);
            Assert.IsFalse(digest.IsDue(19, 40, 9000, Window, Chars));
            Assert.IsTrue(digest.IsDue(20, 40, 9000, Window, Chars));
        }

        [Test]
        public void ADigestOfALocalSession_NeverGoesToTheCloud()
        {
            var digest = new SceneDigest();
            var generation = digest.Begin(10);
            Assert.IsTrue(digest.Finish(generation, "They are in her bedroom; she is wearing only a shirt.", keptLocal: true));

            Assert.AreEqual(string.Empty, digest.PromptBlock(false));
            Assert.AreEqual(string.Empty, digest.PreviousFor(false), "nor to a cloud digest request");
            StringAssert.Contains("only a shirt", digest.PromptBlock(true));
            Assert.IsFalse(digest.Append("base", false).Contains("only a shirt"));
            StringAssert.Contains("only a shirt", digest.Append("base", true));
        }

        [Test]
        public void TheContinuityRule_IsAlwaysThere()
        {
            var digest = new SceneDigest();
            var prompt = digest.Append("You are Lola.", false);

            StringAssert.StartsWith("You are Lola.", prompt);
            StringAssert.Contains(SceneDigest.ContinuityRule, prompt);
            Assert.IsFalse(prompt.Contains(SceneDigest.Header), "no digest yet");
        }

        [Test]
        public void ADigestForAnEndedSession_IsDropped_AndAnEmptyReplyKeepsTheOldOne()
        {
            var digest = new SceneDigest();
            var first = digest.Begin(10);
            digest.Finish(first, "They are on the beach at sunset.", false);

            var second = digest.Begin(20);
            Assert.IsFalse(digest.Finish(second, "   ", false));
            Assert.AreEqual("They are on the beach at sunset.", digest.Text);
            Assert.IsFalse(digest.Running);

            var third = digest.Begin(30);
            digest.Reset();
            Assert.IsFalse(digest.Finish(third, "A late answer for a session that ended.", false));
            Assert.AreEqual(string.Empty, digest.Text);
        }

        [Test]
        public void TheRequest_CarriesNoMemories_ThePreviousNotes_AndTheEndOfALongTranscript()
        {
            var transcript = new StringBuilder();
            for (var i = 0; i < 400; i++)
                transcript.Append("User: line ").Append(i).Append(" of the scene\n");
            transcript.Append("Lola: the last line");

            var request = SceneDigest.BuildRequest("They are in a cabin in Lapland.", transcript.ToString());
            var body = request.Messages[0].Content;

            Assert.IsTrue(request.NoMemory);
            Assert.AreEqual(SceneDigest.SystemPrompt, request.SystemPrompt);
            StringAssert.Contains("Earlier notes:\nThey are in a cabin in Lapland.", body);
            StringAssert.EndsWith("Lola: the last line", body);
            Assert.IsFalse(body.Contains("line 0 of"), "the oldest part is covered by the earlier notes");
            StringAssert.Contains("\nUser: line ", body);
            Assert.LessOrEqual(body.Length, SceneDigest.MaxTranscriptCharacters + 200);
        }

        [Test]
        public void Clean_KeepsOnlyPlainSentences()
        {
            var clean = SceneDigest.Clean("Notes:\n- They are in her kitchen, late evening.\n* Lola wears a red dress. <animation emotion=\"happy\"/>\n\nJussi is cooking pasta.");

            Assert.AreEqual("They are in her kitchen, late evening. Lola wears a red dress. Jussi is cooking pasta.", clean);
            Assert.AreEqual(string.Empty, SceneDigest.Clean("  \n "));
            Assert.LessOrEqual(SceneDigest.Clean(new string('a', 3000)).Length, SceneDigest.MaxDigestCharacters);
        }
    }
}
