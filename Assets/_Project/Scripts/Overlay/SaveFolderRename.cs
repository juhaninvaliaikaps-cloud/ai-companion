using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Unity's save folder (<c>Application.persistentDataPath</c>, on Windows
    /// <c>%USERPROFILE%\AppData\LocalLow\AI Companion\&lt;product&gt;</c>) follows the product name.
    /// After a rename this copies what the newest earlier folder holds into the new one, once.
    /// It only adds files that are missing, never overwrites, and never deletes the old folder.
    /// The old Player.log files and Unity's own cache stay behind.
    /// Most of her data (settings, chat history, mood, memory, personas, secrets and the
    /// encryption key) lives in <c>%APPDATA%\ProjectLeia</c>, which a rename does not move.
    /// </summary>
    public static class SaveFolderRenamePlan
    {
        public const string MarkerFileName = "renamed-from.txt";

        static readonly string[] SkipTopFiles = { "Player.log", "Player-prev.log", MarkerFileName };
        static readonly string[] SkipTopFolders = { "Unity", "Crashes" };

        /// <summary>The folder next to <paramref name="current"/> named after an earlier product.</summary>
        public static string Sibling(string current, string product)
        {
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(product))
                return null;
            var trimmed = current.TrimEnd('/', '\\');
            var parent = Path.GetDirectoryName(trimmed);
            if (string.IsNullOrEmpty(parent))
                return null;
            return Path.Combine(parent, product);
        }

        /// <summary>The newest earlier folder that exists, or null.</summary>
        public static string PickSource(string current, string[] previousProducts, Func<string, bool> directoryExists)
        {
            if (previousProducts == null || directoryExists == null)
                return null;
            var full = Normalize(current);
            for (var i = 0; i < previousProducts.Length; i++)
            {
                var candidate = Sibling(current, previousProducts[i]);
                if (candidate == null || string.Equals(Normalize(candidate), full, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (directoryExists(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>True for paths (relative to the folder) that stay behind.</summary>
        public static bool ShouldSkip(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return true;
            var parts = relativePath.Split('/', '\\');
            var top = parts[0];
            if (parts.Length == 1)
            {
                for (var i = 0; i < SkipTopFiles.Length; i++)
                {
                    if (string.Equals(top, SkipTopFiles[i], StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }

            for (var i = 0; i < SkipTopFolders.Length; i++)
            {
                if (string.Equals(top, SkipTopFolders[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public struct Result
        {
            public bool Ran;
            public string Source;
            public int Copied;
            public int Failed;

            public string Describe(string current)
            {
                if (!Ran)
                    return string.Empty;
                if (string.IsNullOrEmpty(Source))
                    return "Rename: no earlier save folder next to " + Path.GetFileName(current) + "; nothing to carry over.";
                return "Rename: carried " + Copied + " files from " + Path.GetFileName(Source) + " to " + Path.GetFileName(current)
                    + (Failed > 0 ? " (" + Failed + " failed; will retry next start)." : ".");
            }
        }

        /// <summary>
        /// Carries the newest earlier folder over once. The marker file is written only when
        /// every copy worked, so a failed start retries, and a retry never overwrites anything.
        /// </summary>
        public static Result Run(string current, string[] previousProducts, Func<string, bool> directoryExists)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(current))
                return result;
            var marker = Path.Combine(current, MarkerFileName);
            if (File.Exists(marker))
                return result;

            result.Ran = true;
            result.Source = PickSource(current, previousProducts, directoryExists);
            Directory.CreateDirectory(current);
            if (result.Source != null)
                CopyMissing(result.Source, current, ref result);

            if (result.Failed == 0)
            {
                File.WriteAllText(marker, (result.Source == null
                    ? "No earlier save folder.\n"
                    : "Carried over from " + Path.GetFileName(result.Source) + " (" + result.Copied + " files). The old folder was not changed.\n"));
            }

            return result;
        }

        static void CopyMissing(string from, string to, ref Result result)
        {
            var root = Normalize(from);
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var folder = pending.Pop();
                string[] files;
                string[] folders;
                try
                {
                    files = Directory.GetFiles(folder);
                    folders = Directory.GetDirectories(folder);
                }
                catch (Exception)
                {
                    result.Failed++;
                    continue;
                }

                for (var i = 0; i < folders.Length; i++)
                {
                    if (!ShouldSkip(Relative(root, folders[i]) + "/x"))
                        pending.Push(folders[i]);
                }

                for (var i = 0; i < files.Length; i++)
                {
                    var relative = Relative(root, files[i]);
                    if (ShouldSkip(relative))
                        continue;
                    var target = Path.Combine(to, relative);
                    if (File.Exists(target))
                        continue;
                    try
                    {
                        var dir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                        File.Copy(files[i], target, false);
                        result.Copied++;
                    }
                    catch (Exception)
                    {
                        result.Failed++;
                    }
                }
            }
        }

        static string Relative(string root, string path)
        {
            var full = Normalize(path);
            if (full.Length <= root.Length)
                return string.Empty;
            return full.Substring(root.Length).TrimStart('/', '\\');
        }

        static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            try
            {
                return Path.GetFullPath(path).TrimEnd('/', '\\');
            }
            catch (Exception)
            {
                return path.TrimEnd('/', '\\');
            }
        }
    }

    /// <summary>Runs as early as possible, before anything reads the save folder.</summary>
    static class SaveFolderRename
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ApplyOnce()
        {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            try
            {
                var current = Application.persistentDataPath;
                var result = SaveFolderRenamePlan.Run(current, AppName.PreviousDisplays, Directory.Exists);
                var line = result.Describe(current);
                if (line.Length > 0)
                    Debug.Log(line);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Rename: could not carry the earlier save folder over (" + e.GetType().Name + "). The old folder is unchanged.");
            }
#endif
        }
    }
}
