using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AICompanion.Core
{
    public enum LocalKeyDecision
    {
        Dpapi = 0,
        LabeledFallback = 1,
        Refuse = 2
    }

    /// <summary>
    /// A Windows player that cannot use DPAPI must not invent an unprotected key.
    /// Editor and CI may store a key file whose first line is <see cref="LocalKeyPolicy.DevFallbackLabel"/>.
    /// </summary>
    public static class LocalKeyPolicy
    {
        public const string DevFallbackLabel = "LEIA-DEV-FALLBACK-NOT-DPAPI";

        public static LocalKeyDecision Choose(bool windowsPlayer, bool dpapiReady)
        {
            if (dpapiReady)
                return LocalKeyDecision.Dpapi;
            if (windowsPlayer)
                return LocalKeyDecision.Refuse;
            return LocalKeyDecision.LabeledFallback;
        }
    }

    /// <summary>
    /// One 256-bit key per ProjectLeia folder. On Windows the file is a DPAPI blob
    /// for the current user. Elsewhere the file starts with a fixed label so it is
    /// obvious that the key is not protected by Windows.
    /// </summary>
    public static class InstallKeyFile
    {
        public const string FileName = "install-key.leia";
        const string DpapiMagic = "LEIAKEY1";

        static readonly object Gate = new object();
        static readonly Dictionary<string, byte[]> Cache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> MissingKeyLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Tests set this to force a key, or null to refuse writes. Production leaves it null.</summary>
        internal static Func<string, byte[]> KeyForPath;

        public static string RootFor(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return null;
            }

            var dir = full;
            if (!Directory.Exists(full))
                dir = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(dir))
                return null;

            var name = Path.GetFileName(TrimSlash(dir));
            if (string.Equals(name, "personas", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "portraits", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(TrimSlash(dir));
                if (!string.IsNullOrEmpty(parent))
                    return parent;
            }

            return dir;
        }

        public static byte[] ForPath(string path)
        {
            if (KeyForPath != null)
                return KeyForPath(path);

            var root = RootFor(path);
            if (string.IsNullOrEmpty(root))
                return null;
            return ForRoot(root);
        }

        public static byte[] ForRoot(string root)
        {
            if (string.IsNullOrEmpty(root))
                return null;

            string full;
            try
            {
                full = Path.GetFullPath(root);
            }
            catch (Exception)
            {
                return null;
            }

            lock (Gate)
            {
                byte[] cached;
                if (Cache.TryGetValue(full, out cached))
                    return cached;

                var player = Application.platform == RuntimePlatform.WindowsPlayer;
                var windows = player || Application.platform == RuntimePlatform.WindowsEditor;
                var key = LoadOrCreate(full, player, windows, null, null);
                if (key != null)
                    Cache[full] = Copy(key);
                return key;
            }
        }

        public static void Forget(string root)
        {
            if (string.IsNullOrEmpty(root))
                return;

            string full;
            try
            {
                full = Path.GetFullPath(root);
            }
            catch (Exception)
            {
                return;
            }

            lock (Gate)
            {
                Cache.Remove(full);
                MissingKeyLogged.Remove(full);
            }
        }

        static void WarnMissingOnce(string root)
        {
            lock (Gate)
            {
                if (!MissingKeyLogged.Add(root))
                    return;
            }

            Debug.LogWarning("Install key is missing. Encrypted files were left in place.");
        }

        public static byte[] LoadOrCreate(
            string root,
            bool windowsPlayer,
            bool preferDpapi,
            Func<byte[], byte[]> protect,
            Func<byte[], byte[]> unprotect)
        {
            if (string.IsNullOrWhiteSpace(root))
                return null;

            string full;
            try
            {
                full = Path.GetFullPath(root);
                Directory.CreateDirectory(full);
            }
            catch (Exception)
            {
                return null;
            }

            var path = Path.Combine(full, FileName);
            if (File.Exists(path))
                return ReadExisting(path, windowsPlayer, unprotect);

            if (LocalDataActions.HasSealedPayload(full))
            {
                WarnMissingOnce(full);
                return null;
            }

            var created = new byte[LocalAesGcm.KeyBytes];
            try
            {
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(created);

                var decision = LocalKeyPolicy.Choose(windowsPlayer, preferDpapi);
                if (decision == LocalKeyDecision.Dpapi)
                {
                    try
                    {
                        var blob = protect != null ? protect(created) : DpapiBlob.Protect(created);
                        if (blob == null || blob.Length == 0)
                            throw new InvalidOperationException("Windows could not protect the install key.");
                        WriteDpapi(path, blob);
                        return Copy(created);
                    }
                    catch (Exception)
                    {
                        decision = LocalKeyPolicy.Choose(windowsPlayer, false);
                        if (decision == LocalKeyDecision.Refuse)
                        {
                            Debug.LogWarning("Install key was not created. Windows data protection failed, so private files stay unwritten.");
                            return null;
                        }
                    }
                }

                if (decision == LocalKeyDecision.Refuse)
                {
                    Debug.LogWarning("Install key was not created. A Windows player will not store an unprotected key.");
                    return null;
                }

                WriteFallback(path, created);
                return Copy(created);
            }
            finally
            {
                Array.Clear(created, 0, created.Length);
            }
        }

        static byte[] ReadExisting(string path, bool windowsPlayer, Func<byte[], byte[]> unprotect)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Install key could not be read: " + e.GetType().Name);
                return null;
            }

            if (IsFallback(bytes))
            {
                if (windowsPlayer)
                {
                    Debug.LogWarning("Install key is an unprotected fallback. The Windows player will not use it.");
                    return null;
                }

                return ParseFallback(bytes);
            }

            if (!IsDpapi(bytes))
            {
                Debug.LogWarning("Install key file was not recognized. It was left in place.");
                return null;
            }

            try
            {
                var blob = new byte[bytes.Length - 10];
                Buffer.BlockCopy(bytes, 10, blob, 0, blob.Length);
                var key = unprotect != null ? unprotect(blob) : DpapiBlob.Unprotect(blob);
                if (key == null || key.Length != LocalAesGcm.KeyBytes)
                {
                    Debug.LogWarning("Install key could not be unwrapped. The key file was left in place.");
                    return null;
                }

                return key;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Install key could not be unwrapped: " + e.GetType().Name);
                return null;
            }
        }

        static bool IsFallback(byte[] bytes)
        {
            var label = Encoding.UTF8.GetBytes(LocalKeyPolicy.DevFallbackLabel + "\n");
            if (bytes == null || bytes.Length < label.Length)
                return false;
            for (var i = 0; i < label.Length; i++)
            {
                if (bytes[i] != label[i])
                    return false;
            }

            return true;
        }

        static bool IsDpapi(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 10)
                return false;
            var magic = Encoding.ASCII.GetBytes(DpapiMagic);
            for (var i = 0; i < magic.Length; i++)
            {
                if (bytes[i] != magic[i])
                    return false;
            }

            return bytes[8] == 1 && bytes[9] == 1;
        }

        static byte[] ParseFallback(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2 || !string.Equals(lines[0].Trim(), LocalKeyPolicy.DevFallbackLabel, StringComparison.Ordinal))
                return null;

            try
            {
                var key = Convert.FromBase64String(lines[1].Trim());
                if (key.Length != LocalAesGcm.KeyBytes)
                    return null;
                return key;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        static void WriteDpapi(string path, byte[] blob)
        {
            var bytes = new byte[10 + blob.Length];
            var magic = Encoding.ASCII.GetBytes(DpapiMagic);
            Buffer.BlockCopy(magic, 0, bytes, 0, 8);
            bytes[8] = 1;
            bytes[9] = 1;
            Buffer.BlockCopy(blob, 0, bytes, 10, blob.Length);
            File.WriteAllBytes(path, bytes);
        }

        static void WriteFallback(string path, byte[] key)
        {
            var text = LocalKeyPolicy.DevFallbackLabel + "\n" + Convert.ToBase64String(key) + "\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        static byte[] Copy(byte[] key)
        {
            var copy = new byte[key.Length];
            Buffer.BlockCopy(key, 0, copy, 0, key.Length);
            return copy;
        }

        static string TrimSlash(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
