using System;

namespace AICompanion.Settings
{
    /// <summary>
    /// Happi is the default companion. New installs (no settings file yet), the setup wizard and a
    /// full local-data wipe start with the built-in Happi persona. Anyone who already has settings
    /// keeps the persona they selected, including the setup-name companion (no persona id), so
    /// nobody is switched silently. Leia stays available as the built-in "friend" persona.
    /// </summary>
    public static class PersonaDefaults
    {
        public const string HappiId = "happi";
        public const string HappiName = "Happi";

        /// <summary>The persona to start with: Happi when there is no settings file yet, otherwise the saved one.</summary>
        public static string ForLaunch(bool settingsFileExists, string savedPersonaId)
        {
            if (!settingsFileExists)
                return HappiId;
            return savedPersonaId ?? string.Empty;
        }

        /// <summary>
        /// The wizard's Personality step keeps Happi when the name is Happi (or left empty) and the
        /// relationship is Friend. Any other name or Girlfriend makes the user's own companion.
        /// </summary>
        public static bool WizardKeepsHappi(string name, string relationship)
        {
            var clean = CompanionPersona.SanitizeName(name);
            var isHappi = clean.Length == 0 || string.Equals(clean, HappiName, StringComparison.OrdinalIgnoreCase);
            return isHappi && RelationshipStyles.Normalize(relationship) == RelationshipStyles.Friend;
        }

        /// <summary>The name the wizard shows: Happi while Happi (or nothing) is chosen, otherwise the setup name.</summary>
        public static string WizardName(CompanionSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(settings.PersonaId) && string.IsNullOrEmpty(settings.CompanionName))
                return HappiName;
            if (string.Equals(settings.PersonaId, HappiId, StringComparison.OrdinalIgnoreCase))
                return HappiName;
            return string.IsNullOrEmpty(settings.CompanionName) ? HappiName : settings.CompanionName;
        }
    }
}
