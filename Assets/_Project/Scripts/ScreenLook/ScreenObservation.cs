using System.Collections.Generic;

namespace AICompanion.ScreenLook
{
    /// <summary>
    /// A one-frame description. Events stay empty: a single image is not a
    /// win, a death, or any other game fact.
    /// </summary>
    public sealed class ScreenObservation
    {
        public string Summary = string.Empty;
        public string WindowTitle = string.Empty;
        public int Generation;
        public readonly List<string> Events = new List<string>();

        public static ScreenObservation FromReply(string summary, string windowTitle, int generation)
        {
            return new ScreenObservation
            {
                Summary = summary ?? string.Empty,
                WindowTitle = windowTitle ?? string.Empty,
                Generation = generation
            };
        }
    }
}
