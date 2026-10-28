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
    /// contains a cue tag, even when the tag is malformed.
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
    /// Reads cue tags out of a reply and strips them from the visible text.
    /// The format is one self-closing tag, normally on its own line at the end:
    /// <c>&lt;animation emotion="happy" intensity="0.8" gesture="wave_hello" /&gt;</c>.
    /// A missing tag means no cue. A tag that cannot be read is still stripped.
    /// Text after a closed tag is kept. An unclosed <c>&lt;animation</c> eats only
    /// the rest of the reply, so a half-written tag cannot leak markup.
    /// </summary>
    public static class AnimationCueParser
    {
        public const string TagName = "animation";
        public const float NeutralIntensity = 0.5f;
        public const float DefaultIntensity = 0.6f;
        public const int MaxGestureLength = 32;

        public const string Instruction =
            "End every reply with one cue tag on its own line: " +
            "<animation emotion=\"happy\" intensity=\"0.8\" gesture=\"wave_hello\" />. " +
            "Set gesture to a catalog id (wave_hello) or that row's gesture name (wave). " +
            "Leave the gesture attribute out for an emotion only. " +
            "The tag is not part of the reply text, so never mention it.";

        static readonly string Marker = "<" + TagName;

        static readonly Regex AttributePattern = new Regex(
            @"([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:""([^""]*)""|'([^']*)')",
            RegexOptions.CultureInvariant);

        public static AnimationCue NeutralCue
        {
            get { return new AnimationCue(Emotion.Neutral, NeutralIntensity, string.Empty); }
        }

        public static CueParseResult Parse(string reply)
        {
            return Scan(reply ?? string.Empty, false);
        }

        /// <summary>
        /// The part of a still-streaming reply that is safe to show and speak.
        /// A finished cue tag is already removed. A suffix that could still grow
        /// into <c>&lt;animation</c> is held back, including an unclosed tag.
        /// </summary>
        public static string VisiblePrefix(string reply)
        {
            return Scan(reply ?? string.Empty, true).Text;
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

        /// <summary>
        /// True when <paramref name="value"/> survives <see cref="SanitizeGesture"/> unchanged
        /// apart from case, and fits in a cue attribute.
        /// </summary>
        public static bool IsCueToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (trimmed.Length > MaxGestureLength || trimmed.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                return false;
            }

            return string.Equals(SanitizeGesture(trimmed), trimmed, StringComparison.OrdinalIgnoreCase);
        }

        static CueParseResult Scan(string text, bool holdPartial)
        {
            var limit = holdPartial ? FindHold(text) : text.Length;
            var slice = limit < text.Length ? text.Substring(0, limit) : text;
            var builder = new StringBuilder(slice.Length);
            var cue = NeutralCue;
            var status = CueParseStatus.None;
            var removed = false;

            var i = 0;
            while (i < slice.Length)
            {
                if (slice[i] != '<')
                {
                    builder.Append(slice[i]);
                    i++;
                    continue;
                }

                int end;
                string body;
                bool closed;
                if (!TryReadTag(slice, i, out end, out body, out closed))
                {
                    builder.Append(slice[i]);
                    i++;
                    continue;
                }

                removed = true;
                if (closed)
                {
                    AnimationCue parsed;
                    if (TryReadCue(body, out parsed))
                    {
                        cue = parsed;
                        status = CueParseStatus.Parsed;
                    }
                    else
                    {
                        cue = NeutralCue;
                        status = CueParseStatus.Invalid;
                    }

                    i = SkipTag(slice, builder, i, end);
                    continue;
                }

                DropUnclosedLine(builder, slice, i);
                status = status == CueParseStatus.Parsed ? status : CueParseStatus.Invalid;
                if (status != CueParseStatus.Parsed)
                {
                    cue = NeutralCue;
                }

                break;
            }

            var visible = builder.ToString();
            if (removed)
            {
                visible = visible.TrimEnd();
            }

            return new CueParseResult(visible, cue, status);
        }

        /// <summary>
        /// Index of the earliest suffix that might still become a cue tag.
        /// Closed tags are not holds; the caller strips those.
        /// </summary>
        static int FindHold(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '<')
                {
                    continue;
                }

                if (IsHoldPoint(text, i))
                {
                    return i;
                }
            }

            return text.Length;
        }

        static bool IsHoldPoint(string text, int index)
        {
            var remaining = text.Length - index;
            var compare = remaining < Marker.Length ? remaining : Marker.Length;
            for (var i = 0; i < compare; i++)
            {
                if (char.ToLowerInvariant(text[index + i]) != Marker[i])
                {
                    return false;
                }
            }

            if (remaining < Marker.Length)
            {
                return true;
            }

            if (index + Marker.Length < text.Length && !IsBoundary(text[index + Marker.Length]))
            {
                return false;
            }

            var close = text.IndexOf('>', index + Marker.Length);
            var next = text.IndexOf('<', index + 1);
            if (close < 0 || (next >= 0 && next < close))
            {
                return true;
            }

            return false;
        }

        static bool TryReadTag(string text, int index, out int end, out string body, out bool closed)
        {
            end = index;
            body = string.Empty;
            closed = false;
            if (index + Marker.Length > text.Length)
            {
                return false;
            }

            if (string.Compare(text, index, Marker, 0, Marker.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            if (index + Marker.Length < text.Length && !IsBoundary(text[index + Marker.Length]))
            {
                return false;
            }

            var close = text.IndexOf('>', index + Marker.Length);
            var next = text.IndexOf('<', index + 1);
            if (close < 0 || (next >= 0 && next < close))
            {
                end = text.Length;
                closed = false;
                return true;
            }

            closed = true;
            end = close + 1;
            body = text.Substring(index + Marker.Length, close - (index + Marker.Length));
            return true;
        }

        static bool TryReadCue(string body, out AnimationCue cue)
        {
            cue = NeutralCue;
            var trimmed = body == null ? string.Empty : body.Trim();
            if (!trimmed.EndsWith("/", StringComparison.Ordinal))
            {
                return false;
            }

            trimmed = trimmed.Substring(0, trimmed.Length - 1);
            var emotion = Emotion.Neutral;
            var intensity = DefaultIntensity;
            var gesture = string.Empty;
            var ok = true;

            foreach (Match attribute in AttributePattern.Matches(trimmed))
            {
                var name = attribute.Groups[1].Value.ToLowerInvariant();
                var quoted = attribute.Groups[2].Success ? attribute.Groups[2] : attribute.Groups[3];
                var value = quoted.Value.Trim();

                switch (name)
                {
                    case "emotion":
                        if (!EmotionNames.TryParse(value, out emotion))
                        {
                            ok = false;
                        }

                        break;
                    case "intensity":
                        float parsed;
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                            float.IsNaN(parsed))
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
                return false;
            }

            cue = new AnimationCue(emotion, intensity, gesture);
            return true;
        }

        static int SkipTag(string text, StringBuilder builder, int start, int end)
        {
            var lineStart = start;
            while (lineStart > 0 && text[lineStart - 1] != '\n')
            {
                lineStart--;
            }

            var cursor = end;
            var onlyAfter = true;
            while (cursor < text.Length && text[cursor] != '\n')
            {
                if (!char.IsWhiteSpace(text[cursor]))
                {
                    onlyAfter = false;
                    break;
                }

                cursor++;
            }

            if (IsBlank(text, lineStart, start) && onlyAfter)
            {
                var delete = start - lineStart;
                if (delete > 0 && builder.Length >= delete)
                {
                    builder.Length -= delete;
                }

                if (cursor < text.Length && text[cursor] == '\n')
                {
                    return cursor + 1;
                }

                return text.Length;
            }

            var back = 0;
            while (back < builder.Length &&
                   (builder[builder.Length - 1 - back] == ' ' || builder[builder.Length - 1 - back] == '\t'))
            {
                back++;
            }

            var forward = end;
            while (forward < text.Length && (text[forward] == ' ' || text[forward] == '\t'))
            {
                forward++;
            }

            var leftContent = builder.Length - back > 0 && builder[builder.Length - back - 1] != '\n';
            var rightContent = forward < text.Length && text[forward] != '\n';
            builder.Length -= back;
            if (leftContent && rightContent)
            {
                builder.Append(' ');
            }

            return forward;
        }

        static void DropUnclosedLine(StringBuilder builder, string text, int tagStart)
        {
            var lineStart = tagStart;
            while (lineStart > 0 && text[lineStart - 1] != '\n')
            {
                lineStart--;
            }

            if (!IsBlank(text, lineStart, tagStart))
            {
                return;
            }

            var delete = tagStart - lineStart;
            if (delete > 0 && builder.Length >= delete)
            {
                builder.Length -= delete;
            }
        }

        static bool IsBlank(string text, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return false;
                }
            }

            return true;
        }

        static bool IsBoundary(char c)
        {
            return !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_');
        }
    }
}
