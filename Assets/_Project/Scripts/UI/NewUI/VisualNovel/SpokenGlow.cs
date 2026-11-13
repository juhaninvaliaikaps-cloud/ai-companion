using System;

namespace AICompanion.NewUI.VisualNovel
{
    /// <summary>
    /// JUH-108 spoken-text glow timing. The current line is split into words; each word gets a
    /// weight (its letters, plus a short pause after punctuation). While a clip plays, the
    /// clip's real playback fraction is spread over the words that clip says, by weight, so the
    /// glow sweeps word by word at her actual speed. A word glows from when she starts saying it
    /// until it is said, then fades back over <see cref="FadeSeconds"/>. When the voice reports no
    /// clip timing, a fixed pace is used instead. Pure logic: arrays are allocated once per line
    /// (<see cref="BeginLine"/>) and <see cref="Update"/> / <see cref="Glow"/> allocate nothing.
    /// </summary>
    public sealed class SpokenGlowTracker
    {
        int[] _start = new int[0];
        int[] _end = new int[0];
        float[] _word = new float[0];
        float[] _pause = new float[0];
        float[] _litAt = new float[0];
        float[] _endedAt = new float[0];
        int _count;
        int _serial;
        bool _hasSerial;
        int _clipFirst;
        int _clipCount;
        float _clipWeight;
        bool _estimating;
        float _estimateStart;
        int _estimateFirst;

        public float FadeSeconds = 0.3f;
        public float CommaPause = 2f;
        public float SentencePause = 4f;
        public float SecondsPerLetter = 0.07f;

        public int WordCount => _count;

        /// <summary>First character of word <paramref name="index"/> in the line.</summary>
        public int WordStart(int index) => _start[index];

        /// <summary>One past the last character of word <paramref name="index"/>.</summary>
        public int WordEnd(int index) => _end[index];

        public float WordWeight(int index) => _word[index];

        public float PauseWeight(int index) => _pause[index];

        /// <summary>A new line starts: split it into words. Allocates (once per line).</summary>
        public void BeginLine(string text)
        {
            text = text ?? string.Empty;
            var count = CountWords(text);
            if (_start.Length < count)
            {
                _start = new int[count];
                _end = new int[count];
                _word = new float[count];
                _pause = new float[count];
                _litAt = new float[count];
                _endedAt = new float[count];
            }

            _count = 0;
            var i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                    i++;
                if (i >= text.Length)
                    break;
                var start = i;
                var letters = 0;
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    if (char.IsLetterOrDigit(text[i]))
                        letters++;
                    i++;
                }

                _start[_count] = start;
                _end[_count] = i;
                _word[_count] = Math.Max(1, letters);
                _pause[_count] = PauseAfter(text[i - 1]);
                _litAt[_count] = -1f;
                _endedAt[_count] = -1f;
                _count++;
            }

