namespace AICompanion.Overlay
{
    /// <summary>
    /// Per-user "Start with Windows" entry. The player writes
    /// HKCU\Software\Microsoft\Windows\CurrentVersion\Run. Tests use a fake store.
    /// </summary>
    public interface IStartupRegistration
    {
        bool TryGetEnabled(string valueName, out bool enabled);

        bool TrySetEnabled(string valueName, bool enabled, string command);
    }

    public static class WindowsStartupEntry
    {
        public const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName = "AI Companion";

        public static string BuildCommand(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return string.Empty;

            var trimmed = executablePath.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            if (trimmed.Length == 0 || trimmed.IndexOf('"') >= 0)
                return string.Empty;
            return "\"" + trimmed + "\"";
        }
    }

    public static class StartupToggle
    {
        public static bool TryApply(IStartupRegistration registration, bool enabled, string executablePath)
        {
            if (registration == null)
                return false;

            var command = WindowsStartupEntry.BuildCommand(executablePath);
            if (enabled && command.Length == 0)
                return false;
            return registration.TrySetEnabled(WindowsStartupEntry.ValueName, enabled, command);
        }
    }

    /// <summary>
    /// Editor, Linux, and any player that cannot touch HKCU.
    /// </summary>
    public sealed class UnavailableStartupRegistration : IStartupRegistration
    {
        public bool TryGetEnabled(string valueName, out bool enabled)
        {
            enabled = false;
            return true;
        }

        public bool TrySetEnabled(string valueName, bool enabled, string command)
        {
            return false;
        }
    }
}
