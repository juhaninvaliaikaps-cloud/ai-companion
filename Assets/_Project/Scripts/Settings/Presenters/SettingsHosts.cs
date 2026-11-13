using System.Collections.Generic;

namespace AICompanion.Settings.Presenters
{
    /// <summary>
    /// What a settings presenter needs from whoever owns the live settings. The old
    /// <see cref="SettingsPanel"/> implements it (SettingsPanel.Presenters.cs); tests use a fake.
    /// </summary>
    public interface ISettingsHost
    {
        /// <summary>The live settings object. Presenters change fields, then call <see cref="ApplyAndSave"/>.</summary>
        CompanionSettings Settings { get; }

        /// <summary>Applies the settings to the running app and writes the encrypted settings file.</summary>
        void ApplyAndSave();

        /// <summary>The one-line status message under the settings.</summary>
        void SetStatus(string message);

        string Status { get; }

        /// <summary>Asks open settings views to redraw.</summary>
        void RequestRefresh();

        /// <summary>"Grok (model)" / "Local (model)" for the provider that answers right now.</summary>
        string ActiveModelText { get; }
    }

    /// <summary>Persona switching on the settings owner (history per persona, voice overlay).</summary>
    public interface IPersonaHost
    {
        IReadOnlyList<PersonaEntry> PersonaEntries { get; }

        /// <summary>Empty while the setup name is used.</summary>
        string ActivePersonaId { get; }

        /// <summary>Same as the persona button on the Persona tab.</summary>
        void UsePersona(string id);

        /// <summary>Same as "Use setup name" on the Persona tab.</summary>
        void UseSetupName();
    }
}
