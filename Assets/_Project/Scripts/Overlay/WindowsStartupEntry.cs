using AICompanion.Core;

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

        /// <summary>
        /// True when the read worked. <paramref name="command"/> is null when the value is absent.
        /// </summary>
        bool TryReadCommand(string valueName, out string command);
    }

    public static class WindowsStartupEntry
    {
        public const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName = AppName.Display;
        public const string PreviousValueName = AppName.PreviousDisplay;

        /// <summary>Every earlier Run value name, newest first.</summary>
        public static string[] PreviousValueNames
        {
            get { return AppName.PreviousDisplays; }
        }

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
    /// Moves a Run value off the previous app name so an old entry does not keep launching.
    /// An existing value under the new name is left as it is.
    /// </summary>
    public static class StartupRename
    {
        public static bool Migrate(IStartupRegistration registration, string previousName, string currentName)
        {
            return Migrate(registration, previousName, currentName, null);
        }

        /// <summary>
        /// Moves every earlier Run value, newest first. When <paramref name="currentCommand"/> is
        /// set (the running exe), the new value points at it, so a renamed exe does not keep the
        /// old exe's path. True when all of them moved.
        /// </summary>
        public static bool MigrateAll(IStartupRegistration registration, string[] previousNames, string currentName, string currentCommand)
        {
            if (previousNames == null)
                return Migrate(registration, null, currentName, currentCommand);
            var all = true;
            for (var i = 0; i < previousNames.Length; i++)
                all &= Migrate(registration, previousNames[i], currentName, currentCommand);
            return all;
        }

        public static bool Migrate(IStartupRegistration registration, string previousName, string currentName, string currentCommand)
        {
            if (registration == null || string.IsNullOrEmpty(currentName))
                return false;
            if (string.IsNullOrEmpty(previousName) || previousName == currentName)
                return true;

            string previous;
            if (!registration.TryReadCommand(previousName, out previous))
                return false;
            if (string.IsNullOrWhiteSpace(previous))
                return true;

            string current;
            if (!registration.TryReadCommand(currentName, out current))
                return false;
            var command = string.IsNullOrWhiteSpace(currentCommand) ? previous : currentCommand;
            if (string.IsNullOrWhiteSpace(current) && !registration.TrySetEnabled(currentName, true, command))
                return false;

            return registration.TrySetEnabled(previousName, false, string.Empty);
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

        public bool TryReadCommand(string valueName, out string command)
        {
            command = null;
            return !string.IsNullOrEmpty(valueName);
        }
    }
}
