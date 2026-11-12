using System;
using System.Collections.Generic;

namespace AICompanion.Core
{
    /// <summary>
    /// Word lists for <see cref="UserMoodDetector"/>. Every entry is lower case with
    /// apostrophes removed, because the detector normalises tokens the same way
    /// ("don't" is "dont"). English first, then a small Finnish set.
    /// A weight of 1 is an ordinary emotion word, 1.5 a strong one, 2 a very strong one.
    /// </summary>
    public static class UserMoodLexicon
    {
        public readonly struct Hit
        {
            public UserEmotion Emotion { get; }
            public float Weight { get; }

            public Hit(UserEmotion emotion, float weight)
            {
                Emotion = emotion;
                Weight = weight;
            }
        }

        public readonly struct Stem
        {
            public string Prefix { get; }
            public Hit Hit { get; }

            public Stem(string prefix, Hit hit)
            {
                Prefix = prefix;
                Hit = hit;
            }
        }

        public readonly struct Phrase
        {
            public string[] Tokens { get; }
            public Hit Hit { get; }

            public Phrase(string[] tokens, Hit hit)
            {
                Tokens = tokens;
                Hit = hit;
            }
        }

        public readonly struct Symbol
        {
            public string Text { get; }
            public Hit Hit { get; }

            public Symbol(string text, Hit hit)
            {
                Text = text;
                Hit = hit;
            }
        }

        static readonly Dictionary<string, Hit> Words = BuildWords();
        static readonly Stem[] Stems = BuildStems();
        static readonly Phrase[] PhraseList = BuildPhrases();
        static readonly Symbol[] SymbolList = BuildSymbols();

        static readonly HashSet<string> Negators = new HashSet<string>(StringComparer.Ordinal)
        {
            "not", "never", "dont", "doesnt", "didnt", "isnt", "arent", "wasnt", "werent",
            "cant", "cannot", "wont", "wouldnt", "aint", "hardly",
            "en", "et", "ei", "emme", "ette", "eivät", "älä",
        };

        // "no" negates only the very next word ("no fun"). As a reply it stands alone:
        // "No I'm happy" is still happy. "without" is not a negator at all, because
        // "without you I'm sad" is sad.
        static readonly HashSet<string> AdjacentNegators = new HashSet<string>(StringComparer.Ordinal)
        {
            "no",
        };

        static readonly HashSet<string> Intensifiers = new HashSet<string>(StringComparer.Ordinal)
        {
            "very", "really", "so", "extremely", "super", "totally", "absolutely", "incredibly",
            "too", "fucking", "freaking", "insanely",
            "tosi", "todella", "erittäin", "niin",
        };

        // "kind" is left out: "you are kind and sweet" is not "kind of sweet".
        static readonly HashSet<string> Dampeners = new HashSet<string>(StringComparer.Ordinal)
        {
            "slightly", "somewhat", "kinda", "bit", "little", "fairly",
            "vähän", "hieman", "hiukan",
        };

        /// <summary>Phrases, longest first, so "i love you" is tried before "love you".</summary>
        public static Phrase[] Phrases
        {
            get { return PhraseList; }
        }

        /// <summary>Emoticons and emoji, matched against the raw text.</summary>
        public static Symbol[] Symbols
        {
            get { return SymbolList; }
        }

        public static bool TryGetWord(string token, out Hit hit)
        {
            hit = default(Hit);
            if (string.IsNullOrEmpty(token))
                return false;
            if (Words.TryGetValue(token, out hit))
                return true;

            for (var i = 0; i < Stems.Length; i++)
            {
                if (token.StartsWith(Stems[i].Prefix, StringComparison.Ordinal))
                {
                    hit = Stems[i].Hit;
                    return true;
                }
            }

            return false;
        }

        public static bool IsNegator(string token)
        {
            return token != null && Negators.Contains(token);
        }

        /// <summary>A negator that only counts directly before the word it negates.</summary>
        public static bool IsAdjacentNegator(string token)
        {
            return token != null && AdjacentNegators.Contains(token);
        }

        public static bool IsIntensifier(string token)
        {
            return token != null && Intensifiers.Contains(token);
        }

        public static bool IsDampener(string token)
        {
            return token != null && Dampeners.Contains(token);
        }

