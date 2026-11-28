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

        public const string FaceBeats =
            "For a clear change of feeling within one spoken sentence, you may put a face-only tag before the words it belongs to. " +
            "Example dialogue: I thought you forgot, <animation portrait_beat=\"surprised\" /> but you remembered, <animation portrait_beat=\"happy\" /> and that made my day. " +
            "Use at most two face tags per reply, only at meaningful changes. Keep the same line and normal punctuation. " +
            "The face tags are silent and separate from the required main animation tag. Use only a named regular portrait expression.";

        /// <summary>The line to add now: the instruction while the new UI is on, otherwise empty.</summary>
        public static string Current
        {
            get { return NewUiSwitch.Active ? Instruction + (RibbonFaceBeats ? " " + FaceBeats : string.Empty) : string.Empty; }
        }

        public static bool RibbonFaceBeats { get; } =
            System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--ribbon-ui") >= 0;
    }
}
