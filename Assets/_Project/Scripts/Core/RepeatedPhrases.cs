using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-171: finds the stock phrases her recent replies keep reusing ("can't just", "her
    /// cheeks flushed") so the next request can ask her to vary them. Small local models fall into
    /// these loops, and Ollama's default repeat penalty only looks back 64 tokens. Plain C#: the
    /// chat passes her last replies, and this returns at most <see cref="MaxPhrases"/> phrases of
    /// two to four words that showed up in at least <see cref="MinReplies"/> of them. A phrase made
    /// only of common small words ("of the", "you and i") is never named.
    /// </summary>
    public static class RepeatedPhrases
    {
        public const int MaxPhrases = 4;
        public const int MinReplies = 3;
        public const int RepliesLooked = 5;
        public const int MinWords = 2;
        public const int MaxWords = 4;

        static readonly HashSet<string> Small = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "an", "the", "and", "or", "but", "of", "to", "in", "on", "at", "for", "with", "as", "by", "from", "up", "so",
            "it", "is", "was", "be", "are", "am", "i", "you", "me", "my", "your", "we", "us", "our", "he", "she", "her", "his", "him",
            "they", "them", "their", "that", "this", "there", "what", "if", "not", "do", "just", "all", "out", "into", "over", "about",
        };

        /// <summary>
        /// The phrases, longest first; empty when nothing repeats. <paramref name="replies"/> is
        /// her replies oldest first; only the last <see cref="RepliesLooked"/> count.
        /// </summary>
        public static List<string> Find(IList<string> replies)
        {
            var found = new List<string>();
            if (replies == null || replies.Count < MinReplies)
                return found;

            var start = Math.Max(0, replies.Count - RepliesLooked);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var r = start; r < replies.Count; r++)
            {
                seen.Clear();
                var words = Words(replies[r]);
                for (var n = MinWords; n <= MaxWords; n++)
                {
                    for (var i = 0; i + n <= words.Count; i++)
                    {
                        if (!HasContent(words, i, n))
                            continue;
                        var phrase = Join(words, i, n);
                        if (seen.Add(phrase))
                        {
                            int count;
                            counts.TryGetValue(phrase, out count);
                            counts[phrase] = count + 1;
                        }
                    }
                }
            }

            var candidates = new List<string>();
            foreach (var pair in counts)
            {
                if (pair.Value >= MinReplies)
                    candidates.Add(pair.Key);
            }

            // Longest first, so "can't just" is not named again inside "you can't just".
            candidates.Sort((a, b) =>
            {
                var byLength = WordCount(b).CompareTo(WordCount(a));
                return byLength != 0 ? byLength : string.CompareOrdinal(a, b);
            });
            for (var i = 0; i < candidates.Count && found.Count < MaxPhrases; i++)
            {
                var phrase = candidates[i];
                var inside = false;
                for (var j = 0; j < found.Count; j++)
                {
                    if ((" " + found[j] + " ").IndexOf(" " + phrase + " ", StringComparison.Ordinal) >= 0)
                    {
                        inside = true;
                        break;
                    }
                }

                if (!inside)
                    found.Add(phrase);
            }

            return found;
        }

        /// <summary>Words a sample line and a reply must share in a row to count as a copy.</summary>
        public const int CopyWords = 4;

        /// <summary>
        /// JUH-171: the voice reading's sample lines are for the feel only. When one of her last
        /// replies copied four words in a row from a sample, that run is named at once (no need
        /// to wait for three replies). Adds to <paramref name="found"/> without repeating a
        /// phrase already in it, up to <see cref="MaxPhrases"/>.
        /// </summary>
        public static void AddCopied(IList<string> replies, IList<string> samples, List<string> found)
        {
            if (replies == null || samples == null || found == null || replies.Count == 0)
                return;
            var start = Math.Max(0, replies.Count - RepliesLooked);
            var recent = new List<string>(replies.Count - start);
            for (var r = start; r < replies.Count; r++)
            {
                var words = Words(replies[r]);
                recent.Add(" " + Join(words, 0, words.Count) + " ");
            }

            for (var s = 0; s < samples.Count && found.Count < MaxPhrases; s++)
            {
                var sample = Words(samples[s]);
                for (var i = 0; i + CopyWords <= sample.Count; i++)
                {
                    if (!HasContent(sample, i, CopyWords))
                        continue;
                    var run = " " + Join(sample, i, CopyWords) + " ";
                    var copied = false;
                    for (var r = 0; r < recent.Count && !copied; r++)
                        copied = recent[r].IndexOf(run, StringComparison.Ordinal) >= 0;
                    if (!copied)
                        continue;
                    var phrase = run.Trim();
                    if (!found.Contains(phrase))
                        found.Add(phrase);
                    break;
                }
            }
        }

        /// <summary>
        /// JUH-173: true when one of <paramref name="replies"/> (the last <see cref="RepliesLooked"/>)
        /// has four words in a row from <paramref name="line"/>, so the rotating pool skips it for now.
        /// </summary>
        public static bool Echoes(IList<string> replies, string line)
        {
            if (replies == null || replies.Count == 0 || string.IsNullOrEmpty(line))
                return false;
            var words = Words(line);
            var start = Math.Max(0, replies.Count - RepliesLooked);
            for (var r = start; r < replies.Count; r++)
            {
                var reply = Words(replies[r]);
                var text = " " + Join(reply, 0, reply.Count) + " ";
                for (var i = 0; i + CopyWords <= words.Count; i++)
                {
                    if (HasContent(words, i, CopyWords) && text.IndexOf(" " + Join(words, i, CopyWords) + " ", StringComparison.Ordinal) >= 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>The prompt line for these phrases, or empty when there are none.</summary>
        public static string Line(IList<string> phrases)
        {
            if (phrases == null || phrases.Count == 0)
                return string.Empty;
            var sb = new StringBuilder("Your recent replies kept reusing these phrases; do not use them in this reply, and vary your wording and sentence shapes: ");
            for (var i = 0; i < phrases.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append('"').Append(phrases[i]).Append('"');
            }

            return sb.Append('.').ToString();
        }

        /// <summary>Lower-case words; letters, digits and apostrophes (curly ones made straight). Everything else splits.</summary>
        public static List<string> Words(string text)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(text))
                return words;
            var sb = new StringBuilder();
            for (var i = 0; i <= text.Length; i++)
            {
                var c = i < text.Length ? text[i] : ' ';
                if (c == '’')
                    c = '\'';
                if (char.IsLetterOrDigit(c) || (c == '\'' && sb.Length > 0))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    continue;
                }

                if (sb.Length > 0)
                {
                    words.Add(sb.ToString().TrimEnd('\''));
                    sb.Length = 0;
                }
            }

            return words;
        }

        static bool HasContent(List<string> words, int start, int count)
        {
            for (var i = start; i < start + count; i++)
            {
                if (!Small.Contains(words[i]))
                    return true;
            }

            return false;
        }

        static string Join(List<string> words, int start, int count)
        {
            var sb = new StringBuilder();
            for (var i = start; i < start + count; i++)
            {
                if (i > start)
                    sb.Append(' ');
                sb.Append(words[i]);
            }

            return sb.ToString();
        }

        static int WordCount(string phrase)
        {
            var n = 1;
            for (var i = 0; i < phrase.Length; i++)
            {
                if (phrase[i] == ' ')
                    n++;
            }

            return n;
        }
    }
}