        static Dictionary<string, Hit> BuildWords()
        {
            var words = new Dictionary<string, Hit>(StringComparer.Ordinal);

            // Happy
            AddWords(words, UserEmotion.Happy, 0.6f, "good", "finally");
            AddWords(words, UserEmotion.Happy, 0.8f, "nice", "cool", "fun", "funny", "lol", "thanks", "thank", "won", "best", "enjoy", "enjoyed", "enjoying", "cheers");
            AddWords(words, UserEmotion.Happy, 1f, "happy", "glad", "great", "haha", "lmao", "congrats", "pleased", "relieved", "lovely");
            AddWords(words, UserEmotion.Happy, 1.2f, "love", "perfect", "excellent", "hahaha", "proud", "brilliant", "joy");
            AddWords(words, UserEmotion.Happy, 1.5f, "awesome", "amazing", "wonderful", "fantastic", "yay", "woohoo", "excited", "delighted");
            AddWords(words, UserEmotion.Happy, 2f, "thrilled", "ecstatic");

            // Sad
            AddWords(words, UserEmotion.Sad, 0.6f, "bad", "lost", "miss");
            AddWords(words, UserEmotion.Sad, 0.8f, "alone", "tired", "fail", "sigh", "bored", "boring");
            AddWords(words, UserEmotion.Sad, 1f, "hurt", "hurts", "tears", "failed", "sucks", "gloomy");
            AddWords(words, UserEmotion.Sad, 1.2f, "sad", "unhappy", "cry", "cried", "awful", "terrible", "horrible", "worst", "exhausted", "disappointing", "upset");
            AddWords(words, UserEmotion.Sad, 1.5f, "depressing", "lonely", "crying", "disappointed", "died");
            AddWords(words, UserEmotion.Sad, 2f, "depressed", "miserable", "heartbroken", "hopeless", "worthless", "grief");

            // Angry
            AddWords(words, UserEmotion.Angry, 0.8f, "dumb", "damn", "shit");
            AddWords(words, UserEmotion.Angry, 1f, "stupid", "dammit", "ugh", "unfair");
            AddWords(words, UserEmotion.Angry, 1.2f, "mad", "annoyed", "annoying", "hated", "idiot", "fuck", "irritated", "irritating", "outrageous");
            AddWords(words, UserEmotion.Angry, 1.5f, "angry", "pissed", "hate", "frustrated", "frustrating", "rage", "bullshit");
            AddWords(words, UserEmotion.Angry, 2f, "furious");

            // Anxious
            AddWords(words, UserEmotion.Anxious, 1f, "scary", "uneasy");
            AddWords(words, UserEmotion.Anxious, 1.2f, "worry", "nervous", "stress", "stressful", "fear");
            AddWords(words, UserEmotion.Anxious, 1.5f, "scared", "afraid", "worried", "anxious", "anxiety", "panic", "stressed", "frightened", "dread", "overwhelmed");
            AddWords(words, UserEmotion.Anxious, 2f, "terrified", "panicking");

            // Surprised
            AddWords(words, UserEmotion.Surprised, 0.6f, "seriously");
            AddWords(words, UserEmotion.Surprised, 0.8f, "holy");
            AddWords(words, UserEmotion.Surprised, 1f, "wtf", "incredible", "surprising", "unexpected");
            AddWords(words, UserEmotion.Surprised, 1.2f, "wow", "whoa", "omg", "unbelievable", "shocking", "surprised");
            AddWords(words, UserEmotion.Surprised, 1.5f, "shocked");

            // Affectionate
            AddWords(words, UserEmotion.Affectionate, 0.6f, "dear");
            AddWords(words, UserEmotion.Affectionate, 0.8f, "sweet");
            AddWords(words, UserEmotion.Affectionate, 1f, "cute", "babe", "beautiful");
            AddWords(words, UserEmotion.Affectionate, 1.2f, "adorable", "darling", "sweetheart", "hug", "hugs", "kiss", "kisses", "cuddle", "gorgeous");
            AddWords(words, UserEmotion.Affectionate, 1.5f, "adore");

            // Confused
            AddWords(words, UserEmotion.Confused, 0.6f, "hmm", "hmmm");
            AddWords(words, UserEmotion.Confused, 0.8f, "idk");
            AddWords(words, UserEmotion.Confused, 1f, "huh", "unsure", "unclear");
            AddWords(words, UserEmotion.Confused, 1.2f, "puzzled");
            AddWords(words, UserEmotion.Confused, 1.5f, "confused", "confusing");

            // Finnish, whole words. Inflected forms are in BuildStems.
            AddWords(words, UserEmotion.Happy, 0.8f, "kiitos", "kiitti", "kiva", "paras");
            AddWords(words, UserEmotion.Happy, 1f, "ihana", "jee");
            AddWords(words, UserEmotion.Sad, 0.6f, "paha");
            AddWords(words, UserEmotion.Sad, 1f, "harmi", "kurja");
            AddWords(words, UserEmotion.Angry, 1f, "perkele", "saatana", "tyhmä");
            AddWords(words, UserEmotion.Angry, 1.5f, "vihaan");
            AddWords(words, UserEmotion.Anxious, 1f, "huoli");
            AddWords(words, UserEmotion.Surprised, 1.2f, "vau", "oho");
            AddWords(words, UserEmotion.Affectionate, 1.2f, "rakas", "söpö", "halaus", "pusu");
            AddWords(words, UserEmotion.Confused, 1f, "sekava");

            return words;
        }

