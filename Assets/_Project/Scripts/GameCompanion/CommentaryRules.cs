using System.Collections.Generic;
using System.Text;
using AICompanion.Core;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// JUH-47 decisions (Jussi, 2026-11-21): live game commentary is a cloud feature. It needs
    /// its own opt-in, and it is off in private mode and while the 18+ gate is open. There is
    /// no local model during play.
    /// </summary>
    /// <summary>
    /// How she may react in a game right now (JUH-49). The model comes first; pre-written lines
    /// in her personality are the fallback and never leave this PC.
    /// </summary>
    public enum CommentaryMode
    {
        /// <summary>No reactions at all.</summary>
        Off = 0,

        /// <summary>Only pre-written lines: nothing about the game is sent anywhere.</summary>
        PreWrittenOnly = 1,

        /// <summary>Ask the cloud model first; fall back to a pre-written line.</summary>
        ModelFirst = 2,
    }

    /// <summary>Where one line comes from.</summary>
    public enum CommentarySource
    {
        None = 0,
        Model = 1,
        PreWritten = 2,
    }

    public static class CommentaryRoute
    {
        /// <summary>
        /// A model line that takes longer than this is too late: the moment has passed, so she
        /// says a pre-written line instead.
        /// </summary>
        public const double ModelTimeoutSeconds = 6;

        public static CommentarySource Choose(CommentaryMode mode, CommentDecision decision)
        {
            if (mode == CommentaryMode.Off || !decision.Speak)
                return CommentarySource.None;
            return mode == CommentaryMode.ModelFirst && decision.UseModel ? CommentarySource.Model : CommentarySource.PreWritten;
        }

        /// <summary>
        /// After a model request: its line, or the fallback when it failed, came back empty or
        /// came too late.
        /// </summary>
        public static bool UseModelReply(bool ok, string text, double elapsedSeconds)
        {
            return ok && !string.IsNullOrWhiteSpace(text) && elapsedSeconds <= ModelTimeoutSeconds;
        }
    }

    public static class CommentaryGate
    {
        public const string NoCloud = "No cloud AI API is set up, so she uses her pre-written lines. Nothing about your game is sent.";

        public const string PrivatePreWritten = "Private mode or 18+ is on, so she uses her pre-written lines and the local voice. Nothing about your game is sent.";

        /// <summary>
        /// The mode for now, and why it is not <see cref="CommentaryMode.ModelFirst"/>.
        /// Private mode and an open 18+ gate allow pre-written lines only (Jussi, 2026-11-21):
        /// they never leave this PC, so the cloud stays out.
        /// </summary>
        public static CommentaryMode Decide(bool featureOn, bool cloudConsent, bool cloudReady, bool privateMode, out string reason)
        {
            reason = string.Empty;
            if (!featureOn)
            {
                reason = Off;
                return CommentaryMode.Off;
            }

            if (IntimateExpressions.KeepsLocal(privateMode))
            {
                reason = PrivatePreWritten;
                return CommentaryMode.PreWrittenOnly;
            }

            if (!cloudConsent)
            {
                reason = NoConsent;
                return CommentaryMode.PreWrittenOnly;
            }

            if (!cloudReady)
            {
                reason = NoCloud;
                return CommentaryMode.PreWrittenOnly;
            }

            return CommentaryMode.ModelFirst;
        }

        public const string Off = "Game commentary is off.";
        public const string NoConsent = "Game commentary sends what happens in your game to the cloud AI API. Until you allow it, she uses her pre-written lines.";
        public const string Private = "Private mode or 18+ is on, so game commentary is off. Nothing about your game is sent.";

        /// <summary>True when a comment may be asked of the cloud model now; otherwise why not.</summary>
        public static bool Allowed(bool featureOn, bool cloudConsent, bool privateMode, out string reason)
        {
            reason = string.Empty;
            if (!featureOn)
            {
                reason = Off;
                return false;
            }

            // An open 18+ gate counts as private, as everywhere else.
            if (IntimateExpressions.KeepsLocal(privateMode))
            {
                reason = Private;
                return false;
            }

            if (!cloudConsent)
            {
                reason = NoConsent;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// The words for one comment. The rules are the fair-play rules from JUH-48: she reacts to
    /// what already happened and what the player can see, and never gives hidden information,
    /// timers, enemy positions or advice that would be an unfair advantage.
    /// </summary>
    public static class CommentaryPrompt
    {
        public const int MaxTokens = 80;
        public const float Temperature = 0.9f;

        public const string Rules =
            "You are watching the user play a video game and say one short line out loud, like a friend on the couch. " +
            "React only to what already happened, which the user just saw. Never give tactical advice, timers, " +
            "cooldowns, enemy positions or any information the user could not see. Do not name other players. " +
            "One or two short sentences, spoken, no lists, no emojis.";

        /// <summary>
        /// The system text for a model comment: her persona prompt first (who she is, from the
        /// Pisara reading), then the game style from her sliders, then the fair-play rules.
        /// </summary>
        public static string SystemText(string personaPrompt, GameVoice voice)
        {
            var sb = new StringBuilder(1024);
            if (!string.IsNullOrWhiteSpace(personaPrompt))
                sb.Append(personaPrompt.Trim()).Append("\n\n");
            sb.Append(GameVoices.StyleLine(voice)).Append("\n\n").Append(Rules);
            return sb.ToString();
        }

        public static string GameName(GameTitle game)
        {
            switch (game)
            {
                case GameTitle.CounterStrike2: return "Counter-Strike 2";
                case GameTitle.Dota2: return "Dota 2";
                case GameTitle.LeagueOfLegends: return "League of Legends";
                default: return "a game";
            }
        }

        /// <summary>The user turn: the game and what just happened, oldest first.</summary>
        public static string Turn(GameTitle game, IReadOnlyList<GameEvent> recent, GameEvent now)
        {
            var sb = new StringBuilder(256);
            sb.Append("The user is playing ").Append(GameName(game)).Append(". Just now: ").Append(Describe(now)).Append('.');
            if (recent != null && recent.Count > 1)
            {
                sb.Append(" Before that:");
                var listed = 0;
                for (var i = 0; i < recent.Count; i++)
                {
                    if (ReferenceEquals(recent[i], now))
                        continue;
                    sb.Append(listed == 0 ? " " : "; ").Append(Describe(recent[i]));
                    listed++;
                }

                sb.Append('.');
            }

            sb.Append(" Say your line.");
            return sb.ToString();
        }

        public static string Describe(GameEvent e)
        {
            if (e == null)
                return "nothing";
            var detail = e.Detail.Length > 0 ? " (" + e.Detail + ")" : string.Empty;
            switch (e.Kind)
            {
                case GameEventKind.MatchStarted: return "a new match started";
                case GameEventKind.MatchWon: return "they won the match";
                case GameEventKind.MatchLost: return "they lost the match";
                case GameEventKind.RoundWon: return "their team won the round";
                case GameEventKind.RoundLost: return "their team lost the round";
                case GameEventKind.Kill: return "they got a kill";
                case GameEventKind.Death: return "they died";
                case GameEventKind.Assist: return "they got an assist";
                case GameEventKind.MultiKill: return "they got " + MultiName(e.Count);
                case GameEventKind.KillStreak: return "they are on a " + e.Count + "-kill streak";
                case GameEventKind.FirstBlood: return "they drew first blood";
                case GameEventKind.Ace: return "they got an ace";
                case GameEventKind.ObjectiveTaken: return "their team took an objective" + detail;
                case GameEventKind.ObjectiveLost: return "their team lost an objective" + detail;
                case GameEventKind.BombPlanted: return "the bomb was planted";
                case GameEventKind.BombDefused: return "the bomb was defused";
                case GameEventKind.BombExploded: return "the bomb exploded";
                case GameEventKind.LowHealth: return "they are low on health";
                case GameEventKind.LevelUp: return "they reached level " + e.Count;
                default: return e.Kind.ToString();
            }
        }

        static string MultiName(int count)
        {
            switch (count)
            {
                case 2: return "a double kill";
                case 3: return "a triple kill";
                case 4: return "a quadra kill";
                default: return count >= 5 ? "a penta kill" : "a kill";
            }
        }
    }
}
