namespace AICompanion.Settings
{
    public struct PortraitFadeSample
    {
        public string From;
        public float FromAlpha;
        public string Between;
        public float BetweenAlpha;
        public string To;
        public float ToAlpha;

        public PortraitFadeSample(string from, float fromAlpha, string between, float betweenAlpha, string to, float toAlpha)
        {
            From = from;
            FromAlpha = fromAlpha;
            Between = between;
            BetweenAlpha = betweenAlpha;
            To = to;
            ToAlpha = toAlpha;
        }

        public static PortraitFadeSample Settled(string file)
        {
            return new PortraitFadeSample(null, 0f, null, 0f, file, 1f);
        }
    }

    /// <summary>
    /// Expression changes blend for 150–250 ms. An optional in-between frame
    /// holds the middle of that blend.
    /// </summary>
    public struct PortraitCrossfade
    {
        public const float MinSeconds = 0.15f;
        public const float MaxSeconds = 0.25f;
        public const float BetweenEdge = 0.35f;

        public string From;
        public string To;
        public string Between;
        public float Elapsed;
        public float Duration;
        public bool Active;

        public static float DurationFor(float unit)
        {
            return PortraitMath.Lerp(MinSeconds, MaxSeconds, unit);
        }

        public void Begin(string from, string to, string between, float duration)
        {
            From = from ?? string.Empty;
            To = to ?? string.Empty;
            Between = between ?? string.Empty;
            Elapsed = 0f;
            if (float.IsNaN(duration) || duration < MinSeconds)
                duration = MinSeconds;
            if (duration > MaxSeconds)
                duration = MaxSeconds;
            Duration = duration;
            Active = !string.Equals(From, To, System.StringComparison.Ordinal) || Between.Length > 0;
        }

        public PortraitFadeSample Advance(float delta)
        {
            if (!Active)
                return PortraitFadeSample.Settled(To);

            if (delta > 0f && !float.IsNaN(delta))
                Elapsed += delta;

            var t = Duration <= 0f ? 1f : Elapsed / Duration;
            if (t >= 1f)
            {
                Active = false;
                Elapsed = Duration;
                return PortraitFadeSample.Settled(To);
            }

            return Sample(t, From, To, Between);
        }

        public static PortraitFadeSample Sample(float t, string from, string to, string between)
        {
            if (float.IsNaN(t) || t < 0f)
                t = 0f;
            if (t >= 1f)
                return PortraitFadeSample.Settled(to);
            if (string.IsNullOrEmpty(between))
                return new PortraitFadeSample(from, 1f - t, null, 0f, to, t);

            if (t < BetweenEdge)
            {
                var u = t / BetweenEdge;
                return new PortraitFadeSample(from, 1f - u, between, u, null, 0f);
            }

            if (t < 1f - BetweenEdge)
                return new PortraitFadeSample(null, 0f, between, 1f, null, 0f);

            var v = (t - (1f - BetweenEdge)) / BetweenEdge;
            return new PortraitFadeSample(null, 0f, between, 1f - v, to, v);
        }
    }
}
