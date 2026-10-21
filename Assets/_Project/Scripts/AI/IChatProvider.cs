namespace AICompanion.AI
{
    /// <summary>
    /// Chat backend. Cloud providers (xAI Grok first) and local models both implement this.
    /// The skeleton performs no network I/O.
    /// </summary>
    public interface IChatProvider
    {
        string ProviderId { get; }

        bool IsConfigured { get; }
    }
}
