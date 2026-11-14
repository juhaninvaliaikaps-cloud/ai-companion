namespace AICompanion.Core
{
    /// <summary>
    /// Familiarity from real chat time (JUH-118 section 4). Applying
    /// <see cref="RelationshipMath.FamiliarityAfter"/> every frame loses the gain to float
    /// rounding: at 60 fps one frame adds (100 - f) * 2.3e-7, which is less than half a float
    /// step above about 83, so a persona at 90 never grew. The seconds are collected in a double
    /// instead and applied every <see cref="ApplySeconds"/>, and before a save or a landed reply.
    /// Because the gain is exponential, many short steps give the same result as one long one.
    /// </summary>
    public sealed class FamiliarityClock
    {
        public const double ApplySeconds = 5d;

        double _pending;

        /// <summary>Chat seconds not applied yet.</summary>
        public double Pending
        {
            get { return _pending; }
        }

        /// <summary>Adds chat time. True when enough has gathered to apply it.</summary>
        public bool Add(double seconds)
        {
            if (!double.IsNaN(seconds) && seconds > 0d)
                _pending += seconds;
            return _pending >= ApplySeconds;
        }

        /// <summary>The familiarity after the pending time; the pending time is then used up.</summary>
        public float Apply(float familiarity)
        {
            if (_pending <= 0d)
                return familiarity;
            var after = RelationshipMath.FamiliarityAfter(familiarity, _pending);
            _pending = 0d;
            return after;
        }

        /// <summary>Drops the pending time (the numbers were set some other way).</summary>
        public void Clear()
        {
            _pending = 0d;
        }
    }
}
