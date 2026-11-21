using System;
using System.Collections.Generic;
using AICompanion.GameCompanion;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-148: her reactions to your pulls, from the history link the game caches.</summary>
    public sealed class GachaHistoryTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);

        const string GenshinPage = "https://webstatic-sea.hoyoverse.com/genshin/event/e20190909gacha-v2/index.html?win_mode=fullscreen&authkey_ver=1&sign_type=2&auth_appid=webview_gacha&init_type=301&gacha_id=abc&lang=en&device_type=pc&game_version=OS&plat_type=pc&region=os_euro&authkey=OLD%2Bkey&game_biz=hk4e_global#/log";
        const string GenshinApi = "https://public-operation-hk4e-sg.hoyoverse.com/gacha_info/api/getGachaLog?win_mode=fullscreen&authkey_ver=1&sign_type=2&auth_appid=webview_gacha&init_type=301&timestamp=123&lang=en&region=os_euro&authkey=NEW%2Bkey&game_biz=hk4e_global&gacha_type=301&page=1&size=5&end_id=0";
        const string StarRailApi = "https://public-operation-hkrpg-sg.hoyoverse.com/common/gacha_record/api/getGachaLog?authkey_ver=1&sign_type=2&auth_appid=webview_gacha&authkey=RAIL&game_biz=hkrpg_global&region=prod_official_eur&gacha_type=11&page=1&size=5&end_id=0";
        const string Evil = "https://evil.example.com/steal?authkey=X&game_biz=hk4e_global";

        static string Cache(params string[] urls)
        {
            return "\u0000\u0001garbage 1/0/" + string.Join("\u0000\u0003binary\u0002 1/0/", urls) + "\u0000end";
        }

        [Test]
        public void TheNewestLinkForTheGame_IsFound_AndOnlyOnHoyoHosts()
        {
            var cache = Cache(GenshinPage, StarRailApi, GenshinApi, Evil);
            Assert.AreEqual(GenshinApi, GachaHistoryLink.FindLatest(cache, GameTitle.GenshinImpact), "the last Genshin link; never the foreign host");
            Assert.AreEqual(StarRailApi, GachaHistoryLink.FindLatest(cache, GameTitle.HonkaiStarRail));
            Assert.IsNull(GachaHistoryLink.FindLatest(cache, GameTitle.ZenlessZoneZero));
            Assert.IsNull(GachaHistoryLink.FindLatest(Cache(Evil), GameTitle.GenshinImpact));
            Assert.IsNull(GachaHistoryLink.FindLatest(null, GameTitle.GenshinImpact));
            Assert.IsFalse(GachaHistoryLink.Supports(GameTitle.ArknightsEndfield));
            Assert.IsFalse(GachaHistoryLink.Supports(GameTitle.NevernessToEverness));
        }

        [Test]
        public void TheRequest_KeepsOnlyTheAuthParameters_AndGoesToHoyoverse()
        {
            var url = GachaHistoryLink.PageUrl(GameTitle.GenshinImpact, GenshinApi, "302", "0", 20);
            StringAssert.StartsWith("https://public-operation-hk4e-sg.hoyoverse.com/gacha_info/api/getGachaLog?", url);
            StringAssert.Contains("authkey=NEW%2Bkey", url, "the key stays escaped as cached");
            StringAssert.Contains("gacha_type=302", url);
            StringAssert.Contains("size=20", url);
            StringAssert.Contains("lang=en", url);
            StringAssert.DoesNotContain("timestamp", url);
            StringAssert.DoesNotContain("win_mode", url);

            var fromPage = GachaHistoryLink.PageUrl(GameTitle.GenshinImpact, GenshinPage, "301", "0", 20);
            StringAssert.StartsWith("https://public-operation-hk4e-sg.hoyoverse.com/gacha_info/api/getGachaLog?", fromPage, "the page link uses the known API host");

            var zzz = GachaHistoryLink.PageUrl(GameTitle.ZenlessZoneZero, "https://webstatic.example.hoyoverse.com/x?authkey=Z&game_biz=nap_global", "2001", "0", 20);
            StringAssert.StartsWith("https://public-operation-nap-sg.hoyoverse.com/common/gacha_record/api/getGachaLog?", zzz);
            StringAssert.Contains("real_gacha_type=2001", zzz);

            var cn = GachaHistoryLink.PageUrl(GameTitle.HonkaiStarRail, "https://webstatic.mihoyo.com/x?authkey=C&game_biz=hkrpg_cn", "11", "0", 20);
            StringAssert.StartsWith("https://public-operation-hkrpg.mihoyo.com/", cn, "a CN account uses the CN host");

            Assert.IsNull(GachaHistoryLink.PageUrl(GameTitle.GenshinImpact, "https://public-operation-hk4e-sg.hoyoverse.com/x?game_biz=hk4e_global", "301", "0", 20), "no authkey, no request");
            Assert.IsTrue(GachaHistoryLink.IsHoyoHost("public-operation-nap-sg.hoyoverse.com"));
            Assert.IsFalse(GachaHistoryLink.IsHoyoHost("hoyoverse.com.evil.net"));
        }

        static string Page(params string[] items)
        {
            return "{\"retcode\":0,\"message\":\"OK\",\"data\":{\"page\":\"1\",\"size\":\"20\",\"list\":[" + string.Join(",", items) + "],\"region\":\"os_euro\"}}";
        }

        static string Item(long id, string name, string rank)
        {
            return "{\"uid\":\"700000000\",\"gacha_type\":\"301\",\"item_id\":\"\",\"count\":\"1\",\"time\":\"2026-11-21 17:00:00\",\"name\":\"" + name
                   + "\",\"lang\":\"en-us\",\"item_type\":\"Character\",\"rank_type\":\"" + rank + "\",\"id\":\"" + id + "\"}";
        }

        static string Items(long newestId, int count, int topAt, string topName)
        {
            var items = new List<string>();
            for (var i = 0; i < count; i++)
                items.Add(Item(newestId - i, i == topAt ? topName : "Sword", i == topAt ? "5" : "3"));
            return Page(items.ToArray());
        }

        [Test]
        public void APage_IsParsed_WithTheGamesTopRarity()
        {
            var page = GachaLogPage.Parse(Page(Item(1700000000000000002, "Kafka", "5"), Item(1700000000000000001, "Arlan", "4")), GameTitle.HonkaiStarRail);
            Assert.IsTrue(page.Ok);
            Assert.AreEqual(2, page.Pulls.Count);
            Assert.IsTrue(page.Pulls[0].Top);
            Assert.AreEqual("Kafka", page.Pulls[0].Name);
            Assert.IsFalse(page.Pulls[1].Top);

            Assert.IsTrue(GachaLogPage.IsTop(GameTitle.ZenlessZoneZero, "4"), "ZZZ's S-rank is rank 4");
            Assert.IsFalse(GachaLogPage.IsTop(GameTitle.ZenlessZoneZero, "3"));
            Assert.AreEqual(GachaLogPage.AuthkeyTimeout, GachaLogPage.Parse("{\"retcode\":-101,\"message\":\"authkey timeout\",\"data\":null}", GameTitle.GenshinImpact).RetCode);
            Assert.IsFalse(GachaLogPage.Parse("not json", GameTitle.GenshinImpact).Ok);
        }

        [Test]
        public void NewPulls_BecomeEvents_WithThePity()
        {
            var tracker = new GachaBannerTracker();
            const long first = 1700000000000000100;
            // Baseline: the newest 3 pulls have no 5-star, the 4th is one: pity 3.
            Assert.IsEmpty(tracker.Read(GameTitle.GenshinImpact, GachaLogPage.Parse(Items(first, 10, 3, "Diluc"), GameTitle.GenshinImpact), T0));
            Assert.AreEqual(3, tracker.Pity);
            Assert.IsTrue(tracker.PityKnown);

            // A ten-pull with a 5-star as its 7th pull (index 3 from newest): 3 + 7 = pity 10.
            var ten = tracker.Read(GameTitle.GenshinImpact, GachaLogPage.Parse(Items(first + 10, 20, 3, "Furina"), GameTitle.GenshinImpact), T0);
            Assert.AreEqual(1, ten.Count);
            Assert.AreEqual(GameEventKind.GachaTopPull, ten[0].Kind);
            Assert.AreEqual(10, ten[0].Count);
            Assert.AreEqual("Furina", ten[0].Detail);
            Assert.AreEqual(GameMoment.Big, ten[0].Moment);
            Assert.AreEqual(3, tracker.Pity, "the three after her");

            var nothing = tracker.Read(GameTitle.GenshinImpact, GachaLogPage.Parse(Items(first + 20, 20, -1, null), GameTitle.GenshinImpact), T0);
            Assert.AreEqual(1, nothing.Count);
            Assert.AreEqual(GameEventKind.GachaNoTop, nothing[0].Kind);
            Assert.AreEqual(10, nothing[0].Count);
            Assert.AreEqual(GameMoment.Normal, nothing[0].Moment);

            Assert.IsEmpty(tracker.Read(GameTitle.GenshinImpact, GachaLogPage.Parse(Items(first + 20, 20, -1, null), GameTitle.GenshinImpact), T0), "nothing new");
            Assert.IsEmpty(tracker.Read(GameTitle.GenshinImpact, GachaLogPage.Parse("{\"retcode\":-110}", GameTitle.GenshinImpact), T0));

            var single = tracker.Read(GameTitle.GenshinImpact, GachaLogPage.Parse(Items(first + 21, 20, -1, null), GameTitle.GenshinImpact), T0);
            Assert.AreEqual(GameMoment.Minor, single[0].Moment, "one pull without gold is not worth a line");
        }

        [Test]
        public void ThePity_IsUnknown_WhenTheFirstPageHasNoTopPull()
        {
            var tracker = new GachaBannerTracker();
            tracker.Read(GameTitle.ZenlessZoneZero, GachaLogPage.Parse(Items(5000, 20, -1, null), GameTitle.ZenlessZoneZero), T0);
            Assert.IsFalse(tracker.PityKnown);
            var page = new List<string> { Item(5001, "Miyabi", "4") };
            var events = tracker.Read(GameTitle.ZenlessZoneZero, GachaLogPage.Parse(Page(page.ToArray()), GameTitle.ZenlessZoneZero), T0);
            Assert.AreEqual(0, events[0].Count, "no made-up pity");
            StringAssert.Contains("S-rank: Miyabi", CommentaryPrompt.Describe(events[0]));
        }

        [Test]
        public void Ids_CompareAsGrowingNumbers()
        {
            Assert.IsTrue(GachaBannerTracker.Newer("1700000000000000002", "1700000000000000001"));
            Assert.IsTrue(GachaBannerTracker.Newer("10", "9"));
            Assert.IsFalse(GachaBannerTracker.Newer("9", "10"));
            Assert.IsTrue(GachaBannerTracker.Newer("1", string.Empty));
            Assert.IsFalse(GachaBannerTracker.Newer(null, "1"));
        }

        [Test]
        public void TheCache_IsFoundFromTheGamesLog()
        {
            Assert.AreEqual("D:/Games/Genshin Impact game/GenshinImpact_Data",
                GachaCacheFiles.DataFolder(GameTitle.GenshinImpact, "[Subsystems] Discovering subsystems at path D:/Games/Genshin Impact game/GenshinImpact_Data/UnitySubsystems\n"));
            Assert.AreEqual("C:/HoYoPlay/games/Star Rail Games/StarRail_Data",
                GachaCacheFiles.DataFolder(GameTitle.HonkaiStarRail, "Loading player data from C:/HoYoPlay/games/Star Rail Games/StarRail_Data/data.unity3d"));
            Assert.AreEqual("E:/ZZZ/ZenlessZoneZero Game/ZenlessZoneZero_Data",
                GachaCacheFiles.DataFolder(GameTitle.ZenlessZoneZero, "path E:\\ZZZ\\ZenlessZoneZero Game\\ZenlessZoneZero_Data\\StreamingAssets"));
            Assert.IsNull(GachaCacheFiles.DataFolder(GameTitle.GenshinImpact, "nothing here"));

            Assert.AreEqual(2, GachaCacheFiles.LogFiles(GameTitle.HonkaiStarRail, "C:/Users/a/AppData/LocalLow").Count);
            StringAssert.Contains("Cognosphere", GachaCacheFiles.LogFiles(GameTitle.HonkaiStarRail, "C:/Users/a/AppData/LocalLow")[0]);

            var files = GachaCacheFiles.CacheFiles("D:/G/GenshinImpact_Data", new[] { "2.40.0.0", "2.9.0.0", "2.41.0.0" });
            StringAssert.Contains("2.41.0.0", files[0], "the newest version first");
            StringAssert.Contains("2.9.0.0", files[2]);
            StringAssert.DoesNotContain("2.", files[3].Replace("D:/G/", string.Empty), "then the old unversioned place");
            StringAssert.EndsWith("data_2", files[0]);
        }

        [Test]
        public void PullHistory_IsOptIn_AndRoundTrips()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.IsFalse(fresh.GamePullHistory);
            fresh.GamePullHistory = true;
            Assert.IsTrue(CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None).GamePullHistory);
            StringAssert.Contains("5-star: Furina, after 10 pulls",
                CommentaryPrompt.Describe(GameEvent.Of(GameTitle.GenshinImpact, GameEventKind.GachaTopPull, T0, 10, "Furina")));
        }
    }
}
