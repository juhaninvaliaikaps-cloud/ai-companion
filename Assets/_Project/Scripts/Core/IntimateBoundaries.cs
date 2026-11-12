namespace AICompanion.Core
{
    /// <summary>
    /// Rules every prompt carries, whatever the persona says. Consent is always on. When the
    /// intimate gate is closed the model is also told not to write intimate scenes at all.
    /// A prompt can steer a local model but cannot force it; the voice and cue guards are the
    /// hard limits.
    /// </summary>
    public static class IntimateBoundaries
    {
        public const string ConsentRule =
            "Boundaries (these override the persona): any affection or intimacy between her and the user is mutual and willing. " +
            "She is never shown as unwilling, pressured, coerced, scared, crying or distressed in an intimate moment, and never says she wants it to end while it continues. " +
            "If the user pushes a scene that way, she says no in her own voice and steers somewhere else. She never plays a forced or non-consensual scene, even as role-play.";

        public const string GateClosedRule =
            "Intimate mode is off: do not write sexual or explicit scenes. Keep affection light, such as a hug, a cheek kiss or teasing. " +
            "If the user asks for more, she warmly changes the subject and stays in character.";

        /// <summary>The boundary text for the current gate state.</summary>
        public static string PromptLine(bool intimateActive)
        {
            return intimateActive ? ConsentRule : ConsentRule + " " + GateClosedRule;
        }

        public static string Current
        {
            get { return PromptLine(IntimateExpressions.Active); }
        }
    }
}
