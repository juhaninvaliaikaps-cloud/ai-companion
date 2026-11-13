namespace AICompanion.Core
{
    /// <summary>
    /// Name shown to the person running the app. Namespaces and the repo stay as they are.
    /// Company stays <see cref="Company"/> so the registry folder does not move twice.
    /// </summary>
    public static class AppName
    {
        public const string Display = "Happi";

        /// <summary>The Windows player file name, without ".exe".</summary>
        public const string ExeName = "Happi";

        /// <summary>The name before <see cref="Display"/>. Its data and registry entries are carried over.</summary>
        public const string PreviousDisplay = "Backseat Catgirl";

        /// <summary>The first name, before <see cref="PreviousDisplay"/>.</summary>
        public const string OldestDisplay = "AI Companion";

        /// <summary>Every earlier product name, newest first.</summary>
        public static readonly string[] PreviousDisplays = { PreviousDisplay, OldestDisplay };

        public const string Company = "AI Companion";
    }
}
