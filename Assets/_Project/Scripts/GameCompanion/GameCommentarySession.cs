using System;
using System.Text;
using UnityEngine;

namespace AICompanion.GameCompanion
{
    /// <summary>What to do about one game event (JUH-47).</summary>
    public struct GameCommentaryStep
    {
        public CommentarySource Source;

        /// <summary>The event the step is about, after big-moment detection.</summary>
        public GameEvent Event;

        /// <summary>The user turn for a model request; empty for a pre-written line.</summary>
        public string Turn;

        /// <summary>
        /// The pre-written reaction: said as it is for <see cref="CommentarySource.PreWritten"/>,
        /// and the fallback, plus the face and gesture, for a model line.
        /// </summary>
        public GameReaction Reaction;

        public bool Nothing
        {
            get { return Source == CommentarySource.None; }
        }
    }

    /// <summary>
    /// JUH-47: one game companion session in plain C#. Each game event goes through big-moment
    /// detection, the pacer and the route. The step says whether to ask the model, say a
    /// pre-written line, or stay quiet. While a model request is out, new events are only
    /// context. The Unity component that owns the feeds and the chat stays thin.
    /// </summary>
    public sealed class GameCommentarySession
    {
        public const int MaxModelLineLength = 220;

        readonly BigMomentDetector _detector = new BigMomentDetector();
        readonly GameReactionLines _lines;

        public readonly CommentaryPacer Pacer = new CommentaryPacer();

        public GameCommentarySession(System.Random random = null)
        {
            _lines = new GameReactionLines(random);
        }

        /// <summary>True while a model line is being written.</summary>
        public bool Waiting { get; private set; }

        public GameCommentaryStep Next(GameEvent e, CommentaryMode mode, GameVoice voice, DateTime nowUtc)
        {
            var nothing = new GameCommentaryStep { Source = CommentarySource.None, Event = e, Turn = string.Empty };
            if (e == null || mode == CommentaryMode.Off)
                return nothing;

            // League reports multi-kills itself; the others only count kills.
            if (e.Game != GameTitle.LeagueOfLegends)
                e = _detector.Process(e);
            nothing.Event = e;
            if (e.Kind == GameEventKind.MatchStarted)
                Pacer.NewMatch();

            var decision = Pacer.Consider(e, nowUtc);
            if (Waiting)
                return nothing;

            var source = CommentaryRoute.Choose(mode, decision);
            if (source == CommentarySource.None)
                return nothing;

            var reaction = _lines.Pick(voice, e);
            if (source == CommentarySource.Model)
            {
                Waiting = true;
                return new GameCommentaryStep
                {
                    Source = CommentarySource.Model,
                    Event = e,
                    Turn = CommentaryPrompt.Turn(e.Game, Pacer.Context, e),
                    Reaction = reaction,
                };
            }

            if (reaction.IsEmpty)
                return nothing;
            Pacer.MarkSpoken(nowUtc, false);
            return new GameCommentaryStep { Source = CommentarySource.PreWritten, Event = e, Turn = string.Empty, Reaction = reaction };
        }

        /// <summary>
        /// The model answered (or didn't). Returns what to say: the model's line with the
        /// step's face and gesture, or the pre-written fallback, or nothing.
        /// </summary>
        public GameReaction Resolve(GameCommentaryStep step, bool ok, string text, double elapsedSeconds, DateTime nowUtc, out bool fromModel)
        {
            Waiting = false;
            fromModel = false;
            var line = CleanReply(text);
            if (CommentaryRoute.UseModelReply(ok, line, elapsedSeconds))
            {
                fromModel = true;
                Pacer.MarkSpoken(nowUtc, true);
                return new GameReaction { Text = line, Face = step.Reaction.Face, Gesture = step.Reaction.Gesture ?? string.Empty };
            }

            if (step.Reaction.IsEmpty)
                return default(GameReaction);
            Pacer.MarkSpoken(nowUtc, false);
            return step.Reaction;
        }

        /// <summary>Forget the current request without speaking, e.g. when the feature is turned off.</summary>
        public void Cancel()
        {
            Waiting = false;
        }

        /// <summary>
        /// One spoken line from a model reply: stage directions in asterisks, tags in angle
        /// brackets and wrapping quotes go, spaces collapse, and it is capped at a sentence end.
        /// </summary>
        public static string CleanReply(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var sb = new StringBuilder(text.Length);
            var inStars = false;
            var inTag = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '*')
                {
                    inStars = !inStars;
                    continue;
                }

                if (c == '<')
                    inTag = true;
                if (!inStars && !inTag)
                    sb.Append(char.IsWhiteSpace(c) ? ' ' : c);
                if (c == '>')
                    inTag = false;
            }

            var line = sb.ToString();
            while (line.Contains("  "))
                line = line.Replace("  ", " ");
            line = line.Trim().Trim('"', '“', '”').Trim();
            if (line.Length <= MaxModelLineLength)
                return line;
            var cut = line.LastIndexOfAny(new[] { '.', '!', '?' }, MaxModelLineLength - 1);
            return cut > 20 ? line.Substring(0, cut + 1) : line.Substring(0, MaxModelLineLength).TrimEnd() + "...";
        }

        public static CommentaryFrequency FrequencyFor(string name)
        {
            switch ((name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "rare": return CommentaryFrequency.Rare;
                case "chatty": return CommentaryFrequency.Chatty;
                default: return CommentaryFrequency.Normal;
            }
        }
    }

    /// <summary>JUH-47: which game a GSI post came from, by the provider's app id.</summary>
    public static class GsiRouting
    {
        public static GameTitle GameOf(string json)
        {
            if (string.IsNullOrEmpty(json))
                return GameTitle.Unknown;
            try
            {
                var payload = JsonUtility.FromJson<ProviderOnly>(json);
                var appId = payload != null && payload.provider != null ? payload.provider.appid : 0;
                if (appId == SteamLibraries.Cs2AppId)
                    return GameTitle.CounterStrike2;
                if (appId == SteamLibraries.Dota2AppId)
                    return GameTitle.Dota2;
                return GameTitle.Unknown;
            }
            catch (Exception)
            {
                return GameTitle.Unknown;
            }
        }

#pragma warning disable 0649
        [Serializable] sealed class ProviderOnly { public Provider provider = new Provider(); }
        [Serializable] sealed class Provider { public int appid; }
#pragma warning restore 0649
    }
}
