using System;
using System.Diagnostics;
using System.Threading;

namespace AICompanion.AI
{
    /// <summary>
    /// JUH-183: how long a chat request may wait. The timeout counts the time since the last
    /// data arrived, not since the request started, so a slow reply that keeps arriving (a 12B
    /// model sharing the GPU with Chatterbox) finishes, and a server that stops answering still
    /// times out after the same time. Before the first data the wait is the timeout too, as
    /// before; a reply that comes in one piece behaves as before. A hard cap is the backstop.
    /// Pure, for the transport and tests.
    /// </summary>
    public static class StreamTimeout
    {
        /// <summary>The longest a request may run, however steadily it streams: 15 minutes.</summary>
        public const float HardCapSeconds = 900f;

        /// <summary>The whole-request limit given to the web request: the hard cap, or the timeout when that is longer.</summary>
        public static int HardCap(float timeoutSeconds)
        {
            var timeout = Sanitize(timeoutSeconds);
            return (int)Math.Ceiling(Math.Max(timeout, HardCapSeconds));
        }

        /// <summary>True when nothing has arrived for <paramref name="timeoutSeconds"/> or longer.</summary>
        public static bool Idle(double now, double lastData, float timeoutSeconds)
        {
            return now - lastData >= Sanitize(timeoutSeconds);
        }

        static float Sanitize(float timeoutSeconds)
        {
            if (float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0f)
                return ChatDefaults.TimeoutSeconds;
            return timeoutSeconds;
        }

        /// <summary>
        /// When data last arrived, as stopwatch ticks. <see cref="Mark"/> is safe off the main
        /// thread and calls no Unity API. <see cref="Idle(float)"/> reads the same stamp with
        /// an interlocked read, so the main thread and the download callback do not tear it.
        /// The timeout still counts from the last data received.
        /// </summary>
        public sealed class Clock
        {
            long _lastTicks;

            public Clock()
            {
                Mark();
            }

            public void Mark()
            {
                Interlocked.Exchange(ref _lastTicks, Stopwatch.GetTimestamp());
            }

            /// <summary>Tests: place the last-data stamp at an exact tick.</summary>
            public void MarkTicks(long ticks)
            {
                Interlocked.Exchange(ref _lastTicks, ticks);
            }

            public bool Idle(float timeoutSeconds)
            {
                return IdleAt(Stopwatch.GetTimestamp(), Stopwatch.Frequency, timeoutSeconds);
            }

            /// <summary>The idle check at <paramref name="nowTicks"/>, using this clock's last mark.</summary>
            public bool IdleAt(long nowTicks, long ticksPerSecond, float timeoutSeconds)
            {
                var last = Interlocked.Read(ref _lastTicks);
                return ElapsedIdle(nowTicks, last, ticksPerSecond, timeoutSeconds);
            }

            /// <summary>
            /// True when <paramref name="nowTicks"/> is at least the timeout after
            /// <paramref name="lastTicks"/>. Ticks share one clock; <paramref name="ticksPerSecond"/>
            /// is that clock's frequency.
            /// </summary>
            public static bool ElapsedIdle(long nowTicks, long lastTicks, long ticksPerSecond, float timeoutSeconds)
            {
                if (ticksPerSecond <= 0L)
                    return false;
                var elapsed = (nowTicks - lastTicks) / (double)ticksPerSecond;
                return StreamTimeout.Idle(elapsed, 0d, timeoutSeconds);
            }
        }
    }
}
