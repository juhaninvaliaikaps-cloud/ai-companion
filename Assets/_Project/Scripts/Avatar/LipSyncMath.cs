using System;
using System.Globalization;

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
    /// Speech leaves Ih at 0: on ARKit that channel is mouthClose, and pairing it
    /// with jawOpen seals the lips while the jaw hangs open.
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
    /// One place for the mouth tunables. Strength scales the jaw. Sensitivity turns
    /// raw RMS into a level, then a soft knee keeps loud syllables from spiking.
    /// MaxJawOpen is the hard cap. VowelAmount caps funnel and pucker; stretch is
    /// a fraction of that. Attack, release, and viseme hold keep the mouth from
    /// flickering between A/I/U/E/O.
    /// </summary>
    public struct LipSyncTuning
    {
        public const float DefaultStrength = 0.55f;
        public const float DefaultSensitivity = 3f;
        public const float DefaultAttackSeconds = 0.04f;
        public const float DefaultReleaseSeconds = 0.16f;
        public const float DefaultSilenceThreshold = 0.012f;
        public const float DefaultJawPitchDegrees = -12f;
        public const float DefaultMaxJawOpen = 0.40f;
        public const float DefaultVowelAmount = 0.20f;
        public const float DefaultVisemeHoldSeconds = 0.08f;
        public const float DefaultKneeThreshold = 0.40f;
        public const float DefaultKneeRatio = 4f;
        public const float DefaultKneeWidth = 0.25f;

        public float Strength;
        public float Sensitivity;
        public float AttackSeconds;
        public float ReleaseSeconds;
        public float SilenceThreshold;
        public float MaxJawOpen;
        public float VowelAmount;
        public float VisemeHoldSeconds;
        public float KneeThreshold;
        public float KneeRatio;
        public float KneeWidth;

        public static LipSyncTuning Default => new LipSyncTuning
        {
            Strength = DefaultStrength,
            Sensitivity = DefaultSensitivity,
            AttackSeconds = DefaultAttackSeconds,
            ReleaseSeconds = DefaultReleaseSeconds,
            SilenceThreshold = DefaultSilenceThreshold,
            MaxJawOpen = DefaultMaxJawOpen,
            VowelAmount = DefaultVowelAmount,
            VisemeHoldSeconds = DefaultVisemeHoldSeconds,
            KneeThreshold = DefaultKneeThreshold,
            KneeRatio = DefaultKneeRatio,
            KneeWidth = DefaultKneeWidth
        };

        public LipSyncTuning Sanitized()
        {
            var ratio = KneeRatio;
            if (float.IsNaN(ratio) || ratio < 1f)
                ratio = DefaultKneeRatio;

            var threshold = KneeThreshold;
            if (float.IsNaN(threshold) || threshold <= 0f)
                threshold = DefaultKneeThreshold;

            var width = KneeWidth;
            if (float.IsNaN(width) || width <= 0f)
                width = DefaultKneeWidth;

            return new LipSyncTuning
            {
                Strength = Clamp(Strength, 0f, 1.5f),
                Sensitivity = Clamp(Sensitivity, 0f, 16f),
                AttackSeconds = Clamp(AttackSeconds, 0f, 0.2f),
                ReleaseSeconds = Clamp(ReleaseSeconds, 0f, 0.4f),
                SilenceThreshold = Clamp(SilenceThreshold, 0f, 0.2f),
                MaxJawOpen = Clamp(MaxJawOpen, 0f, 1f),
                VowelAmount = Clamp(VowelAmount, 0f, 1f),
                VisemeHoldSeconds = Clamp(VisemeHoldSeconds, 0f, 0.4f),
                KneeThreshold = Clamp(threshold, 0.05f, 1f),
                KneeRatio = Clamp(ratio, 1f, 16f),
                KneeWidth = Clamp(width, 0.01f, 1f)
            };
        }

        public string Describe()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "strength {0:0.00} sensitivity {1:0.00} jaw {2:0.00} vowel {3:0.00}",
                Strength,
                Sensitivity,
                MaxJawOpen,
                VowelAmount);
        }

        public string AppendDiagnostic(string diagnostic)
        {
            if (string.IsNullOrEmpty(diagnostic))
                diagnostic = "LipSync:";
            return diagnostic + " | " + Sanitized().Describe();
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
        public MouthViseme Pending;
        public float PendingSeconds;
        public MouthWeights Weights;
    }

    /// <summary>
    /// RMS, formant-band visemes, compression, and attack/release. No Unity audio types,
    /// so EditMode can run it. The same shaped weights feed blendshapes, VRM, and the jaw bone.
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
        public const float IJawScale = 0.85f;
        public const float VowelJawShare = 0.65f;
        public const float StretchShare = 0.25f;
        public const float StretchJawShare = 0.2f;
        public const float VowelSmoothSeconds = 0.07f;
        public const float CloseSuppressStart = 0.04f;
        public const float CloseSuppressEnd = 0.12f;
        public const float OwnAtLevel = 0.12f;

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
            tuning = tuning.Sanitized();
            if (float.IsNaN(rms) || rms < tuning.SilenceThreshold)
                return 0f;

            var sensed = rms * tuning.Sensitivity;
            if (sensed <= 0f)
                return 0f;
            return Compress(sensed, tuning);
        }

        /// <summary>
        /// Soft-knee compressor. Below the knee the level is linear. Across the knee
        /// it eases into <c>threshold + (x - threshold) / ratio</c>, then clamps at 1.
        /// </summary>
        public static float Compress(float x, LipSyncTuning tuning)
        {
            tuning = tuning.Sanitized();
            if (float.IsNaN(x) || x <= 0f)
                return 0f;

            var threshold = tuning.KneeThreshold;
            var ratio = tuning.KneeRatio;
            var width = tuning.KneeWidth;
            var half = width * 0.5f;
            float y;
            if (x <= threshold - half)
                y = x;
            else if (x >= threshold + half)
                y = threshold + (x - threshold) / ratio;
            else
            {
                var distance = x - threshold + half;
                y = x + ((1f / ratio - 1f) * distance * distance) / (2f * width);
            }

            if (y < 0f)
                return 0f;
            if (y > 1f)
                return 1f;
            return y;
        }

        /// <summary>
        /// 1 while the jaw is shut, 0 once it is open enough that mouthClose would
        /// seal the lips around the opening.
        /// </summary>
        public static float CloseSuppression(float jawOpen)
        {
            if (float.IsNaN(jawOpen) || jawOpen <= CloseSuppressStart)
                return 1f;
            if (jawOpen >= CloseSuppressEnd)
                return 0f;
            return 1f - (jawOpen - CloseSuppressStart) / (CloseSuppressEnd - CloseSuppressStart);
        }

        /// <summary>
        /// Jaw pitch for a full modest open (<see cref="LipSyncTuning.MaxJawOpen"/>)
        /// is <paramref name="fullPitchDegrees"/>. Louder audio does not pitch further.
        /// </summary>
        public static float JawPitchDegrees(MouthWeights weights, LipSyncTuning tuning, float fullPitchDegrees)
        {
            tuning = tuning.Sanitized();
            if (tuning.MaxJawOpen <= 0.0001f || float.IsNaN(fullPitchDegrees))
                return 0f;

            var amount = weights.A / tuning.MaxJawOpen;
            if (amount < 0f)
                amount = 0f;
            else if (amount > 1f)
                amount = 1f;
            return fullPitchDegrees * amount;
        }

        /// <summary>
        /// Jaw on A, a smaller accent on O/U, and a much smaller one on E (mouthStretch).
        /// I narrows the jaw slightly and never writes the close channel.
        /// </summary>
        public static MouthWeights Shape(float level, MouthViseme viseme, LipSyncTuning tuning)
        {
            tuning = tuning.Sanitized();
            if (float.IsNaN(level) || level < 0f)
                level = 0f;
            if (viseme == MouthViseme.Rest || level <= 0f || tuning.Strength <= 0f)
                return default;

            var jaw = level * tuning.Strength;
            if (jaw > tuning.MaxJawOpen)
                jaw = tuning.MaxJawOpen;
            if (viseme == MouthViseme.I)
                jaw *= IJawScale;

            var vowel = jaw * VowelJawShare;
            if (vowel > tuning.VowelAmount)
                vowel = tuning.VowelAmount;

            var stretch = tuning.VowelAmount * StretchShare;
            var stretchFromJaw = jaw * StretchJawShare;
            if (stretch > stretchFromJaw)
                stretch = stretchFromJaw;
            if (stretch < 0f)
                stretch = 0f;
            if (vowel < 0f)
                vowel = 0f;

            var weights = new MouthWeights { A = jaw };
            switch (viseme)
            {
                case MouthViseme.U:
                    weights.U = vowel;
                    break;
                case MouthViseme.O:
                    weights.O = vowel;
                    break;
                case MouthViseme.E:
                    weights.E = stretch;
                    break;
            }

            return weights;
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
            var pending = current.Pending;
            var pendingSeconds = current.PendingSeconds;

            if (target <= 0f)
            {
                pending = MouthViseme.Rest;
                pendingSeconds = 0f;
            }
            else
            {
                var next = estimated == MouthViseme.Rest ? MouthViseme.A : estimated;
                if (viseme == MouthViseme.Rest || tuning.VisemeHoldSeconds <= 0f)
                {
                    viseme = next;
                    pending = MouthViseme.Rest;
                    pendingSeconds = 0f;
                }
                else if (next == viseme)
                {
                    pending = MouthViseme.Rest;
                    pendingSeconds = 0f;
                }
                else
                {
                    if (pending != next)
                    {
                        pending = next;
                        pendingSeconds = 0f;
                    }

                    pendingSeconds += deltaTime;
                    if (pendingSeconds >= tuning.VisemeHoldSeconds)
                    {
                        viseme = pending;
                        pending = MouthViseme.Rest;
                        pendingSeconds = 0f;
                    }
                }
            }

            var shaped = Shape(level, viseme, tuning);
            var weights = current.Weights;
            weights.A = shaped.A;
            weights.I = Move(weights.I, 0f, deltaTime, VowelSmoothSeconds);
            weights.U = Move(weights.U, shaped.U, deltaTime, VowelSmoothSeconds);
            weights.E = Move(weights.E, shaped.E, deltaTime, VowelSmoothSeconds);
            weights.O = Move(weights.O, shaped.O, deltaTime, VowelSmoothSeconds);

            return new LipSyncFrame
            {
                Level = level,
                MouthOverride = Ownership(level, tuning.Strength),
                Viseme = viseme,
                Pending = pending,
                PendingSeconds = pendingSeconds,
                Weights = weights
            };
        }

        static float Ownership(float level, float strength)
        {
            if (strength <= 0f || level <= 0f)
                return 0f;
            var own = level / OwnAtLevel;
            if (own > 1f)
                return 1f;
            return own;
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
