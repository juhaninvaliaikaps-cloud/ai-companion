using System;

namespace AICompanion.Core
{
    /// <summary>
    /// Which files under the ProjectLeia folder are private app data.
    /// Paths use forward slashes and are relative to that folder.
    /// </summary>
    public static class LocalDataInventory
    {
        public static string Normalize(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;
            var text = relativePath.Trim().Replace('\\', '/');
            while (text.StartsWith("/", StringComparison.Ordinal))
                text = text.Substring(1);
            return text.ToLowerInvariant();
        }

        public static bool IsEncryptedFile(string relativePath)
        {
            var rel = Normalize(relativePath);
            if (rel.StartsWith("personas/", StringComparison.Ordinal) && rel.EndsWith(".json", StringComparison.Ordinal))
            {
                var slash = rel.IndexOf('/', "personas/".Length);
                return slash < 0;
            }

            if (rel.IndexOf('/') >= 0)
                return false;

            return rel == "settings.json"
                || rel == "personality.json"
                || IsMemoryName(rel)
                || IsHistoryName(rel)
                || IsMoodName(rel);
        }

        public static bool IsSecretFile(string relativePath)
        {
            return Normalize(relativePath) == "secrets.json";
        }

        public static bool IsRawCopy(string relativePath)
        {
            var rel = Normalize(relativePath);
            return rel.StartsWith("portraits/", StringComparison.Ordinal) && !rel.EndsWith("/", StringComparison.Ordinal);
        }

        public static bool IsExcluded(string relativePath)
        {
            var rel = Normalize(relativePath);
            var name = rel;
            var slash = rel.LastIndexOf('/');
            if (slash >= 0)
                name = rel.Substring(slash + 1);
            return name == InstallKeyFile.FileName
                || name.IndexOf(".unreadable-", StringComparison.Ordinal) >= 0
                || name.EndsWith(".writing", StringComparison.Ordinal)
                || name.EndsWith(".held", StringComparison.Ordinal);
        }

        public static bool IsMemoryName(string fileName)
        {
            var name = Normalize(fileName);
            if (name == "memory.json")
                return true;
            return name.StartsWith("memory-", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal);
        }

        /// <summary>Her mood per persona (JUH-103): mood.json and mood-&lt;persona&gt;.json.</summary>
        public static bool IsMoodName(string fileName)
        {
            var name = Normalize(fileName);
            if (name == "mood.json")
                return true;
            return name.StartsWith("mood-", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal);
        }

        public static bool IsHistoryName(string fileName)
        {
            var name = Normalize(fileName);
            if (name == "chat-history.json")
                return true;
            return name.StartsWith("chat-history-", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal);
        }
    }
}
