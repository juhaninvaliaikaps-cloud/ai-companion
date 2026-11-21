using System;
using System.Collections.Generic;

namespace AICompanion.GameCompanion
{
    /// <summary>How often she comments (JUH-46: rare / normal / chatty).</summary>
    public enum CommentaryFrequency
    {
        Rare = 0,
        Normal = 1,
        Chatty = 2,
    }

    public struct CommentDecision
    {
        public bool Speak;
        public string Reason;

        /// <summary>
        /// True when the line may come from the model. False when she may only say a
        /// pre-written line, because the hourly model cap is used up (JUH-49).
        /// </summary>
        public bool UseModel;

        public static CommentDecision No(string reason)
        {
            return new CommentDecision { Speak = false, Reason = reason, UseModel = false };
        }

        public static CommentDecision Yes(string reason)
        {
            return new CommentDecision { Speak = true, Reason = reason, UseModel = true };
        }

        public static CommentDecision PreWritten(string reason)
        {
            return new CommentDecision { Speak = true, Reason = reason, UseModel = false };
        }
    }

    /// <summary>
    /// JUH-46: decides when she says something about the game, so she is fun and not annoying,
    /// and so cloud calls stay sparse (the game companion is a cloud feature, JUH-47).
    ///
    /// * A cooldown between comments, set by the frequency.
    /// * A big moment (a match won, an ace, a triple kill) may break the cooldown, but never
    ///   comes sooner than <see cref="BigMomentGapSeconds"/> after the last comment.
    /// * At most <see cref="HourlyBudget"/> model comments in any hour, big moments included.
    ///   After that she only says pre-written lines in her personality (JUH-49), which cost
    ///   nothing; the cooldown still applies to them.
    /// * Minor moments (a single kill, a level-up) are never spoken alone; they are only
    ///   context for the next comment.
    /// * Quiet mode and push-to-mute stop everything.
    ///
    /// Plain C#, no scene objects; the caller passes the time.
    /// </summary>
    public sealed class CommentaryPacer
    {
        public const double BigMomentGapSeconds = 8;
        public const int ContextSize = 6;
        public const double ContextSeconds = 90;

        readonly List<DateTime> _spoken = new List<DateTime>();
        DateTime _lastSpokenUtc = DateTime.MinValue;
        readonly List<GameEvent> _context = new List<GameEvent>();
        DateTime _mutedUntilUtc = DateTime.MinValue;

        public CommentaryFrequency Frequency = CommentaryFrequency.Normal;

        /// <summary>Quiet mode: no comments at all until turned off.</summary>
        public bool Quiet;

        public double CooldownSeconds
        {
            get { return CooldownFor(Frequency); }
        }

        public int HourlyBudget
        {
            get { return BudgetFor(Frequency); }
        }

        public static double CooldownFor(CommentaryFrequency frequency)
        {
            switch (frequency)
            {
                case CommentaryFrequency.Rare: return 90;
                case CommentaryFrequency.Chatty: return 15;
                default: return 40;
            }
        }

        public static int BudgetFor(CommentaryFrequency frequency)
        {
            switch (frequency)
            {
                case CommentaryFrequency.Rare: return 8;
                case CommentaryFrequency.Chatty: return 40;
                default: return 20;
            }
        }

        /// <summary>Push-to-mute: nothing until <paramref name="nowUtc"/> + <paramref name="duration"/>.</summary>
        public void MuteFor(TimeSpan duration, DateTime nowUtc)
        {
            var until = nowUtc + duration;
            if (until > _mutedUntilUtc)
                _mutedUntilUtc = until;
        }

        public void Unmute()
        {
            _mutedUntilUtc = DateTime.MinValue;
        }

        public bool IsMuted(DateTime nowUtc)
        {
            return Quiet || nowUtc < _mutedUntilUtc;
        }

        /// <summary>The recent events, oldest first, for the comment's prompt.</summary>
        public IReadOnlyList<GameEvent> Context
        {
            get { return _context; }
        }

