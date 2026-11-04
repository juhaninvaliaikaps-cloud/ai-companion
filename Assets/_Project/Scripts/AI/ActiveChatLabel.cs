using System;

namespace AICompanion.AI
{
    /// <summary>
    /// The model that will actually answer. Private mode is local even when Grok is remembered.
    /// </summary>
    public static class ActiveChatLabel
    {
        public static bool IsLocal(string effectiveProviderId)
        {
            return string.Equals(Normalize(effectiveProviderId), ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase);
        }

        public static string Format(string effectiveProviderId, bool privateMode, string grokModel, string localModel)
        {
            if (privateMode || IsLocal(effectiveProviderId))
            {
                var name = Display(localModel, ChatDefaults.LocalModel);
                return privateMode ? "Local · " + name + " · private" : "Local · " + name;
            }

            return "Grok · " + Display(grokModel, ChatDefaults.Model);
        }

        static string Display(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            var trimmed = value.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
            return trimmed.Length == 0 ? fallback : trimmed;
        }

        static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}
