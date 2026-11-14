namespace AICompanion.Memory
{
    /// <summary>
    /// Whether a session must stay on the local model (JUH-118 review fix). Decided from the
    /// whole session, not only from the moment it ends: once any message of it was sent while
    /// Private mode or the intimate gate kept chat local, its session memories are asked of the
    /// local model only and stored as private, even after Private mode is turned off.
    /// </summary>
    public sealed class SessionPrivacy
    {
        bool _keptLocal;

        /// <summary>True when a message of this session was sent while chat stayed local.</summary>
        public bool KeptLocal
        {
            get { return _keptLocal; }
        }

        /// <summary>Called for every message sent in the session, with the rule at that moment.</summary>
        public void Sent(bool keepsLocalNow)
        {
            if (keepsLocalNow)
                _keptLocal = true;
        }

        /// <summary>The rule for the session-end request: local if the session ever was, or is now.</summary>
        public bool AtEnd(bool keepsLocalNow)
        {
            return _keptLocal || keepsLocalNow;
        }

        public void Reset()
        {
            _keptLocal = false;
        }
    }
}
