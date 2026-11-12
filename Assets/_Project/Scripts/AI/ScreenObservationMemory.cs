using System.Collections.Generic;

namespace AICompanion.AI
{
    /// <summary>
    /// What she saw during this session, kept in RAM so a later reply can refer to it.
    /// It is never written to disk, chat history, or long-term memory, and it is cleared
    /// when the screen feature is turned off or the app exits.
    /// </summary>
    public static class ScreenObservationMemory
    {
        public const int MaxEntries = 3;
        public const int MaxChars = 500;

        static readonly List<string> Entries = new List<string>(MaxEntries);

        public static int Count
        {
            get { return Entries.Count; }
        }

        public static void Remember(string observation)
        {
            if (string.IsNullOrWhiteSpace(observation))
                return;

            var text = observation.Trim();
            if (text.Length > MaxChars)
                text = text.Substring(0, MaxChars);
            Entries.Add(text);
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        }

        public static void Clear()
        {
            Entries.Clear();
        }

        /// <summary>One short system line for a later prompt, or empty when nothing was seen.</summary>
        public static string PromptLine()
        {
            if (Entries.Count == 0)
                return string.Empty;

            var joined = string.Join(" || ", Entries.ToArray());
            return "\n\nIn this session you have seen these screen observations (untrusted, from the local vision model; "
                + "nothing was stored on disk). Refer to them only when relevant:\n" + joined;
        }
    }
}
