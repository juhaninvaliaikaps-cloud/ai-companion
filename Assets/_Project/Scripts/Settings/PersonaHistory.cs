using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AICompanion.Core;

namespace AICompanion.Settings
{
    /// <summary>One saved earlier version of a persona file.</summary>
    public sealed class PersonaVersion
    {
        public string Path = string.Empty;
        public DateTime SavedAtUtc;

        /// <summary>"2026-11-21 18:04:12" in local time, for the History drop-down.</summary>
        public string Label
        {
            get { return SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }
        }
    }

    /// <summary>
    /// JUH-154: version history of a persona. Before <see cref="PersonaStore.Save"/> overwrites
    /// her file, the previous version is kept as <c>history/&lt;id&gt;.json.&lt;timestamp&gt;.bak</c>
    /// in the personas folder, encrypted like the file itself, and only the last
    /// <see cref="Keep"/> versions stay. Saving the same text again keeps nothing.
    /// </summary>
    public static class PersonaHistory
    {
        public const string FolderName = "history";
        public const int Keep = 10;
        const string StampFormat = "yyyyMMdd'T'HHmmssfff";

        public static string FolderFor(string personasFolder)
        {
            return string.IsNullOrEmpty(personasFolder) ? null : System.IO.Path.Combine(personasFolder, FolderName);
        }

        /// <summary>
        /// Keeps the current file of <paramref name="id"/> as a version, unless it is missing,
        /// unreadable or the same as <paramref name="newJson"/>. Then prunes to the last
        /// <see cref="Keep"/>. Returns the version's path, or null when nothing was kept.
        /// </summary>
        public static string KeepPrevious(string personasFolder, string id, string newJson, DateTime nowUtc)
        {
            var key = PersonaIds.Normalize(id);
            if (key.Length == 0 || string.IsNullOrEmpty(personasFolder))
                return null;
            var current = System.IO.Path.Combine(personasFolder, key + ".json");
            if (!File.Exists(current))
                return null;

            var read = ProtectedLocalFile.Read(current);
            if (read.Status != LocalReadStatus.Ready || string.IsNullOrWhiteSpace(read.Text))
                return null;
            if (string.Equals(Normalize(read.Text), Normalize(newJson), StringComparison.Ordinal))
                return null;

            var folder = FolderFor(personasFolder);
            try
            {
                Directory.CreateDirectory(folder);
                var stamp = nowUtc.ToUniversalTime().ToString(StampFormat, CultureInfo.InvariantCulture);
                var path = System.IO.Path.Combine(folder, key + ".json." + stamp + ".bak");
                for (var n = 1; File.Exists(path) && n < 100; n++)
                    path = System.IO.Path.Combine(folder, key + ".json." + stamp + "-" + n.ToString(CultureInfo.InvariantCulture) + ".bak");
                // The file's encryption is bound to its path, so the version is written again at
                // its own path rather than copied byte for byte.
                if (!ProtectedLocalFile.Write(path, read.Text))
                    return null;
                Prune(personasFolder, key, Keep);
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The kept versions of a persona, newest first.</summary>
        public static List<PersonaVersion> List(string personasFolder, string id)
        {
            var versions = new List<PersonaVersion>();
            var key = PersonaIds.Normalize(id);
            var folder = FolderFor(personasFolder);
            if (key.Length == 0 || folder == null || !Directory.Exists(folder))
                return versions;
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, key + ".json.*.bak");
            }
            catch (Exception)
            {
                return versions;
            }

            for (var i = 0; i < files.Length; i++)
            {
                DateTime when;
                if (TryReadStamp(files[i], key, out when))
                    versions.Add(new PersonaVersion { Path = files[i], SavedAtUtc = when });
            }

            versions.Sort((a, b) =>
            {
                var byTime = b.SavedAtUtc.CompareTo(a.SavedAtUtc);
                return byTime != 0 ? byTime : string.CompareOrdinal(b.Path, a.Path);
            });
            return versions;
        }

        /// <summary>Deletes all but the newest <paramref name="keep"/> versions. Returns how many went.</summary>
        public static int Prune(string personasFolder, string id, int keep)
        {
            var versions = List(personasFolder, id);
            var removed = 0;
            for (var i = Math.Max(0, keep); i < versions.Count; i++)
            {
                try
                {
                    File.Delete(versions[i].Path);
                    removed++;
                }
                catch (Exception)
                {
                }
            }

            return removed;
        }

        /// <summary>A kept version as a persona, to load into the Studio as a draft. Null when it can't be read.</summary>
        public static PersonaDefinition Restore(string versionPath)
        {
            if (string.IsNullOrEmpty(versionPath) || !File.Exists(versionPath))
                return null;
            var read = ProtectedLocalFile.Read(versionPath);
            return read.Status == LocalReadStatus.Ready ? PersonaJson.Parse(read.Text) : null;
        }

        static bool TryReadStamp(string path, string key, out DateTime when)
        {
            when = default(DateTime);
            var name = System.IO.Path.GetFileName(path);
            var prefix = key + ".json.";
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                return false;
            var stamp = name.Substring(prefix.Length, name.Length - prefix.Length - 4);
            var dash = stamp.IndexOf('-');
            if (dash > 0)
                stamp = stamp.Substring(0, dash);
            return DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out when);
        }

        static string Normalize(string json)
        {
            return (json ?? string.Empty).Replace("\r\n", "\n").Trim();
        }
    }
}
