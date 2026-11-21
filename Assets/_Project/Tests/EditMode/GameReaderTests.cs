using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AICompanion.GameCompanion;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-48: the game readers. Each game's own official feed becomes the shared game events.
    /// Only the user's own play counts; nothing reads game memory.
    /// </summary>
    public sealed class GameReaderTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 21, 3, 30, 0, DateTimeKind.Utc);
        const string Me = "76561198000000001";

        static string Cs2(string mapPhase = "live", int ct = 0, int t = 0, string roundPhase = "live", string win = "", string bomb = "",
            int kills = 0, int deaths = 0, int assists = 0, int roundKills = 0, int health = 100, string player = Me)
        {
            return "{\"provider\":{\"name\":\"Counter-Strike: Global Offensive\",\"appid\":730,\"steamid\":\"" + Me + "\"}," +
                "\"map\":{\"mode\":\"competitive\",\"name\":\"de_dust2\",\"phase\":\"" + mapPhase + "\",\"round\":3," +
                "\"team_ct\":{\"score\":" + ct + "},\"team_t\":{\"score\":" + t + "}}," +
                "\"round\":{\"phase\":\"" + roundPhase + "\"" + (win.Length > 0 ? ",\"win_team\":\"" + win + "\"" : "") + (bomb.Length > 0 ? ",\"bomb\":\"" + bomb + "\"" : "") + "}," +
                "\"player\":{\"steamid\":\"" + player + "\",\"name\":\"me\",\"team\":\"CT\",\"activity\":\"playing\"," +
                "\"state\":{\"health\":" + health + ",\"armor\":100,\"round_kills\":" + roundKills + "}," +
                "\"match_stats\":{\"kills\":" + kills + ",\"assists\":" + assists + ",\"deaths\":" + deaths + ",\"mvps\":0,\"score\":0}}," +
                "\"auth\":{\"token\":\"tok\"}}";
        }

        static List<GameEventKind> Kinds(List<GameEvent> events)
        {
            return events.Select(e => e.Kind).ToList();
        }

        [Test]
        public void Cs2_ReadsTheRound_TheBomb_AndTheUsersOwnScore()
        {
            var reader = new Cs2GsiReader();
            CollectionAssert.AreEqual(new[] { GameEventKind.MatchStarted }, Kinds(reader.Read(Cs2(), Now)));

            CollectionAssert.AreEqual(new[] { GameEventKind.Kill }, Kinds(reader.Read(Cs2(kills: 1, roundKills: 1), Now)));
            CollectionAssert.AreEqual(new[] { GameEventKind.BombPlanted }, Kinds(reader.Read(Cs2(kills: 1, roundKills: 1, bomb: "planted"), Now)));
            var defused = reader.Read(Cs2(kills: 1, roundKills: 1, bomb: "defused", roundPhase: "over", win: "CT", ct: 1), Now);
            CollectionAssert.AreEquivalent(new[] { GameEventKind.RoundWon, GameEventKind.BombDefused }, Kinds(defused));
            Assert.AreEqual(GameMoment.Big, defused.First(e => e.Kind == GameEventKind.BombDefused).Moment);

            CollectionAssert.AreEqual(new[] { GameEventKind.LowHealth }, Kinds(reader.Read(Cs2(kills: 1, ct: 1, health: 12), Now)));
            CollectionAssert.AreEqual(new[] { GameEventKind.Death }, Kinds(reader.Read(Cs2(kills: 1, ct: 1, deaths: 1, health: 0), Now)));
        }

        [Test]
        public void Cs2_AnAceAndTheMatchResult()
        {
            var reader = new Cs2GsiReader();
            reader.Read(Cs2(kills: 4, roundKills: 4), Now);
            CollectionAssert.Contains(Kinds(reader.Read(Cs2(kills: 5, roundKills: 5), Now)), GameEventKind.Ace);

            reader.Read(Cs2(kills: 5, ct: 13, t: 9), Now);
            CollectionAssert.AreEqual(new[] { GameEventKind.MatchWon }, Kinds(reader.Read(Cs2("gameover", 13, 9, kills: 5), Now)));
        }

        [Test]
        public void Cs2_SpectatingSomeoneElse_IsIgnored()
        {
            var reader = new Cs2GsiReader();
            reader.Read(Cs2(), Now);
            Assert.AreEqual(0, reader.Read(Cs2(kills: 9, player: "76561198000000002"), Now).Count);
            Assert.AreEqual(0, reader.Read("{\"not\":\"gsi\"}", Now).Count);
            Assert.AreEqual(0, reader.Read("garbage", Now).Count);
        }

        static string Dota(string state = "DOTA_GAMERULES_STATE_GAME_IN_PROGRESS", string win = "none", int kills = 0, int deaths = 0,
            int assists = 0, int streak = 0, int level = 1, int health = 100, bool alive = true)
        {
            return "{\"provider\":{\"name\":\"Dota 2\",\"appid\":570}," +
                "\"map\":{\"name\":\"start\",\"game_state\":\"" + state + "\",\"win_team\":\"" + win + "\"}," +
                "\"player\":{\"steamid\":\"" + Me + "\",\"team_name\":\"radiant\",\"kills\":" + kills + ",\"deaths\":" + deaths +
                ",\"assists\":" + assists + ",\"kill_streak\":" + streak + "}," +
                "\"hero\":{\"name\":\"npc_dota_hero_axe\",\"level\":" + level + ",\"alive\":" + (alive ? "true" : "false") +
                ",\"health_percent\":" + health + "},\"auth\":{\"token\":\"tok\"}}";
        }

        [Test]
        public void Dota2_ReadsKills_Streaks_UltimateLevels_AndTheResult()
        {
            var reader = new Dota2GsiReader();
            CollectionAssert.AreEqual(new[] { GameEventKind.MatchStarted }, Kinds(reader.Read(Dota(), Now)));
            CollectionAssert.AreEqual(new[] { GameEventKind.Kill }, Kinds(reader.Read(Dota(kills: 1, streak: 1), Now)));
            var streak = reader.Read(Dota(kills: 3, streak: 3, level: 6), Now);
            CollectionAssert.AreEquivalent(new[] { GameEventKind.Kill, GameEventKind.KillStreak, GameEventKind.LevelUp }, Kinds(streak));
            Assert.AreEqual(3, streak.First(e => e.Kind == GameEventKind.KillStreak).Count);
            CollectionAssert.AreEqual(new[] { GameEventKind.LowHealth }, Kinds(reader.Read(Dota(kills: 3, streak: 3, level: 6, health: 10), Now)));
            CollectionAssert.AreEqual(new[] { GameEventKind.Death }, Kinds(reader.Read(Dota(kills: 3, deaths: 1, level: 6, health: 0, alive: false), Now)));
            CollectionAssert.AreEqual(new[] { GameEventKind.MatchLost },
                Kinds(reader.Read(Dota("DOTA_GAMERULES_STATE_POST_GAME", "dire", kills: 3, deaths: 1, level: 6), Now)));
        }

        const string Players =
            "[{\"championName\":\"Ahri\",\"riotIdGameName\":\"Jussi\",\"summonerName\":\"Jussi#EUW\",\"team\":\"ORDER\"}," +
            "{\"championName\":\"Zed\",\"riotIdGameName\":\"Foe\",\"summonerName\":\"Foe#EUW\",\"team\":\"CHAOS\"}," +
            "{\"championName\":\"Lux\",\"riotIdGameName\":\"Friend\",\"summonerName\":\"Friend#EUW\",\"team\":\"ORDER\"}]";

        static LeagueLiveReader League()
        {
            var reader = new LeagueLiveReader();
            reader.SetActivePlayer("\"Jussi#EUW\"");
            reader.SetPlayers(Players);
            return reader;
        }

        [Test]
        public void League_ReadsTheUsersKills_AndTheTeamsObjectives()
        {
            var reader = League();
            Assert.AreEqual("Jussi", reader.Me);
            var json = "{\"Events\":[" +
                "{\"EventID\":0,\"EventName\":\"GameStart\",\"EventTime\":0.05}," +
                "{\"EventID\":1,\"EventName\":\"ChampionKill\",\"EventTime\":360.5,\"KillerName\":\"Jussi\",\"VictimName\":\"Foe\",\"Assisters\":[]}," +
                "{\"EventID\":2,\"EventName\":\"FirstBlood\",\"EventTime\":360.5,\"Recipient\":\"Jussi\"}," +
                "{\"EventID\":3,\"EventName\":\"DragonKill\",\"EventTime\":600.1,\"DragonType\":\"Earth\",\"Stolen\":\"False\",\"KillerName\":\"Friend\",\"Assisters\":[\"Jussi\"]}," +
                "{\"EventID\":4,\"EventName\":\"ChampionKill\",\"EventTime\":700.0,\"KillerName\":\"Foe\",\"VictimName\":\"Jussi\",\"Assisters\":[]}," +
                "{\"EventID\":5,\"EventName\":\"TurretKilled\",\"EventTime\":800.0,\"TurretKilled\":\"Turret_T2_R_03_A\",\"KillerName\":\"Minion_T100L1S17N0001\",\"Assisters\":[]}," +
                "{\"EventID\":6,\"EventName\":\"BaronKill\",\"EventTime\":1300.0,\"Stolen\":\"False\",\"KillerName\":\"Foe\",\"Assisters\":[]}," +
                "{\"EventID\":7,\"EventName\":\"ChampionKill\",\"EventTime\":1310.0,\"KillerName\":\"Foe\",\"VictimName\":\"Friend\",\"Assisters\":[]}]}";

            var events = reader.Read(json, Now);

            CollectionAssert.AreEqual(new[]
            {
                GameEventKind.MatchStarted, GameEventKind.Kill, GameEventKind.FirstBlood, GameEventKind.ObjectiveTaken,
                GameEventKind.Death, GameEventKind.ObjectiveTaken, GameEventKind.ObjectiveLost,
            }, Kinds(events), "a kill between two other players is not the user's");
            Assert.AreEqual("dragon", events[3].Detail);
            Assert.AreEqual("tower", events[5].Detail, "their tower, taken by our minions");
            Assert.AreEqual("baron", events[6].Detail);
        }

        [Test]
        public void League_OnlyNewEventsCount_AndANewGameStartsOver()
        {
            var reader = League();
            var first = "{\"Events\":[{\"EventID\":0,\"EventName\":\"GameStart\"},{\"EventID\":1,\"EventName\":\"Multikill\",\"KillerName\":\"Jussi\",\"KillStreak\":3}]}";
            var both = reader.Read(first, Now);
            Assert.AreEqual(2, both.Count);
            Assert.AreEqual(GameMoment.Big, both[1].Moment, "a triple kill is a big moment");
            Assert.AreEqual(0, reader.Read(first, Now).Count, "the same events again are not new");

            var won = reader.Read("{\"Events\":[{\"EventID\":0,\"EventName\":\"GameStart\"},{\"EventID\":1,\"EventName\":\"Multikill\",\"KillerName\":\"Jussi\",\"KillStreak\":3},{\"EventID\":2,\"EventName\":\"GameEnd\",\"Result\":\"Win\"}]}", Now);
            CollectionAssert.AreEqual(new[] { GameEventKind.MatchWon }, Kinds(won));

            var next = reader.Read("{\"Events\":[{\"EventID\":0,\"EventName\":\"GameStart\"}]}", Now);
            CollectionAssert.AreEqual(new[] { GameEventKind.MatchStarted }, Kinds(next), "a new game numbers from 0 again");
        }

        [Test]
        public void TheGsiConfig_AsksOnlyForTheUsersOwnData_OnThisPc()
        {
            var cs2 = GsiConfig.Cs2(GsiConfig.DefaultPort, "abc123");
            StringAssert.Contains("\"uri\"       \"http://127.0.0.1:39271/\"", cs2);
            StringAssert.Contains("\"token\" \"abc123\"", cs2);
            StringAssert.Contains("\"player_match_stats\" \"1\"", cs2);
            Assert.IsFalse(cs2.Contains("allplayers"), "never other players' data");
            Assert.IsFalse(GsiConfig.Dota2(GsiConfig.DefaultPort, "abc").Contains("allplayers"));
            Assert.IsFalse(GsiConfig.Cs2(1, "a\"b}c").Contains("a\"b"), "a token cannot break the file");

            Assert.IsTrue(GsiConfig.HasToken(Cs2(), "tok"));
            Assert.IsFalse(GsiConfig.HasToken(Cs2(), "other"));
            Assert.IsFalse(GsiConfig.HasToken("{}", "tok"));
        }

        [Test]
        public void TheListener_TakesPostsWithTheToken_AndDropsTheRest()
        {
            var port = FreePort();
            using (var listener = new GsiListener(port, "tok"))
            {
                if (!listener.Start())
                    Assert.Ignore("This machine does not allow a loopback listener.");

                Assert.AreEqual(200, Post(port, Cs2()));
                Assert.AreEqual(403, Post(port, Cs2().Replace("\"tok\"", "\"wrong\"")));

                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (listener.Pending.Count < 1 && DateTime.UtcNow < deadline)
                    Thread.Sleep(20);
                Assert.AreEqual(1, listener.Pending.Count);
                string body;
                Assert.IsTrue(listener.Pending.TryDequeue(out body));
                StringAssert.Contains("de_dust2", body);
                Assert.AreEqual(1, listener.Dropped);
            }
        }

        static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        static int Post(int port, string body)
        {
            var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Proxy = null;
            request.Timeout = 3000;
            var bytes = Encoding.UTF8.GetBytes(body);
            request.ContentLength = bytes.Length;
            using (var stream = request.GetRequestStream())
                stream.Write(bytes, 0, bytes.Length);
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                    return (int)response.StatusCode;
            }
            catch (WebException e)
            {
                var response = e.Response as HttpWebResponse;
                return response != null ? (int)response.StatusCode : 0;
            }
        }
    }
}
