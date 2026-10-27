using System;

namespace AICompanion.Avatar
{
    public enum LipSyncTargetKind
    {
        None = 0,
        Vrm,
        BlendShapes,
        JawBone,
        Custom
    }

    public enum MouthViseme
    {
        Rest = 0,
        A,
        I,
        U,
        E,
        O
    }

    /// <summary>
    /// One mouth pose. Channels are 0 to 1 and map onto Aa, Ih, Ou, Ee, and Oh.
    /// </summary>
    public struct MouthWeights
    {
        public float A;
        public float I;
        public float U;
        public float E;
        public float O;

        public float Opening
        {
            get
            {
                var value = A;
                if (I > value)
                    value = I;
                if (U > value)
                    value = U;
                if (E > value)
                    value = E;
                if (O > value)
                    value = O;
                return value;
            }
        }

        public static MouthWeights For(MouthViseme viseme, float amount)
        {
            if (amount < 0f)
                amount = 0f;
            else if (amount > 1.5f)
                amount = 1.5f;

            var weights = new MouthWeights();
            switch (viseme)
            {
                case MouthViseme.A:
                    weights.A = amount;
                    break;
                case MouthViseme.I:
                    weights.I = amount;
                    break;
                case MouthViseme.U:
                    weights.U = amount;
                    break;
                case MouthViseme.E:
                    weights.E = amount;
                    break;
                case MouthViseme.O:
                    weights.O = amount;
                    break;
            }

            return weights;
        }
    }

    /// <summary>
    /// Strength scales how wide the mouth opens. Sensitivity turns raw RMS into that
    /// open amount. Attack is short so a syllable shows up immediately. Release is
    /// longer so the mouth closes after the clip instead of chattering between phonemes.
    /// </summary>
    public struct LipSyncTuning
    {
        public const float DefaultStrength = 1f;
        public const float DefaultSensitivity = 8f;
        public const float DefaultAttackSeconds = 0.02f;
        public const float DefaultReleaseSeconds = 0.1f;
        public const float DefaultSilenceThreshold = 0.012f;
        public const float DefaultJawPitchDegrees = -12f;

        public float Strength;
        public float Sensitivity;
        public float AttackSeconds;
        public float ReleaseSeconds;
        public float SilenceThreshold;

        public static LipSyncTuning Default => new LipSyncTuning
        {
            Strength = DefaultStrength,
            Sensitivity = DefaultSensitivity,
            AttackSeconds = DefaultAttackSeconds,
            ReleaseSeconds = DefaultReleaseSeconds,
            SilenceThreshold = DefaultSilenceThreshold
        };

        public LipSyncTuning Sanitized()
        {
            return new LipSyncTuning
            {
                Strength = Clamp(Strength, 0f, 1.5f),
                Sensitivity = Clamp(Sensitivity, 0f, 32f),
                AttackSeconds = Clamp(AttackSeconds, 0f, 0.2f),
                ReleaseSeconds = Clamp(ReleaseSeconds, 0f, 0.4f),
                SilenceThreshold = Clamp(SilenceThreshold, 0f, 0.2f)
            };
        }

        static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
                return min;
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }

    public struct LipSyncFrame
    {
        public float Level;
        public float MouthOverride;
        public MouthViseme Viseme;
        public MouthWeights Weights;
    }

    /// <summary>
    /// RMS, formant-band visemes, and attack/release. No Unity audio types, so EditMode can run it.
    /// </summary>
    public static class LipSyncMath
    {
        public const float UStartHz = 150f;
        public const float UEndHz = 400f;
        public const float OStartHz = 400f;
        public const float OEndHz = 750f;
        public const float AStartHz = 750f;
        public const float AEndHz = 1400f;
        public const float EStartHz = 1400f;
        public const float EEndHz = 2300f;
        public const float IStartHz = 2300f;
        public const float IEndHz = 4000f;
        public const float VisemeLead = 1.2f;
        public const float MinBandShare = 0.28f;

        public static float Rms(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return 0f;

            double sum = 0d;
            var count = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                if (float.IsNaN(sample))
                    continue;
                sum += (double)sample * sample;
                count++;
            }

            if (count == 0)
                return 0f;
            return (float)Math.Sqrt(sum / count);
        }

