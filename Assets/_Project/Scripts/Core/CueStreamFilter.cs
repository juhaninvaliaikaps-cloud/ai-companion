using System;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// Hides cue tags while a reply is still streaming. A suffix that could still
    /// grow into <c>&lt;animation</c> is held back, and a tag that has already
    /// closed is removed even when more text follows it. Both the transcript and
    /// speech receive only the released text. <see cref="Flush"/> releases the
    /// rest and parses the cue.
    /// </summary>
    public sealed class CueStreamFilter
    {
        readonly StringBuilder _raw = new StringBuilder();
        string _shown = string.Empty;

        public AnimationCue Cue { get; private set; }

        public CueParseStatus Status { get; private set; }

        /// <summary>The visible text after the last <see cref="Flush"/>.</summary>
        public string Text { get; private set; }

        public CueStreamFilter()
        {
            Cue = AnimationCueParser.NeutralCue;
            Status = CueParseStatus.None;
            Text = string.Empty;
        }

        public string Push(string piece)
        {
            if (!string.IsNullOrEmpty(piece))
            {
                _raw.Append(piece);
            }

            return TakeNew(AnimationCueParser.VisiblePrefix(_raw.ToString()));
        }

        public string Flush()
        {
            var result = AnimationCueParser.Parse(_raw.ToString());
            Cue = result.Cue;
            Status = result.Status;
            Text = result.Text;
            return TakeNew(result.Text);
        }

        string TakeNew(string visible)
        {
            if (visible == null)
            {
                visible = string.Empty;
            }

            if (visible.StartsWith(_shown, StringComparison.Ordinal))
            {
                var delta = visible.Substring(_shown.Length);
                _shown = visible;
                return delta;
            }

            // The parsed text trimmed a prefix that was already released, usually the
            // space before a tag. Do not speak that prefix again.
            _shown = visible;
            return string.Empty;
        }
    }
}
