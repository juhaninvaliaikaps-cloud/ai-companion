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
            "Base emotions: happy (good news, a joke, a warm greeting; high intensity laughs, low intensity is a gentle smile); " +
            "sad (sympathy, disappointment, bad news); surprised (something unexpected); thinking (working something out); " +
            "embarrassed (a compliment or being caught out, strong blush); shy (quiet, looking away, light blush); " +
            "flustered (lost for words, strong blush, open mouth); concerned (worried about the user, no blush; worried means concerned); " +
            "neutral (calm, or no feeling fits). " +
            "Drawn portrait expressions you can also use: crying (strong sadness; use this instead of sad at 0.9 or more), " +
            "teary_happy, shocked (strong surprise; use this instead of surprised at 0.9 or more), confused, bored, scared, " +
            "tsundere, determined, angry (sharper than pouty), smug (knowing smile; teasing means smug), smug_wink, " +
            "sleepy (tired means sleepy), embarrassed_worried, flustered_heavy (overwhelmed means flustered_heavy), " +
            "seductive (flirty means seductive), kiss (love means kiss), and loving (warm affection, not a kiss). " +
            "Other synonyms: afraid or fear means scared; annoyed means tsundere; focused means determined. " +
            "pouty stays a playful pout and is not the same as angry. " +
            "These names are emotion values, so write them as emotion=\"crying\" and not as a gesture.";

        /// <summary>
        /// Appended to the prompt only when the adult drawn set is enabled and its folder exists.
        /// The finger referred to is the user's, and the wording stays suggestive, not explicit.
        /// </summary>
        public const string IntimateCatalogLine =
            "Intimate drawn expressions are on. They refer to the user's finger at her lips, not her own. " +
            "finger_suck: his fingertip held at her lips, teasing. " +
            "finger_suck_deep: holding his finger between her lips, eye contact. " +
            "finger_suck_hold: eyes closed, holding it. " +
            "finger_pull_start: his finger just leaving her lips. " +
            "finger_pull: after, a shy satisfied smile. " +
            "Use them only when the conversation is already intimate, and keep the wording suggestive.";

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
                case "finger_suck":
                case "finger_suck_deep":
                case "finger_suck_hold":
                    emotion = Emotion.Flustered;
                    return true;
                case "annoyed":
                    emotion = Emotion.Sad;
                    return true;
                case "finger_pull":
                case "finger_pull_start":
                    emotion = Emotion.Shy;
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
            if (IntimateExpressions.IsIntimateId(token) && !IntimateExpressions.Allows(token))
                return string.Empty;
            switch (token)
            {
                case "sad":
                    return intensity >= 0.9f ? "crying" : string.Empty;
                case "surprised":
                case "surprise":
                    return intensity >= 0.9f ? "shocked" : string.Empty;
                case "shock":
                    return "shocked";
                case "confusion":
                    return "confused";
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
                case "finger_suck":
                case "finger_suck_deep":
                case "finger_suck_hold":
                case "finger_pull":
                case "finger_pull_start":
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
