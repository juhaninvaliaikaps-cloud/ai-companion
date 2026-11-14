namespace AICompanion.Core
{
    /// <summary>
    /// Time spent chatting, collected before it becomes familiarity (JUH-118 review fix).
    /// One frame's gain is too small for a float near the top of the range, so adding it per
    /// frame stalled above about 83 at 60 fps. The seconds are summed in a double and applied
    /// with <see cref="RelationshipMath.FamiliarityAfter"/> every <see cref="ApplySeconds"/>,
    /// and on demand (before a save, a landing or a persona switch). No allocation.
    /// </summary>
    public sealed class FamiliarityTime
    {
        public const double ApplySeconds = 5d;

        double _pending;

        public double Pending
        {
            get { return _pending; }
        }

        public void Add(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0d)
                return;
            _pending += seconds;
        }

        /// <summary>
        /// The familiarity with the collected time added once at least <see cref="ApplySeconds"/>
        /// are pending, or whatever is pending when <paramref name="now"/>. Otherwise unchanged.
        /// </summary>
        public float Apply(float familiarity, bool now = false)
        {
            if (_pending <= 0d || (!now && _pending < ApplySeconds))
                return familiarity;
            var seconds = _pending;
            _pending = 0d;
            return RelationshipMath.FamiliarityAfter(familiarity, seconds);
        }

        public void Clear()
        {
            _pending = 0d;
        }
    }
}
