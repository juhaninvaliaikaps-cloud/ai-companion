using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>
    /// JUH-141: the meaning vectors of one persona's timeline, in memory only. Each vector is
    /// kept with the text it was made from, so an entry whose summary was corrected is embedded
    /// again. Nothing is written to disk; after a restart the vectors are rebuilt in the
    /// background a batch at a time, and until then the search uses words only.
    /// </summary>
    public sealed class EmbeddingIndex
    {
        // nomic-embed-text was trained with these task prefixes; Ollama does not add them.
        public const string QueryPrefix = "search_query: ";
        public const string DocumentPrefix = "search_document: ";

        struct Item
        {
            public string Text;
            public float[] Vector;
        }

        readonly Dictionary<string, Item> _byId = new Dictionary<string, Item>(StringComparer.Ordinal);

        public int Count
        {
            get { return _byId.Count; }
        }

        public static string DocumentText(TimelineEntry entry)
        {
            return DocumentPrefix + (entry.summary ?? string.Empty).Trim();
        }

        /// <summary>The latest message, and the one before it when the latest is short ("what did I tell you?").</summary>
        public static string QueryText(string latest, string previous)
        {
            var text = (latest ?? string.Empty).Trim();
            if (text.Length < 40 && !string.IsNullOrWhiteSpace(previous))
                text = previous.Trim() + " " + text;
            return QueryPrefix + text;
        }

        public bool TryGet(TimelineEntry entry, out float[] vector)
        {
            vector = null;
            Item item;
            if (entry == null || string.IsNullOrEmpty(entry.id) || !_byId.TryGetValue(entry.id, out item))
                return false;
            if (!string.Equals(item.Text, DocumentText(entry), StringComparison.Ordinal))
                return false;
            vector = item.Vector;
            return true;
        }

        public void Put(TimelineEntry entry, float[] vector)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || vector == null || vector.Length == 0)
                return;
            _byId[entry.id] = new Item { Text = DocumentText(entry), Vector = Normalized(vector) };
        }

        /// <summary>Open entries that have no current vector, newest first, at most <paramref name="max"/>.</summary>
        public List<TimelineEntry> Missing(IReadOnlyList<TimelineEntry> entries, int max)
        {
            var missing = new List<TimelineEntry>();
            if (entries == null)
                return missing;
            for (var i = entries.Count - 1; i >= 0 && missing.Count < max; i--)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsValid || entry.resolved)
                    continue;
                float[] vector;
                if (!TryGet(entry, out vector))
                    missing.Add(entry);
            }

            return missing;
        }

        /// <summary>Cosine similarity of two normalized vectors, or NaN when they do not match.</summary>
        public static float Cosine(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0)
                return float.NaN;
            var dot = 0f;
            for (var i = 0; i < a.Length; i++)
                dot += a[i] * b[i];
            return dot;
        }

        public static float[] Normalized(float[] vector)
        {
            var sum = 0d;
            for (var i = 0; i < vector.Length; i++)
                sum += vector[i] * (double)vector[i];
            var length = (float)Math.Sqrt(sum);
            var result = new float[vector.Length];
            if (length <= 0f)
                return result;
            for (var i = 0; i < vector.Length; i++)
                result[i] = vector[i] / length;
            return result;
        }
    }

    /// <summary>
    /// JUH-141: the smarter memory search switch and its local model. Off by default
    /// (Settings → Memory). When it is on and nomic-embed-text answers on this PC, the timeline
    /// search also matches by meaning; otherwise it is the word search, unchanged.
    /// </summary>
    public static class MemoryEmbeddings
    {
        /// <summary>How long a chat request waits for its query vector before using words only.</summary>
        public const float QueryWaitSeconds = 3f;

        /// <summary>Entries embedded per background pass.</summary>
        public const int FillPerPass = 64;

        static readonly object Gate = new object();
        static readonly Dictionary<string, EmbeddingIndex> Indexes = new Dictionary<string, EmbeddingIndex>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> Filling = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static OllamaEmbedder _embedder;
        static string _lastLogged = string.Empty;

        public static bool Enabled { get; private set; }

        public static void Configure(bool enabled)
        {
            if (enabled != Enabled)
                Debug.Log("Memory: smarter search (local " + OllamaEmbedder.DefaultModel + ") " + (enabled ? "on" : "off"));
            Enabled = enabled;
        }

        public static void UseEmbedder(OllamaEmbedder embedder)
        {
            _embedder = embedder;
        }

        /// <summary>For tests: forgets the vectors, the model and the switch.</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                Indexes.Clear();
                Filling.Clear();
            }

            _embedder = null;
            Enabled = false;
            _lastLogged = string.Empty;
        }

        public static EmbeddingIndex IndexFor(string timelinePath)
        {
            var key = timelinePath ?? string.Empty;
            lock (Gate)
            {
                EmbeddingIndex index;
                if (!Indexes.TryGetValue(key, out index))
                {
                    index = new EmbeddingIndex();
                    Indexes[key] = index;
                }

                return index;
            }
        }

        /// <summary>
        /// The meaning score of each entry for this turn (cosine to the query; NaN when unknown),
        /// or null when the smarter search is off or the model is not there. Also starts filling
        /// vectors that are missing, in the background.
        /// </summary>
        public static async Task<Func<TimelineEntry, float>> SemanticFor(RelationshipTimeline timeline, string latest, string previous,
            CancellationToken cancellationToken)
        {
            var embedder = _embedder;
            if (!Enabled || embedder == null || timeline == null || string.IsNullOrWhiteSpace(latest) || !embedder.Ready(DateTime.UtcNow))
                return null;

            var index = IndexFor(timeline.Path);
            Fill(timeline, index, embedder);

            float[][] query;
            using (var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                wait.CancelAfter(TimeSpan.FromSeconds(QueryWaitSeconds));
                query = await embedder.EmbedAsync(new[] { EmbeddingIndex.QueryText(latest, previous) }, wait.Token);
            }

            if (query == null || query.Length != 1)
            {
                LogProblem(embedder);
                return null;
            }

            var vector = EmbeddingIndex.Normalized(query[0]);
            return entry =>
            {
                float[] document;
                return index.TryGet(entry, out document) ? EmbeddingIndex.Cosine(vector, document) : float.NaN;
            };
        }

        /// <summary>Embeds up to <see cref="FillPerPass"/> missing entries, one pass per timeline at a time.</summary>
        static async void Fill(RelationshipTimeline timeline, EmbeddingIndex index, OllamaEmbedder embedder)
        {
            var key = timeline.Path ?? string.Empty;
            lock (Gate)
            {
                if (!Filling.Add(key))
                    return;
            }

            try
            {
                var missing = index.Missing(timeline.Entries, FillPerPass);
                for (var start = 0; start < missing.Count; start += OllamaEmbedder.MaxBatch)
                {
                    var batch = missing.GetRange(start, Math.Min(OllamaEmbedder.MaxBatch, missing.Count - start));
                    var texts = new List<string>(batch.Count);
                    for (var i = 0; i < batch.Count; i++)
                        texts.Add(EmbeddingIndex.DocumentText(batch[i]));
                    var vectors = await embedder.EmbedAsync(texts, CancellationToken.None);
                    if (vectors == null)
                    {
                        LogProblem(embedder);
                        break;
                    }

                    for (var i = 0; i < batch.Count; i++)
                        index.Put(batch[i], vectors[i]);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Memory: smarter search could not embed (" + e.GetType().Name + ")");
            }
            finally
            {
                lock (Gate)
                    Filling.Remove(key);
            }
        }

        static void LogProblem(OllamaEmbedder embedder)
        {
            var problem = embedder.LastProblem;
            if (string.IsNullOrEmpty(problem) || string.Equals(problem, _lastLogged, StringComparison.Ordinal))
                return;
            _lastLogged = problem;
            Debug.LogWarning("Memory: smarter search is using words only: " + problem);
        }
    }
}
