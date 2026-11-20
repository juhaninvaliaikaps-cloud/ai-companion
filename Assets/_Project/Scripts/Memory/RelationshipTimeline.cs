using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>
    /// One dated entry in the shared relationship timeline. This is deliberately separate
    /// from <see cref="MemoryEntry"/>: Pisara reseeding and the 14-day fact fade must never
    /// delete the relationship history.
    /// </summary>
    [Serializable]
    public sealed class TimelineEntry
    {
        public string id = string.Empty;
        public string createdAtUtc = string.Empty;
        public string happenedAtUtc = string.Empty;
        public string title = string.Empty;
        public string summary = string.Empty;
        public int importance = 2;
        public string tags = string.Empty;
        public string source = "session";
        public bool isPrivate;
        public bool resolved;

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(summary); }
        }
    }

    [Serializable]
    public sealed class TimelineFile
    {
        public int version = 1;
        public List<TimelineEntry> entries = new List<TimelineEntry>();
    }

    /// <summary>
    /// The local, encrypted relationship timeline for one persona. Phase 1 writes dated
    /// session summaries and exposes them to the Profile viewer; retrieval injection is a
    /// later phase. The path is derived from the memory file so both stay persona-scoped.
    /// </summary>
    public sealed class RelationshipTimeline
    {
        public const int MaxEntries = 1000;
        public const int TitleLength = 72;
        public const int SummaryLength = 280;

        readonly string _path;
        readonly TimelineFile _file;

        RelationshipTimeline(string path, TimelineFile file)
        {
            _path = path;
            _file = file ?? new TimelineFile();
            if (_file.entries == null)
                _file.entries = new List<TimelineEntry>();
        }

        public string Path
        {
            get { return _path; }
        }

        public IReadOnlyList<TimelineEntry> Entries
        {
            get { return _file.entries; }
        }

        public int Count
        {
            get { return _file.entries.Count; }
        }

        public static RelationshipTimeline Load(string path)
        {
            var read = ProtectedLocalFile.Read(path);
            if (read.Status != LocalReadStatus.Ready || string.IsNullOrWhiteSpace(read.Text))
                return new RelationshipTimeline(path, new TimelineFile());

            try
            {
                var file = JsonUtility.FromJson<TimelineFile>(read.Text);
                if (file == null)
                {
                    ProtectedLocalFile.Quarantine(path);
                    Debug.LogWarning("Relationship timeline could not be read, starting empty.");
                    return new RelationshipTimeline(path, new TimelineFile());
                }

                return new RelationshipTimeline(path, file);
            }
            catch (Exception e)
            {
                ProtectedLocalFile.Quarantine(path);
                Debug.LogWarning("Relationship timeline could not be read, starting empty: " + e.GetType().Name);
                return new RelationshipTimeline(path, new TimelineFile());
            }
        }

        /// <summary>The timeline that belongs to the same persona as a memory store.</summary>
        public static RelationshipTimeline ForStore(MemoryStore store)
        {
            return Load(MemoryPaths.TimelineFileForMemoryPath(store != null ? store.Path : null));
        }

        /// <summary>
        /// Turns the session summary lines into dated entries. Returns how many new entries
        /// were added. Existing identical summaries are skipped, so a retry cannot duplicate
        /// a session.
        /// </summary>
        public int AddSession(IReadOnlyList<string> summaries, bool privateMode, DateTime nowUtc)
        {
            if (summaries == null || summaries.Count == 0 || string.IsNullOrEmpty(_path))
                return 0;

            var added = 0;
            for (var i = 0; i < summaries.Count; i++)
            {
                var clean = Clean(summaries[i]);
                if (clean.Length == 0 || Duplicate(clean))
                    continue;

                _file.entries.Add(new TimelineEntry
                {
                    id = Guid.NewGuid().ToString("N"),
                    createdAtUtc = nowUtc.ToString("o"),
                    happenedAtUtc = nowUtc.ToString("o"),
                    title = TitleOf(clean),
                    summary = clean.Length > SummaryLength ? clean.Substring(0, SummaryLength).TrimEnd() : clean,
                    importance = ImportanceOf(clean),
                    tags = "session",
                    source = "session",
                    isPrivate = privateMode,
                });
                added++;
            }

            if (added > 0)
            {
                if (_file.entries.Count > MaxEntries)
                    _file.entries.RemoveRange(0, _file.entries.Count - MaxEntries);
                Save();
            }

            return added;
        }

        /// <summary>Newest first, for the Profile viewer.</summary>
        public IReadOnlyList<TimelineEntry> Page(int max)
        {
            var result = new List<TimelineEntry>();
            var sorted = new List<TimelineEntry>();
            for (var i = 0; i < _file.entries.Count; i++)
            {
                if (_file.entries[i] != null && _file.entries[i].IsValid)
                    sorted.Add(_file.entries[i]);
            }

            sorted.Sort((a, b) => string.CompareOrdinal(b.happenedAtUtc, a.happenedAtUtc));
            for (var i = 0; i < sorted.Count && (max <= 0 || result.Count < max); i++)
                result.Add(sorted[i]);
            return result;
        }

        public bool Forget(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            for (var i = 0; i < _file.entries.Count; i++)
            {
                if (_file.entries[i] != null && string.Equals(_file.entries[i].id, id, StringComparison.Ordinal))
                {
                    _file.entries.RemoveAt(i);
                    Save();
                    return true;
                }
            }

            return false;
        }

        public int Clear()
        {
            var removed = _file.entries.Count;
            if (removed == 0)
                return 0;
            _file.entries.Clear();
            Save();
            return removed;
        }

        void Save()
        {
            if (string.IsNullOrEmpty(_path))
                return;
            try
            {
                ProtectedLocalFile.Write(_path, JsonUtility.ToJson(_file, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Relationship timeline could not be saved: " + e.GetType().Name);
            }
        }

        bool Duplicate(string summary)
        {
            for (var i = 0; i < _file.entries.Count; i++)
            {
                var entry = _file.entries[i];
                if (entry != null && string.Equals(entry.summary, summary, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var value = text.Trim().Replace("\r\n", "\n");
            while (value.StartsWith("-", StringComparison.Ordinal) || value.StartsWith("*", StringComparison.Ordinal))
                value = value.Substring(1).TrimStart();
            return value.Trim();
        }

        static string TitleOf(string summary)
        {
            var line = summary;
            var newline = line.IndexOf('\n');
            if (newline >= 0)
                line = line.Substring(0, newline).Trim();
            if (line.Length <= TitleLength)
                return line;
            var cut = line.LastIndexOf(' ', TitleLength);
            if (cut < 24)
                cut = TitleLength;
            return line.Substring(0, cut).TrimEnd() + "…";
        }

        static int ImportanceOf(string summary)
        {
            var lower = summary.ToLowerInvariant();
            if (lower.IndexOf("promise", StringComparison.Ordinal) >= 0
                || lower.IndexOf("remember", StringComparison.Ordinal) >= 0
                || lower.IndexOf("boundary", StringComparison.Ordinal) >= 0
                || lower.IndexOf("family", StringComparison.Ordinal) >= 0
                || lower.IndexOf("never forget", StringComparison.Ordinal) >= 0)
                return 4;
            return summary.Length > 140 ? 3 : 2;
        }
    }
}
