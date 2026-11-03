using System;
using System.Collections.Generic;
using System.IO;

namespace AICompanion.Settings
{
    public enum PortraitHideReason
    {
        None = 0,
        Disabled = 1,
        FolderMissing = 2,
        NeutralMissing = 3
    }

    public struct PortraitShowPlan
    {
        public bool Show;
        public bool ShouldLog;
        public PortraitHideReason Reason;

        public static PortraitShowPlan Describe(bool enabled, bool folderFound, bool neutralFound)
        {
            if (!enabled)
                return new PortraitShowPlan { Show = false, ShouldLog = false, Reason = PortraitHideReason.Disabled };
            if (!folderFound)
                return new PortraitShowPlan { Show = false, ShouldLog = true, Reason = PortraitHideReason.FolderMissing };
            if (!neutralFound)
                return new PortraitShowPlan { Show = false, ShouldLog = true, Reason = PortraitHideReason.NeutralMissing };
            return new PortraitShowPlan { Show = true, ShouldLog = false, Reason = PortraitHideReason.None };
        }
    }

    public static class PortraitFolders
    {
        public const string FolderName = "portraits";
        public const string MapFileName = "portraits.json";

        public static string Primary(string applicationData, string persistentDataPath)
        {
            var root = SettingsLocations.Folder(applicationData, persistentDataPath);
            if (string.IsNullOrEmpty(root))
                return null;
            return Path.Combine(root, FolderName);
        }

        public static string PersistentFallback(string persistentDataPath)
        {
            if (string.IsNullOrEmpty(persistentDataPath))
                return null;
            return Path.Combine(persistentDataPath, SettingsLocations.FolderName, FolderName);
        }

        public static string Resolve(string applicationData, string persistentDataPath, Func<string, bool> directoryExists)
        {
            if (directoryExists == null)
                return null;

            var primary = Primary(applicationData, persistentDataPath);
            if (!string.IsNullOrEmpty(primary) && directoryExists(primary))
                return primary;

            var fallback = PersistentFallback(persistentDataPath);
            if (string.IsNullOrEmpty(fallback))
                return null;
            if (string.Equals(fallback, primary, StringComparison.OrdinalIgnoreCase))
                return null;
            if (directoryExists(fallback))
                return fallback;
            return null;
        }
    }

    /// <summary>
    /// Stems present in the portraits folder. PNG wins over JPG when both exist.
    /// </summary>
    public sealed class PortraitLibrary
    {
        readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public int Count
        {
            get { return _paths.Count; }
        }

        public static PortraitLibrary FromFileNames(IEnumerable<string> fileNames)
        {
            var library = new PortraitLibrary();
            if (fileNames == null)
                return library;
            foreach (var name in fileNames)
                library.Add(name, name);
            return library;
        }

        public static PortraitLibrary Load(string folder)
        {
            var library = new PortraitLibrary();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return library;

            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception)
            {
                return library;
            }

            for (var i = 0; i < files.Length; i++)
                library.Add(Path.GetFileName(files[i]), files[i]);
            return library;
        }

        public void Add(string fileName, string fullPath)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fullPath))
                return;
            if (!PortraitStems.IsImageExtension(Path.GetExtension(fileName)))
                return;
            if (!PortraitStems.IsSafe(fileName))
                return;

            var stem = PortraitStems.Stem(fileName);
            string current;
            if (!_paths.TryGetValue(stem, out current) || Prefers(fileName, current))
                _paths[stem] = fullPath;
        }

        public string Existing(string stemOrFile)
        {
            if (!PortraitStems.IsSafe(stemOrFile))
                return null;
            string path;
            if (_paths.TryGetValue(PortraitStems.Stem(stemOrFile), out path))
                return path;
            return null;
        }

        public bool ContainsNeutral(PortraitMap map)
        {
            if (map == null)
                return false;
            return Existing(map.Resolve("neutral").Open) != null;
        }

        static bool Prefers(string candidate, string current)
        {
            return Rank(candidate) < Rank(current);
        }

        static int Rank(string path)
        {
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase))
                return 1;
            return 2;
        }
    }
}
