namespace AICompanion.Core
{
    /// <summary>
    /// JUH-150: the active persona's 18+ notes, handed to chat like
    /// <see cref="IntimateSecretsNow"/>. <see cref="Append"/> adds nothing while the 18+ gate is
    /// closed, and an open gate keeps chat on the local model, so the notes never reach the cloud.
    /// </summary>
    public static class AdultNotesNow
    {
        public const string Label = "Private notes about her (18+):";

        static string _notes = string.Empty;

        public static string Notes
        {
            get { return _notes; }
        }

        public static void Set(string notes)
        {
            _notes = string.IsNullOrWhiteSpace(notes) ? string.Empty : notes.Trim();
        }

        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_notes.Length == 0 || !IntimateExpressions.Active)
                return body;
            var block = Label + "\n" + _notes;
            return body.Length == 0 ? block : body + "\n\n" + block;
        }

        public static void Reset()
        {
            _notes = string.Empty;
        }
    }
}
