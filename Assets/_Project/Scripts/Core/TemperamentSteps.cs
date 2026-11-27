using System;

namespace AICompanion.Core
{
    /// <summary>
    /// The Settings editor shows the nine 0-100 temperament axes as ten integer steps.
    /// A stored value is only shown at step precision; the stored value is left alone until
    /// the user deliberately moves a slider.
    /// </summary>
    public static class TemperamentSteps
    {
        public const int Min = 1;
        public const int Max = 10;

        /// <summary>The 1-10 step a finite stored 0-100 value displays as.</summary>
        public static int DisplayStep(float stored)
        {
            var value = Finite(stored);
            value = Math.Min(TemperamentAxes.Max, Math.Max(TemperamentAxes.Min, value));
            var offset = Math.Round(9.0 * value / 100.0, MidpointRounding.AwayFromZero);
            return Min + (int)offset;
        }

        /// <summary>The nearest whole 0-100 value for a newly chosen step. Endpoints stay exact.</summary>
        public static int ValueForStep(int step)
        {
            var bounded = Math.Min(Max, Math.Max(Min, step));
            return (int)Math.Round(
                100.0 * (bounded - Min) / (Max - Min),
                MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// True when choosing this step is a new edit. Reselecting a step that merely hides a
        /// stored 11 or 15 (both shown as 2) must not rewrite the stored value.
        /// </summary>
        public static bool IsNewStep(float stored, int chosenStep)
        {
            return chosenStep != DisplayStep(stored);
        }

        /// <summary>The existing descriptive label, with the displayed step instead of the raw value.</summary>
        public static string Label(int axis, int step)
        {
            if (axis < 0 || axis >= TemperamentAxes.Count)
                return string.Empty;
            var bounded = Math.Min(Max, Math.Max(Min, step));
            ProfilePersonality.SliderEnds(axis, out var low, out var high);
            return $"{TemperamentAxes.Names[axis]} {bounded}/10 · {low} → {high}";
        }

        static float Finite(float stored)
        {
            if (float.IsNaN(stored) || float.IsInfinity(stored))
                return TemperamentAxes.Middle;
            return stored;
        }
    }
}
