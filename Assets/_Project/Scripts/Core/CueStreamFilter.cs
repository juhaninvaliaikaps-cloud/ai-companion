using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// Hides a cue tag while a reply is still streaming. Text is released up to the
    /// last character that could still turn out to be the start of a tag, so a
    /// partially arrived <c>&lt;animation</c> never flashes in the transcript or
    /// reaches speech. <see cref="Flush"/> releases the rest and parses the cue.
    /// </summary>
    public sealed class CueStreamFilter
    {
        static readonly string Marker = "<" + AnimationCueParser.TagName;

        readonly StringBuilder _raw = new StringBuilder();
        int _emitted;

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

            var safe = SafeLength(_raw);
            if (safe <= _emitted)
            {
                return string.Empty;
            }

            var delta = _raw.ToString(_emitted, safe - _emitted);
            _emitted = safe;
            return delta;
        }

        public string Flush()
        {
            var result = AnimationCueParser.Parse(_raw.ToString());
            Cue = result.Cue;
            Status = result.Status;
            Text = result.Text;

            if (Text.Length <= _emitted)
            {
                _emitted = Text.Length;
                return string.Empty;
            }

            var delta = Text.Substring(_emitted);
            _emitted = Text.Length;
            return delta;
        }

        static int SafeLength(StringBuilder raw)
        {
            var length = raw.Length;
            var index = raw.Length - 1;
            while (index >= 0 && raw[index] != '<')
            {
                index--;
            }

            if (index < 0)
            {
                return length;
            }

            var remaining = length - index;
            if (remaining >= Marker.Length)
            {
                for (var i = 0; i < Marker.Length; i++)
                {
                    if (char.ToLowerInvariant(raw[index + i]) != char.ToLowerInvariant(Marker[i]))
                    {
                        return length;
                    }
                }

                return index;
            }

            for (var i = 0; i < remaining; i++)
            {
                if (char.ToLowerInvariant(raw[index + i]) != char.ToLowerInvariant(Marker[i]))
                {
                    return length;
                }
            }

            return index;
        }
    }
}
