using System;
using System.IO;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>
    /// The raw end-of-session request, written before the model call so a slow local model
    /// or a quit cannot lose the session. It is encrypted, single-slot, and processed on the
    /// next start if the first attempt did not finish.
    /// </summary>
    [Serializable]
    public sealed class PendingSession
    {
        public string id = string.Empty;
        public string createdAtUtc = string.Empty;
        public string transcript = string.Empty;
        public string herName = string.Empty;
        public string providerId = string.Empty;
        public string memoryPath = string.Empty;
        public string why = string.Empty;
        public int userMessages;
        public bool keepsLocal;
        public bool intimateActive;

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(transcript); }
        }
    }

    public static class SessionHandoff
    {
        public const string FileName = "pending-session.json";

        public static string Path
        {
            get
            {
                var folder = MemoryPaths.Folder();
                return string.IsNullOrEmpty(folder) ? null : System.IO.Path.Combine(folder, FileName);
            }
        }

        public static PendingSession Save(string transcript, string herName, string providerId,
            MemoryStore store, string why, int userMessages, bool keepsLocal, bool intimateActive)
        {
            return SaveAt(Path, transcript, herName, providerId, store, why, userMessages, keepsLocal, intimateActive);
        }

        public static PendingSession SaveAt(string path, string transcript, string herName, string providerId,
            MemoryStore store, string why, int userMessages, bool keepsLocal, bool intimateActive)
        {
            if (string.IsNullOrWhiteSpace(transcript) || store == null || string.IsNullOrEmpty(store.Path))
                return null;

            var pending = new PendingSession
            {
                id = Guid.NewGuid().ToString("N"),
                createdAtUtc = DateTime.UtcNow.ToString("o"),
                transcript = transcript,
                herName = herName ?? string.Empty,
                providerId = providerId ?? string.Empty,
                memoryPath = store.Path,
                why = why ?? string.Empty,
                userMessages = userMessages,
                keepsLocal = keepsLocal,
                intimateActive = intimateActive,
            };

            if (!ProtectedLocalFile.Write(path, JsonUtility.ToJson(pending, true)))
            {
                Debug.LogWarning("Session handoff: pending session could not be saved.");
                return null;
            }

            return pending;
        }

        public static PendingSession Load()
        {
            return LoadAt(Path);
        }

        public static PendingSession LoadAt(string path)
        {
            var read = ProtectedLocalFile.Read(path);
            if (read.Status != LocalReadStatus.Ready || string.IsNullOrWhiteSpace(read.Text))
                return null;
            try
            {
                var pending = JsonUtility.FromJson<PendingSession>(read.Text);
                if (pending == null || !pending.IsValid)
                {
                    ClearFile(path);
                    return null;
                }

                return pending;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session handoff: pending session could not be read (" + e.GetType().Name + ")");
                ClearFile(path);
                return null;
            }
        }

        public static void Clear(PendingSession pending)
        {
            ClearAt(pending, Path);
        }

        public static void ClearAt(PendingSession pending, string path)
        {
            if (pending == null)
                return;
            var current = LoadAt(path);
            if (current == null || !string.Equals(current.id, pending.id, StringComparison.Ordinal))
                return;
            ClearFile(path);
        }

        public static void ClearAll()
        {
            ClearFile(Path);
        }

        static void ClearFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session handoff: pending session could not be cleared (" + e.GetType().Name + ")");
            }
        }
    }
}