        static Stem[] BuildStems()
        {
            var stems = new List<Stem>();

            // Finnish inflects, so these match the start of a word: "iloi" covers iloinen and iloisia.
            AddStems(stems, UserEmotion.Happy, 1f, "iloi", "hieno", "hauska", "huippu");
            AddStems(stems, UserEmotion.Happy, 1.5f, "onnelli", "mahtav", "loista");
            AddStems(stems, UserEmotion.Sad, 1f, "harmitt", "väsyn", "väsyt");
            AddStems(stems, UserEmotion.Sad, 1.2f, "surulli", "itke");
            AddStems(stems, UserEmotion.Sad, 1.5f, "yksinäi", "pettyn");
            AddStems(stems, UserEmotion.Sad, 2f, "masenn", "masentu");
            AddStems(stems, UserEmotion.Angry, 1.2f, "ärsyt");
            AddStems(stems, UserEmotion.Angry, 1.5f, "vihai", "vitu", "raivo");
            AddStems(stems, UserEmotion.Anxious, 1f, "jännit");
            AddStems(stems, UserEmotion.Anxious, 1.2f, "stressa");
            AddStems(stems, UserEmotion.Anxious, 1.5f, "pelott", "pelkää", "huolest", "ahdist");
            AddStems(stems, UserEmotion.Surprised, 1.2f, "uskomat", "yllätt");
            AddStems(stems, UserEmotion.Affectionate, 1.5f, "rakasta");
            AddStems(stems, UserEmotion.Confused, 1.5f, "hämmenn");

            return stems.ToArray();
        }

