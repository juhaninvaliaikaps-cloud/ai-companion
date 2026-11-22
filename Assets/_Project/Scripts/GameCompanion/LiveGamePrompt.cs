namespace AICompanion.GameCompanion
{
    /// <summary>
    /// JUH-160: a short, opt-in live commentary line for a game with no event feed.
    /// Unlike the gacha look, this is asked only after the frame actually changed.
    /// </summary>
    public static class LiveGamePrompt
    {
        public const string Silent = "SILENT";

        public const string Rules =
            "You are watching the game while the user plays. Comment only on what visibly changed in this frame. " +
            "One or two short spoken sentences, in your own voice. Do not describe static menus, HUD numbers, loading screens, " +
            "or the fact that you received a picture. Do not invent events that are not visible. " +
            "Do not mention private or hidden information. If there is nothing worth saying, reply with exactly SILENT. " +
            "Do not use stage directions or tags.";

        public static string Turn(GameTitle game)
        {
            return "Look at this new frame from " + CommentaryPrompt.GameName(game)
                + ". What changed, and what is worth saying out loud?";
        }

        public static bool IsSilent(string reply)
        {
            var text = (reply ?? string.Empty).Trim();
            return text.Length == 0 || text.Equals(Silent, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
