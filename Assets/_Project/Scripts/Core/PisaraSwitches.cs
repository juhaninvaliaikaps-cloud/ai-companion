namespace AICompanion.Core
{
    /// <summary>
    /// One switch per Pisara leaf (JUH-117), so each can be turned off on its own while tuning.
    /// All default to on. Settings write them (<c>SettingsApplicator</c>); the code that wires
    /// a leaf into the app checks its switch before calling it. Turning one off freezes that
    /// leaf: nothing already stored is deleted.
    /// </summary>
    public static class PisaraSwitches
    {
        /// <summary>Off: replies no longer change affection or familiarity, and the relationship line leaves the prompt.</summary>
        public static bool Relationship { get; private set; } = true;

        /// <summary>Off: open threads no longer turn out while the user is away, and no new ones start.</summary>
        public static bool AwayEvents { get; private set; } = true;

        /// <summary>Off: her world is not seeded into memory and the first scene is not used. Memories already there stay.</summary>
        public static bool World { get; private set; } = true;

        public static void Configure(bool relationship, bool awayEvents, bool world)
        {
            Relationship = relationship;
            AwayEvents = awayEvents;
            World = world;
        }

        public static void Reset()
        {
            Configure(true, true, true);
        }
    }
}
