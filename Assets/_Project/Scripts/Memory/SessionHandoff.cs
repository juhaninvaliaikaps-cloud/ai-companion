using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>
    /// The raw end-of-session request, written before the model call so a slow local model
    /// or a quit cannot lose the session. It is encrypted, one file per session
    /// (pending-session-&lt;session&gt;.json), and processed on the next start if the first
    /// attempt did not finish. JUH-134 review: with one shared file, the next session's rolling
    /// checkpoint overwrote a session whose summary was still running.
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
        /// <summary>JUH-188: "intimate" when the session held an 18+ moment, so it survives a restart.</summary>
        public string momentKind = string.Empty;
        /// <summary>The chat session this belongs to; a checkpoint and the final save share it.</summary>
        public string session = string.Empty;

        /// <summary>The file it was read from or written to. Not saved.</summary>
        [NonSerialized] public string path = string.Empty;

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(transcript); }
        }
    }

    public static class SessionHandoff
    {
        /// <summary>The single slot older builds wrote. Still read and finished on start.</summary>
        public const string FileName = "pending-session.json";
        public const string FilePrefix = "pending-session-";

        public static string Path
        {
            get
            {
                var folder = MemoryPaths.Folder();
                return string.IsNullOrEmpty(folder) ? null : System.IO.Path.Combine(folder, FileName);
            }
        }

        /// <summary>This session's own file. Letters and digits only, so an id cannot leave the folder.</summary>
        public static string PathFor(string folder, string sessionId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var safe = new System.Text.StringBuilder();
            if (sessionId != null)
            {
                for (var i = 0; i < sessionId.Length; i++)
                {
                    if (char.IsLetterOrDigit(sessionId[i]) && sessionId[i] < 128)
                        safe.Append(sessionId[i]);
                }
            }

            return System.IO.Path.Combine(folder, safe.Length == 0 ? FileName : FilePrefix + safe + ".json");
        }

        /// <summary>Saves or replaces this session's handoff. Another session's file is left alone.</summary>
        public static PendingSession Save(string sessionId, string transcript, string herName, string providerId,
            MemoryStore store, string why, int userMessages, bool keepsLocal, bool intimateActive, string momentKind = null)
        {
            var pending = SaveAt(PathFor(MemoryPaths.Folder(), sessionId), transcript, herName, providerId, store,
                why, userMessages, keepsLocal, intimateActive, sessionId, momentKind);
            return pending;
        }

        public static PendingSession SaveAt(string path, string transcript, string herName, string providerId,
            MemoryStore store, string why, int userMessages, bool keepsLocal, bool intimateActive, string sessionId = null,
            string momentKind = null)
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
                momentKind = momentKind ?? string.Empty,
                session = sessionId ?? string.Empty,
                path = path ?? string.Empty,
            };

            if (!ProtectedLocalFile.Write(path, JsonUtility.ToJson(pending, true)))
            {
                Debug.LogWarning("Session handoff: pending session could not be saved.");
                return null;
            }

            return pending;
        }

        /// <summary>Every session waiting to be finished, oldest first (the old single slot too).</summary>
        public static List<PendingSession> LoadAll()
        {
            return LoadAllIn(MemoryPaths.Folder());
        }

        public static List<PendingSession> LoadAllIn(string folder)
        {
            var result = new List<PendingSession>();
            var paths = PendingPaths(folder);
            for (var i = 0; i < paths.Count; i++)
            {
                var pending = LoadAt(paths[i]);
                if (pending != null)
                    result.Add(pending);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.createdAtUtc, b.createdAtUtc));
            return result;
        }

        static List<string> PendingPaths(string folder)
        {
            var paths = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return paths;
            try
            {
                var files = Directory.GetFiles(folder, "pending-session*.json");
                for (var i = 0; i < files.Length; i++)
                {
                    if (LocalDataInventory.IsPendingSessionName(System.IO.Path.GetFileName(files[i])))
                        paths.Add(files[i]);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session handoff: pending sessions could not be listed (" + e.GetType().Name + ")");
            }

            return paths;
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

                pending.path = path;
                return pending;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session handoff: pending session could not be read (" + e.GetType().Name + ")");
                ClearFile(path);
                return null;
            }
        }

        /// <summary>Removes the file this handoff came from, unless a newer save replaced it.</summary>
        public static void Clear(PendingSession pending)
        {
            if (pending == null)
                return;
            ClearAt(pending, string.IsNullOrEmpty(pending.path) ? Path : pending.path);
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
            var paths = PendingPaths(MemoryPaths.Folder());
            for (var i = 0; i < paths.Count; i++)
                ClearFile(paths[i]);
        }

        /// <summary>Removes the old single-file checkpoint for one session before split saves replace it.</summary>
        public static void ClearSession(string sessionId)
        {
            var folder = MemoryPaths.Folder();
            ClearFile(PathFor(folder, sessionId));
            ClearFile(PathFor(folder, sessionId + "pub"));
            ClearFile(PathFor(folder, sessionId + "prv"));
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