            _hasSerial = false;
            _clipFirst = 0;
            _clipCount = 0;
            _clipWeight = 0f;
            _estimating = false;
        }

        /// <summary>No line: nothing glows.</summary>
        public void Clear()
        {
            _count = 0;
            _hasSerial = false;
            _estimating = false;
        }

        float PauseAfter(char last)
        {
            switch (last)
            {
                case '.':
                case '!':
                case '?':
                case '…':
                    return SentencePause;
                case ',':
                case ';':
                case ':':
                case '-':
                case '—':
                case '–':
                    return CommaPause;
                default:
                    return 0f;
            }
        }

        /// <param name="now">Seconds (unscaled time).</param>
        /// <param name="speaking">Her voice is synthesizing or playing this line.</param>
        /// <param name="hasClip">A clip of this line is playing and its timing is known.</param>
        public void Update(float now, bool speaking, bool hasClip, int clipSerial, string clipText, float clipTime, float clipLength)
        {
            if (_count == 0)
                return;

            if (!speaking)
            {
                EndLit(now, 0, _count);
                _estimating = false;
                return;
            }

            if (hasClip && clipLength > 0.05f)
            {
                if (!_hasSerial || clipSerial != _serial)
                {
                    if (_hasSerial)
                    {
                        EndRange(now, _clipFirst, _clipFirst + _clipCount);
                        _clipFirst = Math.Min(_count, _clipFirst + _clipCount);
                    }
                    else if (_estimating)
                    {
                        _clipFirst = FirstUnended();
                    }

                    _hasSerial = true;
                    _serial = clipSerial;
                    _estimating = false;
                    _clipCount = Math.Max(1, Math.Min(CountWords(clipText), _count - _clipFirst));
                    if (_clipFirst >= _count)
                        _clipCount = 0;
                    _clipWeight = Weight(_clipFirst, _clipFirst + _clipCount);
                }

                var fraction = clipTime / clipLength;
                if (fraction < 0f)
                    fraction = 0f;
                if (fraction > 1f)
                    fraction = 1f;
                Advance(now, _clipFirst, _clipFirst + _clipCount, fraction * _clipWeight);
                return;
            }

            // Speaking but no clip timing (still synthesizing, or a voice without progress): a fixed pace.
            if (_hasSerial)
                return;
            if (!_estimating)
            {
                _estimating = true;
                _estimateStart = now;
                _estimateFirst = FirstUnended();
            }

            var position = SecondsPerLetter > 0f ? (now - _estimateStart) / SecondsPerLetter : float.MaxValue;
            Advance(now, _estimateFirst, _count, position);
        }

        /// <summary>0..1: 1 while the word is being said, then fading to 0 over <see cref="FadeSeconds"/>.</summary>
        public float Glow(int index, float now)
        {
            if (index < 0 || index >= _count)
                return 0f;
            var lit = _litAt[index];
            if (lit < 0f)
                return 0f;
            var ended = _endedAt[index];
            if (ended < 0f)
                return 1f;
            if (FadeSeconds <= 0f)
                return 0f;
            var t = (now - ended) / FadeSeconds;
            if (t >= 1f)
                return 0f;
            if (t <= 0f)
                return 1f;
            // Smooth fade-out.
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>True while any word still glows (so the view keeps updating until the fades finish).</summary>
        public bool AnyGlowing(float now)
        {
            for (var i = 0; i < _count; i++)
            {
                if (Glow(i, now) > 0f)
                    return true;
            }

            return false;
        }

        void Advance(float now, int first, int last, float position)
        {
            var cursor = 0f;
            for (var i = first; i < last; i++)
            {
                var wordEnd = cursor + _word[i];
                if (position < cursor)
                    break;
                if (_litAt[i] < 0f)
                    _litAt[i] = now;
                if (position >= wordEnd && _endedAt[i] < 0f)
                    _endedAt[i] = now;
                cursor = wordEnd + _pause[i];
            }
        }

        void EndRange(float now, int first, int last)
        {
            for (var i = first; i < last && i < _count; i++)
            {
                if (_litAt[i] < 0f)
                    _litAt[i] = now;
                if (_endedAt[i] < 0f)
                    _endedAt[i] = now;
            }
        }

        void EndLit(float now, int first, int last)
        {
            for (var i = first; i < last && i < _count; i++)
            {
                if (_litAt[i] >= 0f && _endedAt[i] < 0f)
                    _endedAt[i] = now;
            }
        }

        int FirstUnended()
        {
            for (var i = 0; i < _count; i++)
            {
                if (_endedAt[i] < 0f)
                    return i;
            }

            return _count;
        }

        float Weight(int first, int last)
        {
            var sum = 0f;
            for (var i = first; i < last; i++)
                sum += _word[i] + (i < last - 1 ? _pause[i] : 0f);
            return Math.Max(0.0001f, sum);
        }

        /// <summary>Whitespace-separated words, without allocating.</summary>
        public static int CountWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            var count = 0;
            var inWord = false;
            for (var i = 0; i < text.Length; i++)
            {
                var space = char.IsWhiteSpace(text[i]);
                if (!space && !inWord)
                    count++;
                inWord = !space;
            }

            return count;
        }

        /// <summary>The theme's glow strength for a 0..1 intensity setting.</summary>
        public static float Strength(float intensity, float minStrength)
        {
            if (float.IsNaN(intensity))
                intensity = 0f;
            if (intensity < 0f)
                intensity = 0f;
            if (intensity > 1f)
                intensity = 1f;
            return minStrength + (1f - minStrength) * intensity;
        }
    }
}
