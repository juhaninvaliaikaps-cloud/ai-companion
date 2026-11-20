using System;
using System.Collections.Generic;

namespace AICompanion.Core
{
    /// <summary>
    /// Detects clothing coming off in her reply, so the avatar can use the strip reaction even
    /// when a small model narrates it and forgets the cue. This only drives the avatar; it never
    /// adds text to the reply or the prompt.
    ///
    /// JUH-134 review (Jussi, 2026-11-20): her reply decides, not the user's message. "Take off
    /// your shirt" followed by her saying no leaves her dressed. Words are matched whole, so
    /// "stop" and "laptop" are not a "top", and a sentence that refuses or asks does not count.
    /// </summary>
    public static class ClothingIntent
    {
        static readonly HashSet<string> Clothing = new HashSet<string>(StringComparer.Ordinal)
        {
            "shirt", "shirts", "dress", "blouse", "skirt", "pants", "jeans", "trousers", "bra",
            "panties", "underwear", "clothes", "clothing", "top", "jacket", "hoodie", "sweater",
            "t-shirt", "tshirt", "shorts", "leggings", "stockings", "lingerie", "robe", "nightgown",
        };

        // English removal as whole words or phrases; "off" catches "slid her dress off".
        static readonly string[] RemovalPhrases =
        {
            "take off", "takes off", "took off", "taking off", "pull off", "pulls off", "pulled off",
            "pulling off", "slide off", "slides off", "slid off", "peel off", "peels off", "peeled off",
            "shrug off", "shrugs off", "shrugged off", "slip off", "slips off", "slipped off",
        };

        static readonly HashSet<string> RemovalWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "remove", "removes", "removed", "removing", "strip", "strips", "stripped", "stripping",
            "undress", "undresses", "undressed", "undressing", "unbutton", "unbuttons", "unbuttoned",
            "unbuttoning", "unzip", "unzips", "unzipped", "unzipping", "unhook", "unhooks", "unhooked",
        };

        // "her dress off": only right after a clothing word, so "the top of the
        // hill, then off to work" is not a strip.
        static readonly HashSet<string> AfterClothing = new HashSet<string>(StringComparer.Ordinal) { "off" };
        const int AfterClothingWords = 3;

        static readonly HashSet<string> Refusals = new HashSet<string>(StringComparer.Ordinal)
        {
            "not", "no", "never", "won't", "wont", "don't", "dont", "doesn't", "doesnt", "didn't",
            "didnt", "can't", "cant", "cannot", "refuse", "refuses", "refused", "stop", "stops",
            "stopped", "instead", "keep", "keeps", "kept", "rather", "wait", "later", "maybe",
        };

        /// <summary>
        /// True when a sentence of her reply narrates clothing coming off, and that sentence
        /// neither refuses nor asks. Cue tags in angle brackets are ignored.
        /// </summary>
        public static bool ReplyRemovesClothing(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply))
                return false;

            var text = WithoutTags(reply).ToLowerInvariant();
            var start = 0;
            for (var i = 0; i <= text.Length; i++)
            {
                var end = i == text.Length;
                var c = end ? '.' : text[i];
                if (c != '.' && c != '!' && c != '?' && c != '\n' && c != ';')
                    continue;
                if (i > start && c != '?' && SentenceRemoves(text.Substring(start, i - start)))
                    return true;
                start = i + 1;
            }

            return false;
        }

        static bool SentenceRemoves(string sentence)
        {
            var words = Words(sentence);
            if (words.Count == 0)
                return false;

            var clothing = false;
            var removal = false;
            var lastClothing = -1;
            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (Refusals.Contains(word))
                    return false;
                if (IsClothing(word))
                {
                    clothing = true;
                    lastClothing = i;
                }
                if (!removal && RemovalWords.Contains(word))
                    removal = true;
                if (!removal && AfterClothing.Contains(word) && lastClothing >= 0 && i - lastClothing <= AfterClothingWords)
                    removal = true;
            }

            if (!clothing)
                return false;
            if (removal)
                return true;

            var joined = " " + string.Join(" ", words.ToArray()) + " ";
            for (var i = 0; i < RemovalPhrases.Length; i++)
            {
                if (joined.IndexOf(" " + RemovalPhrases[i] + " ", StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        static bool IsClothing(string word)
        {
            return Clothing.Contains(word);
        }

        /// <summary>Lower-case words; an apostrophe or a hyphen inside a word is kept.</summary>
        static List<string> Words(string sentence)
        {
            var words = new List<string>();
            var current = new System.Text.StringBuilder();
            for (var i = 0; i < sentence.Length; i++)
            {
                var c = sentence[i];
                if (c == '’')
                    c = '\'';
                var inside = char.IsLetterOrDigit(c)
                    || ((c == '\'' || c == '-') && current.Length > 0 && i + 1 < sentence.Length && char.IsLetter(sentence[i + 1]));
                if (inside)
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

        static string WithoutTags(string text)
        {
            if (text.IndexOf('<') < 0)
                return text;
            var builder = new System.Text.StringBuilder(text.Length);
            var depth = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '<')
                {
                    depth++;
                    continue;
                }

                if (c == '>' && depth > 0)
                {
                    depth--;
                    builder.Append(' ');
                    continue;
                }

                if (depth == 0)
                    builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
