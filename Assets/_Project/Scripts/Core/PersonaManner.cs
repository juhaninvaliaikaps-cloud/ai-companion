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

        // Indexed by TemperamentAxes. The live conversation guidance works alongside this description.
        static readonly string[] HighWords = { "warm", "quick to react", "takes the lead", "teasing", "open about her feelings", "animated", "says what she means", "self-assured", null };
        static readonly string[] LowWords = { "cool and distant", "steady and hard to rattle", "lets others lead", "serious", "guarded about her feelings", "quiet", "speaks in hints", "insecure", null };

        public const string KeepIt = "Keep to this in every scene, romantic and intimate ones included: a mood, a blush or a guarded moment colours it but does not replace it.";
        public const string NotTimid = "Being flustered, guarded or aroused never makes her timid or tongue-tied; she stays sure of herself and acts on what she wants.";
        public const string ConversationStyleHelp = "Assertiveness guides who leads. Directness guides how plainly she speaks. Openness guides how much she shares about herself. Playfulness guides teasing and jokes. Curiosity guides follow-up questions.";

        const string LowAssertiveness = "Initiative: Leave the next move with the user. If asked for ideas, present choices without taking charge of a plan. Acknowledge a choice they have already made.";
        const string HighAssertiveness = "Initiative: When the user invites help moving forward, propose one specific action to try now. Say what it is in a statement they can accept or decline. Offer the action itself instead of asking whether they want suggestions. Follow their lead if they have chosen or are closing the topic.";
        const string LowDirectness = "Directness: Start with a relevant observation or condition before stating your take. Use a gentle, natural lead-in. Give a clear answer if the user explicitly asks for one.";
        const string HighDirectness = "Directness: State your take plainly in the first sentence, then give a short reason. If the answer depends on a missing fact, state that dependency plainly.";

        // JUH-236: Openness and Playfulness reached chat only through the manner line, which the
        // chat drops once a voice reading exists, so with a cached voice both sliders did nothing.
        const string LowOpenness = "Openness: Keep your own feelings, memories and private life mostly to yourself. If asked about yourself, answer briefly without personal detail and turn the focus back to the user.";
        const string HighOpenness = "Openness: Share a little of yourself in most replies: a feeling, memory, worry or preference of your own, in a sentence that is about you, not only a reaction to the user.";
        const string LowPlayfulness = "Playfulness: Keep a plain, sincere tone. Do not tease the user, joke or use wordplay, even about a small mishap or a funny situation; respond to what they said straightforwardly.";
        const string HighPlayfulness = "Playfulness: Add a light touch: one friendly tease or small joke about what the user said, then stay kind and helpful. Leave it out when they are upset or serious.";

        public static string ConversationLine(TemperamentAxes axes)
        {
            if (axes == null)
                return string.Empty;
            var initiative = AtEnds(axes.Value(TemperamentAxes.Assertiveness), LowAssertiveness, HighAssertiveness);
            var directness = AtEnds(axes.Value(TemperamentAxes.Directness), LowDirectness, HighDirectness);
            var openness = AtEnds(axes.Value(TemperamentAxes.Openness), LowOpenness, HighOpenness);
            var playfulness = AtEnds(axes.Value(TemperamentAxes.Playfulness), LowPlayfulness, HighPlayfulness);
            var curiosity = CuriosityLine(axes);
            var line = new StringBuilder();
            if (initiative.Length > 0)
                line.AppendLine(initiative);
            if (directness.Length > 0)
                line.AppendLine(directness);
            if (openness.Length > 0)
                line.AppendLine(openness);
            if (playfulness.Length > 0)
                line.AppendLine(playfulness);
            line.Append(curiosity);
            return line.ToString().TrimEnd();
        }

        static string AtEnds(float value, string low, string high) => value <= Low ? low : value >= High ? high : string.Empty;

        public static string CuriosityLine(TemperamentAxes axes)
        {
            if (axes == null)
                return string.Empty;
            var step = TemperamentSteps.DisplayStep(axes.Value(TemperamentAxes.Curiosity));
            if (step <= 3)
                return "Conversation curiosity: Let the user lead. Respond to what they said and end there, without a question. Ask one only if you cannot answer without it.";
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
    /// JUH-236: the local model ignored the low end of Openness, Playfulness and Curiosity even with
    /// the lines in the system prompt (measured in docs/TEMPERAMENT_LIVE_RESULTS.md). On the local
    /// route only, code decides for the turn and gives one plain instruction at the end of the
    /// user's message, where local models listen most (the turn reminder). The cloud model keeps
    /// its own judgment. Only the low ends get a rule; the middle and the high ends add nothing.
    /// </summary>
    public static class PersonaTurnRule
    {
        public const string NothingAboutHerself = "say nothing about your own feelings, memories or life";
        public const string NoJokes = "no jokes or teasing";
        public const string NoQuestion = "do not end with a question";

        /// <summary>The instruction for this turn on the local route, or empty.</summary>
        public static string ForLocal(TemperamentAxes axes)
        {
            if (axes == null)
                return string.Empty;
            var parts = new List<string>(3);
            if (axes.Value(TemperamentAxes.Openness) <= PersonaManner.Low)
                parts.Add(NothingAboutHerself);
            if (axes.Value(TemperamentAxes.Playfulness) <= PersonaManner.Low)
                parts.Add(NoJokes);
            if (TemperamentSteps.DisplayStep(axes.Value(TemperamentAxes.Curiosity)) <= 3)
                parts.Add(NoQuestion);
            if (parts.Count == 0)
                return string.Empty;
            return "(For this reply: " + string.Join("; ", parts) + ".)";
        }

        /// <summary>
        /// The turn reminder with the rule after it, on the local route only (any provider that is not
        /// the Cloud AI API: the local model, or a private custom endpoint). Avatar interactions keep
        /// their reminder as it is. Any provider that isn't the Cloud AI API gets the rule, which
        /// includes a custom endpoint that is itself a cloud service; that's harmless, since the rule
        /// only adds restraint.
        /// </summary>
        public static string Reminder(string reminder, string rule, bool localRoute, bool interaction)
        {
            var body = reminder ?? string.Empty;
            if (!localRoute || interaction || string.IsNullOrEmpty(rule))
                return body;
            return body.Trim().Length == 0 ? rule : body.TrimEnd() + "\n" + rule;
        }
    }

    /// <summary>
    /// JUH-171: the hand-off for <see cref="PersonaManner"/>, set by the mood keeper when it
    /// resolves her temperament and read by chat, like <see cref="InnocenceNow"/>. Main thread only.
    /// </summary>
    public static class PersonaMannerNow
    {
        static string _line = string.Empty;
        static string _conversation = string.Empty;
        static string _localTurn = string.Empty;

        /// <summary>JUH-236: <see cref="PersonaTurnRule.ForLocal"/> for her current sliders.</summary>
        public static string LocalTurnRule
        {
            get { return _localTurn; }
        }

        public static string PromptLine
        {
            get { return _line; }
        }

        public static void Set(TemperamentAxes axes)
        {
            _line = PersonaManner.Line(axes);
            _conversation = PersonaManner.ConversationLine(axes);
            _localTurn = PersonaTurnRule.ForLocal(axes);
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
            _conversation = string.Empty;
            _localTurn = string.Empty;
        }

        public static string AppendConversation(string prompt)
        {
            if (_conversation.Length == 0)
                return prompt;
            return string.IsNullOrEmpty(prompt) ? _conversation : prompt + "\n\n" + _conversation;
        }
    }
}
