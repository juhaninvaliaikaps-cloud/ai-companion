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

        /// <summary>
        /// A look the model asked for through the [look] cue. Returns false when the feature,
        /// the automatic-look consent, the cooldown or a running look blocks it.
        /// </summary>
        bool RequestAutoLook();

        /// <summary>True when the user has allowed her to look during chat.</summary>
        bool AutoLookEnabled { get; }

        void Stop();
    }
}
