using System;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Animator parameter names shared by the controller asset and runtime code.
    /// </summary>
    public static class AvatarAnimatorParameters
    {
        public const string Talking = "Talking";
        public const string Emotion = "Emotion";
        public const string EmotionIntensity = "EmotionIntensity";
        public const string Walking = "Walking";
        public const string Falling = "Falling";
    }

    /// <summary>
    /// Animation layer indices on <c>Companion.controller</c>. Layer 2 is an override
    /// layer for gestures, so the base layer's emotion transitions cannot interrupt one.
    /// </summary>
    public static class AvatarAnimatorLayers
    {
        public const int Base = 0;
        public const int Talk = 1;
        public const int Gestures = 2;
    }

    /// <summary>
    /// State names on <c>Companion.controller</c>. Emotion values match <see cref="Emotion"/>.
    /// </summary>
    public static class AvatarAnimatorStates
    {
        public const string Idle = "Idle";
        public const string Happy = "Happy";
        public const string Sad = "Sad";
        public const string Surprised = "Surprised";
        public const string Thinking = "Thinking";
        public const string Silent = "Silent";
        public const string Talk = "Talk";

        // Gesture layer.
        public const string Empty = "Empty";
        public const string Wave = "Wave";
        public const string Nod = "Nod";
        public const string Shrug = "Shrug";
        public const string Laugh = "Laugh";
        public const string Walk = "Walk";
        public const string Turn180 = "Turn180";
        public const string Fall = "Fall";

        public static string ForEmotion(Emotion emotion)
        {
            switch (emotion)
            {
                case Emotion.Happy:
                    return Happy;
                case Emotion.Sad:
                    return Sad;
                case Emotion.Surprised:
                    return Surprised;
                case Emotion.Thinking:
                    return Thinking;
                default:
                    return Idle;
            }
        }
    }

    /// <summary>
    /// Condition modes and the crossfade used by the controller.
    /// Mode numbers match Unity's AnimatorConditionMode.
    /// </summary>
    public static class AvatarAnimatorRules
    {
        public const int If = 1;
        public const int IfNot = 2;
        public const int Equals = 6;
        public const float TransitionSeconds = 0.3f;

        public static bool Matches(AvatarAnimatorParameterValues values, string parameter, int mode, float threshold)
        {
            if (parameter == AvatarAnimatorParameters.Talking)
            {
                if (mode == If)
                    return values.Talking;
                if (mode == IfNot)
                    return !values.Talking;
                return false;
            }

            if (parameter == AvatarAnimatorParameters.Emotion)
            {
                if (mode == Equals)
                    return values.Emotion == (int)Math.Round(threshold);
                return false;
            }

            if (parameter == AvatarAnimatorParameters.EmotionIntensity && mode == Equals)
                return Math.Abs(values.EmotionIntensity - threshold) < 0.0001f;

            return false;
        }

        public static float BlendProgress(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f)
                return 0f;
            if (elapsedSeconds >= TransitionSeconds)
                return 1f;
            return elapsedSeconds / TransitionSeconds;
        }
    }

    /// <summary>
    /// Values written to the Animator when talking or the emotion changes.
    /// </summary>
    public struct AvatarAnimatorParameterValues
    {
        public bool Talking;
        public int Emotion;
        public float EmotionIntensity;

        public static AvatarAnimatorParameterValues From(bool talking, Emotion emotion, float intensity)
        {
            return new AvatarAnimatorParameterValues
            {
                Talking = talking,
                Emotion = (int)emotion,
                EmotionIntensity = AnimationCue.ClampIntensity(intensity)
            };
        }
    }

    /// <summary>
    /// Talking flag, emotion, and the debug keys that set them.
    /// Number keys 1–5 select an emotion. T toggles talking. F7 and F8 are left alone.
    /// </summary>
    public sealed class AvatarAnimatorModel
    {
        public const string DebugHint = "1-5 emotion   T talk   F10 chat";

        public bool IsTalking { get; private set; }

        public Emotion Emotion { get; private set; }

        public float EmotionIntensity { get; private set; }

        public AvatarAnimatorModel()
        {
            Emotion = Emotion.Neutral;
            EmotionIntensity = 1f;
        }

        public AvatarAnimatorParameterValues Parameters
        {
            get { return AvatarAnimatorParameterValues.From(IsTalking, Emotion, EmotionIntensity); }
        }

        public void SetTalking(bool talking)
        {
            IsTalking = talking;
        }

        public void SetEmotion(Emotion emotion, float intensity)
        {
            Emotion = emotion;
            EmotionIntensity = AnimationCue.ClampIntensity(intensity);
        }

        public bool TryApplyDebugKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    SetEmotion(Emotion.Neutral, 1f);
                    return true;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    SetEmotion(Emotion.Happy, 1f);
                    return true;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    SetEmotion(Emotion.Sad, 1f);
                    return true;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    SetEmotion(Emotion.Surprised, 1f);
                    return true;
                case KeyCode.Alpha5:
                case KeyCode.Keypad5:
                    SetEmotion(Emotion.Thinking, 1f);
                    return true;
                case KeyCode.T:
                    SetTalking(!IsTalking);
                    return true;
                default:
                    return false;
            }
        }

        public static string FormatLabel(Emotion emotion, float intensity, bool talking)
        {
            var clamped = AnimationCue.ClampIntensity(intensity);
            var percent = (int)Math.Round(clamped * 100.0, MidpointRounding.AwayFromZero);
            return emotion.ToString() + " " + percent.ToString() + "%  " + (talking ? "Talk on" : "Talk off");
        }
    }
}
