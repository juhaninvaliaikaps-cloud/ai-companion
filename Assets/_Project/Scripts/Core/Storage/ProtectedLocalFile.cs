using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AICompanion.Core
{
    public enum LocalReadStatus
    {
        Missing = 0,
        Ready = 1,
        Unreadable = 2
    }

    public struct LocalRead
    {
        public LocalReadStatus Status;
        public string Text;

        public static LocalRead Missing
        {
            get { return new LocalRead { Status = LocalReadStatus.Missing }; }
        }

        public static LocalRead Ready(string text)
        {
            return new LocalRead { Status = LocalReadStatus.Ready, Text = text ?? string.Empty };
        }

        public static LocalRead Unreadable
        {
            get { return new LocalRead { Status = LocalReadStatus.Unreadable }; }
        }
    }

    internal enum LocalCommitOutcome
    {
        Verified = 0,
        VerifyMismatch = 1,
        WriteFailed = 2,
        ReadBackFailed = 3,
        BackupFailed = 4,
        CommitFailed = 5,
        InvalidInput = 6
    }

    /// <summary>
    /// Reads and writes one private text file. Plaintext is migrated by writing an
    /// encrypted copy, checking that it decrypts to the same bytes, and only then
    /// replacing the original. A bad encrypted file is moved aside and the caller
    /// starts fresh. Nothing here logs file contents or key bytes.
    /// </summary>
    public static class ProtectedLocalFile
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly UTF8Encoding Utf8Lenient = new UTF8Encoding(false);
        internal static readonly int ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

        public static LocalRead Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return LocalRead.Missing;

            try
            {
                RestoreHeld(path);
                if (!File.Exists(path))
                    return LocalRead.Missing;

                var bytes = File.ReadAllBytes(path);
                if (LocalAesGcm.IsMagic(bytes))
                    return ReadEnvelope(path, bytes);

                string text;
                if (!TryDecode(bytes, out text))
                {
                    Quarantine(path);
                    Warn("Unreadable local file was kept aside: " + SafeName(path));
                    return LocalRead.Unreadable;
                }

                if (!TryMigrate(path, text))
                    Warn("Plaintext local file was kept because encryption could not be verified: " + SafeName(path));
                return LocalRead.Ready(text);
            }
            catch (Exception e)
            {
                Warn("Local file could not be read: " + SafeName(path) + " " + e.GetType().Name + ": " + OneLine(e.Message));
                return LocalRead.Unreadable;
            }
        }

        public static bool Write(string path, string text)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (string.Equals(Path.GetFileName(path), InstallKeyFile.FileName, StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var key = InstallKeyFile.ForPath(path);
                if (key == null)
                {
                    Warn("Local file was not written because the install key is unavailable: " + SafeName(path));
                    return false;
                }

                var plain = Utf8Lenient.GetBytes(text ?? string.Empty);
                var envelope = LocalAesGcm.Seal(key, null, AssociatedData(path), plain);
                var outcome = CommitIfVerified(path, plain, envelope, blob => Open(key, AssociatedData(path), blob), ProcessId);
                if (outcome == LocalCommitOutcome.VerifyMismatch)
                    Warn("Local file was not written because the encrypted copy did not verify: " + SafeName(path));
                return outcome == LocalCommitOutcome.Verified;
            }
            catch (Exception e)
            {
                Warn("Local file was not written: " + SafeName(path) + " " + e.GetType().Name);
                return false;
            }
        }

        public static bool Quarantine(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;

                var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                var dest = path + ".unreadable-" + stamp;
                var n = 0;
                while (File.Exists(dest) && n < 50)
                {
                    n++;
                    dest = path + ".unreadable-" + stamp + "-" + n.ToString(CultureInfo.InvariantCulture);
                }

                File.Move(path, dest);
                return true;
            }
            catch (Exception e)
            {
                Warn("Local file could not be set aside: " + SafeName(path) + " " + e.GetType().Name);
                return false;
            }
        }

        internal static byte[] AssociatedData(string path)
        {
            var root = InstallKeyFile.RootFor(path);
            var name = Path.GetFileName(path) ?? "file";
            if (string.IsNullOrEmpty(root))
                return Encoding.UTF8.GetBytes(name.ToLowerInvariant());

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return Encoding.UTF8.GetBytes(name.ToLowerInvariant());
            }

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return Encoding.UTF8.GetBytes(name.ToLowerInvariant());

            var relative = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relative = relative.Replace('\\', '/').ToLowerInvariant();
            if (relative.Length == 0)
                relative = name.ToLowerInvariant();
            return Encoding.UTF8.GetBytes(relative);
        }

        /// <summary>
        /// Replaces <paramref name="destination"/> only after <paramref name="open"/>
        /// returns the original plaintext for both the temp file and the replaced file.
        /// </summary>
        internal static bool CommitIfVerified(string destination, byte[] plaintext, byte[] envelope, Func<byte[], byte[]> open)
        {
            return CommitIfVerified(destination, plaintext, envelope, open, ProcessId) == LocalCommitOutcome.Verified;
        }

        internal static LocalCommitOutcome CommitIfVerified(string destination, byte[] plaintext, byte[] envelope, Func<byte[], byte[]> open, int processId)
        {
            if (string.IsNullOrEmpty(destination) || plaintext == null || envelope == null || open == null)
                return LocalCommitOutcome.InvalidInput;

            var temp = WritingPath(destination, processId);
            var held = HeldPath(destination, processId);
            DeleteQuiet(temp);
            DeleteQuiet(held);

            try
            {
                File.WriteAllBytes(temp, envelope);
            }
            catch (Exception e)
            {
                WarnStep("write .writing", destination, e);
                DeleteOwnTemps(temp, held);
                return LocalCommitOutcome.WriteFailed;
            }

            try
            {
                if (!SameBytes(open(File.ReadAllBytes(temp)), plaintext))
                {
                    DeleteOwnTemps(temp, held);
                    return LocalCommitOutcome.VerifyMismatch;
                }
            }
            catch (Exception e)
            {
                WarnStep("read-back verify", destination, e);
                DeleteOwnTemps(temp, held);
                return LocalCommitOutcome.ReadBackFailed;
            }

            try
            {
                if (File.Exists(destination))
                    File.Copy(destination, held, true);
            }
            catch (Exception e)
            {
                WarnStep(".held backup", destination, e);
                DeleteOwnTemps(temp, held);
                return LocalCommitOutcome.BackupFailed;
            }

            try
            {
                File.Copy(temp, destination, true);
            }
            catch (Exception e)
            {
                WarnStep("commit", destination, e);
                RestoreHeld(destination, held);
                DeleteOwnTemps(temp, held);
                return LocalCommitOutcome.CommitFailed;
            }

            try
            {
                if (!SameBytes(open(File.ReadAllBytes(destination)), plaintext))
                {
                    RestoreOrRemoveHeld(destination, held);
                    DeleteOwnTemps(temp, held);
                    return LocalCommitOutcome.VerifyMismatch;
                }
            }
            catch (Exception e)
            {
                WarnStep("read-back verify", destination, e);
                RestoreOrRemoveHeld(destination, held);
                DeleteOwnTemps(temp, held);
                return LocalCommitOutcome.ReadBackFailed;
            }

            DeleteOwnTemps(temp, held);
            return LocalCommitOutcome.Verified;
        }

        internal static string WritingPath(string destination, int processId)
        {
            return destination + ".writing." + processId.ToString(CultureInfo.InvariantCulture);
        }

        internal static string HeldPath(string destination, int processId)
        {
            return destination + ".held." + processId.ToString(CultureInfo.InvariantCulture);
        }

        static void WarnStep(string step, string destination, Exception e)
        {
            Warn("Local file write failed at " + step + " for " + SafeName(destination) + ": " + e.GetType().Name + ": " + OneLine(e.Message));
        }

        static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var line = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return line.Length > 240 ? line.Substring(0, 240) : line;
        }

        static void RestoreHeld(string destination, string held)
        {
            try
            {
                if (File.Exists(held))
                    File.Copy(held, destination, true);
            }
            catch (Exception)
            {
            }
        }

        static void RestoreOrRemoveHeld(string destination, string held)
        {
            if (File.Exists(held))
            {
                RestoreHeld(destination, held);
                return;
            }

            DeleteQuiet(destination);
        }

        static void DeleteOwnTemps(string temp, string held)
        {
            DeleteQuiet(temp);
            DeleteQuiet(held);
        }

        static LocalRead ReadEnvelope(string path, byte[] bytes)
        {
            var key = InstallKeyFile.ForPath(path);
            if (key == null)
            {
                Warn("Local file was not decrypted because the install key is unavailable: " + SafeName(path));
                return LocalRead.Unreadable;
            }

            var plain = Open(key, AssociatedData(path), bytes);
            if (plain == null)
            {
                Quarantine(path);
                Warn("Unreadable local file was kept aside: " + SafeName(path));
                return LocalRead.Unreadable;
            }

            string text;
            if (!TryDecode(plain, out text))
            {
                Quarantine(path);
                Warn("Unreadable local file was kept aside: " + SafeName(path));
                return LocalRead.Unreadable;
            }

            return LocalRead.Ready(text);
        }

        static bool TryMigrate(string path, string text)
        {
            var key = InstallKeyFile.ForPath(path);
            if (key == null)
                return false;

            var plain = Utf8Lenient.GetBytes(text ?? string.Empty);
            var envelope = LocalAesGcm.Seal(key, null, AssociatedData(path), plain);
            return CommitIfVerified(path, plain, envelope, blob => Open(key, AssociatedData(path), blob));
        }

        static void RestoreHeld(string path)
        {
            var held = HeldPath(path, ProcessId);
            if (!File.Exists(held))
                return;

            if (!KeepDestination(path) && File.Exists(held))
                File.Copy(held, path, true);
            DeleteQuiet(held);
        }

        static bool KeepDestination(string path)
        {
            if (!File.Exists(path))
                return false;

            var bytes = File.ReadAllBytes(path);
            if (!LocalAesGcm.IsMagic(bytes))
                return true;

            var key = InstallKeyFile.ForPath(path);
            if (key == null)
                return true;
            return Open(key, AssociatedData(path), bytes) != null;
        }

        static byte[] Open(byte[] key, byte[] aad, byte[] envelope)
        {
            byte[] plain;
            if (!LocalAesGcm.TryOpen(key, aad, envelope, out plain))
                return null;
            return plain;
        }

        static bool TryDecode(byte[] bytes, out string text)
        {
            text = null;
            if (bytes == null)
                return false;

            var offset = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                offset = 3;

            try
            {
                text = Utf8.GetString(bytes, offset, bytes.Length - offset);
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = null;
                return false;
            }
        }

        static bool SameBytes(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            var diff = 0;
            for (var i = 0; i < left.Length; i++)
                diff |= left[i] ^ right[i];
            return diff == 0;
        }

        static void DeleteQuiet(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        static string SafeName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "file";
            return Path.GetFileName(path);
        }

        static void Warn(string message)
        {
            Debug.LogWarning(message);
        }
    }
}
