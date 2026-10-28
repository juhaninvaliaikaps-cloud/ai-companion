using System;

namespace AICompanion.Avatar
{
    /// <summary>
    /// A slow chest pitch around zero, so the first frame does not pop off the clip.
    /// About thirteen breaths a minute. The amplitude stays under two degrees.
    /// </summary>
    public static class BreathingMotion
    {
        public const float BreathsPerSecond = 0.22f;
        public const float ChestDegrees = 1.15f;

        public static float ChestPitch(float timeSeconds)
        {
            if (float.IsNaN(timeSeconds) || timeSeconds < 0f)
                timeSeconds = 0f;

            var phase = timeSeconds * BreathsPerSecond * Math.PI * 2.0;
            return (float)(Math.Sin(phase) * ChestDegrees);
        }
    }
}
