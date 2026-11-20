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
        public string kind = "event";
        public string why = string.Empty;
        public string canonicalKey = string.Empty;
        public string tags = string.Empty;
        public string source = "session";
        public bool isPrivate;
        public bool resolved;
        public int recallCount;
        public string lastRecalledAtUtc = string.Empty;

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
        public const int RecentRecallMinutes = 10;

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
        public int AddSession(IReadOnlyList<string> summaries, bool privateMode, DateTime nowUtc, MemoryStore existingFacts = null)
        {
            if (summaries == null || summaries.Count == 0 || string.IsNullOrEmpty(_path))
                return 0;

            var added = 0;
            for (var i = 0; i < summaries.Count; i++)
            {
                var clean = Clean(summaries[i]);
                if (clean.Length == 0 || !MemoryDurability.IsDurable(clean) || Duplicate(clean, existingFacts))
                    continue;

                var kind = MemoryDurability.KindOf(clean);
                _file.entries.Add(new TimelineEntry
                {
                    id = Guid.NewGuid().ToString("N"),
                    createdAtUtc = nowUtc.ToString("o"),
                    happenedAtUtc = nowUtc.ToString("o"),
                    title = TitleOf(clean),
                    summary = clean.Length > SummaryLength ? clean.Substring(0, SummaryLength).TrimEnd() : clean,
                    importance = ImportanceOf(clean, kind),
                    kind = kind,
                    why = MemoryDurability.Why(kind),
                    canonicalKey = MemoryDurability.CanonicalKey(clean),
                    tags = kind,
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

        /// <summary>
        /// Finds the few dated entries relevant to this turn. Matches are keyword-based for
        /// now, then weighted by importance and recency. Private entries never reach a cloud
        /// prompt. Entries already covered by a durable fact are skipped.
        /// </summary>
        public IReadOnlyList<TimelineEntry> Search(string query, int max, bool includePrivate, IReadOnlyList<MemoryEntry> facts = null)
        {
            var result = new List<TimelineEntry>();
            var tokens = QueryTokens(query);
            if (tokens.Count == 0 || max <= 0)
                return result;

            var scored = new List<ScoredEntry>();
            var now = DateTime.UtcNow;
            for (var i = 0; i < _file.entries.Count; i++)
            {
                var entry = _file.entries[i];
                if (entry == null || !entry.IsValid || entry.resolved)
                    continue;
                if (entry.isPrivate && !includePrivate)
                    continue;
                if (RecentlyRecalled(entry, now))
                    continue;
                if (CoveredByFact(entry, facts))
                    continue;

                var matches = 0;
                var haystack = (entry.title + " " + entry.summary + " " + entry.kind).ToLowerInvariant();
                for (var t = 0; t < tokens.Count; t++)
                {
                    if (haystack.IndexOf(tokens[t], StringComparison.Ordinal) >= 0)
                        matches++;
                }

                if (matches == 0)
                    continue;

                DateTime happened;
                var ageDays = DateTime.TryParse(entry.happenedAtUtc, out happened)
                    ? Math.Max(0d, (now - happened.ToUniversalTime()).TotalDays)
                    : 365d;
                var recency = Math.Max(0d, 1d - ageDays / 60d);
                var score = matches * 3f + entry.importance * 0.4f + (float)recency;
                scored.Add(new ScoredEntry { Entry = entry, Score = score });
            }

            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (var i = 0; i < scored.Count && result.Count < max; i++)
                result.Add(scored[i].Entry);
            return result;
        }

        /// <summary>Records that these entries were actually put in front of the model.</summary>
        public void MarkRecalled(IReadOnlyList<TimelineEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;
            var now = DateTime.UtcNow.ToString("o");
            var changed = false;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;
                entry.recallCount++;
                entry.lastRecalledAtUtc = now;
                changed = true;
            }

            if (changed)
                Save();
        }

        static bool CoveredByFact(TimelineEntry entry, IReadOnlyList<MemoryEntry> facts)
        {
            if (facts == null)
                return false;
            for (var i = 0; i < facts.Count; i++)
            {
                var fact = facts[i];
                if (fact != null && fact.kind == MemoryKind.Fact && MemoryDurability.Similar(fact.text, entry.summary))
                    return true;
            }

            return false;
        }

        static bool RecentlyRecalled(TimelineEntry entry, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(entry.lastRecalledAtUtc))
                return false;
            DateTime last;
            if (!DateTime.TryParse(entry.lastRecalledAtUtc, out last))
                return false;
            return nowUtc - last.ToUniversalTime() < TimeSpan.FromMinutes(RecentRecallMinutes);
        }

        static List<string> QueryTokens(string query)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(query))
                return tokens;
            var parts = query.ToLowerInvariant().Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ';', ':', '\'', '"' },
                StringSplitOptions.RemoveEmptyEntries);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (part.Length >= 4 && seen.Add(part))
                    tokens.Add(part);
            }

            return tokens;
        }

        struct ScoredEntry
        {
            public TimelineEntry Entry;
            public float Score;
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

        bool Duplicate(string summary, MemoryStore existingFacts)
        {
            for (var i = 0; i < _file.entries.Count; i++)
            {
                var entry = _file.entries[i];
                if (entry != null && MemoryDurability.Similar(entry.summary, summary))
                    return true;
            }

            if (existingFacts != null)
            {
                for (var i = 0; i < existingFacts.Count; i++)
                {
                    var fact = existingFacts.Entries[i];
                    if (fact != null && fact.kind == MemoryKind.Fact && MemoryDurability.Similar(fact.text, summary))
                        return true;
                }
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

        static int ImportanceOf(string summary, string kind)
        {
            if (kind == MemoryDurability.Boundary || kind == MemoryDurability.Promise)
                return 4;
            if (kind == MemoryDurability.RelationshipChange || kind == MemoryDurability.OpenThread)
                return 3;
            return summary.Length > 140 ? 3 : 2;
        }
    }
}
