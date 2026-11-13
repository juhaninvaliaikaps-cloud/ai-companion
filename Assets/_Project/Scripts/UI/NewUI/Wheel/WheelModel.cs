using System;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>How a slice is coloured: blue, or the pink intimate/affectionate accent.</summary>
    public enum WheelAccent
    {
        Normal = 0,
        Intimate = 1,
    }

    /// <summary>One slice of a radial wheel.</summary>
    public sealed class WheelSlice
    {
        public string Id = string.Empty;
        public string Label = string.Empty;
        public WheelAccent Accent = WheelAccent.Normal;
        public bool Enabled = true;
        /// <summary>Shown when a disabled slice is clicked (a refusal or a hint).</summary>
        public string DisabledReason = string.Empty;
        /// <summary>Adult option: only offered while the 18+ gate (<see cref="IntimateExpressions.Active"/>) is open.</summary>
        public bool RequiresIntimateGate;
        public Action OnChoose;
    }

    /// <summary>A wheel: a centre label with a small hint, and any number of slices clockwise from the top.</summary>
    public sealed class WheelDefinition
    {
        public string CenterLabel = string.Empty;
        public string CenterHint = "right-click";
        public readonly List<WheelSlice> Slices = new List<WheelSlice>();
        /// <summary>Clicking the centre: back to the previous wheel, or close.</summary>
        public Action OnCenter;

        /// <summary>The slices that may be shown now: adult slices drop out while the 18+ gate is closed.</summary>
        public static List<WheelSlice> Visible(IReadOnlyList<WheelSlice> slices, bool intimateGateOpen)
        {
            var list = new List<WheelSlice>();
            if (slices == null)
                return list;
            for (var i = 0; i < slices.Count; i++)
            {
                var slice = slices[i];
                if (slice == null)
                    continue;
                if (slice.RequiresIntimateGate && !intimateGateOpen)
                    continue;
                list.Add(slice);
            }

            return list;
        }
    }

    /// <summary>The main wheel's fixed slices, clockwise from the top (approved mockup).</summary>
    public static class MainWheel
    {
        public const string Talk = "talk";
        public const string WatchScreen = "watch_screen";
        public const string Mood = "mood";
        public const string Persona = "persona";
        public const string DoSomething = "do_something";
        public const string LeaveMeAlone = "leave_me_alone";
        public const string Memories = "memories";
        public const string Settings = "settings";

        /// <summary>Fixed order. Settings is last, so it sits top-left, just before Talk.</summary>
        public static readonly IReadOnlyList<string> Order = new[]
        {
            Talk, WatchScreen, Mood, Persona, DoSomething, LeaveMeAlone, Memories, Settings,
        };

        public static string Label(string id, bool paused)
        {
            switch (id)
            {
                case Talk:
                    return "Talk";
                case WatchScreen:
                    return "Watch my screen";
                case Mood:
                    return "Mood";
                case Persona:
                    return "Persona";
                case DoSomething:
                    return "Do something";
                case LeaveMeAlone:
                    return paused ? "Come back" : "Leave me alone";
                case Memories:
                    return "Memories";
                case Settings:
                    return "Settings";
                default:
                    return id ?? string.Empty;
            }
        }
    }

    /// <summary>What a slice gate decided.</summary>
    public readonly struct SliceGate
    {
        public readonly bool Enabled;
        public readonly string Reason;

        public SliceGate(bool enabled, string reason)
        {
            Enabled = enabled;
            Reason = reason ?? string.Empty;
        }

        public static readonly SliceGate Open = new SliceGate(true, string.Empty);
    }

    /// <summary>
    /// Hook: slices can be greyed out by her mood or persona (for example a bratty persona
    /// refusing). Replace <see cref="Rule"/> for richer logic; the default stays simple.
    /// </summary>
    public static class WheelSliceGates
    {
        public const float RefuseAt = 0.6f;
        public const string AnnoyedRefusal = "She's too annoyed to perform for you right now.";
        public const string SleepyRefusal = "She's too sleepy for that right now.";

        /// <summary>(slice id, shown mood, its strength, persona id) → open or refused.</summary>
        public static Func<string, CompanionMood, float, string, SliceGate> Rule = DefaultRule;

        public static SliceGate Evaluate(string sliceId, CompanionMood mood, float strength, string personaId)
        {
            var rule = Rule ?? DefaultRule;
            try
            {
                return rule(sliceId, mood, strength, personaId ?? string.Empty);
            }
            catch (Exception)
            {
                return SliceGate.Open;
            }
        }

        /// <summary>Only "Do something" can be refused, and only when she is clearly annoyed or sleepy.</summary>
        public static SliceGate DefaultRule(string sliceId, CompanionMood mood, float strength, string personaId)
        {
            if (sliceId != MainWheel.DoSomething || strength < RefuseAt)
                return SliceGate.Open;
            if (mood == CompanionMood.Annoyed)
                return new SliceGate(false, AnnoyedRefusal);
            if (mood == CompanionMood.Sleepy)
                return new SliceGate(false, SleepyRefusal);
            return SliceGate.Open;
        }
    }

    /// <summary>Body parts a context wheel can belong to (JUH-101 hit zones).</summary>
    public enum BodyPart
    {
        None = 0,
        Lips = 1,
        Head = 2,
        Hands = 3,
        Cheeks = 4,
    }

    /// <summary>JUH-101 hook: which body part is under a screen point, and where to anchor its wheel.</summary>
    public interface IBodyHitZones
    {
        /// <param name="unityScreen">The right-click, in Unity screen pixels (origin bottom-left).</param>
        /// <param name="anchor">The point on her the connector line starts from, in Unity screen pixels.</param>
        bool TryHit(Vector2 unityScreen, out BodyPart part, out Vector2 anchor);
    }

    /// <summary>
    /// Body-part wheels. Nothing is registered yet: JUH-101 supplies <see cref="HitZones"/> and
    /// registers slice sets. Planned sets (not built now): lips (kiss / touch / say something),
    /// head (pat / stroke hair), hands (hold hand / high five / pull along), cheeks (poke / pinch).
    /// Adult options must set <see cref="WheelSlice.RequiresIntimateGate"/> and stay local.
    /// </summary>
    public static class BodyPartWheels
    {
        static readonly Dictionary<BodyPart, Func<WheelDefinition>> Sets = new Dictionary<BodyPart, Func<WheelDefinition>>();

        /// <summary>Set by JUH-101. While null, right-click always opens the main wheel.</summary>
        public static IBodyHitZones HitZones;

        public static void Register(BodyPart part, Func<WheelDefinition> build)
        {
            if (part == BodyPart.None)
                return;
            if (build == null)
                Sets.Remove(part);
            else
                Sets[part] = build;
        }

        public static void Clear()
        {
            Sets.Clear();
            HitZones = null;
        }

        /// <summary>A body-part wheel for the click, or null when no zone or no set matches (then the main wheel opens).</summary>
        public static WheelDefinition Resolve(Vector2 unityScreen, out Vector2 anchor)
        {
            anchor = unityScreen;
            var zones = HitZones;
            if (zones == null)
                return null;
            BodyPart part;
            try
            {
                if (!zones.TryHit(unityScreen, out part, out anchor))
                    return null;
            }
            catch (Exception)
            {
                return null;
            }

            Func<WheelDefinition> build;
            if (part == BodyPart.None || !Sets.TryGetValue(part, out build) || build == null)
                return null;
            return build();
        }
    }
}
