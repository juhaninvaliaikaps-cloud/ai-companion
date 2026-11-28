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
                string Build(bool ordinaryChat) => (string)prompt.Invoke(chat, new object[] { ordinaryChat, false });
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

        [TestCase(false)]
        [TestCase(true)]
        public void CurrentInitiativeAndDirectnessReachOnlyOrdinaryChat(bool cachedVoice)
        {
            var host = new GameObject("Chat style prompt test");
            host.SetActive(false);
            try
            {
                var chat = host.AddComponent<ChatPanel>();
                var prompt = typeof(ChatPanel).GetMethod("PersonaPrompt", BindingFlags.Instance | BindingFlags.NonPublic);
                string Build(bool ordinary, bool interaction = false) =>
                    (string)prompt.Invoke(chat, new object[] { ordinary, interaction });
                VoiceNow.Set(cachedVoice ? "Fixed voice calibration." : string.Empty, null, null);
                var axes = TemperamentAxes.Neutral();
                PersonaMannerNow.Set(axes);
                var neutral = Build(true);
                StringAssert.DoesNotContain("Initiative:", neutral);
                StringAssert.DoesNotContain("Directness:", neutral);
                axes.Set(TemperamentAxes.Assertiveness, TemperamentSteps.ValueForStep(9));
                axes.Set(TemperamentAxes.Directness, TemperamentSteps.ValueForStep(2));
                PersonaMannerNow.Set(axes);
                var first = Build(true);
                StringAssert.Contains("propose one specific action to try now", first);
                StringAssert.Contains("Start with a relevant observation", first);
                if (cachedVoice)
                    StringAssert.Contains("Fixed voice calibration.\n\nInitiative:", first);
                StringAssert.DoesNotContain("Initiative:", Build(false));
                StringAssert.DoesNotContain("Directness:", Build(true, interaction: true));

                axes.Set(TemperamentAxes.Assertiveness, TemperamentSteps.ValueForStep(2));
                axes.Set(TemperamentAxes.Directness, TemperamentSteps.ValueForStep(9));
                PersonaMannerNow.Set(axes);
                var second = Build(true);
                StringAssert.Contains("Leave the next move with the user", second);
                StringAssert.Contains("State your take plainly in the first sentence", second);
                StringAssert.DoesNotContain("propose one specific action to try now", second);
            }
            finally
            {
                PersonaMannerNow.Reset();
                VoiceNow.Reset();
                Object.DestroyImmediate(host);
            }
        }

        // JUH-236: Openness and Playfulness, live in ordinary chat like the three above, with the same
        // ends as Assertiveness and Directness: stored 30 or less (steps 1-3) and 70 or more (steps 8-10).
        [TestCase(TemperamentAxes.Openness, 2, "Keep your own feelings, memories and private life mostly to yourself")]
        [TestCase(TemperamentAxes.Openness, 3, "Keep your own feelings, memories and private life mostly to yourself")]
        [TestCase(TemperamentAxes.Openness, 5, "")]
        [TestCase(TemperamentAxes.Openness, 7, "")]
        [TestCase(TemperamentAxes.Openness, 8, "Share a little of yourself in most replies")]
        [TestCase(TemperamentAxes.Openness, 9, "Share a little of yourself in most replies")]
        [TestCase(TemperamentAxes.Playfulness, 2, "Do not tease the user, joke or use wordplay")]
        [TestCase(TemperamentAxes.Playfulness, 3, "Do not tease the user, joke or use wordplay")]
        [TestCase(TemperamentAxes.Playfulness, 5, "")]
        [TestCase(TemperamentAxes.Playfulness, 7, "")]
        [TestCase(TemperamentAxes.Playfulness, 8, "one friendly tease or small joke")]
        [TestCase(TemperamentAxes.Playfulness, 9, "one friendly tease or small joke")]
        public void OpennessAndPlayfulness_HaveALineOnlyAtTheEnds(int axis, int step, string expected)
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(axis, TemperamentSteps.ValueForStep(step));
            var line = PersonaManner.ConversationLine(axes);
            var label = axis == TemperamentAxes.Openness ? "Openness:" : "Playfulness:";
            if (expected.Length == 0)
                StringAssert.DoesNotContain(label, line, "the middle keeps her current behaviour");
            else
                StringAssert.Contains(expected, line);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OpennessAndPlayfulness_ReachOrdinaryChat_WithAndWithoutCachedVoice(bool cachedVoice)
        {
            var host = new GameObject("Openness and playfulness prompt test");
            host.SetActive(false);
            try
            {
                var chat = host.AddComponent<ChatPanel>();
                var prompt = typeof(ChatPanel).GetMethod("PersonaPrompt", BindingFlags.Instance | BindingFlags.NonPublic);
                string Build(bool ordinary, bool interaction = false) =>
                    (string)prompt.Invoke(chat, new object[] { ordinary, interaction });
                VoiceNow.Set(cachedVoice ? "Fixed voice calibration." : string.Empty, null, null);
                var axes = TemperamentAxes.Neutral();
                axes.Set(TemperamentAxes.Openness, TemperamentSteps.ValueForStep(9));
                axes.Set(TemperamentAxes.Playfulness, TemperamentSteps.ValueForStep(2));
                PersonaMannerNow.Set(axes);
                var first = Build(true);
                StringAssert.Contains("Share a little of yourself in most replies", first);
                StringAssert.Contains("Do not tease the user, joke or use wordplay", first);
                if (cachedVoice)
                    StringAssert.Contains("Fixed voice calibration.\n\nOpenness:", first, "after the voice block, so the slider wins");
                StringAssert.DoesNotContain("Openness:", Build(false), "not for screen, game or other requests");
                StringAssert.DoesNotContain("Playfulness:", Build(true, interaction: true), "not for avatar interactions");

                axes.Set(TemperamentAxes.Openness, TemperamentSteps.ValueForStep(2));
                axes.Set(TemperamentAxes.Playfulness, TemperamentSteps.ValueForStep(9));
                PersonaMannerNow.Set(axes);
                var second = Build(true);
                StringAssert.Contains("Keep your own feelings, memories and private life mostly to yourself", second);
                StringAssert.Contains("one friendly tease or small joke", second);
                StringAssert.DoesNotContain("Share a little of yourself", second);
            }
            finally
            {
                PersonaMannerNow.Reset();
                VoiceNow.Reset();
                Object.DestroyImmediate(host);
            }
        }

        // JUH-236: the local route's per-turn rule for the low ends.
        [Test]
        public void LocalTurnRule_NamesOnlyTheLowEnds()
        {
            var axes = TemperamentAxes.Neutral();
            Assert.AreEqual(string.Empty, PersonaTurnRule.ForLocal(axes), "the middle adds nothing");
            Assert.AreEqual(string.Empty, PersonaTurnRule.ForLocal(null));

            axes.Set(TemperamentAxes.Openness, TemperamentSteps.ValueForStep(9));
            axes.Set(TemperamentAxes.Playfulness, TemperamentSteps.ValueForStep(9));
            axes.Set(TemperamentAxes.Curiosity, TemperamentSteps.ValueForStep(9));
            Assert.AreEqual(string.Empty, PersonaTurnRule.ForLocal(axes), "the high ends work from the system prompt");

            axes.Set(TemperamentAxes.Openness, TemperamentSteps.ValueForStep(2));
            Assert.AreEqual("(For this reply: " + PersonaTurnRule.NothingAboutHerself + ".)", PersonaTurnRule.ForLocal(axes));

            axes.Set(TemperamentAxes.Playfulness, TemperamentSteps.ValueForStep(3));
            axes.Set(TemperamentAxes.Curiosity, TemperamentSteps.ValueForStep(2));
            Assert.AreEqual("(For this reply: " + PersonaTurnRule.NothingAboutHerself + "; " + PersonaTurnRule.NoJokes + "; "
                + PersonaTurnRule.NoQuestion + ".)", PersonaTurnRule.ForLocal(axes), "one instruction");

            axes.Set(TemperamentAxes.Openness, TemperamentSteps.ValueForStep(4));
            axes.Set(TemperamentAxes.Playfulness, TemperamentSteps.ValueForStep(4));
            axes.Set(TemperamentAxes.Curiosity, TemperamentSteps.ValueForStep(4));
            Assert.AreEqual(string.Empty, PersonaTurnRule.ForLocal(axes), "step 4 is not an end");
        }

        [Test]
        public void LocalTurnRule_GoesOnTheLocalRouteOnly_AfterTheReminder()
        {
            const string reminder = "(Start your reply with your cue tag.)";
            const string rule = "(For this reply: no jokes or teasing.)";
            Assert.AreEqual(reminder + "\n" + rule, PersonaTurnRule.Reminder(reminder, rule, true, false));
            Assert.AreEqual(reminder, PersonaTurnRule.Reminder(reminder, rule, false, false), "the cloud model keeps its own judgment");
            Assert.AreEqual(reminder, PersonaTurnRule.Reminder(reminder, rule, true, true), "not for avatar interactions");
            Assert.AreEqual(reminder, PersonaTurnRule.Reminder(reminder, string.Empty, true, false));
            Assert.AreEqual(rule, PersonaTurnRule.Reminder(null, rule, true, false));

            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Playfulness, TemperamentSteps.ValueForStep(2));
            try
            {
                PersonaMannerNow.Set(axes);
                Assert.AreEqual("(For this reply: " + PersonaTurnRule.NoJokes + ".)", PersonaMannerNow.LocalTurnRule);
                PersonaMannerNow.Reset();
                Assert.AreEqual(string.Empty, PersonaMannerNow.LocalTurnRule);
            }
            finally
            {
                PersonaMannerNow.Reset();
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
