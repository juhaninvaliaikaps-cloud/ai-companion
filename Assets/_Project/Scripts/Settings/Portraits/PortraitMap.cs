using System;
using System.Collections.Generic;

namespace AICompanion.Settings
{
    public struct PortraitFrameSet
    {
        public string Open;
        public string BlinkHalf;
        public string BlinkClosed;
        public string Talk;
        public string InBetween;

        public PortraitFrameSet(string open, string blinkHalf, string blinkClosed, string talk, string inBetween)
        {
            Open = open;
            BlinkHalf = blinkHalf;
            BlinkClosed = blinkClosed;
            Talk = talk;
            InBetween = inBetween;
        }

        public static PortraitFrameSet Single(string open)
        {
            return new PortraitFrameSet(open, null, null, null, null);
        }

        public static PortraitFrameSet Animated(string open, string blinkHalf, string blinkClosed, string talk)
        {
            return new PortraitFrameSet(open, blinkHalf, blinkClosed, talk, null);
        }

        public bool HasBlink
        {
            get { return !string.IsNullOrEmpty(BlinkHalf) && !string.IsNullOrEmpty(BlinkClosed); }
        }

        public bool HasTalk
        {
            get { return !string.IsNullOrEmpty(Talk); }
        }
    }

    public enum PortraitLid
    {
        Open = 0,
        Half = 1,
        Closed = 2
    }

    public static class PortraitFrames
    {
        public static string Choose(PortraitFrameSet set, PortraitLid lid, bool showTalk)
        {
            if (showTalk && set.HasTalk)
                return set.Talk;
            if (set.HasBlink)
            {
                if (lid == PortraitLid.Half)
                    return set.BlinkHalf;
                if (lid == PortraitLid.Closed)
                    return set.BlinkClosed;
            }

            return set.Open;
        }

        public static PortraitFrameSet NeutralSet()
        {
            return new PortraitFrameSet(
                PortraitStems.NeutralOpen,
                PortraitStems.NeutralBlinkHalf,
                PortraitStems.NeutralBlinkClosed,
                PortraitStems.NeutralTalk,
                null);
        }

        public static PortraitFrameSet SmileSet()
        {
            return new PortraitFrameSet(
                PortraitStems.SmileOpen,
                PortraitStems.SmileBlinkHalf,
                PortraitStems.SmileBlinkClosed,
                PortraitStems.SmileTalk,
                null);
        }
    }

    /// <summary>
    /// Emotion name to frame set, plus optional directed in-between frames.
    /// A missing or unknown emotion resolves to neutral.
    /// </summary>
    public sealed class PortraitMap
    {
        readonly Dictionary<string, PortraitFrameSet> _entries;
        readonly Dictionary<string, string> _transitions;

