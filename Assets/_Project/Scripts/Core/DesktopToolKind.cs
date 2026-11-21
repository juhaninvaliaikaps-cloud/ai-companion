using System;

namespace AICompanion.Core
{
    /// <summary>
    /// A desktop capability that must be granted before a tool may use it. These are
    /// intentionally separate permissions: allowing web search does not allow file access.
    /// </summary>
    public enum DesktopToolKind
    {
        Files = 0,
        Apps = 1,
        Clipboard = 2,
        Calendar = 3,
        Web = 4,
    }

    public static class DesktopToolCatalog
    {
        public static readonly DesktopToolKind[] All =
        {
            DesktopToolKind.Files,
            DesktopToolKind.Apps,
            DesktopToolKind.Clipboard,
            DesktopToolKind.Calendar,
            DesktopToolKind.Web,
        };

        public static string Label(DesktopToolKind kind)
        {
            switch (kind)
            {
                case DesktopToolKind.Files: return "Files";
                case DesktopToolKind.Apps: return "Apps";
                case DesktopToolKind.Clipboard: return "Clipboard";
                case DesktopToolKind.Calendar: return "Calendar";
                case DesktopToolKind.Web: return "Web search";
                default: return kind.ToString();
            }
        }

        public static string Description(DesktopToolKind kind)
        {
            switch (kind)
            {
                case DesktopToolKind.Files:
                    return "Find files by name in folders you approve.";
                case DesktopToolKind.Apps:
                    return "Launch installed applications by name.";
                case DesktopToolKind.Clipboard:
                    return "Read or reformat the current clipboard text.";
                case DesktopToolKind.Calendar:
                    return "Read today's events and add reminders or events.";
                case DesktopToolKind.Web:
                    return "Search the web and summarize the results in chat.";
                default:
                    return string.Empty;
            }
        }
    }
}
