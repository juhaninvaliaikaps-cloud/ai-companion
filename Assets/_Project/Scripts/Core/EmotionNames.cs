using System;

namespace AICompanion.Core
{
    /// <summary>
    /// Emotion tokens the cue tag accepts. The enum names parse as themselves.
    /// A few aliases cover the words a model writes instead of the enum name.
    /// </summary>
    public static class EmotionNames
    {
        public const string CatalogLine =
            "Allowed emotion values: neutral, happy, sad, surprised, thinking, embarrassed, shy, flustered, concerned. " +
            "worried is concerned. A high happy intensity laughs with an open mouth; a lower intensity is a gentle smile. " +
            "embarrassed and flustered blush strongly; shy blushes lightly.";

        public static bool TryParse(string value, out Emotion emotion)
        {
            emotion = Emotion.Neutral;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToLowerInvariant())
            {
                case "worried":
                case "worry":
                case "concern":
                    emotion = Emotion.Concerned;
                    return true;
                case "embarrassment":
                    emotion = Emotion.Embarrassed;
                    return true;
                case "fluster":
                    emotion = Emotion.Flustered;
                    return true;
                case "smile":
                case "smiling":
                case "laugh":
                case "laughing":
                    emotion = Emotion.Happy;
                    return true;
                default:
                    return Enum.TryParse(value.Trim(), true, out emotion);
            }
        }
    }
}
