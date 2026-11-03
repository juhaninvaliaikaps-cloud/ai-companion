using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Pure helpers for the foot offset: a low-pass filter so breathing and sway do not
    /// bob her against the ground, and a clamp so a bad bone or a stretched model cannot
    /// push the window somewhere silly.
    /// </summary>
    public static class FootOffsetMath
    {
        public const float MinHalfLife = 0.05f;

        /// <summary>
        /// Exponential smoothing expressed as a half-life. After one half-life the value
        /// has covered half the distance to the target, whatever the frame rate is.
        /// </summary>
        public static float Smooth(float current, float target, float deltaSeconds, float halfLifeSeconds)
        {
            if (deltaSeconds <= 0f)
                return current;

            var halfLife = Mathf.Max(MinHalfLife, halfLifeSeconds);
            var alpha = 1f - Mathf.Pow(0.5f, deltaSeconds / halfLife);
            return Mathf.Lerp(current, target, alpha);
        }

        public static float ClampOffset(float pixels, int windowHeightPx)
        {
            var max = windowHeightPx <= 1 ? 0f : windowHeightPx - 1f;
            return Mathf.Clamp(pixels, 0f, max);
        }
    }
}
