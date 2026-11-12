namespace AICompanion.AI
{
    /// <summary>
    /// A text token the model can write to ask for one look at the screen. It is stripped from
    /// the visible reply, and it is only honoured when the user has enabled automatic looks.
    /// </summary>
    public static class ScreenLookCue
    {
        public const string Token = "[look]";

        public const string PromptLine =
            " You may ask for one look at the user's screen by writing [look] in your reply. " +
            "Only do this when the user's message makes the screen genuinely useful, at most once per reply. " +
            "The token is removed before the user sees the reply; do not explain it.";

        public const string SilentMarker = "SILENT";

        public static bool Requests(string text)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(Token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var cleaned = text;
            while (true)
            {
                var at = cleaned.IndexOf(Token, System.StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                    break;
                cleaned = cleaned.Remove(at, Token.Length);
            }

            // Remove the empty space the token left in the middle of a sentence.
            cleaned = cleaned.Replace("  ", " ");
            return cleaned.Trim();
        }

        public static bool IsSilent(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;
            var trimmed = text.Trim().Trim('"', '\'', '.', '!', '*', ' ');
            return string.Equals(trimmed, SilentMarker, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
