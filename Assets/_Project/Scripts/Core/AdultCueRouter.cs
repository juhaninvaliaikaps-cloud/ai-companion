using System;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-188: one small bridge from the animation cue parser to the adult window, without
    /// making the animation assembly depend on Settings. The adult window installs the handler
    /// at scene load; normal reactions are ignored by it.
    /// </summary>
    public static class AdultCueRouter
    {
        public static Action<string> Handler;

        public static void Request(string reaction)
        {
            if (string.IsNullOrWhiteSpace(reaction) || Handler == null)
                return;
            Handler(reaction);
        }
    }
}
