using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AICompanion.Core
{
    public enum CueParseStatus
    {
        None = 0,
        Parsed = 1,
        Invalid = 2,
    }

    /// <summary>
    /// The visible reply text and the cue carried by it. <see cref="Text"/> never
    /// contains the cue tag, even when the tag is malformed.
    /// </summary>
    public readonly struct CueParseResult
    {
        public string Text { get; }
        public AnimationCue Cue { get; }
        public CueParseStatus Status { get; }

        public bool HasCue
        {
            get { return Status != CueParseStatus.None; }
        }

        public CueParseResult(string text, AnimationCue cue, CueParseStatus status)
        {
            Text = text ?? string.Empty;
            Cue = cue;
            Status = status;
        }
    }

    /// <summary>
    /// Reads the trailing cue tag out of a reply and strips it from the visible text.
    /// The format is one self-closing tag at the end of the reply:
    /// <c>&lt;animation emotion="happy" intensity="0.8" gesture="wave" /&gt;</c>.
    /// A missing tag means no cue. A tag that cannot be read still gets stripped and
    /// yields a neutral cue, so a bad tag can never reach the transcript.
    /// </summary>
    public static class AnimationCueParser
    {
        public const string TagName = "animation";
        public const float NeutralIntensity = 0.5f;
        public const float DefaultIntensity = 0.6f;
        public const int MaxGestureLength = 32;

        public const string Instruction =
            "End every reply with one cue tag on its own line: " +
            "<animation emotion=\"happy\" intensity=\"0.8\" gesture=\"wave\" />. " +
            "Use an id from the animation list as the gesture, or leave the gesture attribute out. " +
            "The tag is not part of the reply text, so never mention it.";

        static readonly Regex TagPattern = new Regex(
            @"<" + TagName + @"\b([^<>]*)>\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static readonly Regex AttributePattern = new Regex(
            @"([A-Za-z_][A-Za-z0-9_]*)\s*=\s*""([^""]*)""",
            RegexOptions.CultureInvariant);

        public static AnimationCue NeutralCue
        {
            get { return new AnimationCue(Emotion.Neutral, NeutralIntensity, string.Empty); }
        }

        public static CueParseResult Parse(string reply)
        {
            var text = reply ?? string.Empty;
            var index = FindTagStart(text);
            if (index < 0)
            {
                return new CueParseResult(text, NeutralCue, CueParseStatus.None);
            }

            var visible = text.Substring(0, index).TrimEnd();
            var tail = text.Substring(index);
            var match = TagPattern.Match(tail);
            if (!match.Success)
            {
                return new CueParseResult(visible, NeutralCue, CueParseStatus.Invalid);
            }

            var body = match.Groups[1].Value.Trim();
            if (!body.EndsWith("/", StringComparison.Ordinal))
            {
                // "<animation ...>" without the self-closing slash is not the format.
                return new CueParseResult(visible, NeutralCue, CueParseStatus.Invalid);
            }

            body = body.Substring(0, body.Length - 1);

            var emotion = Emotion.Neutral;
            var intensity = DefaultIntensity;
            var gesture = string.Empty;
            var ok = true;

            foreach (Match attribute in AttributePattern.Matches(body))
            {
                var name = attribute.Groups[1].Value.ToLowerInvariant();
                var value = attribute.Groups[2].Value.Trim();

                switch (name)
                {
                    case "emotion":
                        if (!Enum.TryParse(value, true, out emotion))
                        {
                            ok = false;
                        }
                        break;
                    case "intensity":
                        float parsed;
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                        {
                            ok = false;
                        }
                        else
                        {
                            intensity = parsed;
                        }
                        break;
                    case "gesture":
                        gesture = SanitizeGesture(value);
                        break;
                }
            }

            if (!ok)
            {
                return new CueParseResult(visible, NeutralCue, CueParseStatus.Invalid);
            }

            var cue = new AnimationCue(emotion, intensity, gesture);
            return new CueParseResult(visible, cue, CueParseStatus.Parsed);
        }

        /// <summary>
        /// Lowercase letters, digits, and underscores only. Anything else is dropped,
        /// so a gesture value can never carry markup into a lookup.
        /// </summary>
        public static string SanitizeGesture(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length && builder.Length < MaxGestureLength; i++)
            {
                var c = char.ToLowerInvariant(value[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        static int FindTagStart(string text)
        {
            var index = text.LastIndexOf('<');
            while (index >= 0)
            {
                if (StartsTag(text, index))
                {
                    return index;
                }

                index = index > 0 ? text.LastIndexOf('<', index - 1) : -1;
            }

            return -1;
        }

        static bool StartsTag(string text, int index)
        {
            var remaining = text.Length - index;
            var marker = "<" + TagName;
            if (remaining < marker.Length)
            {
                return false;
            }

            return string.Compare(text, index, marker, 0, marker.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
