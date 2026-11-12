using System;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// Picks a file that exists. A missing blink or talk frame uses that expression's
    /// still. A missing still uses neutral. A missing neutral open stays empty so the
    /// panel can hide instead of drawing a blank frame.
    /// </summary>
    public static class PortraitFiles
    {
        public static string Resolve(PortraitLibrary library, string chosen, string still, string neutral)
        {
            if (library == null || string.IsNullOrEmpty(chosen))
                return null;

            var found = library.Existing(chosen);
            if (found != null)
                return found;

            if (!Same(chosen, still))
            {
                var stillFile = library.Existing(still);
                if (stillFile != null)
                    return stillFile;
            }

            if (Same(chosen, neutral) || Same(still, neutral))
                return null;
            return library.Existing(neutral);
        }

        static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// One Player.log warning per missing portrait file. The next blink must not log it again.
    /// </summary>
    public static class PortraitMissing
    {
        public static string Line(string stem, bool showingNeutral)
        {
            if (showingNeutral)
                return "Expression portraits: missing '" + stem + "'. Showing neutral.";
            return "Expression portraits: missing '" + stem + "'. Showing the still.";
        }
    }

    public struct PortraitView
    {
        public bool Visible;
        public string Back;
        public float BackAlpha;
        public string Mid;
        public float MidAlpha;
        public string Front;
        public float FrontAlpha;

        public static PortraitView Hidden
        {
            get { return new PortraitView(); }
        }

        public static PortraitView Still(string file)
        {
            return new PortraitView
            {
                Visible = !string.IsNullOrEmpty(file),
                Front = file,
                FrontAlpha = string.IsNullOrEmpty(file) ? 0f : 1f
            };
        }

        public static PortraitView FromSample(PortraitFadeSample sample)
        {
            var visible = !string.IsNullOrEmpty(sample.From) || !string.IsNullOrEmpty(sample.Between) || !string.IsNullOrEmpty(sample.To);
            return new PortraitView
            {
                Visible = visible,
                Back = sample.From,
                BackAlpha = sample.FromAlpha,
                Mid = sample.Between,
                MidAlpha = sample.BetweenAlpha,
                Front = sample.To,
                FrontAlpha = sample.ToAlpha
            };
        }
    }

    /// <summary>
    /// Emotion, blink, talk, and the expression crossfade. The view only draws the result.
    /// </summary>
    public sealed class PortraitDirector
    {
        readonly HashSet<string> _warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Action<string> _warn;
        PortraitBlinkScheduler _blink;
        PortraitTalkMotion _talk;
        PortraitCrossfade _fade;
        string _emotion = "neutral";
        bool _ready;

        public PortraitDirector()
            : this(null)
        {
        }

        public PortraitDirector(Action<string> warn)
        {
            _warn = warn;
        }

        public string Emotion
        {
            get { return _emotion; }
        }

        public bool Fading
        {
            get { return _fade.Active; }
        }

        public PortraitView Tick(
            float delta,
            string emotion,
            bool speaking,
            bool hasAmplitude,
            float amplitude,
            PortraitMap map,
            PortraitLibrary library,
            float gapUnit,
            float frameUnit,
            float doubleUnit,
            float talkUnit,
            float fadeUnit)
        {
            if (map == null)
                map = PortraitMap.CreateDefaults(IntimateExpressions.Active);
            if (string.IsNullOrEmpty(emotion))
                emotion = "neutral";

            if (!_ready)
            {
                _ready = true;
                _emotion = emotion;
                _blink = PortraitBlinkScheduler.Start(gapUnit);
            }
            else if (!string.Equals(_emotion, emotion, StringComparison.OrdinalIgnoreCase))
            {
                BeginFade(_emotion, emotion, map, library, fadeUnit);
                _emotion = emotion;
            }

            var fading = _fade.Active;
            _blink = _blink.Tick(delta, gapUnit, frameUnit, doubleUnit);
            var set = map.Resolve(_emotion);
            ReportMissing(set, library);
            var talkReady = library != null && library.Existing(set.Talk) != null;
            if (!fading)
                _talk = _talk.Tick(delta, speaking, talkReady, hasAmplitude, amplitude, talkUnit);

            if (_fade.Active)
                return PortraitView.FromSample(_fade.Advance(delta));

            var blinkReady = library != null
                && library.Existing(set.BlinkHalf) != null
                && library.Existing(set.BlinkClosed) != null;
            var lid = blinkReady ? _blink.Lid : PortraitLid.Open;
            var stem = PortraitFrames.Choose(set, lid, _talk.ShowTalk);
            var file = ResolveExisting(map, library, stem, set.Open);
            return PortraitView.Still(file);
        }

        void BeginFade(string fromEmotion, string toEmotion, PortraitMap map, PortraitLibrary library, float unit)
        {
            var fromSet = map.Resolve(fromEmotion);
            var toSet = map.Resolve(toEmotion);
            var from = ResolveExisting(map, library, fromSet.Open, fromSet.Open);
            var to = ResolveExisting(map, library, toSet.Open, toSet.Open);
            string betweenStem;
            string between = null;
            if (map.TryTransition(fromEmotion, toEmotion, out betweenStem))
                between = library != null ? library.Existing(betweenStem) : null;
            _fade.Begin(from, to, between, PortraitCrossfade.DurationFor(unit));
        }

        void ReportMissing(PortraitFrameSet set, PortraitLibrary library)
        {
            if (library == null)
                return;

            var stillThere = library.Existing(set.Open) != null;
            Report(library, set.Open, stillThere: false);
            Report(library, set.BlinkHalf, stillThere);
            Report(library, set.BlinkClosed, stillThere);
            Report(library, set.Talk, stillThere);
        }

        void Report(PortraitLibrary library, string stem, bool stillThere)
        {
            if (string.IsNullOrEmpty(stem) || library.Existing(stem) != null)
                return;
            if (!_warned.Add(stem))
                return;

            var message = PortraitMissing.Line(stem, !stillThere);
            if (_warn != null)
                _warn(message);
            else
                Debug.LogWarning(message);
        }

        static string ResolveExisting(PortraitMap map, PortraitLibrary library, string stem, string still)
        {
            return PortraitFiles.Resolve(library, stem, still, map.Resolve("neutral").Open);
        }
    }
}
