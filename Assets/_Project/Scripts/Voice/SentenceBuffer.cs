using System.Collections.Generic;
using System.Text;

namespace AICompanion.Voice
{
    /// <summary>
    /// Splits a streaming reply into sentences so the first one can be spoken
    /// before the rest of the text arrives. Periods inside numbers stay put.
    /// </summary>
    public sealed class SentenceBuffer
    {
        public const int ForceLength = 180;

        readonly StringBuilder _pending = new StringBuilder();

        public void Append(string chunk, IList<string> emitted)
        {
            if (string.IsNullOrEmpty(chunk))
                return;
            _pending.Append(chunk);
            Drain(emitted, false);
        }

        public void Flush(IList<string> emitted)
        {
            Drain(emitted, true);
        }

        public void Clear()
        {
            _pending.Length = 0;
        }

        void Drain(IList<string> emitted, bool flush)
        {
            while (_pending.Length > 0)
            {
                var cut = FindCut(flush);
                if (cut <= 0)
                    return;

                if (cut > _pending.Length)
                    cut = _pending.Length;
                var sentence = _pending.ToString(0, cut).Trim();
                _pending.Remove(0, cut);
                if (sentence.Length > 0 && emitted != null)
                    emitted.Add(sentence);
            }
        }

        int FindCut(bool flush)
        {
            var n = _pending.Length;
            for (var i = 0; i < n; i++)
            {
                var c = _pending[i];
                if (c == '\n' || c == '\r')
                    return i + 1;
                if (c == '!' || c == '?')
                    return i + 1;
                if (c != '.')
                    continue;

                var prev = i > 0 ? _pending[i - 1] : '\0';
                var atEnd = i == n - 1;
                if (!atEnd && char.IsDigit(_pending[i + 1]))
                    continue;
                if (atEnd)
                {
                    if (char.IsLetter(prev))
                        return i + 1;
                    continue;
                }

                if (IsFollower(_pending[i + 1]))
                    return i + 1;
            }

            if (!flush && n >= ForceLength)
                return ForceCut();
            if (flush && n > 0)
                return n;
            return -1;
        }

        int ForceCut()
        {
            var limit = _pending.Length < ForceLength ? _pending.Length : ForceLength;
            var space = -1;
            for (var i = 0; i < limit; i++)
            {
                if (char.IsWhiteSpace(_pending[i]))
                    space = i;
            }

            if (space >= 40)
                return space + 1;
            return limit;
        }

        static bool IsFollower(char c)
        {
            return char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == ')' || c == ']';
        }
    }
}
