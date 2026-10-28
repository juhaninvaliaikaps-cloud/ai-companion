using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>
    /// Where memory lives. This is deliberately the same per-user folder as the
    /// settings and the API key, so nothing memory-related can land in the repository
    /// or next to the built player.
    /// </summary>
    public static class MemoryPaths
    {
        public const string FolderName = "ProjectLeia";
        public const string MemoryFileName = "memory.json";
        public const string PersonalityFileName = "personality.json";

        public static string Folder()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
                return Path.Combine(appData, FolderName);

            var persistent = Application.persistentDataPath;
            if (!string.IsNullOrEmpty(persistent))
                return Path.Combine(persistent, FolderName);

            return null;
        }

        public static string MemoryFile()
        {
            var folder = Folder();
            return folder == null ? null : Path.Combine(folder, MemoryFileName);
        }

        public static string PersonalityFile()
        {
            var folder = Folder();
            return folder == null ? null : Path.Combine(folder, PersonalityFileName);
        }
    }

    /// <summary>
    /// Facts and conversation summaries kept on this machine, plus keyword retrieval.
    /// The store never throws at the caller: a corrupt or unreadable file behaves like
    /// an empty store, and a failed write logs a warning instead of breaking chat.
    /// </summary>
    public sealed class MemoryStore
    {
        public const int MaxSearchResults = 8;
        public const int MinTokenLength = 3;

        static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "with", "that", "this", "you", "your", "are", "was", "were",
            "have", "has", "had", "but", "not", "all", "can", "did", "does", "from", "how",
            "what", "when", "where", "who", "why", "will", "would", "there", "their", "them",
            "mutta", "että", "kun", "niin", "joka", "tämä", "tuo", "olen", "olet", "onko",
            "se", "ne", "ja", "tai", "eli", "kun", "nyt", "vielä", "hyvin", "kanssa"
        };

        readonly string _path;
        readonly MemoryFile _file;

        MemoryStore(string path, MemoryFile file)
        {
            _path = path;
            _file = file ?? new MemoryFile();
            if (_file.entries == null)
                _file.entries = new List<MemoryEntry>();
        }

        public string Path
        {
            get { return _path; }
        }

        public IReadOnlyList<MemoryEntry> Entries
        {
            get { return _file.entries; }
        }

        public int Count
        {
            get { return _file.entries.Count; }
        }

        /// <summary>Reads memory.json, or starts empty when it is missing or unreadable.</summary>
        public static MemoryStore Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new MemoryStore(path, new MemoryFile());

            try
            {
                var json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<MemoryFile>(json);
                return new MemoryStore(path, file);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Memory file could not be read, starting empty: " + e.GetType().Name);
                return new MemoryStore(path, new MemoryFile());
            }
        }

        public static MemoryStore CreateDefault()
        {
            return Load(MemoryPaths.MemoryFile());
        }

        public MemoryEntry AddFact(string text, bool privateMode)
        {
            return Add(text, MemoryKind.Fact, privateMode);
        }

        public MemoryEntry AddSummary(string text, bool privateMode)
        {
            return Add(text, MemoryKind.Summary, privateMode);
        }

        public MemoryEntry Add(string text, MemoryKind kind, bool privateMode)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var entry = new MemoryEntry
            {
                id = Guid.NewGuid().ToString("N"),
                text = text.Trim(),
                kind = kind,
                createdAtUtc = DateTime.UtcNow.ToString("o"),
                isPrivate = privateMode,
            };

            _file.entries.Add(entry);
            Save();
            return entry;
        }

        public bool Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            for (var i = 0; i < _file.entries.Count; i++)
            {
                if (!string.Equals(_file.entries[i].id, id, StringComparison.OrdinalIgnoreCase))
                    continue;

                _file.entries.RemoveAt(i);
                Save();
                return true;
            }

            return false;
        }

        public int DeleteAll()
        {
            var removed = _file.entries.Count;
            _file.entries.Clear();
            if (removed > 0)
                Save();
            return removed;
        }

        /// <summary>
        /// Keyword search. Every token that appears in an entry adds a point; a summary
        /// counts a little more than a fact because it carries more context. Ties keep
        /// the newest entry first. <paramref name="includePrivate"/> is false for any
        /// cloud provider, which is what keeps private-mode memories off the wire.
        /// </summary>
        public IReadOnlyList<MemoryEntry> Search(string query, int max = MaxSearchResults, bool includePrivate = true)
        {
            var result = new List<MemoryEntry>();
            if (max <= 0)
                return result;

            var tokens = Tokenize(query);
            if (tokens.Count == 0)
                return result;

            var scored = new List<(MemoryEntry entry, int score)>();
            for (var i = 0; i < _file.entries.Count; i++)
            {
                var entry = _file.entries[i];
                if (entry == null || !entry.IsValid)
                    continue;
                if (entry.isPrivate && !includePrivate)
                    continue;

                var lowered = entry.text.ToLowerInvariant();
                var score = 0;
                for (var t = 0; t < tokens.Count; t++)
                {
                    if (lowered.IndexOf(tokens[t], StringComparison.Ordinal) >= 0)
                        score += entry.kind == MemoryKind.Summary ? 2 : 1;
                }

                if (score > 0)
                    scored.Add((entry, score));
            }

            scored.Sort((a, b) =>
            {
                var byScore = b.score.CompareTo(a.score);
                if (byScore != 0)
                    return byScore;
                return string.CompareOrdinal(b.entry.createdAtUtc, a.entry.createdAtUtc);
            });

            for (var i = 0; i < scored.Count && result.Count < max; i++)
                result.Add(scored[i].entry);

            return result;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_path))
                return;

            try
            {
                var folder = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(_path, JsonUtility.ToJson(_file, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Memory could not be saved: " + e.GetType().Name);
            }
        }

        /// <summary>Lowercase words of at least <see cref="MinTokenLength"/>, stop words removed.</summary>
        public static List<string> Tokenize(string query)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(query))
                return tokens;

            var builder = new System.Text.StringBuilder();
            for (var i = 0; i <= query.Length; i++)
            {
                var atEnd = i == query.Length;
                var c = atEnd ? ' ' : query[i];
                if (!atEnd && char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                    continue;
                }

                if (builder.Length >= MinTokenLength)
                {
                    var token = builder.ToString();
                    if (!StopWords.Contains(token) && !tokens.Contains(token))
                        tokens.Add(token);
                }

                builder.Length = 0;
            }

            return tokens;
        }
    }
}
