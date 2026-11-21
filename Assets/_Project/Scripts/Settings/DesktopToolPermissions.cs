using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Settings
{
    [Serializable]
    public sealed class DesktopToolAuditEntry
    {
        public string atUtc = string.Empty;
        public string tool = string.Empty;
        public string action = string.Empty;
        public bool allowed;
        public string reason = string.Empty;
    }

    [Serializable]
    public sealed class DesktopToolAuditFile
    {
        public int version = 1;
        public List<DesktopToolAuditEntry> entries = new List<DesktopToolAuditEntry>();
    }

    /// <summary>
    /// Local, encrypted record of desktop tool attempts and decisions. It records the tool and
    /// a caller-supplied safe action label, never raw file paths, clipboard text or queries.
    /// </summary>
    public static class DesktopToolAuditLog
    {
        public const string FileName = "desktop-tool-actions.json";
        public const int MaxEntries = 500;

        /// <summary>Test seam; production leaves this empty.</summary>
        public static string PathOverride;

        public static string Path
        {
            get
            {
                if (!string.IsNullOrEmpty(PathOverride))
                    return PathOverride;
                var folder = AICompanion.Memory.MemoryPaths.Folder();
                return string.IsNullOrEmpty(folder) ? null : System.IO.Path.Combine(folder, FileName);
            }
        }

        public static void Record(DesktopToolKind kind, string action, bool allowed, string reason)
        {
            try
            {
                var file = Load();
                file.entries.Add(new DesktopToolAuditEntry
                {
                    atUtc = DateTime.UtcNow.ToString("o"),
                    tool = DesktopToolCatalog.Label(kind),
                    action = string.IsNullOrWhiteSpace(action) ? "(action)" : action.Trim(),
                    allowed = allowed,
                    reason = reason ?? string.Empty,
                });
                if (file.entries.Count > MaxEntries)
                    file.entries.RemoveRange(0, file.entries.Count - MaxEntries);
                var path = Path;
                if (!string.IsNullOrEmpty(path))
                    ProtectedLocalFile.Write(path, JsonUtility.ToJson(file));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Desktop tools: action log write failed (" + e.GetType().Name + ")");
            }
        }

        public static IReadOnlyList<DesktopToolAuditEntry> Recent(int max)
        {
            var file = Load();
            var result = new List<DesktopToolAuditEntry>();
            for (var i = file.entries.Count - 1; i >= 0 && result.Count < max; i--)
                result.Add(file.entries[i]);
            return result;
        }

        public static int Clear()
        {
            var file = Load();
            var count = file.entries.Count;
            if (count == 0)
                return 0;
            var path = Path;
            if (!string.IsNullOrEmpty(path))
                ProtectedLocalFile.Write(path, JsonUtility.ToJson(new DesktopToolAuditFile()));
            return count;
        }

        static DesktopToolAuditFile Load()
        {
            var path = Path;
            var read = ProtectedLocalFile.Read(path);
            if (read.Status != LocalReadStatus.Ready || string.IsNullOrWhiteSpace(read.Text))
                return new DesktopToolAuditFile();
            try
            {
                var file = JsonUtility.FromJson<DesktopToolAuditFile>(read.Text);
                if (file == null || file.entries == null)
                    return new DesktopToolAuditFile();
                return file;
            }
            catch (Exception)
            {
                return new DesktopToolAuditFile();
            }
        }
    }

    public static class DesktopToolPermissions
    {
        public const int Ask = 0;
        public const int Allowed = 1;
        public const int Denied = 2;

        /// <summary>Set by the consent view while the new UI is active. Null means no prompt can be shown.</summary>
        public static Action<DesktopToolKind, Action<bool>> ConsentHandler;

        public static int State(CompanionSettings settings, DesktopToolKind kind)
        {
            if (settings == null)
                return Ask;
            switch (kind)
            {
                case DesktopToolKind.Files: return settings.DesktopFilesPermission;
                case DesktopToolKind.Apps: return settings.DesktopAppsPermission;
                case DesktopToolKind.Clipboard: return settings.DesktopClipboardPermission;
                case DesktopToolKind.Calendar: return settings.DesktopCalendarPermission;
                case DesktopToolKind.Web: return settings.DesktopWebPermission;
                default: return Ask;
            }
        }

        public static void SetState(CompanionSettings settings, DesktopToolKind kind, int state)
        {
            if (settings == null)
                return;
            var clean = state < Ask || state > Denied ? Ask : state;
            switch (kind)
            {
                case DesktopToolKind.Files: settings.DesktopFilesPermission = clean; break;
                case DesktopToolKind.Apps: settings.DesktopAppsPermission = clean; break;
                case DesktopToolKind.Clipboard: settings.DesktopClipboardPermission = clean; break;
                case DesktopToolKind.Calendar: settings.DesktopCalendarPermission = clean; break;
                case DesktopToolKind.Web: settings.DesktopWebPermission = clean; break;
            }
        }

        public static string StateLabel(int state)
        {
            switch (state)
            {
                case Allowed: return "Allowed";
                case Denied: return "Blocked";
                default: return "Ask on first use";
            }
        }

        public static bool IsAllowed(CompanionSettings settings, DesktopToolKind kind)
        {
            return State(settings, kind) == Allowed;
        }

        /// <summary>
        /// The entry point every desktop tool must call before touching files, apps, clipboard,
        /// calendar or the web. Allowed runs immediately; denied is refused; ask shows the
        /// consent prompt and remembers the answer.
        /// </summary>
        public static void Request(CompanionSettings settings, DesktopToolKind kind, string action, Action<bool> callback)
        {
            var state = State(settings, kind);
            if (state == Allowed)
            {
                DesktopToolAuditLog.Record(kind, action, true, "already allowed");
                callback?.Invoke(true);
                return;
            }

            if (state == Denied)
            {
                DesktopToolAuditLog.Record(kind, action, false, "blocked by user");
                callback?.Invoke(false);
                return;
            }

            var handler = ConsentHandler;
            if (handler == null)
            {
                DesktopToolAuditLog.Record(kind, action, false, "no consent UI");
                callback?.Invoke(false);
                return;
            }

            var answered = false;
            handler(kind, allow =>
            {
                if (answered)
                    return;
                answered = true;
                SetState(settings, kind, allow ? Allowed : Denied);
                DesktopToolAuditLog.Record(kind, action, allow, allow ? "allowed in prompt" : "refused in prompt");
                callback?.Invoke(allow);
            });
        }

        public static void Request(SettingsPanel panel, DesktopToolKind kind, string action, Action<bool> callback)
        {
            var settings = panel != null ? panel.CurrentSettings : null;
            Request(settings, kind, action, allow =>
            {
                if (panel != null)
                    panel.Presenter.Change(s => SetState(s, kind, allow ? Allowed : Denied));
                callback?.Invoke(allow);
            });
        }
    }
}
