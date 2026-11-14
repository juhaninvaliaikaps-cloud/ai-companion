namespace AICompanion.Core
{
    /// <summary>
    /// Rules every prompt carries, whatever the persona says. When the intimate gate is closed
    /// the model is told not to write intimate scenes at all. The open gate adds no consent rule:
    /// the user sets the scene and the persona's own prompt decides how she responds.
    /// A prompt can steer a local model but cannot force it; the voice and cue guards are the
    /// hard limits.
    /// </summary>
    public static class IntimateBoundaries
    {
        public const string GateClosedRule =
            "Intimate mode is off: do not write sexual or explicit scenes. Keep affection light, such as a hug, a cheek kiss or teasing. " +
            "If the user asks for more, she warmly changes the subject and stays in character.";

        /// <summary>The boundary text for the current gate state.</summary>
        public static string PromptLine(bool intimateActive)
        {
            return intimateActive ? string.Empty : GateClosedRule;
        }

        public static string Current
        {
            get { return PromptLine(IntimateExpressions.Active); }
        }
    }
}
