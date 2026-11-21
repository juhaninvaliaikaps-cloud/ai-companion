using System;

namespace AICompanion.GameCompanion
{
    /// <summary>The games she follows: official feeds (JUH-48) and gacha games seen through screen looks (JUH-147).</summary>
    public enum GameTitle
    {
        Unknown = 0,
        CounterStrike2 = 1,
        Dota2 = 2,
        LeagueOfLegends = 3,

        // JUH-147: gacha games. No official feed: screen looks, and pull history for the
        // HoYoverse three (JUH-148).
        GenshinImpact = 4,
        HonkaiStarRail = 5,
        ZenlessZoneZero = 6,
        ArknightsEndfield = 7,
        NevernessToEverness = 8,
    }

    /// <summary>
    /// What happened, the same for every game, so the commentary engine never needs to know
    /// which game it is watching. Every kind is something the player already saw happen.
    /// </summary>
    public enum GameEventKind
    {
        MatchStarted = 0,
        MatchWon = 1,
        MatchLost = 2,
        RoundWon = 3,
        RoundLost = 4,
        Kill = 5,
        Death = 6,
        Assist = 7,
        MultiKill = 8,
        KillStreak = 9,
        FirstBlood = 10,
        Ace = 11,
        ObjectiveTaken = 12,
        ObjectiveLost = 13,
        BombPlanted = 14,
        BombDefused = 15,
        BombExploded = 16,
        LowHealth = 17,
        LevelUp = 18,
    }

    /// <summary>How much a moment matters. Only Normal and Big can be spoken about.</summary>
    public enum GameMoment
    {
        Minor = 0,
        Normal = 1,
        Big = 2,
    }

    /// <summary>
    /// One thing that just happened in the player's game. <see cref="Detail"/> is a short,
    /// safe word or two ("dragon", "tower"), never a player name, a chat line or anything
    /// the player could not see.
    /// </summary>
    public sealed class GameEvent
    {
        public GameTitle Game;
        public GameEventKind Kind;
        public GameMoment Moment;

        /// <summary>How many: kills in a multi-kill, the streak length, the new level.</summary>
        public int Count;

        public string Detail = string.Empty;
        public DateTime AtUtc;

        public static GameEvent Of(GameTitle game, GameEventKind kind, DateTime atUtc, int count = 0, string detail = null)
        {
            return new GameEvent
            {
                Game = game,
                Kind = kind,
                Moment = GameEventRules.DefaultMoment(kind, count),
                Count = count,
                Detail = GameEventRules.SafeDetail(detail),
                AtUtc = atUtc,
            };
        }

        public override string ToString()
        {
            return Kind + (Count > 0 ? " x" + Count : string.Empty) + (Detail.Length > 0 ? " (" + Detail + ")" : string.Empty);
        }
    }

    public static class GameEventRules
    {
        public const int MaxDetailLength = 24;

        public static GameMoment DefaultMoment(GameEventKind kind, int count)
        {
            switch (kind)
            {
                case GameEventKind.MatchWon:
                case GameEventKind.MatchLost:
                case GameEventKind.Ace:
                case GameEventKind.BombDefused:
                    return GameMoment.Big;
                case GameEventKind.MultiKill:
                    return count >= 3 ? GameMoment.Big : GameMoment.Normal;
                case GameEventKind.KillStreak:
                    return count >= 5 ? GameMoment.Big : GameMoment.Normal;
                case GameEventKind.Kill:
                case GameEventKind.Assist:
                case GameEventKind.LevelUp:
                case GameEventKind.LowHealth:
                    return GameMoment.Minor;
                default:
                    return GameMoment.Normal;
            }
        }

        /// <summary>Letters, digits, spaces and hyphens only, short. Anything else is dropped.</summary>
        public static string SafeDetail(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
                return string.Empty;
            var chars = new char[Math.Min(detail.Length, MaxDetailLength)];
            var n = 0;
            for (var i = 0; i < detail.Length && n < chars.Length; i++)
            {
                var c = detail[i];
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-')
                    chars[n++] = c;
            }

            return new string(chars, 0, n).Trim();
        }
    }
}
