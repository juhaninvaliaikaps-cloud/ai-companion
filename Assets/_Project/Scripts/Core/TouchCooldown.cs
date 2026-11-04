using System.Collections.Generic;

namespace AICompanion.Core
{
    /// <summary>
    /// Per-zone cooldown so repeated clicks cannot spam the same reaction. Unscaled time, so a
    /// paused game does not freeze the gate.
    /// </summary>
    public sealed class TouchCooldown
    {
        readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();
        readonly float _defaultSeconds;

        public TouchCooldown(float defaultSeconds)
        {
            _defaultSeconds = defaultSeconds < 0f ? 0f : defaultSeconds;
        }

        /// <summary>The gate reads only the stored wait, so the zone that marked it owns it.</summary>
        public bool Ready(string id, float now)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            float readyAt;
            return !_readyAt.TryGetValue(id, out readyAt) || now >= readyAt;
        }

        public void Mark(string id, float now, float seconds)
        {
            if (string.IsNullOrEmpty(id))
                return;

            var wait = seconds <= 0f ? _defaultSeconds : seconds;
            _readyAt[id] = now + wait;
        }

        public void Clear()
        {
            _readyAt.Clear();
        }
    }
}
