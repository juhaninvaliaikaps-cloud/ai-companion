using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Memory
{
    /// <summary>One timeline entry chosen for this turn.</summary>
    public struct TimelineHit
    {
        public TimelineEntry Entry;
        public float Score;

        /// <summary>
        /// She brought it up within the last <see cref="TimelineSearch.RaisedMinutes"/> minutes.
        /// It stays in front of her while the talk is still about it, tagged so she does not
        /// repeat it unprompted (JUH-141).
        /// </summary>
        public bool AlreadyRaised;
    }

    /// <summary>
    /// JUH-141: which timeline entries fit this turn. Plain C#, no scene, so the tests can run
    /// the whole ranking.
    ///
    /// Words are split on whole-word boundaries, English and Finnish stop words are dropped, and
    /// a light stemmer folds endings (talked/talking, siskosta/siskon). Entries are ranked with
    /// BM25: a word that is rare in this persona's timeline counts for more than one in every
    /// entry. Concept groups (family, work, fear, …) let "my family" find "her sister" at a
    /// lower weight than a direct word match. The query is the latest user message plus, at
    /// half weight, the one before it, so "do you remember what I told you?" still has a topic.
    ///
    /// The ranking is then shaded by how much the entry can be trusted. Repetition
    /// (evidenceCount), the explicit "remember this" command and the entry's kind count for more
    /// than a small model's self-reported confidence, which only nudges it.
    /// </summary>
    public static class TimelineSearch
    {
        public const int DefaultMax = 3;

        /// <summary>An entry brought up this recently counts as already raised.</summary>
        public const int RaisedMinutes = 10;

        /// <summary>An already raised entry stays only while the talk names it this directly (distinct words matched).</summary>
        public const int RaisedMinDirectMatches = 2;

        public const float PreviousMessageWeight = 0.5f;
        public const float ConceptWeight = 0.6f;
        const float K1 = 1.2f;
        const float B = 0.75f;

        /// <summary>The entries that fit this turn, best first. Never throws.</summary>
        /// <param name="entries">All timeline entries of this persona.</param>
        /// <param name="latest">The latest user message.</param>
        /// <param name="previous">The user message before it, or empty.</param>
        /// <param name="includePrivate">False for a cloud provider: private entries never go there.</param>
        /// <param name="skip">Entries to leave out, for example ones a retrieved fact already says.</param>
        public static List<TimelineHit> Rank(
            IReadOnlyList<TimelineEntry> entries,
            string latest,
            string previous,
            bool includePrivate,
            DateTime nowUtc,
            int max = DefaultMax,
            Predicate<TimelineEntry> skip = null)
        {
            var result = new List<TimelineHit>();
            if (entries == null || entries.Count == 0 || max <= 0)
                return result;

            var query = QueryWeights(latest, previous);
            if (query.Count == 0)
                return result;

            // Candidate documents: valid, open, allowed for this provider.
            var docs = new List<Doc>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsValid || entry.resolved)
                    continue;
                if (entry.isPrivate && !includePrivate)
                    continue;
                if (skip != null && skip(entry))
                    continue;
                docs.Add(new Doc(entry, TermCounts(entry.title + " " + entry.summary)));
            }

            if (docs.Count == 0)
                return result;

            // Document frequency over this persona's open entries.
            var df = new Dictionary<string, int>(StringComparer.Ordinal);
            var totalLength = 0;
            for (var i = 0; i < docs.Count; i++)
            {
                totalLength += docs[i].Length;
                foreach (var term in docs[i].Terms.Keys)
                {
                    int n;
                    df.TryGetValue(term, out n);
                    df[term] = n + 1;
                }
            }

            var averageLength = Math.Max(1f, totalLength / (float)docs.Count);
            var scored = new List<TimelineHit>();
            for (var i = 0; i < docs.Count; i++)
            {
                var doc = docs[i];
                var relevance = 0f;
                var direct = 0;
                foreach (var pair in query)
                {
                    int tf;
                    if (!doc.Terms.TryGetValue(pair.Key, out tf))
                        continue;
                    int n;
                    df.TryGetValue(pair.Key, out n);
                    var idf = (float)Math.Log(1d + (docs.Count - n + 0.5d) / (n + 0.5d));
                    var norm = tf * (K1 + 1f) / (tf + K1 * (1f - B + B * doc.Length / averageLength));
                    relevance += pair.Value * idf * norm;
                    if (!IsConcept(pair.Key) && pair.Value >= 1f)
                        direct++;
                }

                if (relevance <= 0f)
                    continue;

                var raised = RecentlyRaised(doc.Entry, nowUtc);
                // A just-raised entry stays only while the talk is still plainly about it;
                // a passing word no longer brings it back (the old verbal tic).
                if (raised && direct < RaisedMinDirectMatches)
                    continue;

                var score = relevance * Trust(doc.Entry) * (1f + 0.12f * (Clamp(doc.Entry.importance, 1, 5) - 3)) + 0.25f * Recency(doc.Entry, nowUtc);
                scored.Add(new TimelineHit { Entry = doc.Entry, Score = score, AlreadyRaised = raised });
            }

            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (var i = 0; i < scored.Count && result.Count < max; i++)
                result.Add(scored[i]);
            return result;
        }

        // ---- Trust --------------------------------------------------------------------------

        /// <summary>
        /// How much an entry can be trusted: repetition and the explicit command matter most, the
        /// kind a little, the model's own confidence only within ±10%.
        /// </summary>
        public static float Trust(TimelineEntry entry)
        {
            if (entry == null)
                return 1f;
            var evidence = 1f + 0.1f * Math.Min(5, Math.Max(0, entry.evidenceCount - 1));
            var explicitly = string.Equals(entry.source, "explicit", StringComparison.Ordinal) ? 1.2f : 1f;
            float kind;
            switch (entry.kind)
            {
                case MemoryDurability.Boundary:
                case MemoryDurability.Promise:
                    kind = 1.15f;
                    break;
                case MemoryDurability.Fact:
                case MemoryDurability.Preference:
                case MemoryDurability.RelationshipChange:
                    kind = 1.05f;
                    break;
                default:
                    kind = 1f;
                    break;
            }

            var confidence = entry.confidence > 0 ? Math.Min(100, entry.confidence) : 60;
            var self = 0.9f + 0.2f * confidence / 100f;
            return evidence * explicitly * kind * self;
        }

        static float Recency(TimelineEntry entry, DateTime nowUtc)
        {
            DateTime happened;
            if (!TryParseUtc(entry.happenedAtUtc, out happened))
                return 0f;
            var days = Math.Max(0d, (nowUtc - happened).TotalDays);
            return (float)Math.Max(0d, 1d - days / 60d);
        }

        public static bool RecentlyRaised(TimelineEntry entry, DateTime nowUtc)
        {
            DateTime last;
            if (entry == null || !TryParseUtc(entry.lastRecalledAtUtc, out last))
                return false;
            return nowUtc - last < TimeSpan.FromMinutes(RaisedMinutes);
        }

        static bool TryParseUtc(string text, out DateTime value)
        {
            if (string.IsNullOrEmpty(text))
            {
                value = default(DateTime);
                return false;
            }

            if (!DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out value))
                return false;
            return true;
        }

        static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }

        // ---- Words ------------------------------------------------------------------------

        sealed class Doc
        {
            public readonly TimelineEntry Entry;
            public readonly Dictionary<string, int> Terms;
            public readonly int Length;

            public Doc(TimelineEntry entry, Dictionary<string, int> terms)
            {
                Entry = entry;
                Terms = terms;
                var length = 0;
                foreach (var count in terms.Values)
                    length += count;
                Length = Math.Max(1, length);
            }
        }

        /// <summary>The query terms with weights: the latest message at 1, the one before at half, concepts lower.</summary>
        public static Dictionary<string, float> QueryWeights(string latest, string previous)
        {
            var weights = new Dictionary<string, float>(StringComparer.Ordinal);
            AddQuery(weights, latest, 1f);
            AddQuery(weights, previous, PreviousMessageWeight);
            return weights;
        }

        static void AddQuery(Dictionary<string, float> weights, string text, float weight)
        {
            var words = Words(text);
            for (var i = 0; i < words.Count; i++)
            {
                var term = words[i];
                var w = IsConcept(term) ? weight * ConceptWeight : weight;
                float existing;
                if (!weights.TryGetValue(term, out existing) || existing < w)
                    weights[term] = w;
            }
        }

        static Dictionary<string, int> TermCounts(string text)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var words = Words(text);
            for (var i = 0; i < words.Count; i++)
            {
                int n;
                counts.TryGetValue(words[i], out n);
                counts[words[i]] = n + 1;
            }

            return counts;
        }

        /// <summary>
        /// The searchable terms of a text: whole words, lower case, without stop words, stemmed,
        /// plus a concept term (for example "#family") for each concept a word belongs to.
        /// </summary>
        public static List<string> Words(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return result;
            var lower = text.ToLowerInvariant();
            var finnish = LooksFinnish(lower);
            var word = new StringBuilder(16);
            for (var i = 0; i <= lower.Length; i++)
            {
                var c = i < lower.Length ? lower[i] : ' ';
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(c);
                    continue;
                }

                if (word.Length > 0)
                {
                    AddWord(result, word.ToString(), finnish);
                    word.Length = 0;
                }
            }

            return result;
        }

        static void AddWord(List<string> result, string word, bool finnish)
        {
            if (word.Length < 3 || Stop.Contains(word))
                return;
            var stem = Stem(word, finnish);
            if (stem.Length < 3 || Stop.Contains(stem))
                return;
            result.Add(stem);
            string concept;
            if (ConceptOf.TryGetValue(stem, out concept) || ConceptOf.TryGetValue(word, out concept))
                result.Add(concept);
        }

        public static bool IsConcept(string term)
        {
            return term.Length > 1 && term[0] == '#';
        }

        /// <summary>Finnish when it has ä/ö/å, or more Finnish than English stop words.</summary>
        public static bool LooksFinnish(string lower)
        {
            if (string.IsNullOrEmpty(lower))
                return false;
            if (lower.IndexOf('ä') >= 0 || lower.IndexOf('ö') >= 0 || lower.IndexOf('å') >= 0)
                return true;
            var fi = 0;
            var en = 0;
            var start = -1;
            for (var i = 0; i <= lower.Length; i++)
            {
                var letter = i < lower.Length && char.IsLetter(lower[i]);
                if (letter && start < 0)
                    start = i;
                else if (!letter && start >= 0)
                {
                    var w = lower.Substring(start, i - start);
                    if (FinnishStop.Contains(w))
                        fi++;
                    else if (EnglishStop.Contains(w))
                        en++;
                    start = -1;
                }
            }

            return fi > en;
        }

        /// <summary>
        /// A light stemmer. English: -ing, -ed, -es, -s, a final -e. Finnish: the common case and
        /// possessive endings (siskosta → sisko, töissä → tö… kept at 3+ letters). Crude on
        /// purpose: it only has to make the same word meet itself, not be linguistically right.
        /// </summary>
        public static string Stem(string word, bool finnish)
        {
            if (string.IsNullOrEmpty(word))
                return string.Empty;
            if (finnish)
                return StemFinnish(word);

            var english = StemEnglish(word);
            // A final -e goes too, so promise/promised, live/lived and move/moved meet.
            if (english.Length > 4 && english.EndsWith("e", StringComparison.Ordinal))
                english = english.Substring(0, english.Length - 1);
            return english;
        }

        static string StemFinnish(string word)
        {
            // Possessive endings first, down to 3 letters (isäni → isä, siskoni → sisko).
            for (var i = 0; i < FinnishPossessive.Length; i++)
            {
                var ending = FinnishPossessive[i];
                if (word.Length - ending.Length >= 3 && word.EndsWith(ending, StringComparison.Ordinal))
                    return word.Substring(0, word.Length - ending.Length);
            }

            for (var i = 0; i < FinnishEndings.Length; i++)
            {
                var ending = FinnishEndings[i];
                if (word.Length - ending.Length >= 4 && word.EndsWith(ending, StringComparison.Ordinal))
                    return word.Substring(0, word.Length - ending.Length);
            }

            return word;
        }

        static string StemEnglish(string word)
        {
            if (word.Length > 5 && word.EndsWith("ing", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 3);
            if (word.Length > 4 && word.EndsWith("ied", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 3) + "y";
            if (word.Length > 4 && word.EndsWith("ed", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 2);
            if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 3) + "y";
            if (word.Length > 4 && (word.EndsWith("ches", StringComparison.Ordinal) || word.EndsWith("shes", StringComparison.Ordinal)
                                    || word.EndsWith("sses", StringComparison.Ordinal) || word.EndsWith("xes", StringComparison.Ordinal)))
                return word.Substring(0, word.Length - 2);
            if (word.Length > 3 && word.EndsWith("s", StringComparison.Ordinal) && !word.EndsWith("ss", StringComparison.Ordinal)
                && !word.EndsWith("us", StringComparison.Ordinal) && !word.EndsWith("is", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 1);
            return word;
        }

        static readonly string[] FinnishPossessive = { "mme", "nne", "nsa", "nsä", "ni", "si" };

        // Longest first, so "-ssa" is not cut as "-a". No vowel+n endings (-on, -an…): they
        // collide with the plain genitive -n (siskon → sisko, not sisk).
        static readonly string[] FinnishEndings =
        {
            "iltaan", "issaan", "issään", "istaan", "istään", "illaan", "illään",
            "ssaan", "ssään", "staan", "stään", "llaan", "llään", "ltaan", "ltään", "lleen",
            "issa", "issä", "ista", "istä", "illa", "illä", "ilta", "iltä", "ille", "iksi",
            "ssa", "ssä", "sta", "stä", "lla", "llä", "lta", "ltä", "lle", "ksi", "tta", "ttä",
            "mme", "nne", "nsa", "nsä", "ani", "äni", "asi", "äsi",
            "na", "nä", "ja", "jä", "ni", "si",
            "n", "t", "a", "ä",
        };

        static readonly HashSet<string> EnglishStop = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "and", "for", "with", "that", "this", "those", "these", "was", "were", "are", "is", "be", "been", "being",
            "have", "has", "had", "you", "your", "yours", "mine", "she", "her", "hers", "him", "his", "they", "them", "their",
            "its", "what", "when", "where", "who", "whom", "why", "how", "which", "about", "just", "like", "really", "very",
            "does", "did", "done", "doing", "not", "yes", "but", "then", "than", "there", "here", "from", "into", "onto",
            "over", "after", "before", "all", "any", "some", "can", "could", "would", "should", "will", "shall", "may",
            "might", "must", "also", "too", "again", "more", "most", "much", "many", "now", "today", "okay", "hey",
            "well", "yeah", "know", "think", "get", "got", "going", "said", "say", "says", "tell", "told", "user",
            "users", "remember", "mean", "one", "out", "off", "our", "ours", "we", "way", "thing", "things", "something",
            "anything", "ever", "still", "even", "only", "own", "same", "other", "each", "both", "because", "while",
            "during", "until", "since", "upon", "via", "per", "yet", "maybe", "please", "thanks", "thank", "lot",
            "let", "make", "made", "take", "took", "come", "came", "see", "saw", "look", "want", "wanted",
        };

        static readonly HashSet<string> FinnishStop = new HashSet<string>(StringComparer.Ordinal)
        {
            // Two-letter words that are also English ("on", "me", "he") are left out: they would make
            // an English line look Finnish, and words under 3 letters are never searched anyway.
            "ja", "oli", "ole", "olen", "olet", "olla", "ovat", "ollut", "olisi", "ei", "tämä",
            "tuo", "että", "kun", "jos", "mutta", "tai", "niin", "myös", "vain", "kuin", "mitä", "mikä", "miksi", "miten",
            "missä", "minä", "mä", "sinä", "sä", "hän", "minun", "sinun", "hänen", "meidän", "teidän",
            "heidän", "tässä", "siinä", "siitä", "nyt", "joo", "kyllä", "ihan", "vähän", "sitten", "jo", "vielä",
            "muista", "muistatko", "muistan", "sanoin", "kerroin", "kerro", "sano", "mun", "sun", "mulla", "sulla",
            "mulle", "sulle", "mitään", "jotain", "joka", "jonka", "joita", "kanssa", "tänään", "aina",
        };

        static readonly HashSet<string> Stop = Union(EnglishStop, FinnishStop);

        static HashSet<string> Union(HashSet<string> a, HashSet<string> b)
        {
            var all = new HashSet<string>(a, StringComparer.Ordinal);
            all.UnionWith(b);
            return all;
        }

        /// <summary>Concept groups, English and Finnish, stored by stem.</summary>
        static readonly Dictionary<string, string[]> Concepts = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "#family", new[] { "family", "sister", "brother", "mother", "father", "mom", "dad", "mum", "parent", "relative", "sibling", "grandma", "grandpa", "aunt", "uncle", "cousin", "perhe", "sisko", "sisar", "veli", "äiti", "isä", "vanhemma", "mummo", "ukki", "serkku" } },
            { "#work", new[] { "work", "job", "career", "office", "boss", "colleague", "coworker", "project", "shift", "interview", "työ", "töissä", "työpaikka", "pomo", "kollega", "duuni", "haastattelu" } },
            { "#fear", new[] { "afraid", "scared", "fear", "anxious", "anxiety", "nervous", "worried", "worry", "panic", "pelko", "pelkää", "pelottaa", "jännittää", "ahdistaa", "huolissaan" } },
            { "#anger", new[] { "angry", "annoyed", "mad", "furious", "upset", "irritated", "vihainen", "suuttunut", "ärsyttää" } },
            { "#joy", new[] { "happy", "glad", "joy", "cheerful", "delighted", "excited", "iloinen", "onnellinen", "innoissaan" } },
            { "#promise", new[] { "promise", "promised", "vow", "agreed", "commitment", "lupaus", "lupasi", "lupaan", "lupasin", "sovittiin" } },
            { "#boundary", new[] { "boundary", "limit", "trigger", "uncomfortable", "dislike", "hate", "raja", "rajat", "inhoaa", "vihaa" } },
            { "#home", new[] { "home", "house", "apartment", "flat", "move", "moved", "koti", "kotona", "asunto", "muutti", "muutto" } },
            { "#travel", new[] { "travel", "trip", "visit", "holiday", "vacation", "flight", "matka", "reissu", "loma", "lento", "vierailu" } },
            { "#health", new[] { "health", "sick", "ill", "illness", "doctor", "hospital", "pain", "surgery", "sairas", "lääkäri", "sairaala", "kipu", "leikkaus" } },
            { "#loss", new[] { "loss", "died", "death", "grief", "funeral", "passed", "kuoli", "kuolema", "suru", "hautajaiset" } },
            { "#pet", new[] { "pet", "dog", "cat", "puppy", "kitten", "lemmikki", "koira", "kissa" } },
        };

        static readonly Dictionary<string, string> ConceptOf = BuildConceptIndex();

        static Dictionary<string, string> BuildConceptIndex()
        {
            var index = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in Concepts)
            {
                for (var i = 0; i < pair.Value.Length; i++)
                {
                    var word = pair.Value[i];
                    var finnish = word.IndexOf('ä') >= 0 || word.IndexOf('ö') >= 0 || IsFinnishConcept(word);
                    index[word] = pair.Key;
                    var stem = Stem(word, finnish);
                    if (stem.Length >= 3)
                        index[stem] = pair.Key;
                    // The other language's stem too, since a word is stemmed by its sentence's language.
                    var other = Stem(word, !finnish);
                    if (other.Length >= 3 && !index.ContainsKey(other))
                        index[other] = pair.Key;
                }
            }

            return index;
        }

        static bool IsFinnishConcept(string word)
        {
            // Finnish concept words without ä/ö; a short list beats a language guess here.
            switch (word)
            {
                case "perhe": case "sisko": case "sisar": case "veli": case "vanhemma": case "mummo": case "ukki": case "serkku":
                case "pomo": case "kollega": case "duuni": case "haastattelu": case "pelko": case "vihainen": case "suuttunut":
                case "iloinen": case "onnellinen": case "innoissaan": case "lupaus": case "lupasi": case "lupaan": case "lupasin":
                case "sovittiin": case "raja": case "rajat": case "inhoaa": case "vihaa": case "koti": case "kotona": case "asunto":
                case "muutti": case "muutto": case "matka": case "reissu": case "loma": case "lento": case "vierailu": case "sairas":
                case "sairaala": case "kipu": case "leikkaus": case "kuoli": case "kuolema": case "suru": case "hautajaiset":
                case "lemmikki": case "koira": case "kissa": case "huolissaan": case "ahdistaa":
                    return true;
                default:
                    return false;
            }
        }
    }
}
