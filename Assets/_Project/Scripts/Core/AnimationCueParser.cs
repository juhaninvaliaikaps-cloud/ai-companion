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

        /// <summary>
        /// The tag as the model wrote it: the one that gave the cue, else the last one seen.
        /// Cut to <see cref="AnimationCueParser.MaxRawTagLength"/> characters, on one line.
        /// Empty when the reply had no tag.
        /// </summary>
        public string RawTag { get; }

        public CueParseResult(string text, AnimationCue cue, CueParseStatus status)
            : this(text, cue, status, null)
        {
        }

        public CueParseResult(string text, AnimationCue cue, CueParseStatus status, string rawTag)
        {
            Text = text ?? string.Empty;
            Cue = cue;
            Status = status;
            RawTag = rawTag ?? string.Empty;
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

        public const int MaxRawTagLength = 160;

        public const string Instruction =
            "Start every reply with one cue tag on its own first line, then write the reply. " +
            "The tag moves her face and body. It is never shown or spoken, so never mention it. " +
            "Write it in every reply, also in roleplay and narration. " +
            "The sample <animation emotion=\"happy\" intensity=\"0.8\" gesture=\"wave_hello\" /> is the format only. " +
            "Pick the emotion that fits this reply. Happy and embarrassed are not defaults. " +
            "A sad reply starts with <animation emotion=\"sad\" intensity=\"0.55\" />. " +
            "A surprised reply starts with <animation emotion=\"surprised\" intensity=\"0.6\" />. " +
            "For strong sadness use emotion=\"crying\" (or sad at 0.9 or more). " +
            "For strong shock use emotion=\"shocked\" (or surprised at 0.9 or more). " +
            "Set gesture to a catalog id (wave_hello) or that row's gesture name (wave). " +
            "Leave the gesture attribute out for an emotion only. " +
            "When the user asks you to walk, move, come here, or go to one side, set gesture to walk " +
            "in that same reply, even if you only agree in the sentence. " +
            "Walk to the left is <animation emotion=\"neutral\" gesture=\"walk\" direction=\"left\" />. " +
            "Walk to the right uses direction=\"right\". Omit direction when they do not name a side. " +
            "When they ask you to stop, wait, or stand still, set gesture to stop.";

        /// <summary>
        /// Added to the newest user message in the request only, never to the transcript:
        /// local roleplay models follow the last turn more than the system prompt.
        /// </summary>
        public const string TurnReminder =
            "(Start your reply with your cue tag, for example <animation emotion=\"neutral\" />.)";

        /// <summary>One log line for the tag the model wrote, so Player.log shows what arrived.</summary>
        public static string DescribeTag(string rawTag, CueParseStatus status)
        {
            if (status == CueParseStatus.None || string.IsNullOrEmpty(rawTag))
                return "Cue tag: none found in the reply";
            if (status == CueParseStatus.Invalid)
                return "Cue tag (not readable, used neutral): " + rawTag;
            return "Cue tag: " + rawTag;
        }

        static readonly string Marker = "<" + TagName;

        // Quoted with " or ', or unquoted up to a space, a slash or the end of the tag.
        static readonly Regex AttributePattern = new Regex(
            @"([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'/>]+))",
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

        /// <summary>
        /// Collapses walk and stop aliases onto the tokens the director handles, and
        /// fills a missing side from a gesture such as walk_left. An explicit direction
        /// wins over the alias. Idempotent, so the director can run it again.
        /// </summary>
        public static void NormalizeMovementCue(ref string gesture, ref float direction)
        {
            gesture = gesture == null ? string.Empty : gesture.Trim().ToLowerInvariant();
            direction = AnimationCue.NormalizeDirection(direction);

            if (gesture == "walking")
            {
                gesture = "walk";
                return;
            }

            if (IsWalkLeft(gesture))
            {
                gesture = "walk";
                if (direction == 0f)
                    direction = -1f;
                return;
            }

            if (IsWalkRight(gesture))
            {
                gesture = "walk";
                if (direction == 0f)
                    direction = 1f;
                return;
            }

            if (gesture == "stand_still" || gesture == "standstill" || gesture == "stop_walking")
                gesture = "stop";
        }

        static bool IsWalkLeft(string gesture)
        {
            return gesture == "walk_left"
                || gesture == "walkleft"
                || gesture == "walking_left"
                || gesture == "walkingleft";
        }

        static bool IsWalkRight(string gesture)
        {
            return gesture == "walk_right"
                || gesture == "walkright"
                || gesture == "walking_right"
                || gesture == "walkingright";
        }

        static CueParseResult Scan(string text, bool holdPartial)
        {
            var limit = holdPartial ? FindHold(text) : text.Length;
            var slice = limit < text.Length ? text.Substring(0, limit) : text;
            var builder = new StringBuilder(slice.Length);
            var cue = NeutralCue;
            var status = CueParseStatus.None;
            var removed = false;
            var rawTag = string.Empty;

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
                if (!TryReadTag(slice, i, out end, out body, out closed) &&
                    !TryReadLooseTag(slice, i, out end, out body, out closed))
                {
                    builder.Append(slice[i]);
                    i++;
                    continue;
                }

                removed = true;
                if (closed)
                {
                    AnimationCue parsed;
                    var raw = RawTagText(slice, i, end);
                    if (TryReadCue(body, out parsed))
                    {
                        cue = parsed;
                        status = CueParseStatus.Parsed;
                        rawTag = raw;
                    }
                    else if (status != CueParseStatus.Parsed)
                    {
                        // A broken second tag does not undo a good first one.
                        cue = new AnimationCue(Emotion.Neutral, NeutralIntensity, string.Empty, 0f, parsed.EmotionField, parsed.Reaction, parsed.Landed);
                        status = CueParseStatus.Invalid;
                        rawTag = raw;
                    }

                    i = SkipTag(slice, builder, i, end);
                    continue;
                }

                DropUnclosedLine(builder, slice, i);
                if (status != CueParseStatus.Parsed)
                {
                    cue = NeutralCue;
                    rawTag = RawTagText(slice, i, slice.Length);
                }

                status = status == CueParseStatus.Parsed ? status : CueParseStatus.Invalid;

                break;
            }

            var visible = builder.ToString();
            if (removed)
            {
                visible = visible.TrimEnd();
            }

            return new CueParseResult(visible, cue, status, rawTag);
        }

        static string RawTagText(string text, int start, int end)
        {
            var length = Math.Max(0, Math.Min(end, text.Length) - start);
            var raw = text.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return raw.Length > MaxRawTagLength ? raw.Substring(0, MaxRawTagLength) + "..." : raw;
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

        static readonly string[] HoldMarkers = { "<animation", "<emotion=", "<gesture=", "<intensity=", "<direction=", "<reaction=" };

        static bool IsHoldPoint(string text, int index)
        {
            var remaining = text.Length - index;
            for (var m = 0; m < HoldMarkers.Length; m++)
            {
                var marker = HoldMarkers[m];
                var compare = remaining < marker.Length ? remaining : marker.Length;
                var matches = true;
                for (var i = 0; i < compare; i++)
                {
                    if (char.ToLowerInvariant(text[index + i]) != marker[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return remaining < marker.Length;
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

        /// <summary>
        /// Small models sometimes drop the tag name and write
        /// <c>&lt;emotion="happy" gesture="wave" /&gt;</c>. Accept any tag whose body opens
        /// with a cue attribute, so a sloppy model cannot swallow her reply.
        /// </summary>
        static bool TryReadLooseTag(string text, int index, out int end, out string body, out bool closed)
        {
            end = index;
            body = string.Empty;
            closed = false;

            var close = text.IndexOf('>', index + 1);
            var next = text.IndexOf('<', index + 1);
            if (close < 0 || (next >= 0 && next < close))
            {
                // Unclosed: only hold it back while it still looks like a cue tag.
                var tail = text.Substring(index + 1);
                end = text.Length;
                closed = false;
                return LooksLikeCueStart(tail);
            }

            var inner = text.Substring(index + 1, close - index - 1);
            if (!LooksLikeCueStart(inner))
                return false;

            end = close + 1;
            body = inner;
            closed = true;
            return true;
        }

        static readonly string[] LooseStarts = { "emotion=", "gesture=", "intensity=", "direction=", "reaction=", "landed=" };

        static bool LooksLikeCueStart(string text)
        {
            var trimmed = text.TrimStart();
            for (var i = 0; i < LooseStarts.Length; i++)
            {
                if (trimmed.StartsWith(LooseStarts[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static bool TryReadCue(string body, out AnimationCue cue)
        {
            cue = NeutralCue;
            var trimmed = body == null ? string.Empty : body.Trim();
            // The closing slash is optional: <animation emotion="happy"> is read too.
            if (trimmed.EndsWith("/", StringComparison.Ordinal))
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            var emotion = Emotion.Neutral;
            var intensity = DefaultIntensity;
            var gesture = string.Empty;
            var direction = 0f;
            var emotionField = string.Empty;
            var reaction = string.Empty;
            var landed = LandingEvent.Neutral;
            var ok = true;

            foreach (Match attribute in AttributePattern.Matches(trimmed))
            {
                var name = attribute.Groups[1].Value.ToLowerInvariant();
                var quoted = attribute.Groups[2].Success ? attribute.Groups[2]
                    : attribute.Groups[3].Success ? attribute.Groups[3] : attribute.Groups[4];
                var value = quoted.Value.Trim();

                switch (name)
                {
                    case "emotion":
                        emotionField = value;
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
                    case "direction":
                        direction = ParseDirection(value);
                        break;
                    case "reaction":
                        reaction = SanitizeGesture(value);
                        break;
                    case LandingEvents.Attribute:
                        // JUH-117: an unknown word is neutral and never makes the tag unreadable.
                        landed = LandingEvents.Parse(value);
                        break;
                }
            }

            if (!ok)
            {
                // A reaction named in a tag with a bad emotion still plays.
                cue = new AnimationCue(Emotion.Neutral, NeutralIntensity, string.Empty, 0f, emotionField, reaction, landed);
                return false;
            }

            NormalizeMovementCue(ref gesture, ref direction);
            cue = new AnimationCue(emotion, intensity, gesture, direction, emotionField, reaction, landed);
            return true;
        }

        /// <summary>left / right, or 0 when the attribute is missing or not a side.</summary>
        static float ParseDirection(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0f;

            var token = value.Trim().ToLowerInvariant();
            if (token == "left" || token == "l" || token == "-1")
                return -1f;
            if (token == "right" || token == "r" || token == "1" || token == "+1")
                return 1f;
            return 0f;
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
