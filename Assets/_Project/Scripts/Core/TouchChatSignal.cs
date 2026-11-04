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
        public const string HairText = "*Someone strokes your hair.*";
        public const string CheekText = "*Someone cups your cheek.*";
        public const string ForearmText = "*Someone runs a hand along your arm.*";
        public const string BackText = "*Someone touches your back.*";
        public const string SideText = "*Someone pokes your side.*";
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
            if (id == "hair")
                return HairText;
            if (id == "cheek" || id == "face")
                return CheekText;
            if (id == "forearm_left" || id == "forearm_right" || id == "forearm")
                return ForearmText;
            if (id == "belly")
                return GenericText;
            if (id == "back")
                return BackText;
            if (id == "side" || id == "waist")
                return SideText;
            return GenericText;
        }

        public const string HoldText = "*Someone holds your hand for a while, then lets go.*";
        public const string HoldStartText = "*Someone takes your hand and holds it.*";

        /// <summary>The line sent when a hand drag ends, so holding reads as holding.</summary>
        public static string HoldPrompt(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return HoldText;

            var id = zoneId.Trim().ToLowerInvariant();
            return id == "hand_left" || id == "hand_right" || id == "hand" ? HoldText : GenericText;
        }

        /// <summary>The line sent when a hand drag starts, so she reacts while it is happening.</summary>
        public static string HoldStartPrompt(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return HoldStartText;

            var id = zoneId.Trim().ToLowerInvariant();
            return id == "hand_left" || id == "hand_right" || id == "hand" ? HoldStartText : GenericText;
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
