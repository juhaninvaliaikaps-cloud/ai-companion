using System;
using AICompanion.NewUI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-182: Settings tabs, sub-tabs and when each page shows.</summary>
    public sealed class SettingsMenuTests
    {
        [Test]
        public void EightTabs_InOrder()
        {
            CollectionAssert.AreEqual(new[] { "Her", "AI", "Voice", "Look", "Chat", "Senses", "18+", "App" }, SettingsMenu.TabLabels);
            Assert.AreEqual(Enum.GetValues(typeof(SettingsTab)).Length, SettingsMenu.TabCount);
        }

        [Test]
        public void EveryPageBelongsToExactlyOneTab_AndHasALabel()
        {
            var counted = 0;
            for (var t = 0; t < SettingsMenu.TabCount; t++)
            {
                var pages = SettingsMenu.Visible((SettingsTab)t, true, true);
                Assert.LessOrEqual(pages.Count, SettingsMenu.MaxPages, "the view keeps MaxPages sub-tab buttons");
                foreach (var page in pages)
                {
                    Assert.AreEqual((SettingsTab)t, SettingsMenu.TabOf(page), page.ToString());
                    Assert.IsFalse(string.IsNullOrEmpty(SettingsMenu.Label(page)));
                    Assert.AreNotEqual(page.ToString(), SettingsMenu.Label(page), page + " has a written label");
                    counted++;
                }
            }

            Assert.AreEqual(Enum.GetValues(typeof(SettingsPage)).Length, counted, "every page is on a tab");
        }

        [Test]
        public void The18PlusPages_ButStatus_NeedTheOpenGate()
        {
            CollectionAssert.AreEqual(new[] { SettingsPage.AdultStatus }, SettingsMenu.Visible(SettingsTab.Adult, false, false));
            CollectionAssert.AreEqual(
                new[] { SettingsPage.AdultStatus, SettingsPage.AdultHer, SettingsPage.AdultSecrets, SettingsPage.AdultTalk },
                SettingsMenu.Visible(SettingsTab.Adult, true, false));
        }

        [Test]
        public void No18PlusPageIsOnAnotherTab()
        {
            for (var t = 0; t < SettingsMenu.TabCount; t++)
            {
                if ((SettingsTab)t == SettingsTab.Adult)
                    continue;
                foreach (var page in SettingsMenu.Visible((SettingsTab)t, true, true))
                    StringAssert.DoesNotStartWith("Adult", page.ToString());
            }
        }

        [Test]
        public void Developer_ShowsOnlyWithItsSwitch()
        {
            CollectionAssert.DoesNotContain(SettingsMenu.Visible(SettingsTab.App, true, false), SettingsPage.AppDeveloper);
            CollectionAssert.Contains(SettingsMenu.Visible(SettingsTab.App, false, true), SettingsPage.AppDeveloper);
        }

        [Test]
        public void Resolve_KeepsTheRememberedPage_WhileItShows()
        {
            Assert.AreEqual(SettingsPage.VoiceEngine, SettingsMenu.Resolve(SettingsTab.Voice, SettingsPage.VoiceEngine, false, false));
            Assert.AreEqual(SettingsPage.VoiceSpeaking, SettingsMenu.Resolve(SettingsTab.Voice, SettingsPage.AiChat, false, false), "a page of another tab");
            Assert.AreEqual(SettingsPage.AdultStatus, SettingsMenu.Resolve(SettingsTab.Adult, SettingsPage.AdultHer, false, false), "the gate closed");
            Assert.AreEqual(SettingsPage.AppKeys, SettingsMenu.Resolve(SettingsTab.App, SettingsPage.AppDeveloper, false, false), "developer settings off");
        }

        [Test]
        public void ThePisaraKey_IsOnTheAiTab()
        {
            Assert.AreEqual(SettingsTab.Ai, SettingsMenu.TabOf(SettingsPage.AiPisara));
            Assert.AreEqual("Pisara", SettingsMenu.Label(SettingsPage.AiPisara));
        }

        [TestCase("Short one. And the rest of it.", "Short one. And the rest of it.")]
        [TestCase("This is the first sentence here. And this is the second one.", "This is the first sentence here.")]
        [TestCase("Ollama at http://127.0.0.1:11434/v1. Nothing leaves this PC.", "Ollama at http://127.0.0.1:11434/v1.")]
        [TestCase("No sentence break at all in this line", "No sentence break at all in this line")]
        [TestCase("Put the file in %APPDATA%\\ProjectLeia\\video. then more", "Put the file in %APPDATA%\\ProjectLeia\\video. then more")]
        [TestCase("", "")]
        public void Notes_ShortFormIsTheFirstSentence(string text, string expected)
        {
            Assert.AreEqual(expected, SettingsNotes.Short(text));
        }
    }
}
