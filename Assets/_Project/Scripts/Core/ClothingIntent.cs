using System;

namespace AICompanion.Core
{
    /// <summary>
    /// Detects a direct "take her clothes off" user action so the avatar can use the strip
    /// reaction even when a small model forgets the cue. This only drives the avatar; it never
    /// adds text to the reply or the prompt.
    /// </summary>
    public static class ClothingIntent
    {
        static readonly string[] Clothing =
        {
            "shirt", "dress", "blouse", "skirt", "pants", "jeans", "trousers", "bra",
            "panties", "underwear", "clothes", "clothing", "top", "jacket", "hoodie",
            "riisu", "vaatteet", "paita", "mekko", "hame", "housut", "alusvaatteet",
        };

        static readonly string[] Removal =
        {
            "remove", "removes", "removing", "take off", "takes off", "taking off",
            "pull off", "pulls off", "pulling off", "strip", "strips", "stripping",
            "undress", "unbutton", "unzip", "slide off", "peel off", "riisu", "riisua",
            "pois", "avaa",
        };

        static readonly string[] Negations =
        {
            "don't remove", "do not remove", "don't take off", "do not take off",
            "keep your clothes", "keep the clothes", "leave your clothes",
            "älä riisu", "pidä vaatteet",
        };

        public static bool RemovesClothing(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var lower = text.ToLowerInvariant();
            for (var i = 0; i < Negations.Length; i++)
            {
                if (lower.IndexOf(Negations[i], StringComparison.Ordinal) >= 0)
                    return false;
            }

            var mentionsClothing = false;
            for (var i = 0; i < Clothing.Length; i++)
            {
                if (lower.IndexOf(Clothing[i], StringComparison.Ordinal) >= 0)
                {
                    mentionsClothing = true;
                    break;
                }
            }

            if (!mentionsClothing)
                return false;
            for (var i = 0; i < Removal.Length; i++)
            {
                if (lower.IndexOf(Removal[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }
    }
}
