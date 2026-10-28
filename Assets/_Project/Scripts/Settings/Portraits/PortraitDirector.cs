using System;

namespace AICompanion.Settings
{
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
        PortraitBlinkScheduler _blink;
        PortraitTalkMotion _talk;
        PortraitCrossfade _fade;
        string _emotion = "neutral";
        bool _ready;

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
                map = PortraitMap.CreateDefaults();
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
            var file = ResolveExisting(map, library, stem);
            return PortraitView.Still(file);
        }

        void BeginFade(string fromEmotion, string toEmotion, PortraitMap map, PortraitLibrary library, float unit)
        {
            var from = ResolveExisting(map, library, map.Resolve(fromEmotion).Open);
            var to = ResolveExisting(map, library, map.Resolve(toEmotion).Open);
            string betweenStem;
            string between = null;
            if (map.TryTransition(fromEmotion, toEmotion, out betweenStem))
                between = library != null ? library.Existing(betweenStem) : null;
            _fade.Begin(from, to, between, PortraitCrossfade.DurationFor(unit));
        }

        static string ResolveExisting(PortraitMap map, PortraitLibrary library, string stem)
        {
            if (library == null || string.IsNullOrEmpty(stem))
                return null;
            var found = library.Existing(stem);
            if (found != null)
                return found;
            var neutral = map.Resolve("neutral").Open;
            if (string.Equals(neutral, stem, StringComparison.OrdinalIgnoreCase))
                return null;
            return library.Existing(neutral);
        }
    }
}
