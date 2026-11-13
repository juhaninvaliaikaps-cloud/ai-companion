using System;
using System.Collections.Generic;
using System.Text;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.NewUI.VisualNovel
{
    public enum VnSegmentKind
    {
        /// <summary>Her actions and the scene, written between *asterisks*. Shown; voiced only with "Voice narration".</summary>
        Narration = 0,
        /// <summary>Her spoken words. Unmarked text counts as speech.</summary>
        Speech = 1,
    }

    /// <summary>One line of a reply in the visual-novel chat (JUH-108).</summary>
    public sealed class VnSegment
    {
        public VnSegment(VnSegmentKind kind, string text, IReadOnlyList<AnimationCue> cues)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            Cues = cues ?? Array.Empty<AnimationCue>();
        }

        public VnSegmentKind Kind { get; }
        public string Text { get; }

        /// <summary>Cue tags written right before or inside this line. They fire when this line starts.</summary>
        public IReadOnlyList<AnimationCue> Cues { get; }
    }

    /// <summary>The parsed reply: its lines, plus cue tags that had no line after them.</summary>
    public sealed class VnScript
    {
        public static readonly VnScript Empty = new VnScript(new List<VnSegment>(), new List<AnimationCue>());

        public VnScript(List<VnSegment> segments, List<AnimationCue> trailingCues)
        {
            Segments = segments;
            TrailingCues = trailingCues;
        }

        public IReadOnlyList<VnSegment> Segments { get; }

        /// <summary>Cues after the last line (or in a reply with no text). They fire when the reply ends.</summary>
        public IReadOnlyList<AnimationCue> TrailingCues { get; }
    }

    /// <summary>
    /// JUH-108 tolerant parser. Splits a raw reply (cue tags included) into ordered narration and
    /// speech lines:
    /// - A run of asterisks (<c>*</c> or <c>**</c>) toggles narration; unmarked text is speech.
    /// - A line break ends the current line, and also ends narration whose closing asterisk is
    ///   missing, so a forgotten <c>*</c> cannot swallow the rest of the reply.
    /// - <c>&lt;animation … /&gt;</c> tags are removed from the text and attached to the line that
    ///   follows them, so each cue can fire at its own line.
    /// - While streaming (<c>final</c> false) only lines that are already closed are returned, and
    ///   a half-written tag is held back. Closed lines never change as more text arrives.
    /// </summary>
    public static class VnScriptParser
    {
        public const int MaxSegments = 64;
        const string TagStart = "<" + AnimationCueParser.TagName;

        public static VnScript Parse(string raw, bool final)
        {
            var segments = new List<VnSegment>();
            var pending = new List<AnimationCue>();
            if (string.IsNullOrEmpty(raw))
                return new VnScript(segments, pending);

            var text = raw.Replace("\r\n", "\n").Replace('\r', '\n');
            var buffer = new StringBuilder();
            var narration = false;
            var i = 0;
            var stopped = false;

            while (i < text.Length)
            {
                var c = text[i];
                if (c == '<' && StartsTag(text, i, out var complete))
                {
                    if (!complete)
                    {
                        // A tag (or what could still grow into one) that has not closed yet.
                        if (!final)
                        {
                            stopped = true;
                            break;
                        }

                        // Final reply: an unclosed tag eats the rest of its line, like the cue parser.
                        var lineEnd = text.IndexOf('\n', i);
                        i = lineEnd < 0 ? text.Length : lineEnd;
                        continue;
                    }

                    // A tag after some text starts a new line, so its cue fires where that part begins.
                    if (buffer.ToString().Trim().Length > 0)
                        Flush(buffer, narration, segments, pending);

                    var end = text.IndexOf('>', i);
                    var tag = text.Substring(i, end - i + 1);
                    var parsed = AnimationCueParser.Parse(tag);
                    if (parsed.Status == CueParseStatus.Parsed)
                        pending.Add(parsed.Cue);
                    i = end + 1;
                    continue;
                }

                if (c == '*')
                {
                    while (i < text.Length && text[i] == '*')
                        i++;
                    Flush(buffer, narration, segments, pending);
                    narration = !narration;
                    continue;
                }

                if (c == '\n')
                {
                    Flush(buffer, narration, segments, pending);
                    narration = false;
                    i++;
                    continue;
                }

                buffer.Append(c);
                i++;
            }

            // Tags after the last line stay in pending: they fire when the reply ends.
            if (final && !stopped)
                Flush(buffer, narration, segments, pending);

            Cap(segments);
            return new VnScript(segments, final ? pending : new List<AnimationCue>());
        }

        /// <summary>The visible text of the whole reply, lines joined by spaces (for logs and tests).</summary>
        public static string VisibleText(VnScript script)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < script.Segments.Count; i++)
            {
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(script.Segments[i].Text);
            }

            return sb.ToString();
        }

        static void Flush(StringBuilder buffer, bool narration, List<VnSegment> segments, List<AnimationCue> pending)
        {
            var text = ScreenLookCue.Strip(buffer.ToString()).Trim();
            buffer.Length = 0;
            if (text.Length == 0)
                return;

            var cues = pending.Count > 0 ? new List<AnimationCue>(pending) : null;
            pending.Clear();
            segments.Add(new VnSegment(narration ? VnSegmentKind.Narration : VnSegmentKind.Speech, text, cues));
        }

        /// <summary>
        /// True at <c>&lt;animation</c> (any case). <paramref name="complete"/> is true when the tag
        /// has its closing <c>&gt;</c>. A text end that could still become the tag counts as an
        /// incomplete tag.
        /// </summary>
        static bool StartsTag(string text, int index, out bool complete)
        {
            complete = false;
            var left = text.Length - index;
            if (left < TagStart.Length)
            {
                if (string.Compare(text, index, TagStart, 0, left, StringComparison.OrdinalIgnoreCase) != 0)
                    return false;
                return true;
            }

            if (string.Compare(text, index, TagStart, 0, TagStart.Length, StringComparison.OrdinalIgnoreCase) != 0)
                return false;

            var after = index + TagStart.Length;
            if (after < text.Length)
            {
                var next = text[after];
                if (!char.IsWhiteSpace(next) && next != '/' && next != '>')
                    return false;
            }

            var close = text.IndexOf('>', index);
            var newline = text.IndexOf('\n', index);
            complete = close >= 0 && (newline < 0 || close < newline) && close - index <= AnimationCueParser.MaxRawTagLength + 40;
            return true;
        }

        static void Cap(List<VnSegment> segments)
        {
            if (segments.Count <= MaxSegments)
                return;

            // Very long replies: the overflow joins the last allowed line so nothing is lost.
            var keep = segments.GetRange(0, MaxSegments - 1);
            var rest = new StringBuilder();
            var cues = new List<AnimationCue>();
            for (var i = MaxSegments - 1; i < segments.Count; i++)
            {
                if (rest.Length > 0)
                    rest.Append(' ');
                rest.Append(segments[i].Text);
                cues.AddRange(segments[i].Cues);
            }

            keep.Add(new VnSegment(segments[MaxSegments - 1].Kind, rest.ToString(), cues));
            segments.Clear();
            segments.AddRange(keep);
        }
    }
}
