using System.Collections.Generic;
using System.Reflection;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.UI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-171: she keeps her manner and stops reusing stock phrases.</summary>
    public sealed class ChatVoiceTests
    {
        // ---- Repeated phrases --------------------------------------------------------------------

        [Test]
        public void JussisCase_CantJust_IsNamed()
        {
            var replies = new List<string>
            {
                "\"You can't just—\" *She starts, her cheeks burning.* \"That's not fair.\"",
                "\"We can't just... do this here,\" she whispers.",
                "*She looks away.* \"I can't just pretend nothing happened.\"",
            };
            var phrases = RepeatedPhrases.Find(replies);
            CollectionAssert.Contains(phrases, "can't just");
            StringAssert.Contains("\"can't just\"", RepeatedPhrases.Line(phrases));
        }

        [Test]
        public void SmallWordsAlone_AndTooFewReplies_NameNothing()
        {
            Assert.AreEqual(0, RepeatedPhrases.Find(new List<string> { "one of the best", "most of the time", "out of the blue" }).Count, "\"of the\" is never named");
            Assert.AreEqual(0, RepeatedPhrases.Find(new List<string> { "can't just", "can't just" }).Count, "needs three replies");
            Assert.AreEqual(0, RepeatedPhrases.Find(null).Count);
            Assert.AreEqual(string.Empty, RepeatedPhrases.Line(new List<string>()));
        }

        [Test]
        public void TheLongestPhrase_IsNamedOnce()
        {
            var replies = new List<string>
            {
                "Her cheeks flushed red at that.",
                "Again her cheeks flushed red.",
                "She laughed, her cheeks flushed red.",
            };
            var phrases = RepeatedPhrases.Find(replies);
            CollectionAssert.Contains(phrases, "her cheeks flushed red");
            CollectionAssert.DoesNotContain(phrases, "cheeks flushed", "inside the longer one");
            Assert.LessOrEqual(phrases.Count, RepeatedPhrases.MaxPhrases);
        }

        [Test]
        public void Words_AreLowerCase_WithStraightApostrophes()
        {
            CollectionAssert.AreEqual(new[] { "you", "can't", "just" }, RepeatedPhrases.Words("You can’t just—"));
        }

        [Test]
        public void OnlyTheLastFiveRepliesCount()
        {
            var replies = new List<string> { "can't just", "can't just", "can't just", "a", "b", "c", "d", "e" };
            Assert.AreEqual(0, RepeatedPhrases.Find(replies).Count);
        }

        // ---- Her manner -------------------------------------------------------------------------

        [Test]
        public void ASelfAssuredLeader_KeepsHerMannerInRomanticScenes()
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Assertiveness, 85f);
            axes.Set(TemperamentAxes.Confidence, 90f);
            axes.Set(TemperamentAxes.Directness, 75f);
            var line = PersonaManner.Line(axes);
            StringAssert.Contains("takes the lead", line);
            StringAssert.Contains("self-assured", line);
            StringAssert.Contains("says what she means", line);
            StringAssert.Contains("romantic and intimate ones included", line);
            StringAssert.Contains(PersonaManner.NotTimid, line);
        }

        [Test]
        public void AnInsecureOne_IsNotToldToBeBold_AndTheMiddleSaysNothing()
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Confidence, 15f);
            var line = PersonaManner.Line(axes);
            StringAssert.Contains("insecure", line);
            StringAssert.DoesNotContain(PersonaManner.NotTimid, line);

            Assert.AreEqual(string.Empty, PersonaManner.Line(TemperamentAxes.Neutral()));
            Assert.AreEqual(string.Empty, PersonaManner.Line(null));

            PersonaMannerNow.Set(axes);
            StringAssert.StartsWith("Persona.\nHer manner:", PersonaMannerNow.Append("Persona."));
            PersonaMannerNow.Reset();
            Assert.AreEqual("Persona.", PersonaMannerNow.Append("Persona."));
        }

        [TestCase(1, "Let the user lead")]
        [TestCase(3, "Let the user lead")]
        [TestCase(4, "")]
        [TestCase(5, "")]
        [TestCase(6, "")]
        [TestCase(7, "Ask at most one")]
        [TestCase(8, "Ask at most one")]
        [TestCase(9, "one specific follow-up")]
        [TestCase(10, "one specific follow-up")]
        public void CuriosityRuleMatchesTheVisibleStep(int step, string expected)
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Curiosity, TemperamentSteps.ValueForStep(step));
            var line = PersonaManner.CuriosityLine(axes);
            if (expected.Length == 0)
                Assert.AreEqual(string.Empty, line);
            else
                StringAssert.Contains(expected, line);
        }

        [Test]
        public void LiveCuriosityFollowsCachedVoiceOnlyForOrdinaryChat()
        {
            var host = new GameObject("Chat prompt test");
            host.SetActive(false);
            try
            {
                var chat = host.AddComponent<ChatPanel>();
                var prompt = typeof(ChatPanel).GetMethod("PersonaPrompt", BindingFlags.Instance | BindingFlags.NonPublic);
                string Build(bool ordinaryChat) => (string)prompt.Invoke(chat, new object[] { ordinaryChat });
                var axes = TemperamentAxes.Neutral();
                axes.Set(TemperamentAxes.Curiosity, 100);
                PersonaMannerNow.Set(axes);
                VoiceNow.Reset();
                StringAssert.Contains("one specific follow-up", Build(true));

                VoiceNow.Set("Cached voice calibration.", null, null);
                StringAssert.Contains("Cached voice calibration.\n\nConversation curiosity:", Build(true));
                StringAssert.DoesNotContain("Conversation curiosity:", Build(false));

                axes.Set(TemperamentAxes.Curiosity, 0);
                PersonaMannerNow.Set(axes);
                StringAssert.Contains("Let the user lead", Build(true));
                StringAssert.DoesNotContain("one specific follow-up", Build(true));
                PersonaMannerNow.Reset();
                StringAssert.DoesNotContain("Conversation curiosity:", Build(true));
            }
            finally
            {
                PersonaMannerNow.Reset();
                VoiceNow.Reset();
                Object.DestroyImmediate(host);
            }
        }

        // ---- Sampler settings -------------------------------------------------------------------

        [Test]
        public void HerReplies_GetTheRepeatPenalty_OtherRequestsDoNot()
        {
            var messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "hi") };
            var chat = OllamaChatJson.Build("demo", 0.7f, 64, "persona", messages, 8192, true);
            StringAssert.Contains("\"repeat_penalty\":1.08", chat);
            StringAssert.Contains("\"repeat_last_n\":1024", chat);
            StringAssert.Contains("\"min_p\":0.05", chat);
            StringAssert.Contains("\"temperature\":0.7", chat, "the user's temperature is kept");

            var reading = OllamaChatJson.Build("demo", 0.3f, 64, "read", messages, 8192);
            StringAssert.DoesNotContain("repeat_penalty", reading);

            var conversation = new ChatConversation();
            Assert.IsTrue(conversation.BuildRequest("persona", 0.7f, 64).Roleplay, "the conversation is her talking");
            Assert.IsFalse(new ChatRequest().Roleplay);
        }
    }
}
