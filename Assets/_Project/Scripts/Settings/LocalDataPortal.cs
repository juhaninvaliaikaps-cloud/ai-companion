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
                OmitSecret(exported, result);
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(exported);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var temp = exported + ".writing";
                File.WriteAllText(temp, SecretFileFormat.Write(key, false), Utf8);
                if (File.Exists(exported))
                    File.Delete(exported);
                File.Move(temp, exported);
            }
            catch (Exception)
            {
                OmitSecret(exported, result);
            }
        }

        /// <summary>
        /// Drops a copied secrets file from an export when it cannot be rewritten decrypted.
        /// </summary>
        internal static void OmitSecret(string exported, LocalActionResult result)
        {
            try
            {
                var temp = exported + ".writing";
                if (!string.IsNullOrEmpty(exported) && File.Exists(temp))
                    File.Delete(temp);
                if (!string.IsNullOrEmpty(exported) && File.Exists(exported))
                {
                    File.Delete(exported);
                    if (result != null && result.Files > 0)
                        result.Files--;
                }
            }
            catch (Exception)
            {
            }

            if (result != null)
                result.Notes.Add("secrets.json was not included");
        }
    }
}
