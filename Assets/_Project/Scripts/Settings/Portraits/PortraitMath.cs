using System;
using AICompanion.Core;

namespace AICompanion.Settings
{
    public static class PortraitMath
    {
        public static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }
    }

    /// <summary>
    /// Keeps the mood she was in while she speaks. A reply without a cue resolves to
    /// neutral, which used to flip a blushing face to the neutral talking frame the moment
    /// she started talking. The neutral cue often lands before the audio, so the hold
    /// survives that quiet gap and the whole utterance, then releases once she has been
    /// idle.
    /// </summary>
    public static class PortraitHold
    {
        public const float ReleaseSeconds = 1.5f;

        public static string Resolve(string emotion, ref string held, bool speaking)
        {
            var quiet = 0f;
            return ResolveUtterance(emotion, ref held, speaking, speaking ? 0f : ReleaseSeconds, ref quiet);
        }

        public static string ResolveUtterance(string emotion, ref string held, bool speaking, float delta, ref float quietFor)
        {
            var neutral = string.IsNullOrEmpty(emotion)
                || string.Equals(emotion, "neutral", System.StringComparison.OrdinalIgnoreCase);

            if (!neutral)
            {
                held = emotion;
                quietFor = 0f;
                return emotion;
            }

            if (speaking)
            {
                quietFor = 0f;
                if (!string.IsNullOrEmpty(held))
                    return held;
                return "neutral";
            }

            if (string.IsNullOrEmpty(held))
            {
                quietFor = 0f;
                return "neutral";
            }

            if (delta < 0f || float.IsNaN(delta))
                delta = 0f;

            quietFor += delta;
            if (quietFor < ReleaseSeconds)
                return held;

            held = string.Empty;
            quietFor = 0f;
            return "neutral";
        }
    }

    /// <summary>
    /// F9 cycles the portrait on the avatar. While that preview is up, and she is not
    /// mid-reply or mid-speech, the panel flaps that expression's blink and talk frames.
    /// A later cue, emotion key, or utterance leaves the preview.
    /// </summary>
    public struct PortraitPreview
    {
        public bool Active { get; private set; }

        public string Expression { get; private set; }

        string _hint;
        Emotion _emotion;

        public PortraitPreview Tick(bool keyDown, string hint, Emotion emotion, bool utterance)
        {
            hint = string.IsNullOrEmpty(hint) ? string.Empty : hint;

            if (utterance)
            {
                Active = false;
                Expression = string.Empty;
                return this;
            }

            if (keyDown)
            {
                Active = true;
                _hint = hint;
                _emotion = emotion;
                Expression = hint.Length == 0 ? "neutral" : hint;
                return this;
            }

            if (!Active)
                return this;

            if (!string.Equals(hint, _hint, StringComparison.Ordinal) || emotion != _emotion)
            {
                Active = false;
                Expression = string.Empty;
                return this;
            }

            return this;
        }
    }

    /// <summary>
    /// File stems for the local portraits. The images themselves stay out of the repo.
    /// </summary>
    public static class PortraitStems
    {
        public const string Happy = "01_happy";
        public const string Concerned = "02_concerned";
        public const string Embarrassed = "03_embarrassed";
        public const string NeutralStill = "04_neutral";
        public const string Smile = "05_smile";
        public const string Shy = "06_shy";
        public const string Flustered = "07_flustered";
        public const string EmbarrassedWorried = "08_embarrassed_worried";
        public const string Pouty = "09_pouty";
        public const string Sad = "10_sad";
        public const string Surprised = "11_surprised";
        public const string Sleepy = "12_sleepy";
        public const string Thinking = "13_thinking";
        public const string SmugWink = "14_smug_wink";
        public const string Excited = "15_excited";
        public const string WinkSmile = "16_wink_smile";

        public const string NeutralOpen = "neutral_open";
        public const string NeutralBlinkHalf = "neutral_blink_half";
        public const string NeutralBlinkClosed = "neutral_blink_closed";
        public const string NeutralTalk = "neutral_talk";
        public const string SmileOpen = "smile_open";
        public const string SmileBlinkHalf = "smile_blink_half";
        public const string SmileBlinkClosed = "smile_blink_closed";
        public const string SmileTalk = "smile_talk";

        public const string ConcernedBlinkHalf = "concerned_blink_half";
        public const string ConcernedBlinkClosed = "concerned_blink_closed";
        public const string ConcernedTalk = "concerned_talk";
        public const string EmbarrassedBlinkHalf = "embarrassed_blink_half";
        public const string EmbarrassedBlinkClosed = "embarrassed_blink_closed";
        public const string EmbarrassedTalk = "embarrassed_talk";
        public const string ShyBlinkHalf = "shy_blink_half";
        public const string ShyBlinkClosed = "shy_blink_closed";
        public const string ShyTalk = "shy_talk";
        public const string FlusteredBlinkHalf = "flustered_blink_half";
        public const string FlusteredBlinkClosed = "flustered_blink_closed";
        public const string FlusteredTalk = "flustered_talk";
        public const string PoutyBlinkHalf = "pouty_blink_half";
        public const string PoutyBlinkClosed = "pouty_blink_closed";
        public const string PoutyTalk = "pouty_talk";
        public const string SadBlinkHalf = "sad_blink_half";
        public const string SadBlinkClosed = "sad_blink_closed";
        public const string SadTalk = "sad_talk";
        public const string SurprisedBlinkHalf = "surprised_blink_half";
        public const string SurprisedBlinkClosed = "surprised_blink_closed";
        public const string SurprisedTalk = "surprised_talk";
        public const string ThinkingBlinkHalf = "thinking_blink_half";
        public const string ThinkingBlinkClosed = "thinking_blink_closed";
        public const string ThinkingTalk = "thinking_talk";
        public const string ExcitedBlinkHalf = "excited_blink_half";
        public const string ExcitedBlinkClosed = "excited_blink_closed";
        public const string ExcitedTalk = "excited_talk";
        public const string HappyBlinkHalf = "happy_blink_half";
        public const string HappyBlinkClosed = "happy_blink_closed";
        public const string HappyTalk = "happy_talk";

        static readonly char[] Separators = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

        public static string Stem(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var trimmed = name.Trim();
            if (trimmed.IndexOf("..", StringComparison.Ordinal) >= 0)
                return null;
            if (trimmed.IndexOfAny(Separators) >= 0)
                return null;

            if (EndsWith(trimmed, ".png") || EndsWith(trimmed, ".jpg"))
                trimmed = trimmed.Substring(0, trimmed.Length - 4);
            else if (EndsWith(trimmed, ".jpeg"))
                trimmed = trimmed.Substring(0, trimmed.Length - 5);

            if (trimmed.Length == 0 || trimmed.Length > 64)
                return null;
            return trimmed;
        }

        public static bool IsSafe(string name)
        {
            var stem = Stem(name);
            if (stem == null)
                return false;

            for (var i = 0; i < stem.Length; i++)
            {
                var c = stem[i];
                var ok = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '-';
                if (!ok)
                    return false;
            }

            return true;
        }

        public static bool IsImageExtension(string extension)
        {
            return EndsWith(extension, ".png") || EndsWith(extension, ".jpg") || EndsWith(extension, ".jpeg");
        }

        static bool EndsWith(string value, string suffix)
        {
            if (value == null || suffix == null || value.Length < suffix.Length)
                return false;
            return string.Compare(value, value.Length - suffix.Length, suffix, 0, suffix.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }
    }

    public static class PortraitNames
    {
        public static string For(Emotion emotion)
        {
            if (!Enum.IsDefined(typeof(Emotion), emotion))
                return "neutral";

            switch (emotion)
            {
                case Emotion.Neutral:
                    return "neutral";
                case Emotion.Happy:
                    return "happy";
                case Emotion.Sad:
                    return "sad";
                case Emotion.Surprised:
                    return "surprised";
                case Emotion.Thinking:
                    return "thinking";
                default:
                    return emotion.ToString().ToLowerInvariant();
            }
        }

        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            return name.Trim().ToLowerInvariant();
        }

        public static string TransitionKey(string from, string to)
        {
            return Normalize(from) + "->" + Normalize(to);
        }
    }

    /// <summary>
    /// Letterboxed fit. Width and height stay in proportion inside the frame.
    /// </summary>
    public static class PortraitFit
    {
        public static UnityEngine.Rect Inside(float frameWidth, float frameHeight, float imageWidth, float imageHeight)
        {
            if (frameWidth <= 0f || frameHeight <= 0f)
                return new UnityEngine.Rect(0f, 0f, 0f, 0f);
            if (imageWidth <= 0f || imageHeight <= 0f || float.IsNaN(imageWidth) || float.IsNaN(imageHeight))
                return new UnityEngine.Rect(0f, 0f, frameWidth, frameHeight);

            var scale = frameWidth / imageWidth;
            var byHeight = frameHeight / imageHeight;
            if (byHeight < scale)
                scale = byHeight;

            var width = imageWidth * scale;
            var height = imageHeight * scale;
            return new UnityEngine.Rect((frameWidth - width) * 0.5f, (frameHeight - height) * 0.5f, width, height);
        }
    }
}
