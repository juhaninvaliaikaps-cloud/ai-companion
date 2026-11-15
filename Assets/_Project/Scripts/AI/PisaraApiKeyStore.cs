using System;
using System.IO;
using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>
    /// JUH-126: the optional API key for the separate Pisara reading service. It is stored in
    /// its own encrypted file, never in settings.json. PISARA_API_KEY overrides the file; the
    /// chooser decides when the Grok key may be used as a fallback instead.
    /// </summary>
    public static class PisaraApiKeyStore
    {
        public const string FileName = "pisara-api-key.json";
        public const string FieldName = "pisaraApiKey";
        public const string EnvName = "PISARA_API_KEY";

        public static string AppDataPath(string applicationData)
        {
            if (string.IsNullOrEmpty(applicationData))
                return null;
            return Path.Combine(applicationData, ApiKeyLocations.FolderName, FileName);
        }

        public static string PersistentPath(string persistentDataPath)
        {
            if (string.IsNullOrEmpty(persistentDataPath))
                return null;
            return Path.Combine(persistentDataPath, ApiKeyLocations.FolderName, FileName);
        }

        public static string PreferredPath()
        {
            var appData = AppDataPath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            if (!string.IsNullOrEmpty(appData))
                return appData;
            return PersistentPath(Application.persistentDataPath);
        }

        public static void Save(string key)
        {
            ApiKeyFile.SaveField(PreferredPath(), key, SecretProtector.Create(), FieldName);
        }

        public static void Save(string path, string key, ISecretProtector protector)
        {
            ApiKeyFile.SaveField(path, key, protector, FieldName);
        }

        public static bool TryLoad(out string key)
        {
            return ApiKeyFile.TryReadKeyField(PreferredPath(), SecretProtector.Create(), FieldName, out key);
        }

        public static bool TryRead(string path, ISecretProtector protector, out string key)
        {
            return ApiKeyFile.TryReadKeyField(path, protector, FieldName, out key);
        }

        public static void Delete()
        {
            Delete(PreferredPath());
        }

        public static void Delete(string path)
        {
            ApiKeyFile.Delete(path);
        }

        public static bool TryEnvironment(out string key)
        {
            key = Environment.GetEnvironmentVariable(EnvName);
            if (string.IsNullOrWhiteSpace(key))
            {
                key = null;
                return false;
            }

            key = key.Trim();
            return true;
        }
    }

    /// <summary>
    /// Resolves the two possible Pisara keys separately. The chooser decides whether the Grok
    /// key may be used for this host; the provider only ever receives the chosen key.
    /// </summary>
    public sealed class PisaraApiKeySource
    {
        readonly Func<string, string> _readEnvironment;
        readonly Func<string> _path;
        readonly ISecretProtector _protector;
        readonly IApiKeySource _grok;

        public PisaraApiKeySource(
            Func<string, string> readEnvironment = null,
            Func<string> path = null,
            ISecretProtector protector = null,
            IApiKeySource grok = null)
        {
            _readEnvironment = readEnvironment ?? Environment.GetEnvironmentVariable;
            _path = path ?? PisaraApiKeyStore.PreferredPath;
            _protector = protector ?? SecretProtector.Create();
            _grok = grok ?? new EnvironmentApiKeySource();
        }

        public bool TryGetOwn(out string key)
        {
            key = _readEnvironment != null ? _readEnvironment(PisaraApiKeyStore.EnvName) : null;
            if (!string.IsNullOrWhiteSpace(key))
            {
                key = key.Trim();
                return true;
            }

            var path = _path != null ? _path() : null;
            return PisaraApiKeyStore.TryRead(path, _protector, out key);
        }

        public bool TryGetGrok(out string key)
        {
            key = null;
            return _grok != null && _grok.TryGet(out key) && !string.IsNullOrWhiteSpace(key);
        }

        public bool HasOwnKey()
        {
            return TryGetOwn(out _);
        }
    }
}
