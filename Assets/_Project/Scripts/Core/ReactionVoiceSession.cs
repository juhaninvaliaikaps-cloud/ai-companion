using System;

namespace AICompanion.Core
{
    /// <summary>Why an expression was offered. A later offer in the same frame wins when its cause is higher.</summary>
    public enum ReactionVoiceCause
    {
        /// <summary>Her rest look changed. The same expression does not play again.</summary>
        Mood = 0,

        /// <summary>A chat cue, including the quick face for what the user just said.</summary>
        Cue = 1,

        /// <summary>An interaction, or the F9 expression preview.</summary>
        Event = 2,
    }

    /// <summary>
    /// Which clip to play for an expression the cue, mood, or interaction systems already chose.
    /// It does not read chat text and it does not choose a face of its own.
    /// </summary>
    public sealed class ReactionVoiceSession
    {
        public const double DefaultSeconds = 0.5d;
        public const double MaxSeconds = 8d;

        string _expression = string.Empty;
        string _file = string.Empty;
        double _until;

        public string LastExpression
        {
            get { return _expression; }
        }

        public string LastFile
        {
            get { return _file; }
        }

        /// <summary>
        /// The clip for this offer, or false when there is nothing to play.
        /// The same expression does not stack while its clip is still going.
        /// A mood offer also stays quiet when that expression was the last one played.
        /// A different expression replaces it. A <c>finger_</c> expression plays only
        /// while the 18+ gate is open.
        /// </summary>
        public bool TryChoose(
            ReactionVoiceCatalog catalog,
            string expression,
            ReactionVoiceCause cause,
            double now,
            Func<int, int> next,
            out ReactionVoiceClip clip)
        {
            clip = default(ReactionVoiceClip);
            var name = ReactionVoiceNames.Expression(expression);
            if (name.Length == 0 || name == "neutral")
                return false;
            if (IntimateExpressions.IsIntimateId(name) && !IntimateExpressions.Active)
                return false;
            if (cause == ReactionVoiceCause.Mood && string.Equals(name, _expression, StringComparison.Ordinal))
                return false;
            if (!double.IsNaN(now) && now < _until && string.Equals(name, _expression, StringComparison.Ordinal))
                return false;
            if (catalog == null || !catalog.TryPick(name, _file, next, out clip))
                return false;

            var seconds = clip.DurationSeconds;
            if (double.IsNaN(seconds) || seconds <= 0d)
                seconds = DefaultSeconds;
            if (seconds > MaxSeconds)
                seconds = MaxSeconds;
            if (double.IsNaN(now))
                now = 0d;
            _expression = name;
            _file = clip.FileName;
            _until = now + seconds;
            return true;
        }
    }

    /// <summary>
    /// One pending reaction for this frame. Chat, mood, and interactions offer an
    /// expression; the player takes it once. Nothing here is chat text.
    /// </summary>
    public static class ReactionVoiceRequests
    {
        static bool _pending;
        static string _expression = string.Empty;
        static ReactionVoiceCause _cause;

        public static void Offer(string expression, ReactionVoiceCause cause)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return;
            if (_pending && cause < _cause)
                return;
            _pending = true;
            _expression = expression.Trim();
            _cause = cause;
        }

        public static bool TryTake(out string expression, out ReactionVoiceCause cause)
        {
            if (!_pending)
            {
                expression = string.Empty;
                cause = ReactionVoiceCause.Mood;
                return false;
            }

            expression = _expression;
            cause = _cause;
            _pending = false;
            _expression = string.Empty;
            return true;
        }

        public static void Reset()
        {
            _pending = false;
            _expression = string.Empty;
            _cause = ReactionVoiceCause.Mood;
        }
    }
}
