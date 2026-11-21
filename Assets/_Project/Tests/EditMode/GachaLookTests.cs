using System;
using AICompanion.GameCompanion;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-147: gacha games are found by their window, and she looks every 2 minutes or on the hotkey.</summary>
    public sealed class GachaLookTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 21, 16, 0, 0, DateTimeKind.Utc);

        [Test]
        public void TheGame_IsFoundByProcessName_OrExactTitle()
        {
            Assert.AreEqual(GameTitle.GenshinImpact, GachaGames.Match("GenshinImpact", "anything"));
            Assert.AreEqual(GameTitle.GenshinImpact, GachaGames.Match("YuanShen.exe", ""));
            Assert.AreEqual(GameTitle.HonkaiStarRail, GachaGames.Match("StarRail", null));
            Assert.AreEqual(GameTitle.ZenlessZoneZero, GachaGames.Match("ZenlessZoneZero", "ZenlessZoneZero"));
            Assert.AreEqual(GameTitle.ArknightsEndfield, GachaGames.Match("", "Arknights: Endfield"));
            Assert.AreEqual(GameTitle.NevernessToEverness, GachaGames.Match("", "Neverness to Everness"));
            Assert.AreEqual(GameTitle.HonkaiStarRail, GachaGames.Match(null, "honkai: star rail"), "titles ignore case");
        }

        [Test]
        public void ABrowserTabAboutTheGame_IsNotTheGame()
        {
            Assert.AreEqual(GameTitle.Unknown, GachaGames.Match("chrome", "Genshin Impact tier list - Google Chrome"));
            Assert.AreEqual(GameTitle.Unknown, GachaGames.Match("Discord", "#zenless-zone-zero - Discord"));
            Assert.AreEqual(GameTitle.Unknown, GachaGames.Match(null, null));
            foreach (var game in GachaGames.All)
                Assert.IsTrue(GachaGames.IsGacha(game));
            Assert.IsFalse(GachaGames.IsGacha(GameTitle.CounterStrike2));
        }

        [Test]
        public void SheLooksEveryTwoMinutes_OnlyWhileTheGameIsInFront()
        {
            var look = new GachaLookSchedule();
            bool asked;
            Assert.IsFalse(look.Due(T0, out asked), "the game isn't in front");

            look.Front(true, T0);
            Assert.IsFalse(look.Due(T0.AddSeconds(5), out asked), "a quick alt-tab is not a look");
            Assert.IsTrue(look.Due(T0.AddSeconds(GachaLookSchedule.SettleSeconds), out asked));
            Assert.IsFalse(asked);
            look.Looked(T0.AddSeconds(20));

            Assert.IsFalse(look.Due(T0.AddSeconds(100), out asked));
            Assert.IsTrue(look.Due(T0.AddSeconds(20 + GachaLookSchedule.IntervalSeconds), out asked));

            look.Front(false, T0.AddSeconds(200));
            Assert.IsFalse(look.Due(T0.AddSeconds(400), out asked), "she doesn't look while you're in another window");
        }

        [Test]
        public void TheHotkey_LooksNow_ButNotTwiceInTenSeconds()
        {
            var look = new GachaLookSchedule();
            bool asked;
            look.Front(true, T0);
            look.AskNow();
            Assert.IsTrue(look.Due(T0.AddSeconds(1), out asked), "the hotkey skips the settle time");
            Assert.IsTrue(asked);
            look.Looked(T0.AddSeconds(1));

            look.AskNow();
            Assert.IsFalse(look.Due(T0.AddSeconds(5), out asked), "too soon after the last look");
            Assert.IsTrue(look.Due(T0.AddSeconds(1 + GachaLookSchedule.MinGapSeconds), out asked));
            Assert.IsTrue(asked);
        }

        [Test]
        public void SheMaySaySilent()
        {
            Assert.IsTrue(GachaLookPrompt.IsSilent("SILENT"));
            Assert.IsTrue(GachaLookPrompt.IsSilent(" silent. "));
            Assert.IsTrue(GachaLookPrompt.IsSilent("*shrugs* SILENT"));
            Assert.IsTrue(GachaLookPrompt.IsSilent(""));
            Assert.IsFalse(GachaLookPrompt.IsSilent("Ooh, the Abyss again?"));
            StringAssert.Contains("Genshin Impact", GachaLookPrompt.Turn(GameTitle.GenshinImpact, false));
            StringAssert.Contains(GachaLookPrompt.Silent, GachaLookPrompt.Turn(GameTitle.GenshinImpact, false));
            StringAssert.Contains("asked", GachaLookPrompt.Turn(GameTitle.ZenlessZoneZero, true));
            StringAssert.Contains("user IDs", GachaLookPrompt.Rules);
        }

        [Test]
        public void TheGachaSettings_DefaultToNoLooks_AndRoundTrip()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.IsFalse(fresh.GameScreenLooks, "pictures to the cloud are opt-in");
            foreach (var game in GachaGames.All)
                Assert.IsTrue(GameCompanionController.Enabled(fresh, game), game.ToString());
            Assert.IsFalse(GameCompanionController.Enabled(fresh, GameTitle.CounterStrike2));

            fresh.GameScreenLooks = true;
            fresh.GameZzz = false;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None);
            Assert.IsTrue(back.GameScreenLooks);
            Assert.IsFalse(back.GameZzz);
            Assert.IsTrue(back.GameGenshin);
            Assert.IsFalse(GameCompanionController.Enabled(back, GameTitle.ZenlessZoneZero));
        }
    }
}
