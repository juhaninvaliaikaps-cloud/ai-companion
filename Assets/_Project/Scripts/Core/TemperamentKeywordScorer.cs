using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-119: offline fallback for temperament detection. Scores a persona prompt against
    /// the preset trait words; no model call and no UI wiring. The later reading PR uses the
    /// best id as its fallback when the model cannot answer.
    /// </summary>
    public static class TemperamentKeywordScorer
    {
        /// <summary>A score below this is not a clear enough match, so the result is Balanced.</summary>
        public const float MinimumBestScore = 0.2f;

        const float TotalForHalf = 2f;

        sealed class Word
        {
            public readonly string Text;
            public readonly float Weight;

            public Word(string text, float weight)
            {
                Text = text;
                Weight = weight;
            }
        }

        sealed class Preset
        {
            public readonly string Id;
            public readonly Word[] Words;

            public Preset(string id, params Word[] words)
            {
                Id = id;
                Words = words;
            }
        }

        static readonly Preset[] Presets = BuildPresets();

        static readonly string[] Negations =
        {
            "not", "never", "no", "hardly", "barely", "isnt", "arent", "wasnt", "werent",
            "dont", "doesnt", "didnt", "cannot", "cant", "wont",
            "isn't", "aren't", "wasn't", "weren't", "don't", "doesn't", "didn't", "can't", "won't",
        };

        static readonly Dictionary<string, float> Intensities = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            { "very", 1.5f },
            { "really", 1.4f },
            { "extremely", 1.8f },
            { "incredibly", 1.7f },
            { "so", 1.25f },
            { "quite", 1.2f },
            { "somewhat", 0.6f },
            { "slightly", 0.6f },
        };

        static readonly Dictionary<string, float> IntensityPhrases = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            { "a bit", 0.5f },
            { "a little", 0.55f },
            { "kind of", 0.6f },
            { "sort of", 0.6f },
        };

        static Preset[] BuildPresets()
        {
            return new[]
            {
                new Preset(PersonaTemperaments.CheerfulId,
                    new Word("cheerful", 1f), new Word("bubbly", 0.9f), new Word("energetic", 0.8f),
                    new Word("playful", 0.8f), new Word("upbeat", 0.8f), new Word("sunny", 0.7f),
                    new Word("optimistic", 0.7f), new Word("lively", 0.7f), new Word("happy", 0.7f),
                    new Word("smiling", 0.6f), new Word("giggly", 0.6f)),
                new Preset(PersonaTemperaments.ShyId,
                    new Word("shy", 1f), new Word("timid", 0.9f), new Word("bashful", 0.9f),
                    new Word("blushes easily", 0.8f), new Word("soft-spoken", 0.8f),
                    new Word("reserved", 0.7f), new Word("quiet", 0.6f), new Word("nervous", 0.6f),
                    new Word("hesitant", 0.6f), new Word("flustered", 0.6f), new Word("withdrawn", 0.6f)),
                new Preset(PersonaTemperaments.TsundereId,
                    new Word("tsundere", 1f), new Word("bratty", 0.9f), new Word("sharp-tongued", 0.9f),
                    new Word("secretly caring", 0.9f), new Word("sassy", 0.7f), new Word("prickly", 0.7f),
                    new Word("defensive", 0.6f), new Word("stubborn", 0.6f), new Word("huffy", 0.6f),
                    new Word("teasing", 0.5f), new Word("proud", 0.5f)),
                new Preset(PersonaTemperaments.CalmId,
                    new Word("calm", 1f), new Word("gentle", 0.8f), new Word("patient", 0.8f),
                    new Word("composed", 0.9f), new Word("serene", 0.8f), new Word("steady", 0.7f),
                    new Word("level-headed", 0.8f), new Word("tranquil", 0.7f), new Word("relaxed", 0.6f),
                    new Word("unruffled", 0.6f)),
                new Preset(PersonaTemperaments.MoodyId,
                    new Word("moody", 1f), new Word("temperamental", 0.9f), new Word("unpredictable", 0.8f),
                    new Word("volatile", 0.8f), new Word("brooding", 0.7f), new Word("sulky", 0.6f),
                    new Word("gloomy", 0.6f), new Word("grumpy", 0.6f), new Word("dramatic", 0.5f),
                    new Word("sensitive", 0.5f)),
            };
        }

        /// <summary>
        /// Scores every preset id (including the balanced default, at 0) and picks the clearest
        /// one. A blank prompt, no matching words, or a best score below
        /// <see cref="MinimumBestScore"/> returns "" and Balanced.
        /// </summary>
        public static TemperamentKeywordResult Score(string prompt)
        {
            var scores = new Dictionary<string, float>(StringComparer.Ordinal);
            for (var i = 0; i < PersonaTemperaments.Ids.Length; i++)
                scores[PersonaTemperaments.Ids[i]] = 0f;

            if (string.IsNullOrWhiteSpace(prompt))
                return new TemperamentKeywordResult(scores, string.Empty, 0f);

            var text = Regex.Replace(prompt.ToLowerInvariant(), "\\r\\n?", "\n");
            text = Regex.Replace(text, "[ \\t]+", " ").Trim();
            var bestId = string.Empty;
            var best = 0f;
            for (var p = 0; p < Presets.Length; p++)
            {
                var preset = Presets[p];
                var total = MatchPreset(text, preset);
                var score = total <= 0f ? 0f : total / (total + TotalForHalf);
                scores[preset.Id] = score;
                if (score > best)
                {
                    best = score;
                    bestId = preset.Id;
                }
            }

            if (best < MinimumBestScore)
                return new TemperamentKeywordResult(scores, string.Empty, best);
            return new TemperamentKeywordResult(scores, bestId, best);
        }

        static float MatchPreset(string text, Preset preset)
        {
            var total = 0f;
            for (var w = 0; w < preset.Words.Length; w++)
            {
                var word = preset.Words[w];
                var pattern = "\\b" + Regex.Escape(word.Text) + "\\b";
                foreach (Match match in Regex.Matches(text, pattern))
                {
                    var before = Before(text, match.Index);
                    if (HasNegation(before))
                        continue;
                    total += word.Weight * Intensity(before);
                }
            }

            return total;
        }

        static string Before(string text, int index)
        {
            var start = 0;
            for (var i = index - 1; i >= 0; i--)
            {
                var c = text[i];
                if (c == '.' || c == '!' || c == '?' || c == ';' || c == ','
                    || c == '\n' || c == '\r')
                {
                    start = i + 1;
                    break;
                }
            }

            if (index - start > 80)
                start = index - 80;
            return text.Substring(start, index - start);
        }

        static List<string> Words(string before)
        {
            var words = new List<string>();
            var current = new System.Text.StringBuilder();
            for (var i = 0; i < before.Length; i++)
            {
                var c = before[i];
                if (char.IsLetterOrDigit(c) || c == '-' || c == '\'')
                {
                    current.Append(c);
                    continue;
                }

                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Length = 0;
                }
            }

            if (current.Length > 0)
                words.Add(current.ToString());
            return words;
        }

        static bool HasNegation(string before)
        {
            var words = Words(before);
            var start = Math.Max(0, words.Count - 3);
            for (var i = start; i < words.Count; i++)
            {
                if (Array.IndexOf(Negations, words[i]) >= 0)
                    return true;
            }

            return false;
        }

        static float Intensity(string before)
        {
            var words = Words(before);
            var start = Math.Max(0, words.Count - 3);
            var multiplier = 1f;
            for (var i = start; i < words.Count; i++)
            {
                float value;
                if (i + 1 < words.Count
                    && IntensityPhrases.TryGetValue(words[i] + " " + words[i + 1], out value))
                {
                    multiplier = value;
                    i++;
                    continue;
                }

                if (Intensities.TryGetValue(words[i], out value))
                    multiplier = value;
            }

            return multiplier;
        }
    }

    /// <summary>The scores and best preset from <see cref="TemperamentKeywordScorer"/>.</summary>
    public sealed class TemperamentKeywordResult
    {
        readonly Dictionary<string, float> _scores;

        internal TemperamentKeywordResult(Dictionary<string, float> scores, string bestId, float bestScore)
        {
            _scores = scores;
            BestId = bestId;
            BestScore = bestScore;
        }

        /// <summary>One 0..1 score per preset id, including the balanced default at 0.</summary>
        public IReadOnlyDictionary<string, float> Scores
        {
            get { return _scores; }
        }

        /// <summary>The best preset id, or "" when the result is Balanced.</summary>
        public string BestId { get; private set; }

        public float BestScore { get; private set; }

        public bool IsBalanced
        {
            get { return BestId.Length == 0; }
        }

        public float Score(string id)
        {
            float score;
            return id != null && _scores.TryGetValue(id, out score) ? score : 0f;
        }
    }
}
