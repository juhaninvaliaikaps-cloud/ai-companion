namespace AICompanion.Core
{
    /// <summary>
    /// Manual one-frame look at a window the user selected.
    /// The chat panel and the screen settings tab both call this.
    /// </summary>
    public interface IScreenLook
    {
        bool IsBusy { get; }

        /// <summary>
        /// True when a new look is allowed. Cloud consent is not required for a local vision model.
        /// </summary>
        bool SendEnabled { get; }

        /// <summary>True while a look can be started, or while one is running so Stop still works.</summary>
        bool LookButtonInteractable { get; }

        string StatusText { get; }

        void RequestLook();

        void Stop();
    }
}
