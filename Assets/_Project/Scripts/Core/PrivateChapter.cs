namespace AICompanion.Core
{
    /// <summary>
    /// JUH-177: what public mode may know about a private chapter of the conversation (Jussi,
    /// 2026-11-22). The private lines themselves never go to the cloud; this only decides the one
    /// sentence code writes in their place.
    /// </summary>
    public enum PrivateChapterHint
    {
        /// <summary>Nothing at all: the public model is not told that anything happened.</summary>
        Nothing = 0,

        /// <summary>Only that a private part happened, so she does not act as if nothing did.</summary>
        ThatItHappened = 1,

        /// <summary>What kind of moment it was, from a fixed list (personal, romantic, intimate). The default.</summary>
        WhatKind = 2,
    }

    /// <summary>The kind of a private chapter: from a fixed list, never from its text.</summary>
    public enum PrivateChapterKind
    {
        Personal = 0,
        Romantic = 1,
        Intimate = 2,
    }

    /// <summary>
    /// JUH-177: follows a private chapter while it happens, from data the app already keeps for
    /// each reply (the cue's landing, heat and expression, her mood, whether it was an 18+
    /// interaction). It keeps only the strongest kind seen, never any text. Plain C#.
    /// </summary>
    public sealed class PrivateChapter
    {
        /// <summary>True once a private reply has been noted (since the last <see cref="Reset"/>).</summary>
        public bool Known { get; private set; }

        public PrivateChapterKind Kind { get; private set; }

        /// <summary>The kind one private reply points to.</summary>
        public static PrivateChapterKind KindOf(LandingEvent landed, DesireHeat heat, string emotionField, CompanionMood mood, bool adultLine)
        {
            if (adultLine || heat == DesireHeat.Hot || heat == DesireHeat.Release || IntimateExpressions.IsIntimateId(emotionField)
                || mood == CompanionMood.Intimate)
                return PrivateChapterKind.Intimate;
            if (heat == DesireHeat.Warm || landed == LandingEvent.Flirted || mood == CompanionMood.Affectionate)
                return PrivateChapterKind.Romantic;
            return PrivateChapterKind.Personal;
        }

        public void Note(LandingEvent landed, DesireHeat heat, string emotionField, CompanionMood mood, bool adultLine)
        {
            var kind = KindOf(landed, heat, emotionField, mood, adultLine);
            if (!Known || kind > Kind)
                Kind = kind;
            Known = true;
        }

        public void Reset()
        {
            Known = false;
            Kind = PrivateChapterKind.Personal;
        }
    }

    /// <summary>
    /// JUH-177: the sentence a public request gets when private lines were left out of it.
    /// Written by code from a fixed list, so no private text can pass through.
    /// </summary>
    public static class PrivateChapterPrompt
    {
        public const string Happened =
            "Earlier in this conversation you and the user talked in private; that part is not shown here and stays private. " +
            "Don't guess at it, retell it or ask about it, and don't act as if nothing happened.";

        public const string Personal =
            "Earlier you and the user had a private, personal talk; it is not shown here and stays private. Don't retell it or bring up details.";

        public const string Romantic =
            "Earlier you and the user shared a private romantic moment; it is not shown here and stays private. " +
            "Don't describe it or bring up details; it can leave you feeling closer to them.";

        public const string Intimate =
            "Earlier you and the user shared a private intimate moment; it is not shown here and stays private. " +
            "Don't describe it or bring up details; it can leave you feeling closer and warmer toward them.";

        /// <summary>
        /// The sentence, or empty for <see cref="PrivateChapterHint.Nothing"/>. Without a known
        /// chapter (after a restart, say) "what kind" falls back to "that it happened".
        /// </summary>
        public static string Line(PrivateChapterHint hint, PrivateChapter chapter)
        {
            if (hint == PrivateChapterHint.Nothing)
                return string.Empty;
            if (hint == PrivateChapterHint.ThatItHappened || chapter == null || !chapter.Known)
                return Happened;
            switch (chapter.Kind)
            {
                case PrivateChapterKind.Intimate: return Intimate;
                case PrivateChapterKind.Romantic: return Romantic;
                default: return Personal;
            }
        }

        /// <summary>The note in the chat when the user switches mode.</summary>
        public static string SwitchNote(bool nowPrivate, PrivateChapterHint hint)
        {
            if (nowPrivate)
                return "Private mode on: the chat stays on this PC, with the local model.";
            switch (hint)
            {
                case PrivateChapterHint.Nothing:
                    return "Public mode on: what was said in private stays on this PC, and she won't know it happened.";
                case PrivateChapterHint.ThatItHappened:
                    return "Public mode on: what was said in private stays on this PC; she only knows something private happened.";
                default:
                    return "Public mode on: what was said in private stays on this PC; she only knows what kind of moment it was.";
            }
        }

        public static PrivateChapterHint Normalize(int value)
        {
            return value < 0 || value > 2 ? PrivateChapterHint.WhatKind : (PrivateChapterHint)value;
        }
    }

    /// <summary>JUH-177: the user's setting, set by the settings side, read by chat. Main thread only.</summary>
    public static class PrivateChapterNow
    {
        public static PrivateChapterHint Hint { get; private set; } = PrivateChapterHint.WhatKind;

        public static void Configure(int hint)
        {
            Hint = PrivateChapterPrompt.Normalize(hint);
        }

        public static void Reset()
        {
            Hint = PrivateChapterHint.WhatKind;
        }
    }
}
