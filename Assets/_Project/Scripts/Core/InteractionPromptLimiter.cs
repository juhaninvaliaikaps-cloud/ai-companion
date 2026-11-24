using System;
using System.Collections.Generic;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-188: touching several places in a row must not queue a model prompt for every
    /// window. The visuals still play; only the first two prompts in the window are sent.
    /// Plain C# so the cap is testable without a scene.
    /// </summary>
    public sealed class InteractionPromptLimiter
    {
        public const int MaxPrompts = 2;
        public const double WindowSeconds = 60d;

        readonly Queue<DateTime> _times = new Queue<DateTime>();

        public int Count
        {
            get
            {
                Prune(DateTime.UtcNow);
                return _times.Count;
            }
        }

        public bool TryTake(DateTime nowUtc)
        {
            Prune(nowUtc);
            if (_times.Count >= MaxPrompts)
                return false;
            _times.Enqueue(nowUtc);
            return true;
        }

        public void Reset()
        {
            _times.Clear();
        }

        void Prune(DateTime nowUtc)
        {
            while (_times.Count > 0 && (nowUtc - _times.Peek()).TotalSeconds >= WindowSeconds)
                _times.Dequeue();
        }
    }
}
