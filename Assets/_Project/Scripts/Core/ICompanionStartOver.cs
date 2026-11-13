namespace AICompanion.Core
{
    /// <summary>
    /// "Start over with this persona" (JUH-117). A component that keeps something she feels
    /// or knows about the user, per persona (her mood, the relationship), implements this and
    /// resets it for the active persona. Settings call every implementation it finds. Her
    /// persona, her description and her own life (backstory, threads) are not touched.
    /// </summary>
    public interface ICompanionStartOver
    {
        void StartOver();
    }
}
