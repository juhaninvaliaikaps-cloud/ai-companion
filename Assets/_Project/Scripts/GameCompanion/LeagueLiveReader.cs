using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// JUH-48: League of Legends through Riot's official Live Client Data API, which the game
    /// serves on this PC during a match (https://127.0.0.1:2999/liveclientdata, with Riot's own
    /// self-signed certificate). It reads <c>eventdata</c> (kills, objectives, game end: things
    /// the game announces to every player) and keeps only what involves the user or their team.
    ///
    /// Riot requires its third-party app approval before a product that uses this is released
    /// (JUH-48 note). Names in <c>eventdata</c> have no #tag; <c>activeplayername</c> returns the
    /// full Riot ID, so names are compared without the tag.
    /// </summary>
    public sealed class LeagueLiveReader
    {
        public const string BaseUrl = "https://127.0.0.1:2999/liveclientdata/";
        public const string EventsUrl = BaseUrl + "eventdata";
        public const string ActivePlayerUrl = BaseUrl + "activeplayername";
        public const string PlayerListUrl = BaseUrl + "playerlist";

        readonly Dictionary<string, string> _teamOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string _me = string.Empty;
        string _myTeam = string.Empty;
        int _lastEventId = -1;

        /// <summary>The user's name (from <see cref="ActivePlayerUrl"/>), without the #tag.</summary>
        public string Me
        {
            get { return _me; }
        }

        public void Reset()
        {
            _teamOf.Clear();
            _me = string.Empty;
            _myTeam = string.Empty;
            _lastEventId = -1;
        }

        /// <summary><see cref="ActivePlayerUrl"/>'s reply: a JSON string such as <c>"Name#EUW"</c>.</summary>
        public void SetActivePlayer(string json)
        {
            _me = WithoutTag(Unquote(json));
            _myTeam = _teamOf.TryGetValue(_me, out var team) ? team : _myTeam;
        }

        /// <summary><see cref="PlayerListUrl"/>'s reply: every player and their team (the scoreboard).</summary>
        public void SetPlayers(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;
            PlayerList list;
            try
            {
                list = JsonUtility.FromJson<PlayerList>("{\"items\":" + json + "}");
            }
            catch (Exception)
            {
                return;
            }

            if (list == null || list.items == null)
                return;
            _teamOf.Clear();
            for (var i = 0; i < list.items.Length; i++)
            {
                var player = list.items[i];
                if (player == null || string.IsNullOrEmpty(player.team))
                    continue;
                var name = !string.IsNullOrEmpty(player.riotIdGameName) ? player.riotIdGameName : WithoutTag(player.summonerName);
                if (!string.IsNullOrEmpty(name))
                    _teamOf[name] = player.team;
                if (!string.IsNullOrEmpty(player.summonerName))
                    _teamOf[WithoutTag(player.summonerName)] = player.team;
            }

            if (_me.Length > 0 && _teamOf.TryGetValue(_me, out var mine))
                _myTeam = mine;
        }

        /// <summary><see cref="EventsUrl"/>'s reply. Returns only events not seen before.</summary>
        public List<GameEvent> Read(string json, DateTime nowUtc)
        {
            var events = new List<GameEvent>();
            if (string.IsNullOrWhiteSpace(json))
                return events;
            EventData data;
            try
            {
                data = JsonUtility.FromJson<EventData>(json);
            }
            catch (Exception)
            {
                return events;
            }

            if (data == null || data.Events == null)
                return events;
            // A new game numbers its events from 0 again.
            if (data.Events.Length > 0 && data.Events[data.Events.Length - 1] != null
                && data.Events[data.Events.Length - 1].EventID < _lastEventId)
                _lastEventId = -1;
            for (var i = 0; i < data.Events.Length; i++)
            {
                var e = data.Events[i];
                if (e == null || e.EventID <= _lastEventId)
                    continue;
                _lastEventId = e.EventID;
                var mapped = Map(e, nowUtc);
                if (mapped != null)
                    events.Add(mapped);
            }

            return events;
        }

        GameEvent Map(LiveEvent e, DateTime nowUtc)
        {
            switch (e.EventName)
            {
                case "GameStart":
                    return Event(GameEventKind.MatchStarted, nowUtc);
                case "GameEnd":
                    if (e.Result == "Win")
                        return Event(GameEventKind.MatchWon, nowUtc);
                    return e.Result == "Lose" ? Event(GameEventKind.MatchLost, nowUtc) : null;
                case "ChampionKill":
                    if (IsMe(e.KillerName))
                        return Event(GameEventKind.Kill, nowUtc, 1);
                    if (IsMe(e.VictimName))
                        return Event(GameEventKind.Death, nowUtc);
                    return Assisted(e) ? Event(GameEventKind.Assist, nowUtc) : null;
                case "Multikill":
                    return IsMe(e.KillerName) ? Event(GameEventKind.MultiKill, nowUtc, e.KillStreak) : null;
                case "FirstBlood":
                    return IsMe(e.Recipient) ? Event(GameEventKind.FirstBlood, nowUtc) : null;
                case "Ace":
                    return IsMe(e.Acer) ? Event(GameEventKind.Ace, nowUtc) : null;
                case "DragonKill":
                    return Objective(e.KillerName, "dragon", nowUtc);
                case "HeraldKill":
                    return Objective(e.KillerName, "herald", nowUtc);
                case "BaronKill":
                    return Objective(e.KillerName, "baron", nowUtc);
                case "TurretKilled":
                    return Structure(e.TurretKilled, "tower", nowUtc);
                case "InhibKilled":
                    return Structure(e.InhibKilled, "inhibitor", nowUtc);
                default:
                    return null;
            }
        }

        bool IsMe(string name)
        {
            return _me.Length > 0 && !string.IsNullOrEmpty(name)
                && string.Equals(WithoutTag(name), _me, StringComparison.OrdinalIgnoreCase);
        }

        bool Assisted(LiveEvent e)
        {
            if (e.Assisters == null)
                return false;
            for (var i = 0; i < e.Assisters.Length; i++)
            {
                if (IsMe(e.Assisters[i]))
                    return true;
            }

            return false;
        }

        /// <summary>A dragon, herald or baron: taken by the user's team, or lost to the other one.</summary>
        GameEvent Objective(string killer, string what, DateTime nowUtc)
        {
            var team = TeamOf(killer);
            if (team.Length == 0 || _myTeam.Length == 0)
                return null;
            return Event(team == _myTeam ? GameEventKind.ObjectiveTaken : GameEventKind.ObjectiveLost, nowUtc, 0, what);
        }

        /// <summary>
        /// A tower or inhibitor. Its name says whose it was: <c>Turret_T1_…</c> and
        /// <c>Barracks_T1_…</c> belong to ORDER, <c>…_T2_…</c> to CHAOS.
        /// </summary>
        GameEvent Structure(string name, string what, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(name) || _myTeam.Length == 0)
                return null;
            var owner = name.IndexOf("_T1_", StringComparison.Ordinal) >= 0 ? "ORDER"
                : name.IndexOf("_T2_", StringComparison.Ordinal) >= 0 ? "CHAOS" : string.Empty;
            if (owner.Length == 0)
                return null;
            return Event(owner == _myTeam ? GameEventKind.ObjectiveLost : GameEventKind.ObjectiveTaken, nowUtc, 0, what);
        }

        string TeamOf(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            // Minions are named by team: Minion_T100… is ORDER, Minion_T200… is CHAOS.
            if (name.StartsWith("Minion_T100", StringComparison.Ordinal))
                return "ORDER";
            if (name.StartsWith("Minion_T200", StringComparison.Ordinal))
                return "CHAOS";
            return _teamOf.TryGetValue(WithoutTag(name), out var team) ? team : string.Empty;
        }

        static GameEvent Event(GameEventKind kind, DateTime nowUtc, int count = 0, string detail = null)
        {
            return GameEvent.Of(GameTitle.LeagueOfLegends, kind, nowUtc, count, detail);
        }

        static string WithoutTag(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            var hash = name.IndexOf('#');
            return (hash >= 0 ? name.Substring(0, hash) : name).Trim();
        }

        static string Unquote(string json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;
            var text = json.Trim();
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
                text = text.Substring(1, text.Length - 2);
            return text.Replace("\\\"", "\"");
        }

#pragma warning disable 0649
        [Serializable] sealed class EventData { public LiveEvent[] Events; }

        [Serializable]
        sealed class LiveEvent
        {
            public int EventID;
            public string EventName = string.Empty;
            public string KillerName = string.Empty;
            public string VictimName = string.Empty;
            public string[] Assisters;
            public int KillStreak;
            public string Recipient = string.Empty;
            public string Acer = string.Empty;
            public string Result = string.Empty;
            public string TurretKilled = string.Empty;
            public string InhibKilled = string.Empty;
        }

        [Serializable] sealed class PlayerList { public LivePlayer[] items; }
        [Serializable] sealed class LivePlayer { public string summonerName = string.Empty; public string riotIdGameName = string.Empty; public string team = string.Empty; }
#pragma warning restore 0649
    }
}
