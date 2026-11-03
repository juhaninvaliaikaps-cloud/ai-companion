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

        public static PortraitMap CreateDefaults()
        {
            var map = new PortraitMap();
            map.Set("neutral", PortraitFrames.NeutralSet());
            map.Set("happy", PortraitFrames.SmileSet());
            map.Set("smile", PortraitFrames.SmileSet());
            map.Set("sad", PortraitFrameSet.Single(PortraitStems.Sad));
            map.Set("surprised", PortraitFrameSet.Single(PortraitStems.Surprised));
            map.Set("thinking", PortraitFrameSet.Single(PortraitStems.Thinking));
            map.Set("angry", PortraitFrameSet.Single(PortraitStems.Pouty));
            map.Set("sleepy", PortraitFrameSet.Single(PortraitStems.Sleepy));
            map.Set("tired", PortraitFrameSet.Single(PortraitStems.Sleepy));
            map.Set("teasing", PortraitFrameSet.Single(PortraitStems.SmugWink));
            map.Set("excited", PortraitFrameSet.Single(PortraitStems.Excited));
            map.Set("loving", PortraitFrameSet.Single(PortraitStems.WinkSmile));
            map.Set("concerned", PortraitFrameSet.Single(PortraitStems.Concerned));
            map.Set("embarrassed", PortraitFrameSet.Single(PortraitStems.Embarrassed));
            map.Set("shy", PortraitFrameSet.Single(PortraitStems.Shy));
            map.Set("flustered", PortraitFrameSet.Single(PortraitStems.Flustered));
            map.Set("embarrassed_worried", PortraitFrameSet.Single(PortraitStems.EmbarrassedWorried));
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
