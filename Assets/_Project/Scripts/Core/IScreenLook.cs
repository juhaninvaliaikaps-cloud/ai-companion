namespace AICompanion.Core
{
    /// <summary>
    /// Manual one-frame look at a window the user selected.
    /// The chat panel and the screen settings tab both call this.
    /// </summary>
    public interface IScreenLook
    {
        bool IsBusy { get; }

        string StatusText { get; }

        void RequestLook();

        void Stop();
    }
}
