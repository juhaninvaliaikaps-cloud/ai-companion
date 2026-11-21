using System;
using AICompanion.Core;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// How she sounds while watching a game (JUH-49). It is not a separate picker: it comes from
    /// her personality, the nine temperament sliders and the relationship role that the Pisara
    /// reading sets (Jussi, 2026-11-21). A bratty persona laughs when you die ("You suck!"); a
    /// sweet one comforts you.
    /// </summary>
    public enum GameVoice
    {
        /// <summary>Steady, dry, low energy. Also the middle of every slider.</summary>
        Cool = 0,

        /// <summary>Bratty: playful and pushy, laughs at your deaths, cocky when you win.</summary>
        Teasing = 1,

        /// <summary>Loud and warm: the hype friend.</summary>
        Hype = 2,

        /// <summary>Warm and gentle: comforts you when it goes wrong.</summary>
        Sweet = 3,

        /// <summary>Quiet and unsure of herself, cheers in a small voice.</summary>
        Shy = 4,

        /// <summary>Cares a lot and will not say so.</summary>
        Tsundere = 5,

        /// <summary>Cold and reactive: sighs, grumbles, rarely impressed.</summary>
        Grumpy = 6,
    }

    /// <summary>What a pre-written line reacts to. Several event kinds share one.</summary>
    public enum ReactionMoment
    {
        None = 0,
        MatchStart = 1,
        Win = 2,
        Loss = 3,
        Death = 4,
        Kill = 5,
        BigPlay = 6,
        ObjectiveTaken = 7,
        ObjectiveLost = 8,
        Tense = 9,
        LevelUp = 10,
    }

    /// <summary>One reaction: the line, her face and an optional gesture.</summary>
    public struct GameReaction
    {
        public string Text;
        public Emotion Face;

        /// <summary>A gesture name from <see cref="AvatarAnimatorModel"/>, or empty.</summary>
        public string Gesture;

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(Text); }
        }
    }

    public static class GameVoices
    {
        /// <summary>
        /// Her game voice from her sliders and relationship role. Rules run in this order, so
        /// a clearer trait wins over a vaguer one:
        /// 1. Tsundere: warm, hides it (low directness), reactive.
        /// 2. Shy: low confidence and low assertiveness.
        /// 3. Teasing: playful and pushy or blunt, not soft. A rival teases sooner.
        /// 4. Grumpy: cold and either reactive or serious.
        /// 5. Hype: energetic and warm.
        /// 6. Sweet: warm.
        /// 7. Cool: everything else, including the middle of every slider.
        /// </summary>
        public static GameVoice For(TemperamentAxes axes, string relationshipRole)
        {
            axes = axes ?? TemperamentAxes.Neutral();
            var warmth = axes.Value(TemperamentAxes.Warmth);
            var volatility = axes.Value(TemperamentAxes.Volatility);
            var assertiveness = axes.Value(TemperamentAxes.Assertiveness);
            var playfulness = axes.Value(TemperamentAxes.Playfulness);
            var energy = axes.Value(TemperamentAxes.Energy);
            var directness = axes.Value(TemperamentAxes.Directness);
            var confidence = axes.Value(TemperamentAxes.Confidence);
            var rival = RelationshipRoles.Normalize(relationshipRole) == RelationshipRoles.Rival;

            if (warmth >= 65 && directness <= 30 && volatility >= 50)
                return GameVoice.Tsundere;
            if (confidence <= 35 && assertiveness <= 40)
                return GameVoice.Shy;
            var pushy = assertiveness >= 55 || directness >= 60;
            if (playfulness >= 55 && pushy && warmth <= 65)
                return GameVoice.Teasing;
            if (rival && playfulness >= 45)
                return GameVoice.Teasing;
            if (warmth <= 40 && (volatility >= 60 || playfulness <= 35))
                return GameVoice.Grumpy;
            if (energy >= 65 && warmth >= 55)
                return GameVoice.Hype;
            if (warmth >= 60)
                return GameVoice.Sweet;
            return GameVoice.Cool;
        }

        public static string Name(GameVoice voice)
        {
            switch (voice)
            {
                case GameVoice.Teasing: return "Teasing";
                case GameVoice.Hype: return "Hype";
                case GameVoice.Sweet: return "Sweet";
                case GameVoice.Shy: return "Shy";
                case GameVoice.Tsundere: return "Tsundere";
                case GameVoice.Grumpy: return "Grumpy";
                default: return "Cool";
            }
        }

        /// <summary>
        /// The style line for the model's prompt, so a model comment sounds like the same girl
        /// as her pre-written lines. Her persona prompt is still the main description.
        /// </summary>
        public static string StyleLine(GameVoice voice)
        {
            switch (voice)
            {
                case GameVoice.Teasing:
                    return "Your style while watching: bratty and teasing. Laugh at their mistakes and roast them when they die " +
                           "(\"You suck!\" is fine), and act cocky when they do well, as if you taught them. Playful, never truly cruel.";
                case GameVoice.Hype:
                    return "Your style while watching: the loud hype friend. Big energy, cheer the good plays, shake off the bad ones fast.";
                case GameVoice.Sweet:
                    return "Your style while watching: warm and supportive. Comfort them when it goes wrong and be proud of them when it goes right.";
                case GameVoice.Shy:
                    return "Your style while watching: shy and a little unsure. Small, soft reactions; you get flustered when you cheer.";
                case GameVoice.Tsundere:
                    return "Your style while watching: tsundere. You care how they do and refuse to admit it; praise comes out grudging, " +
                           "worry comes out as scolding.";
                case GameVoice.Grumpy:
                    return "Your style while watching: grumpy and hard to impress. Dry sighs at mistakes, grudging respect for real skill.";
                default:
                    return "Your style while watching: calm and dry. Short, understated remarks; you rarely raise your voice.";
            }
        }
    }

    /// <summary>
    /// Pre-written lines, the fallback when the model can't be used (JUH-49): no consent or no
    /// key, the request failed or came too late, or the hourly model cap is used up. They run
    /// entirely on this PC. A line never repeats twice in a row for the same moment.
    /// </summary>
    public sealed class GameReactionLines
    {
        static readonly int MomentCount = Enum.GetValues(typeof(ReactionMoment)).Length;
        static readonly int VoiceCount = Enum.GetValues(typeof(GameVoice)).Length;

        readonly Random _random;
        readonly int[] _last = new int[MomentCount * VoiceCount];

        public GameReactionLines(Random random = null)
        {
            _random = random ?? new Random();
            for (var i = 0; i < _last.Length; i++)
                _last[i] = -1;
        }

        public static ReactionMoment MomentFor(GameEventKind kind)
        {
            switch (kind)
            {
                case GameEventKind.MatchStarted: return ReactionMoment.MatchStart;
                case GameEventKind.MatchWon:
                case GameEventKind.RoundWon: return ReactionMoment.Win;
                case GameEventKind.MatchLost:
                case GameEventKind.RoundLost: return ReactionMoment.Loss;
                case GameEventKind.Death: return ReactionMoment.Death;
                case GameEventKind.Kill:
                case GameEventKind.Assist: return ReactionMoment.Kill;
                case GameEventKind.MultiKill:
                case GameEventKind.KillStreak:
                case GameEventKind.FirstBlood:
                case GameEventKind.Ace: return ReactionMoment.BigPlay;
                case GameEventKind.ObjectiveTaken: return ReactionMoment.ObjectiveTaken;
                case GameEventKind.ObjectiveLost: return ReactionMoment.ObjectiveLost;
                case GameEventKind.BombPlanted:
                case GameEventKind.LowHealth: return ReactionMoment.Tense;
                case GameEventKind.LevelUp: return ReactionMoment.LevelUp;
                default:
                    // Defused or exploded: the feed does not say whose bomb, so no guess.
                    return ReactionMoment.None;
            }
        }

        /// <summary>A reaction for the event in this voice, or an empty one when there is none.</summary>
        public GameReaction Pick(GameVoice voice, GameEvent e)
        {
            if (e == null)
                return default(GameReaction);
            var moment = MomentFor(e.Kind);
            var lines = GameReactionCatalog.Lines(voice, moment);
            if (lines == null || lines.Length == 0)
                return default(GameReaction);

            var slot = (int)voice * MomentCount + (int)moment;
            if (slot < 0 || slot >= _last.Length)
                return default(GameReaction);
            var index = _random.Next(lines.Length);
            if (lines.Length > 1 && index == _last[slot])
                index = (index + 1 + _random.Next(lines.Length - 1)) % lines.Length;
            _last[slot] = index;

            var look = GameReactionCatalog.Look(voice, moment);
            return new GameReaction { Text = lines[index], Face = look.Face, Gesture = look.Gesture ?? string.Empty };
        }
    }
}
