using System;
using System.IO;
using System.Text;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.Settings
{
    /// <summary>
    /// Settings-layer export. Encrypted files are decrypted by <see cref="LocalDataActions"/>.
    /// The API key is rewritten here so the export contains the key in the existing
    /// plaintext secrets format, and only when this machine can read it.
    /// </summary>
    public static class LocalDataPortal
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static LocalActionResult ExportFolder(string root, string destination)
        {
            var result = LocalDataActions.ExportFolder(root, destination);
            if (result == null || !result.Ok)
                return result;
            RewriteSecrets(root, destination, result);
            return result;
        }

        public static LocalActionResult ExportZip(string root, string zipPath)
        {
            var temp = Path.Combine(Path.GetTempPath(), "leia-export-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                var result = ExportFolder(root, temp);
                if (result == null || !result.Ok)
                    return result;
                LocalZip.WriteFolder(temp, zipPath);
                return result;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temp))
                        Directory.Delete(temp, true);
                }
                catch (Exception)
                {
                }
            }
        }

        static void RewriteSecrets(string root, string destination, LocalActionResult result)
        {
            var source = Path.Combine(root, ApiKeyLocations.FileName);
            var exported = Path.Combine(destination, ApiKeyLocations.FileName);
            if (!File.Exists(source))
                return;

            string key;
            if (!ApiKeyFile.TryReadKey(source, SecretProtector.Create(), out key) || string.IsNullOrWhiteSpace(key))
            {
                try
                {
                    if (File.Exists(exported))
                    {
                        File.Delete(exported);
                        if (result.Files > 0)
                            result.Files--;
                    }
                }
                catch (Exception)
                {
                }

                result.Notes.Add("secrets.json was not included");
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(exported);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(exported, SecretFileFormat.Write(key, false), Utf8);
            }
            catch (Exception)
            {
                result.Notes.Add("secrets.json was not included");
            }
        }
    }
}
