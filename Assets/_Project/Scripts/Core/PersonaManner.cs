using System.Collections.Generic;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-171: one prompt line for how she carries herself, from her nine temperament sliders.
    /// Pisara read them (or the user set them), but before this only her mood and game remarks
    /// used them: a self-assured, assertive swordswoman still came out timid and stammering in
    /// romantic scenes, because the chat model never heard it and the mood and relationship lines
    /// (flustered, guarded) pulled the other way. Only clear traits are named (70 or more, 30 or
    /// less); a profile at 50 gives no line.
    /// </summary>
    public static class PersonaManner
    {
        public const int High = 70;
        public const int Low = 30;

        // Indexed by TemperamentAxes; curiosity has no word here.
        static readonly string[] HighWords = { "warm", "quick to react", "takes the lead", "teasing", "open about her feelings", "animated", "says what she means", "self-assured", null };
        static readonly string[] LowWords = { "cool and distant", "steady and hard to rattle", "lets others lead", "serious", "guarded about her feelings", "quiet", "speaks in hints", "insecure", null };

        public const string KeepIt = "Keep to this in every scene, romantic and intimate ones included: a mood, a blush or a guarded moment colours it but does not replace it.";
        public const string NotTimid = "Being flustered, guarded or aroused never makes her timid or tongue-tied; she stays sure of herself and acts on what she wants.";

        /// <summary>The line, or empty when no slider is clear enough to name.</summary>
        public static string Line(TemperamentAxes axes)
        {
            if (axes == null)
                return string.Empty;
            var words = new List<string>(4);
            for (var i = 0; i < TemperamentAxes.Count; i++)
            {
                var value = axes.Value(i);
                var word = value >= High ? HighWords[i] : value <= Low ? LowWords[i] : null;
                if (word != null)
                    words.Add(word);
            }

            if (words.Count == 0)
                return string.Empty;
            var sb = new StringBuilder("Her manner: ");
            for (var i = 0; i < words.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(words[i]);
            }

            sb.Append(". ").Append(KeepIt);
            if (axes.Value(TemperamentAxes.Confidence) >= High || axes.Value(TemperamentAxes.Assertiveness) >= High)
                sb.Append(' ').Append(NotTimid);
            return sb.ToString();
        }

        /// <summary>
        /// JUH-224: the behavioural half of Assertiveness and Directness, added after a cached
        /// voice block. The manner line is deliberately skipped when cached voice exists; this
        /// line keeps the two displayed controls live without re-reading the voice.
        /// </summary>
        public static string Directive(TemperamentAxes axes)
        {
            if (axes == null)
                return string.Empty;
            var assertiveness = axes.Value(TemperamentAxes.Assertiveness);
            var directness = axes.Value(TemperamentAxes.Directness);
            var parts = new List<string>(2);
            if (assertiveness >= High)
                parts.Add("Offer one concrete next step yourself when there is a natural opening.");
            else if (assertiveness <= Low)
                parts.Add("Let the user name the next step; do not invent one for them.");

            if (directness >= High)
                parts.Add("State your take plainly and early.");
            else if (directness <= Low)
                parts.Add("Hint at your take rather than stating it outright.");

            return parts.Count == 0 ? string.Empty : "How she acts in this reply: " + string.Join(" ", parts);
        }
    }

    /// <summary>
    /// JUH-171: the hand-off for <see cref="PersonaManner"/>, set by the mood keeper when it
    /// resolves her temperament and read by chat, like <see cref="InnocenceNow"/>. Main thread only.
    /// </summary>
    public static class PersonaMannerNow
    {
        static string _line = string.Empty;
        static string _directive = string.Empty;

        public static string PromptLine
        {
            get { return _line; }
        }

        public static string DirectiveLine
        {
            get { return _directive; }
        }

        public static void Set(TemperamentAxes axes)
        {
            _line = PersonaManner.Line(axes);
            _directive = PersonaManner.Directive(axes);
        }

        /// <summary>The prompt with her manner after it, or unchanged when there is none.</summary>
        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_line.Length == 0)
                return body;
            return body.Length == 0 ? _line : body + "\n" + _line;
        }

        /// <summary>
        /// The behaviour line after the cached voice or the rest of the prompt. This is the
        /// JUH-224 path that keeps Assertiveness and Directness live when <see cref="Append"/>
        /// is skipped because a cached voice block exists.
        /// </summary>
        public static string AppendDirective(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_directive.Length == 0)
                return body;
            return body.Length == 0 ? _directive : body + "\n" + _directive;
        }

        public static void Reset()
        {
            _line = string.Empty;
            _directive = string.Empty;
        }
    }
}
