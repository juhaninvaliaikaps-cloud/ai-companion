namespace AICompanion.Core
{
    /// <summary>
    /// JUH-108: one prompt line for the visual-novel chat, so the model marks narration
    /// reliably and may place a cue tag where a later part of the reply begins. Added only
    /// while the new UI is on; with it off the prompt is exactly as on main.
    /// </summary>
    public static class VisualNovelPrompt
    {
        public const string Instruction =
            "Write her actions and any narration between single *asterisks*, and write her spoken words without asterisks. " +
            "Keep each action or spoken part short. " +
            "You may start a later part of the reply with another cue tag, for example <animation emotion=\"surprised\" />, " +
            "so her face changes where that part begins.";

        /// <summary>The line to add now: the instruction while the new UI is on, otherwise empty.</summary>
        public static string Current
        {
            get { return NewUiSwitch.Active ? Instruction : string.Empty; }
        }
    }
}
