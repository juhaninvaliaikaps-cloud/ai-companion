using System;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-116: where she sits between Innocent (0) and Perverted (100), adult mode only.
    /// Plain numbers: the model reads the start from her persona, the user can set it, and
    /// this class moves it. Intimate moments move her toward experienced; a long time without
    /// them moves her back toward where she started (her baseline). Jussi's decisions,
    /// 2026-11-14 (see the JUH-116 issue).
    /// </summary>
    public static class InnocenceMath
    {
        public const float Min = 0f;
        public const float Max = 100f;
        public const float Default = 50f;

        /// <summary>One intimate moment at intensity 0.5 moves her this far when fully innocent; less the closer she is to the top.</summary>
        public const float MomentStep = 2f;

        /// <summary>No drift back for this long after the last intimate moment.</summary>
        public const double GraceDays = 2d;

        /// <summary>After the grace days, half of what she gained above her baseline fades in this many days.</summary>
        public const double HalfLifeDays = 14d;

        public static float Clamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return Default;
            return Math.Max(Min, Math.Min(Max, value));
        }

        /// <summary>
        /// After one intimate moment: a step toward experienced, scaled by intensity
        /// (0.5 to 1.5 of the step) and by how much room is left above her.
        /// </summary>
        public static float AfterMoment(float value, float intensity)
        {
            value = Clamp(value);
            if (float.IsNaN(intensity))
                intensity = 0.5f;
            intensity = Math.Max(0f, Math.Min(1f, intensity));
            var room = (Max - value) / Max;
            return Clamp(value + MomentStep * (0.5f + intensity) * room);
        }

        /// <summary>
        /// Where she is <paramref name="daysSince"/> days after her last intimate moment, when she
        /// was at <paramref name="atLast"/> then. Only what she gained above her baseline fades,
        /// never below it. Computed from the saved value, so it does not compound.
        /// </summary>
        public static float DriftBack(float atLast, float baseline, double daysSince)
        {
            atLast = Clamp(atLast);
            baseline = Clamp(baseline);
            if (atLast <= baseline || double.IsNaN(daysSince) || daysSince <= GraceDays)
                return atLast;
            var fade = Math.Pow(0.5d, (daysSince - GraceDays) / HalfLifeDays);
            return Clamp(baseline + (float)((atLast - baseline) * fade));
        }

        /// <summary>
        /// A new baseline from a new reading. What she gained above the old baseline is kept on
        /// top of the new one, so a re-read does not erase what happened between them.
        /// </summary>
        public static float Rebase(float current, float oldBaseline, float newBaseline)
        {
            var gained = Math.Max(0f, Clamp(current) - Clamp(oldBaseline));
            return Clamp(Clamp(newBaseline) + gained);
        }

        /// <summary>0 very innocent, 1 innocent, 2 in between, 3 experienced, 4 perverted.</summary>
        public static int Band(float value)
        {
            value = Clamp(value);
            if (value < 15f)
                return 0;
            if (value < 35f)
                return 1;
            if (value <= 65f)
                return 2;
            if (value <= 85f)
                return 3;
            return 4;
        }

        public static string BandName(float value)
        {
            switch (Band(value))
            {
                case 0: return "very innocent";
                case 1: return "innocent";
                case 2: return "in between";
                case 3: return "experienced";
                default: return "perverted";
            }
        }

        /// <summary>
        /// True when a finished reply was an intimate moment: an intimate expression, or an
        /// adult video reaction (undressing or a naked clip). Only while the 18+ gate is open.
        /// </summary>
        public static bool IsMoment(AnimationCue cue, bool gateOpen)
        {
            if (!gateOpen)
                return false;
            if (IntimateExpressions.IsIntimateId(cue.EmotionField))
                return true;
            var reaction = cue.Reaction ?? string.Empty;
            return string.Equals(reaction, "strip", StringComparison.OrdinalIgnoreCase) || IntimateExpressions.IsNakedCue(reaction);
        }

        static readonly CompanionMoodPush[] VeryInnocentMoment = { new CompanionMoodPush(CompanionMood.Flustered, 0.2f), new CompanionMoodPush(CompanionMood.Anxious, 0.06f) };
        static readonly CompanionMoodPush[] InnocentMoment = { new CompanionMoodPush(CompanionMood.Flustered, 0.15f), new CompanionMoodPush(CompanionMood.Affectionate, 0.05f) };
        static readonly CompanionMoodPush[] MiddleMoment = { new CompanionMoodPush(CompanionMood.Flustered, 0.08f), new CompanionMoodPush(CompanionMood.Affectionate, 0.08f) };
        static readonly CompanionMoodPush[] ExperiencedMoment = { new CompanionMoodPush(CompanionMood.Affectionate, 0.1f), new CompanionMoodPush(CompanionMood.Happy, 0.06f) };
        static readonly CompanionMoodPush[] PervertedMoment = { new CompanionMoodPush(CompanionMood.Happy, 0.1f), new CompanionMoodPush(CompanionMood.Affectionate, 0.08f) };

        static readonly CompanionMoodPush[] VeryInnocentUndressed = { new CompanionMoodPush(CompanionMood.Flustered, 0.25f), new CompanionMoodPush(CompanionMood.Anxious, 0.1f) };
        static readonly CompanionMoodPush[] InnocentUndressed = { new CompanionMoodPush(CompanionMood.Flustered, 0.2f), new CompanionMoodPush(CompanionMood.Anxious, 0.05f) };
        static readonly CompanionMoodPush[] MiddleUndressed = { new CompanionMoodPush(CompanionMood.Flustered, 0.12f) };
        static readonly CompanionMoodPush[] ExperiencedUndressed = { new CompanionMoodPush(CompanionMood.Flustered, 0.05f), new CompanionMoodPush(CompanionMood.Happy, 0.05f) };
        static readonly CompanionMoodPush[] PervertedUndressed = { new CompanionMoodPush(CompanionMood.Happy, 0.08f) };

        /// <summary>
        /// Her mood after an intimate moment: the innocent side gets flustered (and a little
        /// anxious at the very innocent end), the experienced side is happy and affectionate.
        /// Shared arrays; do not change them. Intimate itself is never pushed from here.
        /// </summary>
        public static CompanionMoodPush[] MomentPushes(float value)
        {
            switch (Band(value))
            {
                case 0: return VeryInnocentMoment;
                case 1: return InnocentMoment;
                case 2: return MiddleMoment;
                case 3: return ExperiencedMoment;
                default: return PervertedMoment;
            }
        }

        /// <summary>Her mood when she has just been undressed: how comfortable she is being naked.</summary>
        public static CompanionMoodPush[] UndressedPushes(float value)
        {
            switch (Band(value))
            {
                case 0: return VeryInnocentUndressed;
                case 1: return InnocentUndressed;
                case 2: return MiddleUndressed;
                case 3: return ExperiencedUndressed;
                default: return PervertedUndressed;
            }
        }
    }
}
