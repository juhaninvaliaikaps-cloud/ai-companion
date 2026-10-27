namespace AICompanion.Overlay
{
    public enum TrayPresence
    {
        Absent = 0,
        Shown = 1,
        Removed = 2
    }

    /// <summary>
    /// Add once on launch, delete once on quit or process exit, and allow a new add
    /// after Explorer recreates the notification area. Shell_NotifyIcon calls follow this.
    /// </summary>
    public sealed class TrayIconLifecycle
    {
        public TrayPresence Presence { get; private set; }
        public int AddCount { get; private set; }
        public int RemoveCount { get; private set; }

        public bool TryMarkAdded()
        {
            if (Presence == TrayPresence.Shown)
                return false;
            Presence = TrayPresence.Shown;
            AddCount++;
            return true;
        }

        public bool TryMarkRemoved()
        {
            if (Presence != TrayPresence.Shown)
                return false;
            Presence = TrayPresence.Removed;
            RemoveCount++;
            return true;
        }

        public void OnTaskbarRecreated()
        {
            if (Presence == TrayPresence.Shown)
                Presence = TrayPresence.Absent;
        }
    }
}
