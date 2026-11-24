using System;

namespace AICompanion.Core
{
    /// <summary>Why an expression was offered. A later offer in the same frame wins when its cause is higher.</summary>
    public enum ReactionVoiceCause
    {
        /// <summary>Her rest look changed. The same expression does not play again.</summary>
        Mood = 0,

        /// <summary>The 2D portrait switched to this expression.</summary>
        Portrait = 1,

        /// <summary>A chat cue, including the quick face for what the user just said.</summary>
        Cue = 2,

        /// <summary>An interaction, or the F9 expression preview.</summary>
        Event = 3,
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

        /// <summary>The length used for the no-stack window. Missing or tiny values become half a second, and nothing waits longer than 8 seconds.</summary>
        public static double ClampSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0d)
                seconds = DefaultSeconds;
            if (seconds > MaxSeconds)
                seconds = MaxSeconds;
            return seconds;
        }

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

            var seconds = ClampSeconds(clip.DurationSeconds);
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

    /// <summary>
    /// The expression the 2D portrait is showing. A blink or a talk frame of the same
    /// expression is not a change. Neutral is remembered and stays quiet.
    /// </summary>
    public sealed class PortraitExpressionWatch
    {
        string _shown = string.Empty;

        /// <summary>The expression just shown, when it is a new non-neutral name.</summary>
        public bool TryChange(string expression, out string name)
        {
            name = ReactionVoiceNames.Expression(expression);
            if (string.Equals(name, _shown, StringComparison.Ordinal))
                return false;

            _shown = name;
            if (name.Length == 0 || name == "neutral")
                return false;
            return true;
        }
    }

    /// <summary>
    /// Holds the start of a spoken line until the reaction for its cue has finished.
    /// Synthesis may run while this is set. Playback asks <see cref="Blocks"/>.
    /// The player calls <see cref="NotePlayback"/> once it knows whether a clip started.
    /// If it never answers, the hold ends after <see cref="AckGraceSeconds"/> so a missing
    /// player cannot keep her silent.
    /// </summary>
    public static class ReactionSpeechLead
    {
        public const double AckGraceSeconds = 0.25d;

        static int _serial;
        static int _acked;
        static double _until;
        static double _armedAt;
        static bool _clockSet;

        public static int Serial
        {
            get { return _serial; }
        }

        /// <summary>True after <see cref="Arm"/> until the player says how long the clip is.</summary>
        public static bool NeedsAnswer
        {
            get { return _serial != 0 && _acked != _serial; }
        }

        public static void Arm()
        {
            _serial++;
            if (_serial == int.MaxValue)
                _serial = 1;
            _acked = 0;
            _until = 0d;
            _clockSet = false;
        }

        public static void Clear()
        {
            _serial = 0;
            _acked = 0;
            _until = 0d;
            _clockSet = false;
        }

        /// <summary>
        /// Speech may start at <paramref name="until"/>. A later clip can push that time
        /// later. It does not pull an existing hold earlier.
        /// </summary>
        public static void NotePlayback(double until)
        {
            if (_serial == 0)
                return;
            if (double.IsNaN(until))
                until = 0d;
            if (_acked != _serial)
            {
                _acked = _serial;
                _until = until;
                return;
            }

            if (until > _until)
                _until = until;
        }

        /// <summary>
        /// Mute or tray pause stopped the clip. Speech may start at <paramref name="now"/>
        /// even when a longer hold was already recorded.
        /// </summary>
        public static void Release(double now)
        {
            if (_serial == 0)
                return;
            if (double.IsNaN(now))
                now = 0d;
            _acked = _serial;
            _until = now;
            _clockSet = true;
        }

        public static bool Blocks(double now)
        {
            if (_serial == 0)
                return false;
            if (double.IsNaN(now))
                now = 0d;
            if (!_clockSet)
            {
                _armedAt = now;
                _clockSet = true;
            }

            if (_acked != _serial)
                return now - _armedAt < AckGraceSeconds;
            return now < _until;
        }
    }
}
