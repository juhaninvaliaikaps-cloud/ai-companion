namespace AICompanion.Core
{
    /// <summary>
    /// The line a touch sends to the model. It reads like something that happened to her, so a
    /// human-like reaction follows: the companion sees the touch, not a command.
    /// </summary>
    public static class TouchChatSignal
    {
        public const string HeadText = "*Someone gently pats your head.*";
        public const string HandText = "*Someone takes your hand and holds it.*";
        public const string ShoulderText = "*Someone touches your shoulder.*";
        public const string GenericText = "*Someone touches you.*";

        public static string Prompt(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return GenericText;

            var id = zoneId.Trim().ToLowerInvariant();
            if (id == "head")
                return HeadText;
            if (id == "hand_left" || id == "hand_right" || id == "hand")
                return HandText;
            if (id == "shoulder_left" || id == "shoulder_right" || id == "shoulder")
                return ShoulderText;
            return GenericText;
        }

        public static bool IsHoldZone(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return false;

            var id = zoneId.Trim().ToLowerInvariant();
            return id == "hand_left" || id == "hand_right" || id == "hand";
        }
    }
}
