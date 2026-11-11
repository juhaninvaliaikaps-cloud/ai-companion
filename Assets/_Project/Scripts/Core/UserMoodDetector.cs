using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// Reads the user's mood from one message with word rules. It runs on this
    /// machine, makes no request, and takes well under a millisecond for a chat line,
    /// so the avatar can react before the model has answered.
    /// It is a guess from wording. It does not understand sarcasm or context.
    /// </summary>
    public static class UserMoodDetector
    {
        /// <summary>Only this many characters are read, so a pasted document stays cheap.</summary>
        public const int MaxCharacters = 2000;

        /// <summary>A total below this is treated as no signal.</summary>
        public const float MinimumScore = 0.5f;

        public const float IntensifierFactor = 1.5f;
        public const float DampenerFactor = 0.6f;

        /// <summary>"not happy" counts as mildly sad, at this share of the word's weight.</summary>
        public const float NegatedPositiveFactor = 0.5f;

        public const int NegationWindow = 3;
        public const int ModifierWindow = 2;

        public const float InterrobangScore = 1f;
        public const float DoubleQuestionScore = 0.8f;

        public const float BaseIntensity = 0.25f;
        public const float IntensityPerScore = 0.25f;
        public const float ExclamationBoost = 0.1f;
        public const int MaxExclamations = 3;
        public const float CapsBoost = 0.15f;
        public const int CapsWordsForBoost = 2;
        public const int CapsWordMinLetters = 3;

        const int EmotionCount = 8;

        // On a tie the earlier entry wins. Distress comes first, because missing it
        // is worse than missing a smile.
        static readonly UserEmotion[] TieOrder =
        {
            UserEmotion.Angry,
            UserEmotion.Sad,
            UserEmotion.Anxious,
            UserEmotion.Affectionate,
            UserEmotion.Happy,
            UserEmotion.Surprised,
            UserEmotion.Confused,
        };

        public static UserMood Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return UserMood.Neutral;

            var length = Math.Min(text.Length, MaxCharacters);
            var tokens = new List<string>();
            var breaks = new List<bool>();
            var word = new StringBuilder();
            var scores = new float[EmotionCount];

            var exclamations = 0;
            var capsWords = 0;
            var interrobang = false;
            var doubleQuestion = false;
            var pendingBreak = false;
            var letters = 0;
            var upperLetters = 0;
            var previous = '\0';

            for (var i = 0; i <= length; i++)
            {
                // One step past the end flushes the last word.
                var c = i < length ? text[i] : '\n';

                if (c == '\'' || c == '’')
                {
                    previous = c;
                    continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                    if (char.IsLetter(c))
                    {
                        letters++;
                        if (char.IsUpper(c))
                            upperLetters++;
                    }

                    previous = c;
                    continue;
                }

                if (word.Length > 0)
                {
                    tokens.Add(word.ToString());
                    breaks.Add(pendingBreak);
                    if (letters >= CapsWordMinLetters && upperLetters == letters)
                        capsWords++;
                    word.Length = 0;
                    letters = 0;
                    upperLetters = 0;
                    pendingBreak = false;
                }

                if (IsBreak(c))
                    pendingBreak = true;

                if (c == '!')
                    exclamations++;
                if ((c == '!' && previous == '?') || (c == '?' && previous == '!'))
                    interrobang = true;
                if (c == '?' && previous == '?')
                    doubleQuestion = true;

                previous = c;
            }

            var consumed = new bool[tokens.Count];
            ScorePhrases(tokens, breaks, consumed, scores);
            ScoreWords(tokens, breaks, consumed, scores);
            ScoreSymbols(text, length, scores);

            if (interrobang)
                scores[(int)UserEmotion.Surprised] += InterrobangScore;
            if (doubleQuestion)
                scores[(int)UserEmotion.Confused] += DoubleQuestionScore;

            var best = UserEmotion.Neutral;
            var bestScore = 0f;
            for (var i = 0; i < TieOrder.Length; i++)
            {
                var score = scores[(int)TieOrder[i]];
                if (score > bestScore)
                {
                    bestScore = score;
                    best = TieOrder[i];
                }
            }

            if (best == UserEmotion.Neutral || bestScore < MinimumScore)
                return UserMood.Neutral;

            var intensity = BaseIntensity + IntensityPerScore * bestScore;
            intensity += ExclamationBoost * Math.Min(exclamations, MaxExclamations);
            if (capsWords >= CapsWordsForBoost)
                intensity += CapsBoost;

            return new UserMood(best, intensity);
        }

        static void ScorePhrases(List<string> tokens, List<bool> breaks, bool[] consumed, float[] scores)
        {
            var phrases = UserMoodLexicon.Phrases;
            for (var start = 0; start < tokens.Count; start++)
            {
                if (consumed[start])
                    continue;

                for (var p = 0; p < phrases.Length; p++)
                {
                    var parts = phrases[p].Tokens;
                    if (!Matches(tokens, breaks, consumed, start, parts))
                        continue;

                    Add(tokens, breaks, consumed, scores, start, phrases[p].Hit);
                    for (var k = 0; k < parts.Length; k++)
                        consumed[start + k] = true;
                    break;
                }
            }
        }

        static bool Matches(List<string> tokens, List<bool> breaks, bool[] consumed, int start, string[] parts)
        {
            if (start + parts.Length > tokens.Count)
                return false;

            for (var k = 0; k < parts.Length; k++)
            {
                var index = start + k;
                if (consumed[index])
                    return false;
                // A phrase does not reach across a comma or a full stop.
                if (k > 0 && breaks[index])
                    return false;
                if (!string.Equals(tokens[index], parts[k], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        static void ScoreWords(List<string> tokens, List<bool> breaks, bool[] consumed, float[] scores)
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                if (consumed[i])
                    continue;

                UserMoodLexicon.Hit hit;
                if (UserMoodLexicon.TryGetWord(tokens[i], out hit))
                    Add(tokens, breaks, consumed, scores, i, hit);
            }
        }

        /// <summary>
        /// Adds one hit after looking back for "not", "very", and "a bit" in the same clause.
        /// A negated happy or affectionate hit becomes mildly sad. Any other negated hit
        /// is dropped: "not sad" says little about what the user does feel.
        /// </summary>
        static void Add(List<string> tokens, List<bool> breaks, bool[] consumed, float[] scores, int index, UserMoodLexicon.Hit hit)
        {
            var negated = false;
            var intensified = false;
            var dampened = false;

            for (var back = 1; back <= NegationWindow; back++)
            {
                var j = index - back;
                if (j < 0)
                    break;
                // breaks[j + 1] is true when punctuation sits between token j and the next one.
                if (breaks[j + 1])
                    break;
                if (consumed[j])
                    continue;

                var token = tokens[j];
                if (UserMoodLexicon.IsNegator(token))
                    negated = true;
                if (back <= ModifierWindow)
                {
                    if (UserMoodLexicon.IsIntensifier(token))
                        intensified = true;
                    if (UserMoodLexicon.IsDampener(token))
                        dampened = true;
                }
            }

            var emotion = hit.Emotion;
            var weight = hit.Weight;
            if (intensified)
                weight *= IntensifierFactor;
            if (dampened)
                weight *= DampenerFactor;

            if (negated)
            {
                if (emotion != UserEmotion.Happy && emotion != UserEmotion.Affectionate)
                    return;

                emotion = UserEmotion.Sad;
                weight *= NegatedPositiveFactor;
            }

            scores[(int)emotion] += weight;
        }

        static void ScoreSymbols(string text, int length, float[] scores)
        {
            var symbols = UserMoodLexicon.Symbols;
            for (var i = 0; i < symbols.Length; i++)
            {
                if (text.IndexOf(symbols[i].Text, 0, length, StringComparison.Ordinal) >= 0)
                    scores[(int)symbols[i].Hit.Emotion] += symbols[i].Hit.Weight;
            }
        }

        static bool IsBreak(char c)
        {
            return c == '.' || c == ',' || c == '!' || c == '?' || c == ';' || c == ':' || c == '\n' || c == '\r';
        }
    }
}
