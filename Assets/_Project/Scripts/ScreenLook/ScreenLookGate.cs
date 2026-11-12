namespace AICompanion.ScreenLook
{
    /// <summary>
    /// One look at a time. Cancel bumps the generation so a late frame is stale
    /// and is not posted.
    /// </summary>
    public sealed class ScreenLookGate
    {
        public bool Busy { get; private set; }

        public int Generation { get; private set; }

        public bool TryEnter(out int generation)
        {
            generation = 0;
            if (Busy)
                return false;

            Busy = true;
            Generation++;
            generation = Generation;
            return true;
        }

        public bool IsCurrent(int generation)
        {
            return Busy && generation == Generation;
        }

        public void Leave(int generation)
        {
            if (generation == Generation)
                Busy = false;
        }

        public void Cancel()
        {
            Generation++;
            Busy = false;
        }
    }
}
