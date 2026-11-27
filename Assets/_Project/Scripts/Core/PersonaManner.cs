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

        // Indexed by TemperamentAxes. Curiosity guides conversation separately.
        static readonly string[] HighWords = { "warm", "quick to react", "takes the lead", "teasing", "open about her feelings", "animated", "says what she means", "self-assured", null };
        static readonly string[] LowWords = { "cool and distant", "steady and hard to rattle", "lets others lead", "serious", "guarded about her feelings", "quiet", "speaks in hints", "insecure", null };

        public const string KeepIt = "Keep to this in every scene, romantic and intimate ones included: a mood, a blush or a guarded moment colours it but does not replace it.";
        public const string NotTimid = "Being flustered, guarded or aroused never makes her timid or tongue-tied; she stays sure of herself and acts on what she wants.";
        public const string CuriosityHelp = "Curiosity guides follow-up questions in chat. Low lets you lead. High invites her to ask about a detail you shared.";

        public static string CuriosityLine(TemperamentAxes axes)
        {
            if (axes == null)
                return string.Empty;
            var step = TemperamentSteps.DisplayStep(axes.Value(TemperamentAxes.Curiosity));
            if (step <= 3)
                return "Conversation curiosity: Let the user lead. Respond directly and usually leave it there. Ask a clarifying question if you need one to answer.";
            if (step >= 9)
                return "Conversation curiosity: When a detail the user just shared opens a natural thread, respond to them first, then explore it with one specific follow-up. Skip generic, repeated or intrusive questions and let a complete moment end.";
            if (step >= 7)
                return "Conversation curiosity: A specific follow-up is welcome when the user leaves an interesting thread open. Ask at most one and let a complete moment end.";
            return string.Empty;
        }

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
    }

    /// <summary>
    /// JUH-171: the hand-off for <see cref="PersonaManner"/>, set by the mood keeper when it
    /// resolves her temperament and read by chat, like <see cref="InnocenceNow"/>. Main thread only.
    /// </summary>
    public static class PersonaMannerNow
    {
        static string _line = string.Empty;
        static string _curiosity = string.Empty;

        public static string PromptLine
        {
            get { return _line; }
        }

        public static void Set(TemperamentAxes axes)
        {
            _line = PersonaManner.Line(axes);
            _curiosity = PersonaManner.CuriosityLine(axes);
        }

        /// <summary>The prompt with her manner after it, or unchanged when there is none.</summary>
        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_line.Length == 0)
                return body;
            return body.Length == 0 ? _line : body + "\n" + _line;
        }

        public static void Reset()
        {
            _line = string.Empty;
            _curiosity = string.Empty;
        }

        public static string AppendCuriosity(string prompt)
        {
            if (_curiosity.Length == 0)
                return prompt;
            return string.IsNullOrEmpty(prompt) ? _curiosity : prompt + "\n\n" + _curiosity;
        }
    }
}
