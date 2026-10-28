using System;
using AICompanion.Core;

namespace AICompanion.Avatar
{
    /// <summary>
    /// One face pose in expression-preset weights, each channel from 0 to 1.
    /// Unity blendshape weights are those values times 100.
    /// </summary>
    public struct FacePose
    {
        public float Aa;
        public float Ih;
        public float Ou;
        public float Ee;
        public float Oh;
        public float Blink;
        public float Joy;
        public float Angry;
        public float Sorrow;
        public float Fun;
        public float Surprised;
        public float EyeSquint;
        public float BrowInnerUp;
        public float BrowOuterUp;
        public float EyeWide;
        public float EyeLookDown;
        public float Blush;

        public float Get(ExpressionPreset preset)
        {
            switch (preset)
            {
                case ExpressionPreset.Aa:
                    return Aa;
                case ExpressionPreset.Ih:
                    return Ih;
                case ExpressionPreset.Ou:
                    return Ou;
                case ExpressionPreset.Ee:
                    return Ee;
                case ExpressionPreset.Oh:
                    return Oh;
                case ExpressionPreset.Blink:
                    return Blink;
                case ExpressionPreset.Joy:
                    return Joy;
                case ExpressionPreset.Angry:
                    return Angry;
                case ExpressionPreset.Sorrow:
                    return Sorrow;
                case ExpressionPreset.Fun:
                    return Fun;
                case ExpressionPreset.Surprised:
                    return Surprised;
                case ExpressionPreset.EyeSquint:
                    return EyeSquint;
                case ExpressionPreset.BrowInnerUp:
                    return BrowInnerUp;
                case ExpressionPreset.BrowOuterUp:
                    return BrowOuterUp;
                case ExpressionPreset.EyeWide:
                    return EyeWide;
                case ExpressionPreset.EyeLookDown:
                    return EyeLookDown;
                case ExpressionPreset.Blush:
                    return Blush;
                default:
                    return 0f;
            }
        }

        public void Set(ExpressionPreset preset, float value)
        {
            switch (preset)
            {
                case ExpressionPreset.Aa:
                    Aa = value;
                    break;
                case ExpressionPreset.Ih:
                    Ih = value;
                    break;
                case ExpressionPreset.Ou:
                    Ou = value;
                    break;
                case ExpressionPreset.Ee:
                    Ee = value;
                    break;
                case ExpressionPreset.Oh:
                    Oh = value;
                    break;
                case ExpressionPreset.Blink:
                    Blink = value;
                    break;
                case ExpressionPreset.Joy:
                    Joy = value;
                    break;
                case ExpressionPreset.Angry:
                    Angry = value;
                    break;
                case ExpressionPreset.Sorrow:
                    Sorrow = value;
                    break;
                case ExpressionPreset.Fun:
                    Fun = value;
                    break;
                case ExpressionPreset.Surprised:
                    Surprised = value;
                    break;
                case ExpressionPreset.EyeSquint:
                    EyeSquint = value;
                    break;
                case ExpressionPreset.BrowInnerUp:
                    BrowInnerUp = value;
                    break;
                case ExpressionPreset.BrowOuterUp:
                    BrowOuterUp = value;
                    break;
                case ExpressionPreset.EyeWide:
                    EyeWide = value;
                    break;
                case ExpressionPreset.EyeLookDown:
                    EyeLookDown = value;
                    break;
                case ExpressionPreset.Blush:
                    Blush = value;
                    break;
            }
        }

        public FacePose Scale(float scale)
        {
            return new FacePose
            {
                Aa = Aa * scale,
                Ih = Ih * scale,
                Ou = Ou * scale,
                Ee = Ee * scale,
                Oh = Oh * scale,
                Blink = Blink * scale,
                Joy = Joy * scale,
                Angry = Angry * scale,
                Sorrow = Sorrow * scale,
                Fun = Fun * scale,
                Surprised = Surprised * scale,
                EyeSquint = EyeSquint * scale,
                BrowInnerUp = BrowInnerUp * scale,
                BrowOuterUp = BrowOuterUp * scale,
                EyeWide = EyeWide * scale,
                EyeLookDown = EyeLookDown * scale,
                Blush = Blush * scale
            };
        }

