using System;

namespace AICompanion.Avatar
{
    /// <summary>
    /// One additive idle offset in degrees. Zero is the clip pose.
    /// </summary>
    public struct IdlePose
    {
        public float SpinePitch;
        public float SpineYaw;
        public float SpineRoll;
        public float ChestPitch;
        public float HeadYaw;
        public float HeadPitch;
        public float HeadRoll;

        public static IdlePose Lerp(IdlePose from, IdlePose to, float t)
        {
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;

            return new IdlePose
            {
                SpinePitch = from.SpinePitch + (to.SpinePitch - from.SpinePitch) * t,
                SpineYaw = from.SpineYaw + (to.SpineYaw - from.SpineYaw) * t,
                SpineRoll = from.SpineRoll + (to.SpineRoll - from.SpineRoll) * t,
                ChestPitch = from.ChestPitch + (to.ChestPitch - from.ChestPitch) * t,
                HeadYaw = from.HeadYaw + (to.HeadYaw - from.HeadYaw) * t,
                HeadPitch = from.HeadPitch + (to.HeadPitch - from.HeadPitch) * t,
                HeadRoll = from.HeadRoll + (to.HeadRoll - from.HeadRoll) * t
            };
        }

        public float MaxAbs()
        {
            var max = 0f;
            max = Larger(max, SpinePitch);
            max = Larger(max, SpineYaw);
            max = Larger(max, SpineRoll);
            max = Larger(max, ChestPitch);
            max = Larger(max, HeadYaw);
            max = Larger(max, HeadPitch);
            max = Larger(max, HeadRoll);
            return max;
        }

        public static bool Near(IdlePose a, IdlePose b, float degrees)
        {
            return Math.Abs(a.SpinePitch - b.SpinePitch) <= degrees
                && Math.Abs(a.SpineYaw - b.SpineYaw) <= degrees
                && Math.Abs(a.SpineRoll - b.SpineRoll) <= degrees
                && Math.Abs(a.ChestPitch - b.ChestPitch) <= degrees
                && Math.Abs(a.HeadYaw - b.HeadYaw) <= degrees
                && Math.Abs(a.HeadPitch - b.HeadPitch) <= degrees
                && Math.Abs(a.HeadRoll - b.HeadRoll) <= degrees;
        }

        static float Larger(float max, float value)
        {
            var abs = Math.Abs(value);
            return abs > max ? abs : max;
        }
    }

    public static class IdlePoses
    {
        public const int Count = 7;

        public static IdlePose For(int index)
        {
            switch (index)
            {
                case 1:
                    return new IdlePose { SpineRoll = -2.4f, HeadRoll = -1.4f };
                case 2:
                    return new IdlePose { SpineRoll = 2.4f, HeadRoll = 1.4f };
                case 3:
                    return new IdlePose { HeadRoll = 3.2f, HeadPitch = -0.8f };
                case 4:
                    return new IdlePose { HeadRoll = -3.2f, HeadPitch = -0.8f };
                case 5:
                    return new IdlePose { HeadYaw = -4.2f, SpineYaw = -1.6f };
                case 6:
                    return new IdlePose { HeadYaw = 4.2f, SpineYaw = 1.6f, ChestPitch = 0.6f };
                default:
                    return default;
            }
        }
    }

    /// <summary>
    /// Crossfades weight shifts and small look-arounds. The next pose is never the
    /// current one. Blend 0 matches the current pose and blend 1 matches the next,
    /// so the handoff does not snap.
    /// </summary>
    public struct IdleVariationPlayer
    {
        public const float CrossfadeSeconds = 1.1f;
        public const float MinHoldSeconds = 4.5f;
        public const float MaxHoldSeconds = 9f;
        public const float MaxStepSeconds = 0.1f;

        public int Current;
        public int Upcoming;
        public float Blend;
        public float Hold;
        public bool Fading;

        public static IdleVariationPlayer Begin(int current, float holdRandom)
        {
            if (current < 0 || current >= IdlePoses.Count)
                current = 0;

            return new IdleVariationPlayer
            {
                Current = current,
                Upcoming = current,
                Hold = HoldSeconds(holdRandom)
            };
        }

        public static float HoldSeconds(float unitRandom)
        {
            var t = Clamp01(unitRandom);
            return MinHoldSeconds + (MaxHoldSeconds - MinHoldSeconds) * t;
        }

        public static int PickNext(int current, float unitRandom)
        {
            var count = IdlePoses.Count;
            if (count <= 1)
                return current < 0 ? 0 : current;

            if (current < 0 || current >= count)
                current = 0;

            var span = count - 1;
            var slot = (int)(Clamp01(unitRandom) * span);
            if (slot >= span)
                slot = span - 1;
            if (slot >= current)
                slot++;
            return slot;
        }

        public static float SmoothStep(float t)
        {
            if (t <= 0f)
                return 0f;
            if (t >= 1f)
                return 1f;
            return t * t * (3f - 2f * t);
        }

        public IdlePose Sample()
        {
            var from = IdlePoses.For(Current);
            if (!Fading)
                return from;
            return IdlePose.Lerp(from, IdlePoses.For(Upcoming), SmoothStep(Blend));
        }

        public IdleVariationPlayer Advance(float deltaTime, float nextRandom, float holdRandom)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;
            if (deltaTime > MaxStepSeconds)
                deltaTime = MaxStepSeconds;

            if (!Fading)
            {
                Hold -= deltaTime;
                if (Hold > 0f)
                    return this;

                Upcoming = PickNext(Current, nextRandom);
                Fading = true;
                Blend = 0f;
                Hold = 0f;
                return this;
            }

            if (CrossfadeSeconds <= 0.0001f)
                Blend = 1f;
            else
                Blend += deltaTime / CrossfadeSeconds;

            if (Blend >= 1f)
            {
                Current = Upcoming;
                Fading = false;
                Blend = 0f;
                Hold = HoldSeconds(holdRandom);
            }

            return this;
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
}