        static Phrase[] BuildPhrases()
        {
            var phrases = new List<Phrase>();

            AddPhrase(phrases, UserEmotion.Affectionate, 2.5f, "i love you");
            AddPhrase(phrases, UserEmotion.Affectionate, 2f, "love you");
            AddPhrase(phrases, UserEmotion.Affectionate, 2f, "love u");
            AddPhrase(phrases, UserEmotion.Affectionate, 1.5f, "miss you");
            // Not the bare "like you": "like you said" and "sounds like you" are not affection.
            AddPhrase(phrases, UserEmotion.Affectionate, 1.2f, "i like you");
            AddPhrase(phrases, UserEmotion.Happy, 1f, "thank you");
            AddPhrase(phrases, UserEmotion.Happy, 1.5f, "cant wait");
            AddPhrase(phrases, UserEmotion.Happy, 1.2f, "looking forward");
            AddPhrase(phrases, UserEmotion.Happy, 1f, "well done");
            AddPhrase(phrases, UserEmotion.Happy, 1.2f, "good job");
            AddPhrase(phrases, UserEmotion.Angry, 1.5f, "fed up");
            AddPhrase(phrases, UserEmotion.Angry, 1.8f, "pissed off");
            AddPhrase(phrases, UserEmotion.Angry, 1.2f, "sick of");
            AddPhrase(phrases, UserEmotion.Angry, 1.2f, "shut up");
            AddPhrase(phrases, UserEmotion.Surprised, 1f, "no way");
            AddPhrase(phrases, UserEmotion.Surprised, 1.2f, "oh my god");
            AddPhrase(phrases, UserEmotion.Anxious, 1f, "oh no");
            AddPhrase(phrases, UserEmotion.Anxious, 1.5f, "freaking out");
            AddPhrase(phrases, UserEmotion.Sad, 1.2f, "give up");
            AddPhrase(phrases, UserEmotion.Sad, 1.2f, "feel down");
            AddPhrase(phrases, UserEmotion.Sad, 1.2f, "feeling down");
            AddPhrase(phrases, UserEmotion.Confused, 1.5f, "dont understand");
            AddPhrase(phrases, UserEmotion.Confused, 1.5f, "do not understand");
            AddPhrase(phrases, UserEmotion.Confused, 1.2f, "dont get it");
            AddPhrase(phrases, UserEmotion.Confused, 1f, "no idea");
            AddPhrase(phrases, UserEmotion.Confused, 1f, "not sure");
            AddPhrase(phrases, UserEmotion.Confused, 1.5f, "makes no sense");
            AddPhrase(phrases, UserEmotion.Confused, 1.2f, "what do you mean");

            // Finnish
            AddPhrase(phrases, UserEmotion.Confused, 1.5f, "en ymmärrä");
            AddPhrase(phrases, UserEmotion.Confused, 1.5f, "en tajua");
            AddPhrase(phrases, UserEmotion.Surprised, 1.2f, "mitä ihmettä");
            AddPhrase(phrases, UserEmotion.Affectionate, 1.5f, "tykkään sinusta");
            AddPhrase(phrases, UserEmotion.Affectionate, 1.5f, "ikävä sinua");

            phrases.Sort((a, b) => b.Tokens.Length.CompareTo(a.Tokens.Length));
            return phrases.ToArray();
        }

        static Symbol[] BuildSymbols()
        {
            var symbols = new List<Symbol>();

            // ":/" is left out on purpose: it is part of every "http://".
            AddSymbols(symbols, UserEmotion.Happy, 1f, ":)", ":-)", "=)", ":D", "xD", "XD", "😀", "😃", "😄", "😁", "😊", "🙂", "🎉");
            AddSymbols(symbols, UserEmotion.Happy, 1.2f, "😂", "🤣");
            AddSymbols(symbols, UserEmotion.Happy, 0.8f, "^^");
            AddSymbols(symbols, UserEmotion.Sad, 1f, ":(", ":-(", "😞", "😔");
            AddSymbols(symbols, UserEmotion.Sad, 1.5f, ":'(", "😢", "😭", "💔");
            AddSymbols(symbols, UserEmotion.Angry, 1.5f, ">:(", "😠", "😡", "🤬");
            AddSymbols(symbols, UserEmotion.Anxious, 1.2f, "😨", "😰", "😱");
            AddSymbols(symbols, UserEmotion.Surprised, 1f, ":O", ":-O", "😮", "😲", "🤯");
            AddSymbols(symbols, UserEmotion.Affectionate, 1.5f, "<3", "❤", "♥", "💕", "😍", "🥰", "😘");
            AddSymbols(symbols, UserEmotion.Confused, 1f, "🤔", "😕");

            return symbols.ToArray();
        }

        static void AddWords(Dictionary<string, Hit> words, UserEmotion emotion, float weight, params string[] entries)
        {
            for (var i = 0; i < entries.Length; i++)
                words[entries[i]] = new Hit(emotion, weight);
        }

        static void AddStems(List<Stem> stems, UserEmotion emotion, float weight, params string[] prefixes)
        {
            for (var i = 0; i < prefixes.Length; i++)
                stems.Add(new Stem(prefixes[i], new Hit(emotion, weight)));
        }

        static void AddPhrase(List<Phrase> phrases, UserEmotion emotion, float weight, string phrase)
        {
            phrases.Add(new Phrase(phrase.Split(' '), new Hit(emotion, weight)));
        }

        static void AddSymbols(List<Symbol> symbols, UserEmotion emotion, float weight, params string[] entries)
        {
            for (var i = 0; i < entries.Length; i++)
                symbols.Add(new Symbol(entries[i], new Hit(emotion, weight)));
        }
    }
}
