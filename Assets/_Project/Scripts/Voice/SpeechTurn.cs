using System.Collections.Generic;

namespace AICompanion.Voice
{
    /// <summary>
    /// One spoken reply. A new begin, a cancel, or a mute drops whatever was queued.
    /// </summary>
    public sealed class SpeechTurn
    {
        readonly SentenceBuffer _buffer = new SentenceBuffer();
        readonly Queue<string> _queue = new Queue<string>();
        readonly List<string> _scratch = new List<string>();
        int _generation;
        bool _accepting;

        public int Generation => _generation;

        public bool Accepting => _accepting;

        public int PendingCount => _queue.Count;

        public void Begin(bool speak)
        {
            _buffer.Clear();
            _queue.Clear();
            _generation++;
            _accepting = speak;
        }

        public void Append(string text)
        {
            if (!_accepting || string.IsNullOrEmpty(text))
                return;

            _scratch.Clear();
            _buffer.Append(text, _scratch);
            EnqueueCleaned();
        }

        public void Complete()
        {
            if (!_accepting)
                return;

            _scratch.Clear();
            _buffer.Flush(_scratch);
            EnqueueCleaned();
            _accepting = false;
        }

        public void Cancel()
        {
            _buffer.Clear();
            _queue.Clear();
            _accepting = false;
            _generation++;
        }

        /// <summary>
        /// Drops sentences that have not been sent yet. Audio already playing stays.
        /// </summary>
        public void Abandon()
        {
            _buffer.Clear();
            _queue.Clear();
            _accepting = false;
        }

        public bool TryTake(out int generation, out string sentence)
        {
            generation = _generation;
            if (_queue.Count == 0)
            {
                sentence = null;
                return false;
            }

            sentence = _queue.Dequeue();
            return true;
        }

        public bool IsCurrent(int generation)
        {
            return generation == _generation;
        }

        void EnqueueCleaned()
        {
            for (var i = 0; i < _scratch.Count; i++)
            {
                var clean = SpeechText.Clean(_scratch[i]);
                if (SpeechText.HasSpokenCharacter(clean))
                    _queue.Enqueue(clean);
            }
        }
    }
}
