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
