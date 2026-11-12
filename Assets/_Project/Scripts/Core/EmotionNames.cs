using System;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// Emotion tokens the cue tag accepts. The enum names parse as themselves.
    /// A few aliases cover the words a model writes instead of the enum name.
    /// </summary>
    public static class EmotionNames
    {
        public const int MaxFieldLength = 64;

        public const string CatalogLine =
            "Choose the emotion that matches this reply. Do not default to happy or embarrassed. " +
            "happy: good news, a joke, or a warm greeting. A high intensity laughs with an open mouth; a lower intensity is a gentle smile. " +
            "sad: sympathy, disappointment, or bad news. " +
            "surprised: something unexpected, a sudden reveal, or shock. " +
            "thinking: working something out, unsure, or pausing to consider. " +
            "embarrassed: a compliment or being caught out. Strong blush. " +
            "shy: quiet and looking away, with a small smile. Light blush. " +
            "flustered: lost for words or overwhelmed. Strong blush and an open mouth. " +
            "concerned: worried about the user or about what they said. Inner brows, and no blush. worried means concerned. " +
            "pouty or angry: playful annoyance. Use emotion sad, or gesture pouty. " +
            "excited: delighted and energetic. Use emotion happy, or gesture excited. " +
            "teasing: a smug knowing smile. Use gesture teasing. " +
            "loving: warm and affectionate. Use gesture loving. " +
            "sleepy: tired. Use gesture sleepy. " +
            "laughing: an open laugh. Use gesture laughing. The face is the happy smile. " +
            "Also available, and the 2D portrait has drawn frames for each: crying (strong sadness), teary_happy, " +
            "shocked (strong surprise), confused, bored, scared, tsundere, determined, angry, smug, smug_wink, sleepy, " +
            "embarrassed_worried, flustered_heavy, seductive, kiss. " +
            "Synonyms: afraid or fear means scared; annoyed means tsundere; teasing means smug; focused means determined; " +
            "tired means sleepy; love means kiss; flirty means seductive; overwhelmed means flustered_heavy. " +
            "Use sad at high intensity for crying, and surprised at high intensity for shocked. " +
            "neutral: calm, or when no feeling fits.";

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
                case "excited":
                case "teasing":
                case "loving":
                    emotion = Emotion.Happy;
                    return true;
                case "angry":
                case "pout":
                case "pouty":
                    emotion = Emotion.Sad;
                    return true;
                case "sleepy":
                case "tired":
                    emotion = Emotion.Neutral;
                    return true;
                case "crying":
                    emotion = Emotion.Sad;
                    return true;
                case "teary_happy":
                case "smug":
                case "smug_wink":
                case "kiss":
                case "love":
                    emotion = Emotion.Happy;
                    return true;
                case "shocked":
                case "shock":
                case "surprise":
                    emotion = Emotion.Surprised;
                    return true;
                case "confused":
                case "confusion":
                    emotion = Emotion.Thinking;
                    return true;
                case "bored":
                    emotion = Emotion.Neutral;
                    return true;
                case "scared":
                case "afraid":
                case "fear":
                case "embarrassed_worried":
                    emotion = Emotion.Concerned;
                    return true;
                case "tsundere":
                case "seductive":
                case "flirty":
                    emotion = Emotion.Shy;
                    return true;
                case "determined":
                case "focused":
                    emotion = Emotion.Neutral;
                    return true;
                case "flustered_heavy":
                case "overwhelmed":
                    emotion = Emotion.Flustered;
                    return true;
                case "annoyed":
                    emotion = Emotion.Sad;
                    return true;
                default:
                    return Enum.TryParse(value.Trim(), true, out emotion);
            }
        }

        /// <summary>
        /// Portrait still for a raw emotion word the face enum does not name.
        /// Empty when the word is already an emotion, or when it should not pick a still.
        /// worried stays empty so the concerned face uses the concerned still.
        /// Intensity decides the strong forms: sad becomes crying and surprise becomes shocked.
        /// </summary>
        public static string PortraitAlias(string field)
        {
            return PortraitAlias(field, 0f);
        }

        public static string PortraitAlias(string field, float intensity)
        {
            var token = Token(field);
            switch (token)
            {
                case "sad":
                    return intensity >= 0.7f ? "crying" : string.Empty;
                case "surprised":
                case "surprise":
                    return intensity >= 0.7f ? "shocked" : string.Empty;
                case "pout":
                case "pouty":
                    return "pouty";
                case "angry":
                    return "angry";
                case "annoyed":
                    return "tsundere";
                case "tired":
                    return "sleepy";
                case "teasing":
                    return "smug";
                case "afraid":
                case "fear":
                    return "scared";
                case "focused":
                    return "determined";
                case "love":
                    return "kiss";
                case "flirty":
                    return "seductive";
                case "overwhelmed":
                    return "flustered_heavy";
                case "crying":
                case "teary_happy":
                case "shocked":
                case "confused":
                case "bored":
                case "scared":
                case "tsundere":
                case "determined":
                case "smug":
                case "sleepy":
                case "embarrassed_worried":
                case "flustered_heavy":
                case "seductive":
                case "kiss":
                case "smug_wink":
                    return token;
                case "excited":
                case "loving":
                case "laughing":
                    return token;
                default:
                    return string.Empty;
            }
        }

        /// <summary>One console line for the emotion attribute text, before it is interpreted.</summary>
        public static string FormatFieldLog(string field)
        {
            var clean = SanitizeField(field);
            if (clean.Length == 0)
                return "Cue emotion: (none)";
            return "Cue emotion: " + clean;
        }

        /// <summary>Trimmed attribute text, with line breaks removed and a short cap, for the log.</summary>
        public static string SanitizeField(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length && builder.Length < MaxFieldLength; i++)
            {
                var c = value[i];
                if (c == '\r' || c == '\n')
                    continue;
                builder.Append(c);
            }

            return builder.ToString().Trim();
        }

        static string Token(string field)
        {
            return SanitizeField(field).ToLowerInvariant();
        }
    }
}
