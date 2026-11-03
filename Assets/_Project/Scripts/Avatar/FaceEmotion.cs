using AICompanion.Core;

namespace AICompanion.Avatar
{
    /// <summary>
    /// The emotion the 3D face should show. A portrait cue often stores
    /// <see cref="Emotion.Neutral"/> on the body and puts the real expression in the
    /// portrait name. That name wins when it is one this face knows.
    /// </summary>
    public static class FaceEmotion
    {
        public static Emotion Resolve(string expressionName, Emotion fallback)
        {
            var name = Normalize(expressionName);
            if (name.Length == 0)
                return fallback;

            switch (name)
            {
                case "embarrassed_worried":
                    return Emotion.Embarrassed;
                case "pouty":
                case "pout":
                case "angry":
                    return Emotion.Sad;
            }

            if (EmotionNames.TryParse(name, out var parsed))
                return parsed;

            return fallback;
        }

        public static string FormatLog(Emotion emotion)
        {
            return "Expression: " + emotion.ToString().ToLowerInvariant();
        }

        static string Normalize(string expressionName)
        {
            if (string.IsNullOrWhiteSpace(expressionName))
                return string.Empty;

            var source = expressionName.Trim().ToLowerInvariant();
            var chars = new char[source.Length];
            var count = 0;
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '-' || c == ' ')
                    c = '_';
                chars[count++] = c;
            }

            return new string(chars, 0, count);
        }
    }
}
