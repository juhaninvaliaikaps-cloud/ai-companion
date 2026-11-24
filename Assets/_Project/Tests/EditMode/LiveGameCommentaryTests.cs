using System;
using AICompanion.Core;
using AICompanion.GameCompanion;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-160: the game note and the live prompt stay separate from her persona.</summary>
    public sealed class LiveGameCommentaryTests
    {
        [TearDown]
        public void TearDown()
        {
            GameContextNow.Clear();
            GameKnowledge.Forget();
        }

        [Test]
        public void GameContextAddsGameObservationAndPatch()
        {
            GameContextNow.Clear();
            GameContextNow.SetGame("Arknights: Endfield");
            GameContextNow.SetObservation("The player reached a new area.");
            GameContextNow.SetPatchContext("Patch 1.2 changed the stamina cap.");

            var prompt = GameContextNow.Append("You are Hermione.");

            StringAssert.Contains("Arknights: Endfield", prompt);
            StringAssert.Contains("reached a new area", prompt);
            StringAssert.Contains("stamina cap", prompt);
            GameContextNow.Clear();
            Assert.IsFalse(GameContextNow.Active);
        }

        [Test]
        public void LivePromptRecognisesSilence()
        {
            Assert.IsTrue(LiveGamePrompt.IsSilent("SILENT"));
            Assert.IsTrue(LiveGamePrompt.IsSilent(" silent "));
            Assert.IsFalse(LiveGamePrompt.IsSilent("That was a clean landing."));
        }

        [Test]
        public void CachedPrimer_IsRestoredForTheOpenGame()
        {
            GameContextNow.SetGame(CommentaryPrompt.GameName(GameTitle.ArknightsEndfield));
            GameKnowledge.Remember(GameTitle.ArknightsEndfield, "Endfield hub and squad combat.");

            string got = null;
            GameKnowledge.Request(null, GameTitle.ArknightsEndfield, text => got = text);

            Assert.AreEqual("Endfield hub and squad combat.", got);
            var prompt = GameContextNow.Append("prompt");
            StringAssert.Contains("Game primer", prompt);
            StringAssert.Contains("Endfield hub", prompt);
            StringAssert.DoesNotContain("patch/meta", prompt);
        }

        [Test]
        public void PatchNotes_StaySeparateFromTheGamePage()
        {
            GameContextNow.SetGame(CommentaryPrompt.GameName(GameTitle.ArknightsEndfield));
            Assert.IsTrue(GameKnowledge.ApplyPrimer(GameTitle.ArknightsEndfield, "Endfield hub and squad combat."));
            Assert.IsTrue(GameKnowledge.ApplyPatch(GameTitle.ArknightsEndfield, "Patch 1.2 changed the stamina cap."));

            var prompt = GameContextNow.Append("prompt");
            StringAssert.Contains("Endfield hub", prompt);
            StringAssert.Contains("stamina cap", prompt);
            Assert.AreEqual(prompt.IndexOf("Endfield hub", StringComparison.Ordinal), prompt.LastIndexOf("Endfield hub", StringComparison.Ordinal));
        }

        [Test]
        public void Primer_StaysOffADifferentGame()
        {
            GameContextNow.SetGame("Counter-Strike 2");
            GameContextNow.SetPrimer("bomb sites");

            Assert.IsFalse(GameKnowledge.ApplyPrimer(GameTitle.ArknightsEndfield, "Endfield page"));

            var prompt = GameContextNow.Append("prompt");
            StringAssert.Contains("bomb sites", prompt);
            StringAssert.DoesNotContain("Endfield page", prompt);
        }

        [Test]
        public void PatchContext_FillsOnlyWhileThatGameIsOpen()
        {
            GameContextNow.SetGame(CommentaryPrompt.GameName(GameTitle.ArknightsEndfield));
            Assert.IsTrue(GameKnowledge.ApplyPatch(GameTitle.ArknightsEndfield, "Patch 1.2 changed the stamina cap."));
            var prompt = GameContextNow.Append("prompt");
            StringAssert.Contains("patch/meta", prompt);
            StringAssert.Contains("stamina cap", prompt);

            GameContextNow.SetGame("Counter-Strike 2");
            Assert.IsFalse(GameKnowledge.ApplyPatch(GameTitle.ArknightsEndfield, "Endfield patch"));
            StringAssert.DoesNotContain("Endfield patch", GameContextNow.Append("prompt"));
        }

        [Test]
        public void FeedEvent_DoesNotWipeAnOpenGachaGame()
        {
            GameContextNow.SetGame(CommentaryPrompt.GameName(GameTitle.ArknightsEndfield));
            GameContextNow.SetPrimer("Endfield primer");
            GameContextNow.SetObservation("The player reached a new area.");

            GameContextOwnership.NoteFeed(GameTitle.CounterStrike2, GameTitle.ArknightsEndfield);
            GameContextOwnership.NoteFeed(GameTitle.Dota2, GameTitle.Unknown);
            GameContextOwnership.NoteFeed(GameTitle.LeagueOfLegends, GameTitle.Unknown);

            Assert.AreEqual("Arknights: Endfield", GameContextNow.Game);
            var prompt = GameContextNow.Append("prompt");
            StringAssert.Contains("Endfield primer", prompt);
            StringAssert.Contains("new area", prompt);
            StringAssert.DoesNotContain("Counter-Strike 2", prompt);
            StringAssert.DoesNotContain("Dota 2", prompt);
            StringAssert.DoesNotContain("League of Legends", prompt);
        }

        [Test]
        public void FeedEvent_NamesTheGameWhenNoGachaIsOpen()
        {
            GameContextOwnership.NoteFeed(GameTitle.CounterStrike2, GameTitle.Unknown);
            GameContextNow.SetObservation("An ace.");
            GameContextOwnership.NoteFeed(GameTitle.CounterStrike2, GameTitle.Unknown);

            Assert.AreEqual("Counter-Strike 2", GameContextNow.Game);
            StringAssert.Contains("An ace.", GameContextNow.Append("prompt"));

            GameContextOwnership.NoteFeed(GameTitle.Dota2, GameTitle.Unknown);
            Assert.AreEqual("Dota 2", GameContextNow.Game);
        }
    }
}
