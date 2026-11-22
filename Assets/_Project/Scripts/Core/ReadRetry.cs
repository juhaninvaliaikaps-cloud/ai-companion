using System;
using System.Collections.Generic;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-176: when a Pisara reading may be asked again this launch. A reading was tried once per
    /// launch; when the local model was not running yet at start (Ollama's port refused), the
    /// innocence, 18+ secrets and voice readings all failed in the first seconds and never came
    /// back until the next launch, although chat worked a minute later. Now a failure that can
    /// pass by itself (the model did not answer, took too long, the call failed) is tried again
    /// after 30 s, 60 s, 2 min, then every 5 min, up to <see cref="MaxAttempts"/> tries. Any other
    /// failure, and a success, end it for this launch. Plain C#: the keepers pass the time.
    /// </summary>
    public sealed class ReadRetry
    {
        public static readonly float[] Delays = { 30f, 60f, 120f, 300f };
        public const int MaxAttempts = 6;

        sealed class Entry
        {
            public int Attempts;
            public float Next;
            public bool Busy;
            public bool Done;
        }

        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>True when this reading may start now: never tried, or its wait is over.</summary>
        public bool Ready(string key, float now)
        {
            Entry entry;
            if (key == null || !_entries.TryGetValue(key, out entry))
                return true;
            if (entry.Busy || entry.Done)
                return false;
            return now >= entry.Next;
        }

        /// <summary>True while some reading waits for its next try (so a keeper keeps checking).</summary>
        public bool AnyWaiting
        {
            get
            {
                foreach (var entry in _entries.Values)
                {
                    if (!entry.Busy && !entry.Done)
                        return true;
                }

                return false;
            }
        }

        public void Started(string key)
        {
            if (key == null)
                return;
            Entry entry;
            if (!_entries.TryGetValue(key, out entry))
            {
                entry = new Entry();
                _entries[key] = entry;
            }

            entry.Busy = true;
            entry.Attempts++;
        }

        /// <summary>After a failure: a passing one waits for the next try, any other ends it for this launch.</summary>
        public void Failed(string key, float now, bool passing)
        {
            Entry entry;
            if (key == null || !_entries.TryGetValue(key, out entry))
                return;
            entry.Busy = false;
            if (!passing || entry.Attempts >= MaxAttempts)
            {
                entry.Done = true;
                return;
            }

            entry.Next = now + Delays[Math.Min(entry.Attempts - 1, Delays.Length - 1)];
        }

        public void Succeeded(string key)
        {
            Entry entry;
            if (key == null || !_entries.TryGetValue(key, out entry))
                return;
            entry.Busy = false;
            entry.Done = true;
        }

        /// <summary>Forgets every reading (a wipe, or "Read again").</summary>
        public void Clear()
        {
            _entries.Clear();
        }

        public int AttemptsFor(string key)
        {
            Entry entry;
            return key != null && _entries.TryGetValue(key, out entry) ? entry.Attempts : 0;
        }

        /// <summary>
        /// True for a failure that can pass by itself: the model did not answer (not running yet,
        /// unreachable), took too long, or the call failed. Not for a refusal (gate closed, cloud
        /// refused), no model set up, or a reply that had nothing usable.
        /// </summary>
        public static bool IsPassing(string failure)
        {
            if (string.IsNullOrEmpty(failure))
                return false;
            return failure.IndexOf("did not answer", StringComparison.OrdinalIgnoreCase) >= 0
                || failure.IndexOf("took too long", StringComparison.OrdinalIgnoreCase) >= 0
                || failure.IndexOf("call failed", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
