namespace AICompanion.Core
{
    /// <summary>
    /// Scene hook started from <see cref="AppBootstrap"/> on the same GameObject.
    /// Overlay and later systems implement this so Core does not reference them.
    /// </summary>
    public interface ICompanionStartup
    {
        void StartCompanion();
    }
}
