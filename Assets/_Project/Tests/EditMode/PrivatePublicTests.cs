using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Settings;
using AICompanion.UI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-177: what is said in private mode stays out of public requests, and a code-written sentence takes its place.</summary>
    public sealed class PrivatePublicTests
    {
        static string Join(ChatRequest request)
        {
            var text = string.Empty;
            for (var i = 0; i < request.Messages.Count; i++)
                text += request.Messages[i].Content + "\n";
            return text;
        }

        [Test]
        public void EverythingSaidInPrivate_StaysOutOfTheNextPublicRequest()
        {
            var conversation = new ChatConversation { MarkLocalNow = true };
            Assert.IsTrue(conversation.TryBeginSend("my secret plan", "persona", 0.5f, 64, out var _));
            conversation.Complete(ChatResult.Success("I'll keep it."));
            conversation.AddAssistant("A remark she made on her own.");
            Assert.IsTrue(conversation.Lines[0].LocalOnly, "a plain message in private mode, not only 18+ ones");
            Assert.IsTrue(conversation.Lines[1].LocalOnly);
            Assert.IsTrue(conversation.Lines[2].LocalOnly);

            conversation.MarkLocalNow = false;
            Assert.IsTrue(conversation.AnyLocalOnlyInWindow);
            Assert.IsTrue(conversation.TryBeginSend("hello", "persona", 0.5f, 64, out var cloud, new ChatTurnOptions { OmitLocalOnly = true }));
            var sent = Join(cloud);
            StringAssert.DoesNotContain("secret plan", sent);
            StringAssert.DoesNotContain("keep it", sent);
            StringAssert.DoesNotContain("on her own", sent);
            StringAssert.Contains("hello", sent);
            conversation.Complete(ChatResult.Success("Hi."));
            Assert.IsFalse(conversation.Lines[3].LocalOnly, "a public line is not local-only");

            Assert.IsTrue(conversation.TryBeginSend("back in private", "persona", 0.5f, 64, out var local));
            StringAssert.Contains("secret plan", Join(local), "the local model sees the whole conversation again");
        }

        [Test]
        public void TheKind_ComesFromTheCueOnly_AndTheStrongestStays()
        {
            Assert.AreEqual(PrivateChapterKind.Personal, PrivateChapter.KindOf(LandingEvent.Thanked, DesireHeat.None, "happy", CompanionMood.Happy, false));
            Assert.AreEqual(PrivateChapterKind.Romantic, PrivateChapter.KindOf(LandingEvent.Flirted, DesireHeat.None, "happy", CompanionMood.Content, false));
            Assert.AreEqual(PrivateChapterKind.Romantic, PrivateChapter.KindOf(LandingEvent.Neutral, DesireHeat.Warm, "neutral", CompanionMood.Content, false));
            Assert.AreEqual(PrivateChapterKind.Intimate, PrivateChapter.KindOf(LandingEvent.Neutral, DesireHeat.Hot, "neutral", CompanionMood.Content, false));
            Assert.AreEqual(PrivateChapterKind.Intimate, PrivateChapter.KindOf(LandingEvent.Neutral, DesireHeat.None, "neutral", CompanionMood.Content, true), "an 18+ interaction");

            var chapter = new PrivateChapter();
            Assert.IsFalse(chapter.Known);
            chapter.Note(LandingEvent.Neutral, DesireHeat.Hot, "neutral", CompanionMood.Content, false);
            chapter.Note(LandingEvent.Thanked, DesireHeat.None, "happy", CompanionMood.Happy, false);
            Assert.AreEqual(PrivateChapterKind.Intimate, chapter.Kind, "a later calm reply does not lower it");
            chapter.Reset();
            Assert.IsFalse(chapter.Known);
        }

        [Test]
        public void TheSentence_FollowsTheSetting_AndHasNoPrivateText()
        {
            var chapter = new PrivateChapter();
            chapter.Note(LandingEvent.Neutral, DesireHeat.Release, "neutral", CompanionMood.Content, false);
            Assert.AreEqual(string.Empty, PrivateChapterPrompt.Line(PrivateChapterHint.Nothing, chapter));
            Assert.AreEqual(PrivateChapterPrompt.Happened, PrivateChapterPrompt.Line(PrivateChapterHint.ThatItHappened, chapter));
            Assert.AreEqual(PrivateChapterPrompt.Intimate, PrivateChapterPrompt.Line(PrivateChapterHint.WhatKind, chapter));
            Assert.AreEqual(PrivateChapterPrompt.Happened, PrivateChapterPrompt.Line(PrivateChapterHint.WhatKind, new PrivateChapter()), "after a restart the kind is unknown");
            StringAssert.Contains("Don't describe it", PrivateChapterPrompt.Intimate);

            StringAssert.Contains("stays on this PC", PrivateChapterPrompt.SwitchNote(false, PrivateChapterHint.WhatKind));
            StringAssert.Contains("local model", PrivateChapterPrompt.SwitchNote(true, PrivateChapterHint.WhatKind));
            Assert.AreEqual(PrivateChapterHint.WhatKind, PrivateChapterPrompt.Normalize(7));
        }

        [Test]
        public void TheSetting_DefaultsToWhatKind_AndRoundTrips()
        {
            var settings = new CompanionSettings();
            Assert.AreEqual((int)PrivateChapterHint.WhatKind, settings.PrivateChapterHint);
            settings.PrivateChapterHint = (int)PrivateChapterHint.Nothing;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);
            Assert.AreEqual((int)PrivateChapterHint.Nothing, back.PrivateChapterHint);

            PrivateChapterNow.Configure(1);
            Assert.AreEqual(PrivateChapterHint.ThatItHappened, PrivateChapterNow.Hint);
            PrivateChapterNow.Reset();
        }
    }
}