        public float UnityWeight(ExpressionPreset preset)
        {
            return Clamp01(Get(preset)) * 100f;
        }

        static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }

    public readonly struct FaceWrite
    {
        public FaceWrite(ExpressionPreset preset, float unityWeight)
        {
            Preset = preset;
            UnityWeight = unityWeight;
        }

        public ExpressionPreset Preset { get; }
        public float UnityWeight { get; }
    }

    /// <summary>
    /// Maps emotions and the talking placeholder onto expression presets, and eases between poses.
    /// Missing presets are skipped so a model with no blendshapes is unchanged.
    /// </summary>
    public static class EmotionFace
    {
        public const float BlendSeconds = 0.3f;
        public const float TalkRateHz = 3.5f;

        public static FacePose EmotionPose(Emotion emotion, float intensity)
        {
            return ExpressionPresetLibrary.Active.Get(emotion).Scale(AnimationCue.ClampIntensity(intensity));
        }

        /// <summary>
        /// Blush for an emotion. <paramref name="enabled"/> is the global switch
        /// (<see cref="BlushSettings.Enabled"/>). False always returns 0.
        /// </summary>
        public static float BlushAmount(Emotion emotion, float intensity, bool enabled)
        {
            if (!enabled)
                return 0f;
            return EmotionPose(emotion, intensity).Blush;
        }

        public static FacePose TalkPose(float timeSeconds, float amount)
        {
            if (amount < 0f)
                amount = 0f;
            else if (amount > 1f)
                amount = 1f;
            if (amount <= 0f)
                return default;

            var phase = timeSeconds * TalkRateHz * Math.PI * 2.0;
            var jaw = 0.5 + 0.5 * Math.Sin(phase);
            var alt = 0.5 + 0.5 * Math.Sin(phase * 0.5 + 1.3);
            return new FacePose
            {
                Aa = (float)(jaw * 0.85 * amount),
                Oh = (float)((1.0 - jaw) * alt * 0.45 * amount),
                Ou = (float)((1.0 - jaw) * (1.0 - alt) * 0.35 * amount)
            };
        }

        public static float ProceduralTalkScale(float talkAmount, float mouthOverride)
        {
            if (talkAmount < 0f)
                talkAmount = 0f;
            else if (talkAmount > 1f)
                talkAmount = 1f;

            var keep = 1f - Clamp01(mouthOverride);
            return talkAmount * keep;
        }

        /// <summary>
        /// Replaces emotion mouth channels with the speech weights. Speech does not add
        /// on top of the emotion jaw. Brows, blinks, and smiles stay on the emotion pose.
        /// An open jaw clears mouthClose so the lips are not sealed around it.
        /// </summary>
        public static FacePose OverrideMouth(FacePose pose, MouthWeights mouth, float ownership)
        {
            var t = Clamp01(ownership);
            if (t <= 0f)
                return pose;

            var aa = Lerp(pose.Aa, mouth.A, t);
            var ih = Lerp(pose.Ih, mouth.I, t) * LipSyncMath.CloseSuppression(aa);
            return new FacePose
            {
                Aa = aa,
                Ih = ih,
                Ou = Lerp(pose.Ou, mouth.U, t),
                Ee = Lerp(pose.Ee, mouth.E, t),
                Oh = Lerp(pose.Oh, mouth.O, t),
                Blink = pose.Blink,
                Joy = pose.Joy,
                Angry = pose.Angry,
                Sorrow = pose.Sorrow,
                Fun = pose.Fun,
                Surprised = pose.Surprised,
                EyeSquint = pose.EyeSquint,
                BrowInnerUp = pose.BrowInnerUp,
                BrowOuterUp = pose.BrowOuterUp,
                EyeWide = pose.EyeWide,
                EyeLookDown = pose.EyeLookDown,
                Blush = pose.Blush
            };
        }

