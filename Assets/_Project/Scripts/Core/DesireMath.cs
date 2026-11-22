using System;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-170: how sexual the moment in one reply was, from the cue's <c>heat="..."</c>. The
    /// model judges it; <see cref="DesireMath"/> keeps the numbers.
    /// </summary>
    public enum DesireHeat
    {
        /// <summary>Not a sexual moment, or the tag did not say.</summary>
        None = 0,

        /// <summary>Suggestive: flirting with a sexual edge, teasing, a lingering touch.</summary>
        Warm = 1,

        /// <summary>Openly sexual: deep kissing, undressing, intimate touching, sex, or something arousing she sees.</summary>
        Hot = 2,

        /// <summary>She climaxes.</summary>
        Release = 3,
    }

    public static class DesireHeats
    {
        /// <summary>The cue attribute: <c>heat="hot"</c>.</summary>
        public const string Attribute = "heat";

        /// <summary>An unknown word is None, never an unreadable tag.</summary>
        public static DesireHeat Parse(string value)
        {
            var word = (value ?? string.Empty).Trim().ToLowerInvariant();
            switch (word)
            {
                case "warm":
                case "suggestive":
                case "1":
                    return DesireHeat.Warm;
                case "hot":
                case "sexual":
                case "2":
                    return DesireHeat.Hot;
                case "release":
                case "climax":
                case "orgasm":
                case "3":
                    return DesireHeat.Release;
                default:
                    return DesireHeat.None;
            }
        }

        public static string Name(DesireHeat heat)
        {
            switch (heat)
            {
                case DesireHeat.Warm: return "warm";
                case DesireHeat.Hot: return "hot";
                case DesireHeat.Release: return "release";
                default: return "none";
            }
        }
    }

    /// <summary>
    /// JUH-170: her desire (0 to 100) and libido (0 to 100), adult mode only. Plain numbers, no
    /// scene. Jussi's decisions, 2026-11-22: the user does not set desire, it moves on its own;
    /// libido is how relentless her needs are. Proposed values, tuned here only:
    /// <list type="bullet">
    /// <item>Time pulls desire toward her resting level (<see cref="RestLevel"/>, from libido), faster
    /// with a higher libido and in a warm mood, slower when she is sleepy, sad or annoyed. Above the
    /// resting level it fades back slowly.</item>
    /// <item>A hot or warm moment (the cue's heat) and her kinks or sensitive spots raise it; a
    /// higher libido raises it more; low affection damps it.</item>
    /// <item>A release drops it to a fraction, and for a while (<see cref="RefractoryMinutes"/>,
    /// shorter with a higher libido) it does not build and moments raise it less.</item>
    /// </list>
    /// </summary>
    public static class DesireMath
    {
        public const float Min = 0f;
        public const float Max = 100f;

        /// <summary>Libido when nothing read or set it.</summary>
        public const float DefaultLibido = 50f;

        /// <summary>Where a new persona starts: a little under her resting level.</summary>
        public const float StartShare = 0.5f;

        /// <summary>Time away counts up to this many minutes (3 days).</summary>
        public const double MaxStepMinutes = 4320d;

        public const float WarmGain = 4f;
        public const float HotGain = 10f;

        /// <summary>One of her kinks or sensitive spots in the moment adds this (before scaling).</summary>
        public const float FavouriteGain = 6f;

        /// <summary>Something she is shy to do adds this: it flusters her more than it arouses her.</summary>
        public const float ShyGain = 2f;

        /// <summary>A release keeps this share of her desire.</summary>
        public const float ReleaseKeeps = 0.15f;

        /// <summary>During the refractory time, moments raise her desire this much less.</summary>
        public const float RefractoryGainShare = 0.3f;

        /// <summary>Being undressed raises the level time pulls her toward by this much.</summary>
        public const float UndressedLift = 10f;

        public static float Clamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return Min;
            return Math.Max(Min, Math.Min(Max, value));
        }

        public static float ClampLibido(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                return DefaultLibido;
            return Math.Min(Max, value);
        }

        static float Unit(float libido)
        {
            return ClampLibido(libido) / Max;
        }

        /// <summary>The level time pulls her toward: 10 at libido 0, 40 at 50, 70 at 100.</summary>
        public static float RestLevel(float libido)
        {
            return 10f + 60f * Unit(libido);
        }

        /// <summary>Minutes for time to cover about 63 % of the way up to her resting level: 240 at libido 0, 45 at 100.</summary>
        public static float BuildMinutes(float libido)
        {
            return 240f - 195f * Unit(libido);
        }

        /// <summary>Minutes for desire above her resting level to fade about 63 % of the way back: 20 at libido 0, 60 at 100.</summary>
        public static float FadeMinutes(float libido)
        {
            return 20f + 40f * Unit(libido);
        }

        /// <summary>How long after a release her desire stays spent: 90 minutes at libido 0, 10 at 100.</summary>
        public static float RefractoryMinutes(float libido)
        {
            return 90f - 80f * Unit(libido);
        }

        /// <summary>Where a new persona starts.</summary>
        public static float Start(float libido)
        {
            return RestLevel(libido) * StartShare;
        }

        /// <summary>
        /// How her mood changes the pace of desire building by itself: a warm mood speeds it up,
        /// a low one slows it. Strength 0 means no change, 1 the full effect.
        /// </summary>
        public static float MoodPace(CompanionMood mood, float strength)
        {
            if (float.IsNaN(strength))
                strength = 0f;
            strength = Math.Max(0f, Math.Min(1f, strength));
            float full;
            switch (mood)
            {
                case CompanionMood.Intimate: full = 1.6f; break;
                case CompanionMood.Affectionate: full = 1.3f; break;
                case CompanionMood.Flustered: full = 1.2f; break;
                case CompanionMood.Happy: full = 1.1f; break;
                case CompanionMood.Anxious: full = 0.8f; break;
                case CompanionMood.Annoyed: full = 0.7f; break;
                case CompanionMood.Sad: full = 0.6f; break;
                case CompanionMood.Sleepy: full = 0.5f; break;
                default: full = 1f; break;
            }

            return 1f + (full - 1f) * strength;
        }

        /// <summary>
        /// How much her affection lets a moment arouse her: fully while she likes the user or is
        /// neutral, less while she dislikes them (0.4 at -50 or below).
        /// </summary>
        public static float AffectionShare(float affection)
        {
            if (float.IsNaN(affection) || affection >= 0f)
                return 1f;
            return Math.Max(0.4f, 1f + affection / 50f * 0.6f);
        }

        /// <summary>
        /// Desire after <paramref name="minutes"/> of time. Below the level time pulls her toward it
        /// builds (not during the refractory time); above it, it fades back. Exponential, so a long
        /// time away settles near the level and never overshoots.
        /// </summary>
        public static float AfterTime(float desire, float libido, double minutes, float moodPace, bool undressed, bool refractory)
        {
            desire = Clamp(desire);
            if (double.IsNaN(minutes) || minutes <= 0d)
                return desire;
            minutes = Math.Min(minutes, MaxStepMinutes);
            var target = Clamp(RestLevel(libido) + (undressed ? UndressedLift : 0f));
            if (desire < target)
            {
                if (refractory)
                    return desire;
                if (float.IsNaN(moodPace) || moodPace <= 0f)
                    moodPace = 1f;
                var tau = BuildMinutes(libido) / moodPace;
                return Clamp(desire + (target - desire) * (float)(1d - Math.Exp(-minutes / tau)));
            }

            if (desire > target)
                return Clamp(desire - (desire - target) * (float)(1d - Math.Exp(-minutes / FadeMinutes(libido))));
            return desire;
        }

        /// <summary>
        /// Desire after one reply's moment: its heat, and the kind of 18+ secret it touched (if
        /// any). Scaled by her cue intensity (0.5 to 1.5), her libido (0.6 to 1.4), her affection
        /// and the room left above her. A release is handled by <see cref="AfterRelease"/>.
        /// </summary>
        public static float AfterMoment(float desire, DesireHeat heat, float intensity, float libido, float affection, bool refractory, IntimateSecretGroup? secret)
        {
            desire = Clamp(desire);
            var gain = heat == DesireHeat.Hot ? HotGain : heat == DesireHeat.Warm ? WarmGain : 0f;
            if (secret.HasValue)
                gain += secret.Value == IntimateSecretGroup.Shy ? ShyGain : FavouriteGain;
            if (gain <= 0f)
                return desire;

            if (float.IsNaN(intensity))
                intensity = 0.5f;
            intensity = Math.Max(0f, Math.Min(1f, intensity));
            gain *= 0.5f + intensity;
            gain *= 0.6f + 0.8f * Unit(libido);
            gain *= AffectionShare(affection);
            if (refractory)
                gain *= RefractoryGainShare;
            var room = (Max - desire) / Max;
            gain *= 0.4f + 0.6f * room;
            return Clamp(desire + gain);
        }

        /// <summary>After she climaxes: most of her desire is spent.</summary>
        public static float AfterRelease(float desire)
        {
            return Clamp(Clamp(desire) * ReleaseKeeps);
        }

        /// <summary>0 calm, 1 warm, 2 aroused, 3 very aroused, 4 overwhelmed.</summary>
        public static int Band(float desire)
        {
            desire = Clamp(desire);
            if (desire < 20f)
                return 0;
            if (desire < 45f)
                return 1;
            if (desire < 70f)
                return 2;
            if (desire < 88f)
                return 3;
            return 4;
        }

        public static string BandName(float desire)
        {
            switch (Band(desire))
            {
                case 0: return "calm";
                case 1: return "warm";
                case 2: return "aroused";
                case 3: return "very aroused";
                default: return "overwhelmed";
            }
        }

        /// <summary>0 low, 1 moderate, 2 high, 3 relentless.</summary>
        public static int LibidoBand(float libido)
        {
            libido = ClampLibido(libido);
            if (libido < 30f)
                return 0;
            if (libido < 60f)
                return 1;
            if (libido < 80f)
                return 2;
            return 3;
        }

        public static string LibidoName(float libido)
        {
            switch (LibidoBand(libido))
            {
                case 0: return "low";
                case 1: return "moderate";
                case 2: return "high";
                default: return "relentless";
            }
        }

        /// <summary>
        /// How she shows it, from her innocence band (<see cref="InnocenceMath.Band"/>): 0 she
        /// hides it (very innocent, innocent), 1 in between, 2 she flaunts it (experienced, perverted).
        /// </summary>
        public static int Openness(float innocence)
        {
            var band = InnocenceMath.Band(innocence);
            if (band <= 1)
                return 0;
            return band == 2 ? 1 : 2;
        }

        /// <summary>True when she dislikes the user enough that wanting them anyway frustrates her.</summary>
        public static bool Conflicted(float affection, float desire)
        {
            return !float.IsNaN(affection) && affection <= -10f && Band(desire) >= 2;
        }

        static readonly CompanionMoodPush[] None = new CompanionMoodPush[0];
        static readonly CompanionMoodPush[] HideAroused = { new CompanionMoodPush(CompanionMood.Flustered, 0.12f), new CompanionMoodPush(CompanionMood.Intimate, 0.05f) };
        static readonly CompanionMoodPush[] MiddleAroused = { new CompanionMoodPush(CompanionMood.Intimate, 0.1f), new CompanionMoodPush(CompanionMood.Flustered, 0.05f) };
        static readonly CompanionMoodPush[] OpenAroused = { new CompanionMoodPush(CompanionMood.Intimate, 0.12f), new CompanionMoodPush(CompanionMood.Happy, 0.04f) };
        static readonly CompanionMoodPush[] HideVery = { new CompanionMoodPush(CompanionMood.Flustered, 0.18f), new CompanionMoodPush(CompanionMood.Intimate, 0.1f) };
        static readonly CompanionMoodPush[] MiddleVery = { new CompanionMoodPush(CompanionMood.Intimate, 0.18f), new CompanionMoodPush(CompanionMood.Flustered, 0.06f) };
        static readonly CompanionMoodPush[] OpenVery = { new CompanionMoodPush(CompanionMood.Intimate, 0.22f) };
        static readonly CompanionMoodPush[] Released = { new CompanionMoodPush(CompanionMood.Affectionate, 0.12f), new CompanionMoodPush(CompanionMood.Happy, 0.08f) };

        /// <summary>
        /// Her mood when her desire climbs into a band (2 or more): the shy side gets flustered,
        /// the bold side intimate. Shared arrays; do not change them. Only while the gate is open
        /// (the mood keeper also drops Intimate while it is closed).
        /// </summary>
        public static CompanionMoodPush[] BandPushes(int band, float innocence)
        {
            var open = Openness(innocence);
            if (band == 2)
                return open == 0 ? HideAroused : open == 1 ? MiddleAroused : OpenAroused;
            if (band >= 3)
                return open == 0 ? HideVery : open == 1 ? MiddleVery : OpenVery;
            return None;
        }

        /// <summary>Her mood just after a release: warm and content.</summary>
        public static CompanionMoodPush[] ReleasePushes()
        {
            return Released;
        }
    }
}
