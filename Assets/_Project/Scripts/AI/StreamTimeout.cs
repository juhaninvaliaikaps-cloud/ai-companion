using System;

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
    }
}
