namespace AICompanion.Core
{
    /// <summary>
    /// What tray pause blocks. Speech uses this so a pause does not overwrite the mute pref.
    /// </summary>
    public static class CompanionPause
    {
        public static bool SpeechAllowed(bool userMuted, bool paused)
        {
            return !userMuted && !paused;
        }

        public static bool ListeningAllowed(bool paused)
        {
            return !paused;
        }

        public static bool ReactionsAllowed(bool paused)
        {
            return !paused;
        }
    }
}