        /// <summary>
        /// A procedural blink closes the eyes without pulling the emotion squint back
        /// open, and without writing mouth, brow, or smile channels.
        /// </summary>
        public static FacePose ApplyProceduralBlink(FacePose pose, float blink)
        {
            if (float.IsNaN(blink) || blink < pose.Blink)
                blink = pose.Blink;
            if (blink < 0f)
                blink = 0f;
            else if (blink > 1f)
                blink = 1f;

            pose.Blink = blink;
            return pose;
        }

        public static FacePose Combine(FacePose emotion, FacePose talk)
        {
            return new FacePose
            {
                Aa = Clamp01(emotion.Aa + talk.Aa),
                Ih = Clamp01(emotion.Ih + talk.Ih),
                Ou = Clamp01(emotion.Ou + talk.Ou),
                Ee = Clamp01(emotion.Ee + talk.Ee),
                Oh = Clamp01(emotion.Oh + talk.Oh),
                Blink = Clamp01(emotion.Blink + talk.Blink),
                Joy = Clamp01(emotion.Joy + talk.Joy),
                Angry = Clamp01(emotion.Angry + talk.Angry),
                Sorrow = Clamp01(emotion.Sorrow + talk.Sorrow),
                Fun = Clamp01(emotion.Fun + talk.Fun),
                Surprised = Clamp01(emotion.Surprised + talk.Surprised),
                EyeSquint = Clamp01(emotion.EyeSquint + talk.EyeSquint),
                BrowInnerUp = Clamp01(emotion.BrowInnerUp + talk.BrowInnerUp),
                BrowOuterUp = Clamp01(emotion.BrowOuterUp + talk.BrowOuterUp),
                EyeWide = Clamp01(emotion.EyeWide + talk.EyeWide),
                EyeLookDown = Clamp01(emotion.EyeLookDown + talk.EyeLookDown),
                Blush = Clamp01(emotion.Blush + talk.Blush)
            };
        }

        public static float BlendStep(float deltaTime, float seconds)
        {
            if (seconds <= 0f)
                return 1f;
            if (deltaTime <= 0f)
                return 0f;
            var step = deltaTime / seconds;
            return step > 1f ? 1f : step;
        }

        public static FacePose Approach(FacePose current, FacePose target, float deltaTime, float seconds)
        {
            var step = BlendStep(deltaTime, seconds);
            return new FacePose
            {
                Aa = Move(current.Aa, target.Aa, step),
                Ih = Move(current.Ih, target.Ih, step),
                Ou = Move(current.Ou, target.Ou, step),
                Ee = Move(current.Ee, target.Ee, step),
                Oh = Move(current.Oh, target.Oh, step),
                Blink = Move(current.Blink, target.Blink, step),
                Joy = Move(current.Joy, target.Joy, step),
                Angry = Move(current.Angry, target.Angry, step),
                Sorrow = Move(current.Sorrow, target.Sorrow, step),
                Fun = Move(current.Fun, target.Fun, step),
                Surprised = Move(current.Surprised, target.Surprised, step),
                EyeSquint = Move(current.EyeSquint, target.EyeSquint, step),
                BrowInnerUp = Move(current.BrowInnerUp, target.BrowInnerUp, step),
                BrowOuterUp = Move(current.BrowOuterUp, target.BrowOuterUp, step),
                EyeWide = Move(current.EyeWide, target.EyeWide, step),
                EyeLookDown = Move(current.EyeLookDown, target.EyeLookDown, step),
                Blush = Move(current.Blush, target.Blush, step)
            };
        }

        public static int Collect(FacePose pose, bool[] available, FaceWrite[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            var count = 0;
            var last = (int)ExpressionPreset.Blush;
            for (var i = 1; i <= last && count < destination.Length; i++)
            {
                if (available == null || i >= available.Length || !available[i])
                    continue;

                var preset = (ExpressionPreset)i;
                destination[count++] = new FaceWrite(preset, pose.UnityWeight(preset));
            }

            return count;
        }

        static float Lerp(float from, float to, float t)
        {
            return from + (to - from) * t;
        }

        static float Move(float current, float target, float step)
        {
            return current + (target - current) * step;
        }

        static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }
}
