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
        public int confidence = 60;
        public string kind = "event";
        public string why = string.Empty;
        public string canonicalKey = string.Empty;
        public string tags = string.Empty;
        public string source = "session";
        public bool isPrivate;
        public bool resolved;
        public int recallCount;
        public string lastRecalledAtUtc = string.Empty;
        public int evidenceCount = 1;
        public string lastSeenAtUtc = string.Empty;

        /// <summary>JUH-141: the sentence before the last correction, and when it was corrected.</summary>
        public string previousSummary = string.Empty;
        public string updatedAtUtc = string.Empty;

        /// <summary>JUH-141: when a session summary closed this promise or thread.</summary>
        public string resolvedAtUtc = string.Empty;

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
        FileStamp _stamp;

        // JUH-134 review: one instance per persona file. Loading decrypted up to 1000 entries
        // twice per exchange, and two instances could overwrite each other's new entries.
        static readonly object CacheGate = new object();
        static readonly Dictionary<string, RelationshipTimeline> Cache =
            new Dictionary<string, RelationshipTimeline>(StringComparer.OrdinalIgnoreCase);

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

        /// <summary>A fresh instance read from disk. Chat code uses <see cref="ForStore"/>.</summary>
        public static RelationshipTimeline Load(string path)
        {
            var timeline = LoadFresh(path);
            timeline._stamp = FileStamp.Of(path);
            return timeline;
        }

        static RelationshipTimeline LoadFresh(string path)
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

        /// <summary>
        /// The timeline that belongs to the same persona as a memory store. The same instance
        /// is returned while its file is unchanged; a file written by anything else (another
        /// instance, a wipe, an import) is read again.
        /// </summary>
        public static RelationshipTimeline ForStore(MemoryStore store)
        {
            return Shared(MemoryPaths.TimelineFileForMemoryPath(store != null ? store.Path : null));
        }

        public static RelationshipTimeline Shared(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new RelationshipTimeline(path, new TimelineFile());

            lock (CacheGate)
            {
                RelationshipTimeline cached;
                if (Cache.TryGetValue(path, out cached) && cached._stamp.Equals(FileStamp.Of(path)))
                    return cached;
                var loaded = Load(path);
                Cache[path] = loaded;
                return loaded;
            }
        }

        /// <summary>
        /// Turns the session summary lines into dated entries. Returns how many new entries
        /// were added. Existing identical summaries are skipped, so a retry cannot duplicate
        /// a session.
        /// </summary>
        public int AddSession(IReadOnlyList<string> summaries, bool privateMode, DateTime nowUtc, MemoryStore existingFacts = null,
            IReadOnlyList<SessionMemory> details = null)
        {
            if (summaries == null || summaries.Count == 0 || string.IsNullOrEmpty(_path))
                return 0;

            var added = 0;
            var updated = 0;
            for (var i = 0; i < summaries.Count; i++)
            {
                var clean = Clean(summaries[i]);
                if (clean.Length == 0 || !MemoryDurability.IsDurable(clean))
                    continue;

                var detail = details != null && i < details.Count ? details[i] : null;
                var kind = detail != null && detail.HasMetadata ? detail.Kind : MemoryDurability.KindOf(clean);
                var importance = detail != null && detail.HasMetadata ? detail.Importance : ImportanceOf(clean, kind);
                var confidence = detail != null && detail.HasMetadata ? detail.Confidence : 60;

                var existing = FindSimilar(clean);
                if (existing != null)
                {
                    Merge(existing, clean, privateMode, nowUtc);
                    existing.confidence = Math.Max(existing.confidence, confidence);
                    updated++;
                    continue;
                }

                if (CoveredByFact(clean, existingFacts))
                    continue;

                _file.entries.Add(new TimelineEntry
                {
                    id = Guid.NewGuid().ToString("N"),
                    createdAtUtc = nowUtc.ToString("o"),
                    happenedAtUtc = nowUtc.ToString("o"),
                    lastSeenAtUtc = nowUtc.ToString("o"),
                    title = TitleOf(clean),
                    summary = clean.Length > SummaryLength ? clean.Substring(0, SummaryLength).TrimEnd() : clean,
                    importance = importance,
                    confidence = confidence,
                    kind = kind,
                    why = MemoryDurability.Why(kind),
                    canonicalKey = MemoryDurability.CanonicalKey(clean),
                    tags = kind,
                    source = "session",
                    isPrivate = privateMode,
                    evidenceCount = 1,
                });
                added++;
            }

            if (added > 0 || updated > 0)
            {
                if (_file.entries.Count > MaxEntries)
                    _file.entries.RemoveRange(0, _file.entries.Count - MaxEntries);
                Save();
            }

            return added;
        }

        /// <summary>What <see cref="ApplySession"/> changed.</summary>
        public struct SessionResult
        {
            public int Added;
            public int Updated;
            public int Resolved;
        }

        /// <summary>
        /// JUH-141: applies a whole session summary: new lines (<see cref="AddSession"/>), then
        /// the corrections ("update m1: …") and the closed promises and threads ("resolved m2").
        /// A correction must itself be a durable, concrete sentence; it keeps the entry's id and
        /// date, remembers the old sentence, and counts as one more mention. A correction made in
        /// a private session makes the entry private.
        /// </summary>
        public SessionResult ApplySession(SessionCapture capture, bool privateMode, DateTime nowUtc, MemoryStore existingFacts = null)
        {
            var result = new SessionResult();
            if (capture == null || string.IsNullOrEmpty(_path))
                return result;
            result.Added = AddSession(capture.Memories, privateMode, nowUtc, existingFacts, capture.Details);

            var changed = false;
            var now = nowUtc.ToString("o");
            for (var i = 0; i < capture.Updates.Count; i++)
            {
                var update = capture.Updates[i];
                var entry = FindById(update.EntryId);
                var text = update.Memory != null ? Clean(update.Memory.Text) : string.Empty;
                if (entry == null || text.Length == 0 || !MemoryDurability.IsDurable(text))
                    continue;
                if (string.Equals(text, entry.summary, StringComparison.Ordinal))
                    continue;
                entry.previousSummary = entry.summary;
                entry.summary = text.Length > SummaryLength ? text.Substring(0, SummaryLength).TrimEnd() : text;
                entry.title = TitleOf(entry.summary);
                entry.canonicalKey = MemoryDurability.CanonicalKey(entry.summary);
                if (update.Memory.HasMetadata)
                {
                    entry.kind = update.Memory.Kind;
                    entry.importance = update.Memory.Importance;
                    entry.confidence = update.Memory.Confidence;
                    entry.why = MemoryDurability.Why(entry.kind);
                }

                entry.updatedAtUtc = now;
                entry.lastSeenAtUtc = now;
                entry.evidenceCount = Math.Max(1, entry.evidenceCount) + 1;
                entry.isPrivate |= privateMode;
                entry.resolved = false;
                result.Updated++;
                changed = true;
            }

            for (var i = 0; i < capture.Resolved.Count; i++)
            {
                var entry = FindById(capture.Resolved[i]);
                if (entry == null || entry.resolved)
                    continue;
                entry.resolved = true;
                entry.resolvedAtUtc = now;
                entry.lastSeenAtUtc = now;
                result.Resolved++;
                changed = true;
            }

            if (changed)
                Save();
            return result;
        }

        TimelineEntry FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (var i = 0; i < _file.entries.Count; i++)
            {
                var entry = _file.entries[i];
                if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Stores an explicit "remember this" sentence. Unlike a session summary this is
        /// user-requested, so it bypasses the scene filter and starts at the highest
        /// importance.
        /// </summary>
        public int AddExplicit(string text, bool privateMode, DateTime nowUtc)
        {
            var clean = Clean(text);
            if (clean.Length == 0 || string.IsNullOrEmpty(_path))
                return 0;

            var existing = FindSimilar(clean);
            if (existing != null)
            {
                Merge(existing, clean, privateMode, nowUtc);
                existing.importance = 5;
                existing.confidence = 100;
                existing.source = "explicit";
                existing.why = "The user explicitly asked her to remember this.";
                Save();
                return 0;
            }

            var kind = MemoryDurability.KindOf(clean);
            _file.entries.Add(new TimelineEntry
            {
                id = Guid.NewGuid().ToString("N"),
                createdAtUtc = nowUtc.ToString("o"),
                happenedAtUtc = nowUtc.ToString("o"),
                lastSeenAtUtc = nowUtc.ToString("o"),
                title = TitleOf(clean),
                summary = clean.Length > SummaryLength ? clean.Substring(0, SummaryLength).TrimEnd() : clean,
                importance = 5,
                confidence = 100,
                kind = kind,
                why = "The user explicitly asked her to remember this.",
                canonicalKey = MemoryDurability.CanonicalKey(clean),
                tags = kind,
                source = "explicit",
                isPrivate = privateMode,
                evidenceCount = 1,
            });
            Save();
            return 1;
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
            var hits = SearchHits(query, string.Empty, max, includePrivate, DateTime.UtcNow, facts);
            var result = new List<TimelineEntry>(hits.Count);
            for (var i = 0; i < hits.Count; i++)
                result.Add(hits[i].Entry);
            return result;
        }

        /// <summary>
        /// JUH-141: the entries for this turn, ranked by <see cref="TimelineSearch"/> (BM25,
        /// stop words, stemming, concepts, trust). <paramref name="previous"/> is the user
        /// message before the latest one, at half weight. An entry a retrieved fact already
        /// says is left out. One she brought up in the last few minutes stays only while the
        /// talk is still plainly about it, marked <see cref="TimelineHit.AlreadyRaised"/>.
        /// </summary>
        public List<TimelineHit> SearchHits(string latest, string previous, int max, bool includePrivate, DateTime nowUtc,
            IReadOnlyList<MemoryEntry> facts = null, Func<TimelineEntry, float> semantic = null)
        {
            return TimelineSearch.Rank(_file.entries, latest, previous, includePrivate, nowUtc, max,
                facts == null ? (Predicate<TimelineEntry>)null : entry => CoveredByFact(entry, facts), semantic);
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
            MemoryRuntime.LinksChanged();
            if (string.IsNullOrEmpty(_path))
                return;
            try
            {
                ProtectedLocalFile.Write(_path, JsonUtility.ToJson(_file, true));
                _stamp = FileStamp.Of(_path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Relationship timeline could not be saved: " + e.GetType().Name);
            }
        }

        TimelineEntry FindSimilar(string summary)
        {
            for (var i = 0; i < _file.entries.Count; i++)
            {
                var entry = _file.entries[i];
                if (entry != null && MemoryDurability.Similar(entry.summary, summary))
                    return entry;
            }

            return null;
        }

        static void Merge(TimelineEntry existing, string candidate, bool privateMode, DateTime nowUtc)
        {
            existing.lastSeenAtUtc = nowUtc.ToString("o");
            existing.evidenceCount++;
            existing.isPrivate = existing.isPrivate || privateMode;
            existing.confidence = Math.Max(existing.confidence, 60);

            var kind = MemoryDurability.KindOf(candidate);
            if (ImportanceOf(candidate, kind) > existing.importance)
                existing.importance = ImportanceOf(candidate, kind);
            if (KindRank(kind) > KindRank(existing.kind))
            {
                existing.kind = kind;
                existing.why = MemoryDurability.Why(kind);
            }

            // Prefer the more specific wording. A later paraphrase that only adds a vague
            // "something" must not replace a concrete earlier sentence.
            if (!MemoryDurability.IsVague(candidate) && candidate.Length > existing.summary.Length)
            {
                existing.summary = candidate.Length > SummaryLength
                    ? candidate.Substring(0, SummaryLength).TrimEnd()
                    : candidate;
                existing.title = TitleOf(existing.summary);
                existing.canonicalKey = MemoryDurability.CanonicalKey(existing.summary);
            }
        }

        static int KindRank(string kind)
        {
            switch (kind)
            {
                case MemoryDurability.Boundary: return 5;
                case MemoryDurability.Promise: return 4;
                case MemoryDurability.RelationshipChange: return 3;
                case MemoryDurability.Preference: return 2;
                case MemoryDurability.Fact: return 2;
                case MemoryDurability.OpenThread: return 1;
                default: return 0;
            }
        }

        static bool CoveredByFact(string summary, MemoryStore store)
        {
            if (store == null)
                return false;
            for (var i = 0; i < store.Count; i++)
            {
                var fact = store.Entries[i];
                if (fact != null && fact.kind == MemoryKind.Fact && MemoryDurability.Similar(fact.text, summary))
                    return true;
            }

            return false;
        }

        static bool CoveredByFact(string summary, IReadOnlyList<MemoryEntry> facts)
        {
            if (facts == null)
                return false;
            for (var i = 0; i < facts.Count; i++)
            {
                var fact = facts[i];
                if (fact != null && fact.kind == MemoryKind.Fact && MemoryDurability.Similar(fact.text, summary))
                    return true;
            }

            return false;
        }

        /// <summary>Whether a file is still the one an instance last read or wrote.</summary>
        struct FileStamp : IEquatable<FileStamp>
        {
            bool _exists;
            long _ticks;
            long _length;

            public static FileStamp Of(string path)
            {
                var stamp = new FileStamp();
                try
                {
                    var info = new System.IO.FileInfo(path);
                    if (info.Exists)
                    {
                        stamp._exists = true;
                        stamp._ticks = info.LastWriteTimeUtc.Ticks;
                        stamp._length = info.Length;
                    }
                }
                catch (Exception)
                {
                }

                return stamp;
            }

            public bool Equals(FileStamp other)
            {
                return _exists == other._exists && _ticks == other._ticks && _length == other._length;
            }
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