        public static float Band(float[] spectrum, float hzPerBin, float startHz, float endHz)
        {
            if (spectrum == null || spectrum.Length == 0 || hzPerBin <= 0f || endHz <= startHz)
                return 0f;

            double sum = 0d;
            var count = 0;
            for (var i = 0; i < spectrum.Length; i++)
            {
                var hz = i * hzPerBin;
                if (hz < startHz)
                    continue;
                if (hz >= endHz)
                    break;

                count++;
                var value = spectrum[i];
                if (value > 0f && !float.IsNaN(value))
                    sum += value;
            }

            if (count == 0)
                return 0f;
            return (float)(sum / count);
        }

        public static float Sense(float rms, LipSyncTuning tuning)
        {
            if (float.IsNaN(rms) || rms < tuning.SilenceThreshold)
                return 0f;

            var sensed = rms * tuning.Sensitivity;
            if (sensed < 0f)
                return 0f;
            if (sensed > 1f)
                return 1f;
            return sensed;
        }

        /// <summary>
        /// Picks A/I/U/E/O from low-to-high bands. A flat or missing spectrum falls back to A
        /// when the clip is loud enough, and to rest when it is not.
        /// </summary>
        public static MouthViseme Estimate(float[] spectrum, float hzPerBin, float amplitude, float silenceThreshold)
        {
            if (float.IsNaN(amplitude) || amplitude < silenceThreshold)
                return MouthViseme.Rest;
            if (spectrum == null || spectrum.Length == 0 || hzPerBin <= 0f)
                return MouthViseme.A;

            var u = Band(spectrum, hzPerBin, UStartHz, UEndHz);
            var o = Band(spectrum, hzPerBin, OStartHz, OEndHz);
            var a = Band(spectrum, hzPerBin, AStartHz, AEndHz);
            var e = Band(spectrum, hzPerBin, EStartHz, EEndHz);
            var i = Band(spectrum, hzPerBin, IStartHz, IEndHz);
            var total = u + o + a + e + i;
            if (total <= 0.000001f)
                return MouthViseme.A;

            var best = 0f;
            var second = 0f;
            var viseme = MouthViseme.A;
            Rank(u, MouthViseme.U, ref best, ref second, ref viseme);
            Rank(o, MouthViseme.O, ref best, ref second, ref viseme);
            Rank(a, MouthViseme.A, ref best, ref second, ref viseme);
            Rank(e, MouthViseme.E, ref best, ref second, ref viseme);
            Rank(i, MouthViseme.I, ref best, ref second, ref viseme);

            if (second >= best || best < second * VisemeLead || best / total < MinBandShare)
                return MouthViseme.A;
            return viseme;
        }

        public static LipSyncFrame Advance(LipSyncFrame current, float rms, MouthViseme estimated, float deltaTime, LipSyncTuning tuning)
        {
            tuning = tuning.Sanitized();
            if (deltaTime < 0f)
                deltaTime = 0f;

            var target = Sense(rms, tuning);
            var seconds = target > current.Level ? tuning.AttackSeconds : tuning.ReleaseSeconds;
            var level = Move(current.Level, target, deltaTime, seconds);
            var viseme = current.Viseme;
            if (target > 0f)
                viseme = estimated == MouthViseme.Rest ? MouthViseme.A : estimated;

            var strength = tuning.Strength;
            var weights = MouthWeights.For(viseme, level * strength);
            return new LipSyncFrame
            {
                Level = level,
                MouthOverride = strength <= 0f ? 0f : level,
                Viseme = viseme,
                Weights = weights
            };
        }

        static void Rank(float energy, MouthViseme viseme, ref float best, ref float second, ref MouthViseme bestViseme)
        {
            if (energy > best)
            {
                second = best;
                best = energy;
                bestViseme = viseme;
                return;
            }

            if (energy > second)
                second = energy;
        }

        static float Move(float current, float target, float deltaTime, float seconds)
        {
            if (seconds <= 0f)
                return target;
            if (deltaTime <= 0f)
                return current;
            var step = deltaTime / seconds;
            if (step > 1f)
                step = 1f;
            return current + (target - current) * step;
        }
    }
}