        /// <summary>
        /// Notes the event and says whether to comment now. When it says yes, the caller asks
        /// for a comment and then calls <see cref="MarkSpoken"/>.
        /// </summary>
        public CommentDecision Consider(GameEvent e, DateTime nowUtc)
        {
            if (e == null)
                return CommentDecision.No("no event");
            Remember(e, nowUtc);

            if (IsMuted(nowUtc))
                return CommentDecision.No("muted");
            if (e.Moment == GameMoment.Minor)
                return CommentDecision.No("minor moment");

            PruneSpoken(nowUtc);
            var since = _lastSpokenUtc == DateTime.MinValue ? double.MaxValue : (nowUtc - _lastSpokenUtc).TotalSeconds;
            var gap = e.Moment == GameMoment.Big ? BigMomentGapSeconds : CooldownSeconds;
            if (since < gap)
                return CommentDecision.No(e.Moment == GameMoment.Big ? "just spoke" : "cooldown");

            if (_spoken.Count >= HourlyBudget)
                return CommentDecision.PreWritten("hourly budget used");
            return CommentDecision.Yes(e.Moment == GameMoment.Big ? "big moment" : "cooldown over");
        }

        /// <summary>A model comment was spoken: it counts against the hourly cap.</summary>
        public void MarkSpoken(DateTime nowUtc)
        {
            MarkSpoken(nowUtc, true);
        }

        /// <summary>
        /// A line was spoken. Every line starts the cooldown; only a model line counts against
        /// the hourly cap.
        /// </summary>
        public void MarkSpoken(DateTime nowUtc, bool fromModel)
        {
            if (fromModel)
                _spoken.Add(nowUtc);
            _lastSpokenUtc = nowUtc;
            _context.Clear();
        }

        /// <summary>A new match: the context and the cooldown start over; the hourly budget does not.</summary>
        public void NewMatch()
        {
            _context.Clear();
        }

        void Remember(GameEvent e, DateTime nowUtc)
        {
            _context.Add(e);
            for (var i = _context.Count - 1; i >= 0; i--)
            {
                if ((nowUtc - _context[i].AtUtc).TotalSeconds > ContextSeconds)
                    _context.RemoveAt(i);
            }

            while (_context.Count > ContextSize)
                _context.RemoveAt(0);
        }

        void PruneSpoken(DateTime nowUtc)
        {
            for (var i = _spoken.Count - 1; i >= 0; i--)
            {
                if ((nowUtc - _spoken[i]).TotalHours >= 1)
                    _spoken.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// JUH-46: turns a quick run of kills into one bigger moment, for games whose feed only
    /// counts kills (CS2, Dota 2). Two or more kills within <see cref="WindowSeconds"/> is a
    /// multi-kill; three or more is a big moment. A death or a new round starts over.
    /// </summary>
    public sealed class BigMomentDetector
    {
        public const double WindowSeconds = 10;

        readonly List<DateTime> _kills = new List<DateTime>();

        /// <summary>The event to pass on: a kill that completes a quick run becomes a multi-kill.</summary>
        public GameEvent Process(GameEvent e)
        {
            if (e == null || e.Kind != GameEventKind.Kill)
            {
                if (e != null && (e.Kind == GameEventKind.Death || e.Kind == GameEventKind.RoundWon
                    || e.Kind == GameEventKind.RoundLost || e.Kind == GameEventKind.MatchStarted))
                    _kills.Clear();
                return e;
            }

            var count = Math.Max(1, e.Count);
            for (var i = 0; i < count; i++)
                _kills.Add(e.AtUtc);
            for (var i = _kills.Count - 1; i >= 0; i--)
            {
                if ((e.AtUtc - _kills[i]).TotalSeconds > WindowSeconds)
                    _kills.RemoveAt(i);
            }

            if (_kills.Count >= 2)
                return GameEvent.Of(e.Game, GameEventKind.MultiKill, e.AtUtc, _kills.Count);
            return e;
        }
    }
}
