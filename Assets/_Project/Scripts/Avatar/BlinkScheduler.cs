using System;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Timing for a natural blink. The close is shorter than the open. A double blink
    /// is two of those curves with a short gap. Intervals stay inside
    /// <see cref="MinGapSeconds"/> and <see cref="LongMaxSeconds"/>.
    /// </summary>
    public static class BlinkTiming
    {
        public const float CloseSeconds = 0.07f;
        public const float HoldSeconds = 0.05f;
        public const float OpenSeconds = 0.11f;
        public const float DoubleGapSeconds = 0.08f;
        public const float DoubleChance = 0.2f;
        public const float MinGapSeconds = 1.8f;
        public const float MaxGapSeconds = 6.5f;
        public const float LongChance = 0.12f;
        public const float LongMaxSeconds = 10f;

        public static float SingleDuration
        {
            get { return CloseSeconds + HoldSeconds + OpenSeconds; }
        }

        public static float Duration(bool doubleBlink)
        {
            var one = SingleDuration;
            if (!doubleBlink)
                return one;
            return one + DoubleGapSeconds + one;
        }

        public static bool IsDouble(float unitRandom)
        {
            if (float.IsNaN(unitRandom) || unitRandom < 0f)
                return false;
            return unitRandom < DoubleChance;
        }

        /// <summary>
        /// Gap until the next blink. <paramref name="longRandom"/> below
        /// <see cref="LongChance"/> stretches the gap toward <see cref="LongMaxSeconds"/>.
        /// </summary>
        public static float GapSeconds(float unitRandom, float longRandom)
        {
            var unit = Clamp01(unitRandom);
            var gate = Clamp01(longRandom);
            if (gate < LongChance)
                return MaxGapSeconds + (LongMaxSeconds - MaxGapSeconds) * unit;
            return MinGapSeconds + (MaxGapSeconds - MinGapSeconds) * unit;
        }

        public static float Sample(float time, bool doubleBlink)
        {
            if (float.IsNaN(time) || time <= 0f)
                return 0f;

            var one = SingleDuration;
            if (!doubleBlink)
                return OneShot(time);

            if (time < one)
                return OneShot(time);

            time -= one;
            if (time < DoubleGapSeconds)
                return 0f;

            return OneShot(time - DoubleGapSeconds);
        }

        static float OneShot(float time)
        {
            if (time <= 0f)
                return 0f;
            if (time < CloseSeconds)
            {
                var remain = 1f - time / CloseSeconds;
                return 1f - remain * remain * remain;
            }

            time -= CloseSeconds;
            if (time < HoldSeconds)
                return 1f;

            time -= HoldSeconds;
            if (time >= OpenSeconds)
                return 0f;

            var open = 1f - time / OpenSeconds;
            return open * open;
        }

        static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }

    /// <summary>
    /// Waits a random gap, plays one blink (sometimes two), then waits again.
    /// A large frame does not skip the close: the frame that ends the wait starts
    /// the blink at time zero.
    /// </summary>
    public struct BlinkScheduler
    {
        public float Wait;
        public float Time;
        public float Weight;
        public bool Active;
        public bool Double;

        public static BlinkScheduler Start(float gapRandom, float longRandom)
        {
            return new BlinkScheduler
            {
                Wait = BlinkTiming.GapSeconds(gapRandom, longRandom),
                Weight = 0f
            };
        }

        public BlinkScheduler Tick(float deltaTime, float gapRandom, float longRandom, float doubleRandom)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            if (!Active)
            {
                Wait -= deltaTime;
                if (Wait > 0f)
                {
                    Weight = 0f;
                    return this;
                }

                Active = true;
                Double = BlinkTiming.IsDouble(doubleRandom);
                Time = 0f;
                Weight = 0f;
                Wait = 0f;
                return this;
            }

            Time += deltaTime;
            if (Time >= BlinkTiming.Duration(Double))
            {
                Active = false;
                Double = false;
                Time = 0f;
                Weight = 0f;
                Wait = BlinkTiming.GapSeconds(gapRandom, longRandom);
                return this;
            }

            Weight = BlinkTiming.Sample(Time, Double);
            return this;
        }
    }
}
