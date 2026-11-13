using System;
using System.IO;
using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>
    /// The optional bearer token for a user-provided Private endpoint. It lives in its own
    /// encrypted file next to secrets.json and never in settings.json.
    /// </summary>
    public static class PrivateApiKeyStore
    {
        public const string FileName = "private-api-key.json";
        public const string FieldName = "privateApiKey";

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
    }
}
