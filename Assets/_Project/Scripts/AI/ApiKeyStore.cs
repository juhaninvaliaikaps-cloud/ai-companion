using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace AICompanion.AI
{
    public interface IApiKeySource
    {
        bool TryGet(out string key);
    }

    public interface ISecretProtector
    {
        bool Protects { get; }
        string Protect(string plain);
        bool TryUnprotect(string payload, out string plain);
    }

    public sealed class PlainSecretProtector : ISecretProtector
    {
        public bool Protects => false;

        public string Protect(string plain)
        {
            return plain ?? string.Empty;
        }

        public bool TryUnprotect(string payload, out string plain)
        {
            plain = null;
            return false;
        }
    }

    /// <summary>
    /// Windows DPAPI for the current user. On any other platform this reports
    /// that it does not protect, and the file store writes plain text instead.
    /// </summary>
    public sealed class DpapiSecretProtector : ISecretProtector
    {
        const int CryptProtectUiForbidden = 0x1;

        public bool Protects
        {
            get
            {
                return Application.platform == RuntimePlatform.WindowsPlayer
                    || Application.platform == RuntimePlatform.WindowsEditor;
            }
        }

        public string Protect(string plain)
        {
            if (!Protects)
                throw new InvalidOperationException("Windows data protection is not available.");

            var bytes = Encoding.UTF8.GetBytes(plain ?? string.Empty);
            var encrypted = ProtectBytes(bytes);
            return Convert.ToBase64String(encrypted);
        }

        public bool TryUnprotect(string payload, out string plain)
        {
            plain = null;
            if (!Protects || string.IsNullOrEmpty(payload))
                return false;

            try
            {
                var encrypted = Convert.FromBase64String(payload);
                var raw = UnprotectBytes(encrypted);
                plain = Encoding.UTF8.GetString(raw);
                return !string.IsNullOrWhiteSpace(plain);
            }
            catch (Exception)
            {
                plain = null;
                return false;
            }
        }

        static byte[] ProtectBytes(byte[] plain)
        {
            return Transform(plain, true);
        }

        static byte[] UnprotectBytes(byte[] encrypted)
        {
            return Transform(encrypted, false);
        }

        static byte[] Transform(byte[] input, bool protect)
        {
            var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            var output = new DataBlob();
            try
            {
                var incoming = new DataBlob
                {
                    cbData = input.Length,
                    pbData = inputHandle.AddrOfPinnedObject()
                };
                var ok = protect
                    ? CryptProtectData(ref incoming, "ProjectLeia", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output)
                    : CryptUnprotectData(ref incoming, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output);
                if (!ok || output.pbData == IntPtr.Zero || output.cbData < 0)
                    throw new InvalidOperationException("Windows could not protect the API key.");

                var managed = new byte[output.cbData];
                Marshal.Copy(output.pbData, managed, 0, output.cbData);
                return managed;
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                    LocalFree(output.pbData);
                inputHandle.Free();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DataBlob
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr handle);
    }

    public static class SecretProtector
    {
        public static ISecretProtector Create()
        {
            var dpapi = new DpapiSecretProtector();
            if (dpapi.Protects)
                return dpapi;
            return new PlainSecretProtector();
        }
    }

    public static class ApiKeyLocations
    {
        public const string FolderName = "ProjectLeia";
        public const string FileName = "secrets.json";

        public static string AppDataPath(string applicationData)
        {
            if (string.IsNullOrEmpty(applicationData))
                return null;
            return Path.Combine(applicationData, FolderName, FileName);
        }

        public static string PersistentPath(string persistentDataPath)
        {
            if (string.IsNullOrEmpty(persistentDataPath))
                return null;
            return Path.Combine(persistentDataPath, FolderName, FileName);
        }

        public static string PreferredWritePath(string applicationData, string persistentDataPath)
        {
            var appData = AppDataPath(applicationData);
            if (!string.IsNullOrEmpty(appData))
                return appData;
            return PersistentPath(persistentDataPath);
        }
    }

    public static class SecretFileFormat
    {
        public static string Write(string payload, bool isProtected)
        {
            var sb = new StringBuilder();
            sb.Append("{\"protected\":");
            sb.Append(isProtected ? "true" : "false");
            sb.Append(",\"xaiApiKey\":");
            JsonText.AppendString(sb, payload ?? string.Empty);
            sb.Append('}');
            return sb.ToString();
        }

        public static bool TryReadKey(string json, ISecretProtector protector, out string key)
        {
            key = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            if (!JsonText.TryReadStringField(json, "xaiApiKey", out var payload) || string.IsNullOrWhiteSpace(payload))
                return false;

            var isProtected = false;
            JsonText.TryReadBoolField(json, "protected", out isProtected);
            if (isProtected)
            {
                if (protector == null || !protector.TryUnprotect(payload, out key) || string.IsNullOrWhiteSpace(key))
                {
                    key = null;
                    return false;
                }

                key = key.Trim();
                return true;
            }

            key = payload.Trim();
            return key.Length > 0;
        }
    }

    public static class ApiKeyFile
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static void Save(string path, string key, ISecretProtector protector)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A secrets path is required.", "path");
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("An API key is required.", "key");

            var protect = protector != null && protector.Protects;
            string payload;
            if (protect)
            {
                try
                {
                    payload = protector.Protect(key.Trim());
                }
                catch (Exception)
                {
                    protect = false;
                    payload = key.Trim();
                }
            }
            else
            {
                payload = key.Trim();
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, SecretFileFormat.Write(payload, protect), Utf8);
        }

        public static bool TryReadKey(string path, ISecretProtector protector, out string key)
        {
            key = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                var json = File.ReadAllText(path, Utf8);
                return SecretFileFormat.TryReadKey(json, protector, out key);
            }
            catch (Exception)
            {
                key = null;
                return false;
            }
        }

        public static void Delete(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            File.Delete(path);
        }

        public static string PreferredPath()
        {
            return ApiKeyLocations.PreferredWritePath(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.persistentDataPath);
        }

        public static void SavePreferred(string key)
        {
            Save(PreferredPath(), key, SecretProtector.Create());
        }
    }

    public static class ApiKeyResolver
    {
        public const string EnvName = "XAI_API_KEY";

        public static bool TryResolveValues(string envValue, IReadOnlyList<string> fileJsonInOrder, ISecretProtector protector, out string key)
        {
            key = null;
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                key = envValue.Trim();
                return true;
            }

            if (fileJsonInOrder == null)
                return false;

            for (var i = 0; i < fileJsonInOrder.Count; i++)
            {
                if (SecretFileFormat.TryReadKey(fileJsonInOrder[i], protector, out key))
                    return true;
            }

            key = null;
            return false;
        }

        public static bool TryResolveFiles(Func<string, string> readEnv, IReadOnlyList<string> paths, ISecretProtector protector, out string key)
        {
            var env = readEnv != null ? readEnv(EnvName) : null;
            var json = new List<string>();
            if (paths != null)
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    if (string.IsNullOrEmpty(paths[i]) || !File.Exists(paths[i]))
                        continue;
                    try
                    {
                        json.Add(File.ReadAllText(paths[i]));
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return TryResolveValues(env, json, protector, out key);
        }
    }

    public sealed class EnvironmentApiKeySource : IApiKeySource
    {
        public bool TryGet(out string key)
        {
            var paths = new List<string>(2);
            var appData = ApiKeyLocations.AppDataPath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            if (!string.IsNullOrEmpty(appData))
                paths.Add(appData);
            var persistent = ApiKeyLocations.PersistentPath(Application.persistentDataPath);
            if (!string.IsNullOrEmpty(persistent) && !paths.Contains(persistent))
                paths.Add(persistent);
            return ApiKeyResolver.TryResolveFiles(Environment.GetEnvironmentVariable, paths, SecretProtector.Create(), out key);
        }
    }
}
