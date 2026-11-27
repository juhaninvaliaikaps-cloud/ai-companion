using AICompanion.Core;
using AICompanion.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-174: the colour of a note line in the chat box. Scene text (the first scene, "While you
    /// were away") is a soft green that reads well on the dark box; a change line takes its
    /// floating piece's colour, so the chat keeps a readable copy of every change: pink up, grey
    /// down, gold for a critical gain or a secret, light blue for familiarity, magenta and violet
    /// for desire. Plain notes keep the muted note colour (empty here).
    /// </summary>
    public static class ChatLineColors
    {
        public const string Scene = "#8FE3B0";
        public const string Up = "#FF7EB6";
        public const string Down = "#B4C3DC";
        public const string Gold = "#FFD166";
        public const string Familiarity = "#9FD4FF";
        const string RibbonFamiliarity = "#68F5BD";
        public const string DesireUp = "#E86BFF";
        public const string DesireDown = "#B79CE0";

        /// <summary>The hex colour for this line, or empty for a plain note, a user line or a reply.</summary>
        public static string For(ChatLine line)
        {
            if (line == null || line.Kind != ChatLineKind.System)
                return string.Empty;
            if (line.Tag == ChatLineTag.Scene)
                return Scene;
            if (line.Tag != ChatLineTag.Change)
                return string.Empty;
            switch ((FeedbackKind)line.Change)
            {
                case FeedbackKind.Up: return Up;
                case FeedbackKind.CritUp:
                case FeedbackKind.Secret: return Gold;
                case FeedbackKind.Familiarity: return RibbonMenu.Enabled ? RibbonFamiliarity : Familiarity;
                case FeedbackKind.DesireUp: return DesireUp;
                case FeedbackKind.DesireDown: return DesireDown;
                default: return Down;
            }
        }

        /// <summary>The line's text for the chat box: coloured, scene text in italics, the text itself literal.</summary>
        public static string Format(ChatLine line, string literalText)
        {
            var color = For(line);
            if (color.Length == 0)
                return literalText;
            var body = "<color=" + color + ">" + literalText + "</color>";
            if (RibbonMenu.Enabled && line.Tag == ChatLineTag.Change)
            {
                switch ((FeedbackKind)line.Change)
                {
                    case FeedbackKind.Secret:
                        return "<mark=#FFD16624><b>" + body + "</b></mark>";
                    case FeedbackKind.Familiarity:
                        return "<mark=#68F5BD20><b>" + body + "</b></mark>";
                    case FeedbackKind.Up:
                    case FeedbackKind.CritUp:
                        return "<b>" + body + "</b>";
                }
            }
            return line.Tag == ChatLineTag.Scene ? "<i>" + body + "</i>" : body;
        }
    }
}
