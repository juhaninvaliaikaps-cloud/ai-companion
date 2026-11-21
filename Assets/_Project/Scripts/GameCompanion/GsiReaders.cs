using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// JUH-48: Counter-Strike 2 Game State Integration. The game itself posts its state as JSON
    /// to the address in our config file (see <see cref="GsiConfig"/>). This reads only the
    /// user's own player, the round and the map: what the player sees on the HUD. It never asks
    /// for <c>allplayers</c>, and it ignores a payload about someone they are spectating.
    /// Each call compares with the last payload and returns what changed.
    /// </summary>
    public sealed class Cs2GsiReader
    {
        public const int LowHealth = 25;

        Cs2Payload _last;

        public void Reset()
        {
            _last = null;
        }

        public List<GameEvent> Read(string json, DateTime nowUtc)
        {
            var events = new List<GameEvent>();
            var now = Parse(json);
            if (now == null)
                return events;
            // Spectating someone else (or dead and watching a teammate): not the user's play.
            if (!string.IsNullOrEmpty(now.provider.steamid) && !string.IsNullOrEmpty(now.player.steamid)
                && !string.Equals(now.provider.steamid, now.player.steamid, StringComparison.Ordinal))
            {
                return events;
            }

            var before = _last;
            _last = now;
            if (before == null)
            {
                if (now.map.phase == "live")
                    events.Add(Event(GameEventKind.MatchStarted, nowUtc));
                return events;
            }

            var team = now.player.team ?? string.Empty;
            if (before.map.phase != "live" && now.map.phase == "live" && before.map.phase != "intermission")
                events.Add(Event(GameEventKind.MatchStarted, nowUtc));
            if (before.map.phase != "gameover" && now.map.phase == "gameover")
            {
                var ours = team == "CT" ? now.map.team_ct.score : now.map.team_t.score;
                var theirs = team == "CT" ? now.map.team_t.score : now.map.team_ct.score;
                if (team.Length > 0 && ours != theirs)
                    events.Add(Event(ours > theirs ? GameEventKind.MatchWon : GameEventKind.MatchLost, nowUtc));
            }

            if (before.round.phase != "over" && now.round.phase == "over" && team.Length > 0 && !string.IsNullOrEmpty(now.round.win_team))
                events.Add(Event(now.round.win_team == team ? GameEventKind.RoundWon : GameEventKind.RoundLost, nowUtc));

            if (before.round.bomb != now.round.bomb)
            {
                if (now.round.bomb == "planted")
                    events.Add(Event(GameEventKind.BombPlanted, nowUtc));
                else if (now.round.bomb == "defused")
                    events.Add(Event(GameEventKind.BombDefused, nowUtc));
                else if (now.round.bomb == "exploded")
                    events.Add(Event(GameEventKind.BombExploded, nowUtc));
            }

            var kills = now.player.match_stats.kills - before.player.match_stats.kills;
            if (kills > 0 && kills < 10)
                events.Add(Event(GameEventKind.Kill, nowUtc, kills));
            if (now.player.match_stats.deaths > before.player.match_stats.deaths)
                events.Add(Event(GameEventKind.Death, nowUtc));
            if (now.player.match_stats.assists > before.player.match_stats.assists)
                events.Add(Event(GameEventKind.Assist, nowUtc));
            if (before.player.state.round_kills < 5 && now.player.state.round_kills >= 5)
                events.Add(Event(GameEventKind.Ace, nowUtc));
            if (before.player.state.health >= LowHealth && now.player.state.health > 0 && now.player.state.health < LowHealth)
                events.Add(Event(GameEventKind.LowHealth, nowUtc));
            return events;
        }

        static GameEvent Event(GameEventKind kind, DateTime nowUtc, int count = 0)
        {
            return GameEvent.Of(GameTitle.CounterStrike2, kind, nowUtc, count);
        }

        static Cs2Payload Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.IndexOf("\"provider\"", StringComparison.Ordinal) < 0)
                return null;
            try
            {
                return JsonUtility.FromJson<Cs2Payload>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

#pragma warning disable 0649
        [Serializable] sealed class Cs2Payload { public Provider provider = new Provider(); public Map map = new Map(); public Round round = new Round(); public Player player = new Player(); }
        [Serializable] sealed class Provider { public string steamid = string.Empty; }
        [Serializable] sealed class Map { public string phase = string.Empty; public Team team_ct = new Team(); public Team team_t = new Team(); }
        [Serializable] sealed class Team { public int score; }
        [Serializable] sealed class Round { public string phase = string.Empty; public string win_team = string.Empty; public string bomb = string.Empty; }
        [Serializable] sealed class Player { public string steamid = string.Empty; public string team = string.Empty; public State state = new State(); public Stats match_stats = new Stats(); }
        [Serializable] sealed class State { public int health = 100; public int round_kills; }
        [Serializable] sealed class Stats { public int kills; public int deaths; public int assists; }
#pragma warning restore 0649
    }

    /// <summary>
    /// JUH-48: Dota 2 Game State Integration. Reads the user's own player, hero and the map
    /// state, never other players. Each call compares with the last payload.
    /// </summary>
    public sealed class Dota2GsiReader
    {
        public const int LowHealthPercent = 20;
        static readonly int[] StreakSteps = { 3, 5, 7, 10 };
        static readonly int[] LevelSteps = { 6, 12, 18, 25 };

        DotaPayload _last;

        public void Reset()
        {
            _last = null;
        }

        public List<GameEvent> Read(string json, DateTime nowUtc)
        {
            var events = new List<GameEvent>();
            var now = Parse(json);
            if (now == null)
                return events;
            var before = _last;
            _last = now;

            const string inProgress = "DOTA_GAMERULES_STATE_GAME_IN_PROGRESS";
            const string postGame = "DOTA_GAMERULES_STATE_POST_GAME";
            var state = now.map.game_state ?? string.Empty;
            if (before == null)
            {
                if (state == inProgress)
                    events.Add(Event(GameEventKind.MatchStarted, nowUtc));
                return events;
            }

            if (before.map.game_state != inProgress && state == inProgress)
                events.Add(Event(GameEventKind.MatchStarted, nowUtc));
            if (before.map.game_state != postGame && state == postGame)
            {
                var win = now.map.win_team ?? string.Empty;
                var team = now.player.team_name ?? string.Empty;
                if (team.Length > 0 && (win == "radiant" || win == "dire"))
                    events.Add(Event(win == team ? GameEventKind.MatchWon : GameEventKind.MatchLost, nowUtc));
            }

            var kills = now.player.kills - before.player.kills;
            if (kills > 0 && kills < 10)
                events.Add(Event(GameEventKind.Kill, nowUtc, kills));
            if (now.player.deaths > before.player.deaths)
                events.Add(Event(GameEventKind.Death, nowUtc));
            if (now.player.assists > before.player.assists)
                events.Add(Event(GameEventKind.Assist, nowUtc));
            for (var i = 0; i < StreakSteps.Length; i++)
            {
                if (before.player.kill_streak < StreakSteps[i] && now.player.kill_streak >= StreakSteps[i])
                    events.Add(Event(GameEventKind.KillStreak, nowUtc, StreakSteps[i]));
            }

            for (var i = 0; i < LevelSteps.Length; i++)
            {
                if (before.hero.level < LevelSteps[i] && now.hero.level >= LevelSteps[i])
                    events.Add(Event(GameEventKind.LevelUp, nowUtc, LevelSteps[i]));
            }

            if (now.hero.alive && before.hero.health_percent >= LowHealthPercent && now.hero.health_percent > 0
                && now.hero.health_percent < LowHealthPercent)
                events.Add(Event(GameEventKind.LowHealth, nowUtc));
            return events;
        }

        static GameEvent Event(GameEventKind kind, DateTime nowUtc, int count = 0)
        {
            return GameEvent.Of(GameTitle.Dota2, kind, nowUtc, count);
        }

        static DotaPayload Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.IndexOf("\"map\"", StringComparison.Ordinal) < 0)
                return null;
            try
            {
                return JsonUtility.FromJson<DotaPayload>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

#pragma warning disable 0649
        [Serializable] sealed class DotaPayload { public Map map = new Map(); public Player player = new Player(); public Hero hero = new Hero(); }
        [Serializable] sealed class Map { public string game_state = string.Empty; public string win_team = string.Empty; }
        [Serializable] sealed class Player { public string team_name = string.Empty; public int kills; public int deaths; public int assists; public int kill_streak; }
        [Serializable] sealed class Hero { public int level; public bool alive = true; public int health_percent = 100; }
#pragma warning restore 0649
    }

    /// <summary>
    /// JUH-48: the GSI config file the user copies into the game (installing it automatically is
    /// Jussi's decision, so for now it is a manual step). It sends only the user's own data to
    /// this PC (loopback), with a token so other programs cannot pose as the game.
    /// </summary>
    public static class GsiConfig
    {
        public const string FileName = "gamestate_integration_happi.cfg";
        public const int DefaultPort = 39271;

        /// <summary>Where the file goes, under the game's install folder.</summary>
        public const string Cs2Folder = @"game\csgo\cfg";
        public const string Dota2Folder = @"game\dota\cfg\gamestate_integration";

        public static string Cs2(int port, string token)
        {
            return Build(port, token,
                "\"provider\" \"1\"", "\"map\" \"1\"", "\"round\" \"1\"", "\"player_id\" \"1\"", "\"player_state\" \"1\"", "\"player_match_stats\" \"1\"");
        }

        public static string Dota2(int port, string token)
        {
            return Build(port, token, "\"provider\" \"1\"", "\"map\" \"1\"", "\"player\" \"1\"", "\"hero\" \"1\"");
        }

        /// <summary>A random token for this install (letters and digits).</summary>
        public static string NewToken()
        {
            return Guid.NewGuid().ToString("N");
        }

        static string Build(int port, string token, params string[] data)
        {
            var sb = new System.Text.StringBuilder(512);
            sb.Append("\"Happi game companion\"\n{\n");
            sb.Append(" \"uri\"       \"http://127.0.0.1:").Append(port).Append("/\"\n");
            sb.Append(" \"timeout\"   \"5.0\"\n \"buffer\"    \"0.1\"\n \"throttle\"  \"0.5\"\n \"heartbeat\" \"30.0\"\n");
            sb.Append(" \"auth\"\n {\n  \"token\" \"").Append(SafeToken(token)).Append("\"\n }\n");
            sb.Append(" \"data\"\n {\n");
            for (var i = 0; i < data.Length; i++)
                sb.Append("  ").Append(data[i]).Append('\n');
            sb.Append(" }\n}\n");
            return sb.ToString();
        }

        /// <summary>True when the payload carries this install's token.</summary>
        public static bool HasToken(string json, string token)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(token))
                return false;
            try
            {
                var auth = JsonUtility.FromJson<AuthOnly>(json);
                return auth != null && auth.auth != null && string.Equals(auth.auth.token, token, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string SafeToken(string token)
        {
            var sb = new System.Text.StringBuilder();
            if (token != null)
            {
                for (var i = 0; i < token.Length; i++)
                {
                    if (char.IsLetterOrDigit(token[i]) && token[i] < 128)
                        sb.Append(token[i]);
                }
            }

            return sb.ToString();
        }

#pragma warning disable 0649
        [Serializable] sealed class AuthOnly { public Auth auth = new Auth(); }
        [Serializable] sealed class Auth { public string token = string.Empty; }
#pragma warning restore 0649
    }
}
