using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AICompanion.Core
{
    public sealed class LocalActionResult
    {
        public bool Ok;
        public int Files;
        public readonly List<string> Notes = new List<string>();
    }

    /// <summary>
    /// Export, wipe, and first-run migration for the ProjectLeia folder.
    /// Export writes decrypted text for encrypted files. The API key file is copied
    /// as stored; the settings layer rewrites it decrypted when it can.
    /// </summary>
    public static class LocalDataActions
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static bool IsDataRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return false;

            string full;
            try
            {
                full = Path.GetFullPath(root);
            }
            catch (Exception)
            {
                return false;
            }

            var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rootPath = Path.GetPathRoot(trimmed);
            if (string.IsNullOrEmpty(trimmed) || string.Equals(trimmed, rootPath, StringComparison.OrdinalIgnoreCase))
                return false;
            if (IsReparse(trimmed))
                return false;
            return string.Equals(Path.GetFileName(trimmed), "ProjectLeia", StringComparison.OrdinalIgnoreCase);
        }

        public static bool DestinationInsideRoot(string root, string destination)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(destination))
                return true;

            try
            {
                var left = WithSlash(Path.GetFullPath(root));
                var right = WithSlash(Path.GetFullPath(destination));
                return right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// True when a private file in this folder is already an AES envelope.
        /// Used to avoid minting a new install key over data the old key still owns.
        /// </summary>
        public static bool HasSealedPayload(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return false;

            var aes = new List<string>();
            var secrets = new List<string>();
            var raw = new List<string>();
            var junk = new List<string>();
            Collect(root, aes, secrets, raw, junk);
            for (var i = 0; i < aes.Count; i++)
            {
                if (StartsWithMagic(aes[i]))
                    return true;
            }

            return false;
        }

        public static LocalActionResult MigratePlaintext(string root)
        {
            var result = new LocalActionResult();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                result.Ok = true;
                return result;
            }

            if (!IsDataRoot(root))
            {
                result.Notes.Add("not a ProjectLeia folder");
                return result;
            }

            var aes = new List<string>();
            var secrets = new List<string>();
            var raw = new List<string>();
            var junk = new List<string>();
            Collect(root, aes, secrets, raw, junk);
            for (var i = 0; i < aes.Count; i++)
            {
                var read = ProtectedLocalFile.Read(aes[i]);
                if (read.Status == LocalReadStatus.Unreadable)
                    result.Notes.Add("unreadable: " + Path.GetFileName(aes[i]));
                else if (read.Status == LocalReadStatus.Ready)
                    result.Files++;
            }

            result.Ok = true;
            return result;
        }

        public static LocalActionResult ExportFolder(string root, string destination)
        {
            var result = new LocalActionResult();
            if (!IsDataRoot(root))
            {
                result.Notes.Add("not a ProjectLeia folder");
                return result;
            }

            if (DestinationInsideRoot(root, destination))
            {
                result.Notes.Add("export folder is inside the data folder");
                return result;
            }

            try
            {
                Directory.CreateDirectory(destination);
            }
            catch (Exception)
            {
                result.Notes.Add("export folder was not created");
                return result;
            }

            var aes = new List<string>();
            var secrets = new List<string>();
            var raw = new List<string>();
            var junk = new List<string>();
            Collect(root, aes, secrets, raw, junk);
            for (var i = 0; i < aes.Count; i++)
                ExportText(root, destination, aes[i], result);
            for (var i = 0; i < secrets.Count; i++)
                ExportRaw(root, destination, secrets[i], result);
            for (var i = 0; i < raw.Count; i++)
                ExportRaw(root, destination, raw[i], result);
            result.Ok = true;
            return result;
        }

        public static LocalActionResult Wipe(string root)
        {
            var result = new LocalActionResult();
            if (!IsDataRoot(root))
            {
                result.Notes.Add("not a ProjectLeia folder");
                return result;
            }

            var aes = new List<string>();
            var secrets = new List<string>();
            var raw = new List<string>();
            var junk = new List<string>();
            Collect(root, aes, secrets, raw, junk);
            DeleteAll(aes, result);
            DeleteAll(secrets, result);
            DeleteAll(raw, result);
            DeleteAll(junk, result);
            TryDeleteDirectory(Path.Combine(root, "personas"));
            TryDeleteDirectory(Path.Combine(root, "portraits"));
            InstallKeyFile.Forget(root);
            result.Ok = true;
            return result;
        }

        static void ExportText(string root, string destination, string source, LocalActionResult result)
        {
            var read = ProtectedLocalFile.Read(source);
            if (read.Status != LocalReadStatus.Ready)
            {
                result.Notes.Add("skipped unreadable: " + Path.GetFileName(source));
                return;
            }

            var target = Target(root, destination, source);
            try
            {
                var directory = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(target, read.Text ?? string.Empty, Utf8);
                result.Files++;
            }
            catch (Exception)
            {
                result.Notes.Add("skipped: " + Path.GetFileName(source));
            }
        }

        static void ExportRaw(string root, string destination, string source, LocalActionResult result)
        {
            var target = Target(root, destination, source);
            try
            {
                var directory = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.Copy(source, target, true);
                result.Files++;
            }
            catch (Exception)
            {
                result.Notes.Add("skipped: " + Path.GetFileName(source));
            }
        }

        static void DeleteAll(List<string> paths, LocalActionResult result)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                try
                {
                    if (File.Exists(paths[i]))
                    {
                        File.Delete(paths[i]);
                        result.Files++;
                    }
                }
                catch (Exception)
                {
                    result.Notes.Add("kept: " + Path.GetFileName(paths[i]));
                }
            }
        }

        static void Collect(string root, List<string> aes, List<string> secrets, List<string> raw, List<string> junk)
        {
            ConsiderDirectory(root, root, aes, secrets, raw, junk);
            var personas = Path.Combine(root, "personas");
            if (Directory.Exists(personas) && !IsReparse(personas))
                ConsiderDirectory(root, personas, aes, secrets, raw, junk);
            var portraits = Path.Combine(root, "portraits");
            if (Directory.Exists(portraits) && !IsReparse(portraits))
                ConsiderTree(root, portraits, aes, secrets, raw, junk);
        }

        static void ConsiderTree(string root, string directory, List<string> aes, List<string> secrets, List<string> raw, List<string> junk)
        {
            ConsiderDirectory(root, directory, aes, secrets, raw, junk);
            string[] children;
            try
            {
                children = Directory.GetDirectories(directory);
            }
            catch (Exception)
            {
                return;
            }

            for (var i = 0; i < children.Length; i++)
            {
                if (!IsReparse(children[i]))
                    ConsiderTree(root, children[i], aes, secrets, raw, junk);
            }
        }

        static void ConsiderDirectory(string root, string directory, List<string> aes, List<string> secrets, List<string> raw, List<string> junk)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch (Exception)
            {
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
                if (IsReparse(files[i]))
                    continue;
                Consider(root, files[i], aes, secrets, raw, junk);
            }
        }

        static void Consider(string root, string path, List<string> aes, List<string> secrets, List<string> raw, List<string> junk)
        {
            var rel = Relative(root, path);
            if (LocalDataInventory.IsExcluded(rel))
                junk.Add(path);
            else if (LocalDataInventory.IsSecretFile(rel))
                secrets.Add(path);
            else if (LocalDataInventory.IsEncryptedFile(rel))
                aes.Add(path);
            else if (LocalDataInventory.IsRawCopy(rel))
                raw.Add(path);
        }

        static string Relative(string root, string path)
        {
            var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var full = Path.GetFullPath(path);
            var prefix = basePath + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(path);
            return full.Substring(prefix.Length).Replace('\\', '/');
        }

        static string Target(string root, string destination, string source)
        {
            var rel = Relative(root, source).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(destination, rel);
        }

        static string WithSlash(string path)
        {
            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return trimmed + Path.DirectorySeparatorChar;
        }

        static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path) || IsReparse(path))
                    return;
                if (Directory.GetFileSystemEntries(path).Length == 0)
                    Directory.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        static bool StartsWithMagic(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var magic = new byte[8];
                    if (stream.Read(magic, 0, magic.Length) < magic.Length)
                        return false;
                    return LocalAesGcm.IsMagic(magic);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool IsReparse(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