        PortraitMap()
        {
            _entries = new Dictionary<string, PortraitFrameSet>(StringComparer.OrdinalIgnoreCase);
            _transitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>A stem whose blink and talk frames use the same stem with suffixes.</summary>
        static PortraitFrameSet Animated(string stem)
        {
            return PortraitFrameSet.Animated(
                stem, stem + "_blink_half", stem + "_blink_closed", stem + "_talk");
        }

        public static PortraitMap CreateDefaults()
        {
            var map = new PortraitMap();
            map.Set("neutral", PortraitFrames.NeutralSet());
            map.Set("happy", PortraitFrames.SmileSet());
            map.Set("smile", PortraitFrames.SmileSet());
            map.Set("laughing", PortraitFrameSet.Animated(
                PortraitStems.Happy,
                PortraitStems.HappyBlinkHalf,
                PortraitStems.HappyBlinkClosed,
                PortraitStems.HappyTalk));
            map.Set("sad", PortraitFrameSet.Animated(
                PortraitStems.Sad, PortraitStems.SadBlinkHalf, PortraitStems.SadBlinkClosed, PortraitStems.SadTalk));
            map.Set("surprised", PortraitFrameSet.Animated(
                PortraitStems.Surprised, PortraitStems.SurprisedBlinkHalf, PortraitStems.SurprisedBlinkClosed, PortraitStems.SurprisedTalk));
            map.Set("thinking", PortraitFrameSet.Animated(
                PortraitStems.Thinking, PortraitStems.ThinkingBlinkHalf, PortraitStems.ThinkingBlinkClosed, PortraitStems.ThinkingTalk));
            var pouty = PortraitFrameSet.Animated(
                PortraitStems.Pouty, PortraitStems.PoutyBlinkHalf, PortraitStems.PoutyBlinkClosed, PortraitStems.PoutyTalk);
            map.Set("pouty", pouty);
            map.Set("angry", Animated(PortraitStems.Angry));
            map.Set("sleepy", Animated(PortraitStems.SleepyAnimated));
            map.Set("tired", Animated(PortraitStems.SleepyAnimated));
            map.Set("teasing", Animated(PortraitStems.Smug));
            map.Set("excited", PortraitFrameSet.Animated(
                PortraitStems.Excited, PortraitStems.ExcitedBlinkHalf, PortraitStems.ExcitedBlinkClosed, PortraitStems.ExcitedTalk));
            map.Set("loving", PortraitFrameSet.Single(PortraitStems.WinkSmile));
            map.Set("concerned", PortraitFrameSet.Animated(
                PortraitStems.Concerned, PortraitStems.ConcernedBlinkHalf, PortraitStems.ConcernedBlinkClosed, PortraitStems.ConcernedTalk));
            map.Set("embarrassed", PortraitFrameSet.Animated(
                PortraitStems.Embarrassed, PortraitStems.EmbarrassedBlinkHalf, PortraitStems.EmbarrassedBlinkClosed, PortraitStems.EmbarrassedTalk));
            map.Set("shy", PortraitFrameSet.Animated(
                PortraitStems.Shy, PortraitStems.ShyBlinkHalf, PortraitStems.ShyBlinkClosed, PortraitStems.ShyTalk));
            map.Set("flustered", PortraitFrameSet.Animated(
                PortraitStems.Flustered, PortraitStems.FlusteredBlinkHalf, PortraitStems.FlusteredBlinkClosed, PortraitStems.FlusteredTalk));
            map.Set("embarrassed_worried", Animated(PortraitStems.EmbarrassedWorriedAnimated));

            map.Set("crying", Animated(PortraitStems.Crying));
            map.Set("teary_happy", Animated(PortraitStems.TearyHappy));
            map.Set("shocked", Animated(PortraitStems.Shocked));
            map.Set("confused", Animated(PortraitStems.Confused));
            map.Set("bored", Animated(PortraitStems.Bored));
            map.Set("scared", Animated(PortraitStems.Scared));
            map.Set("tsundere", Animated(PortraitStems.Tsundere));
            map.Set("determined", Animated(PortraitStems.Determined));
            map.Set("smug", Animated(PortraitStems.Smug));
            map.Set("flustered_heavy", Animated(PortraitStems.FlusteredHeavy));
            map.Set("seductive", Animated(PortraitStems.Seductive));
            map.Set("kiss", Animated(PortraitStems.Kiss));
            map.Set("finger_suck", Animated(PortraitStems.FingerSuck));
            map.Set("finger_suck_deep", Animated(PortraitStems.FingerSuckDeep));
            map.Set("finger_suck_hold", PortraitFrameSet.Single(PortraitStems.FingerSuckHold));
            map.Set("finger_pull", PortraitFrameSet.Single(PortraitStems.FingerPull));
            map.Set("finger_pull_start", PortraitFrameSet.Single(PortraitStems.FingerPullStart));

            // A wink has no blink frames: only the open still and its talk frame.
            map.Set("smug_wink", new PortraitFrameSet(
                PortraitStems.SmugWinkAnimated, null, null, PortraitStems.SmugWinkAnimated + "_talk", null));
            map.SetTransition("neutral", "happy", PortraitStems.Smile);
            return map;
        }

        public PortraitFrameSet Resolve(string emotion)
        {
            PortraitFrameSet set;
            if (!string.IsNullOrEmpty(emotion) && _entries.TryGetValue(emotion, out set) && !string.IsNullOrEmpty(set.Open))
                return set;
            if (_entries.TryGetValue("neutral", out set))
                return set;
            return PortraitFrames.NeutralSet();
        }

        public bool TryTransition(string from, string to, out string file)
        {
            var key = PortraitNames.TransitionKey(from, to);
            if (_transitions.TryGetValue(key, out file) && !string.IsNullOrEmpty(file))
                return true;

            PortraitFrameSet destination;
            if (_entries.TryGetValue(PortraitNames.Normalize(to), out destination) && !string.IsNullOrEmpty(destination.InBetween))
            {
                file = destination.InBetween;
                return true;
            }

            file = null;
            return false;
        }

        public void ClearTransitions()
        {
            _transitions.Clear();
        }

        public bool Set(string emotion, PortraitFrameSet set)
        {
            var key = PortraitNames.Normalize(emotion);
            if (key.Length == 0 || key == "transitions")
                return false;
            if (!PortraitStems.IsSafe(set.Open))
                return false;

            set.Open = PortraitStems.Stem(set.Open);
            set.BlinkHalf = CleanOptional(set.BlinkHalf);
            set.BlinkClosed = CleanOptional(set.BlinkClosed);
            set.Talk = CleanOptional(set.Talk);
            set.InBetween = CleanOptional(set.InBetween);
            _entries[key] = set;
            return true;
        }

        public bool SetTransition(string from, string to, string file)
        {
            if (!PortraitStems.IsSafe(file))
                return false;
            var fromKey = PortraitNames.Normalize(from);
            var toKey = PortraitNames.Normalize(to);
            if (fromKey.Length == 0 || toKey.Length == 0)
                return false;
            _transitions[fromKey + "->" + toKey] = PortraitStems.Stem(file);
            return true;
        }

        static string CleanOptional(string value)
        {
            if (!PortraitStems.IsSafe(value))
                return null;
            return PortraitStems.Stem(value);
        }
    }
}
