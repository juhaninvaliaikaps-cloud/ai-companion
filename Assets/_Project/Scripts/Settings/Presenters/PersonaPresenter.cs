using System;
using System.Collections.Generic;

namespace AICompanion.Settings.Presenters
{
    /// <summary>One row of the persona quick switch.</summary>
    public readonly struct PersonaChoice
    {
        public readonly string Id;
        public readonly string Label;
        public readonly bool Active;

        public PersonaChoice(string id, string label, bool active)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Active = active;
        }

        /// <summary>The "use the name from setup" row has an empty id.</summary>
        public bool IsSetupName => Id.Length == 0;
    }

    /// <summary>
    /// JUH-104 PR B: the persona quick switch (wheel → Persona, new settings → Persona).
    /// Switching goes through the host so each persona keeps its own chat history, the same
    /// as the persona buttons on the old Persona tab. Editing personas stays on that tab.
    /// </summary>
    public sealed class PersonaPresenter
    {
        public const string SetupNameLabel = "Use setup name";
        public const string SetupNameActiveLabel = "Setup name (active)";

        readonly IPersonaHost _host;

        public PersonaPresenter(IPersonaHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public string ActiveId => _host.ActivePersonaId ?? string.Empty;

        /// <summary>The setup-name row first, then every persona in catalog order.</summary>
        public IReadOnlyList<PersonaChoice> Choices()
        {
            var active = ActiveId;
            var list = new List<PersonaChoice>();
            list.Add(new PersonaChoice(string.Empty, active.Length == 0 ? SetupNameActiveLabel : SetupNameLabel, active.Length == 0));
            var entries = _host.PersonaEntries;
            if (entries == null)
                return list;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Persona == null || string.IsNullOrEmpty(entry.Persona.Id))
                    continue;
                var isActive = string.Equals(entry.Persona.Id, active, StringComparison.OrdinalIgnoreCase);
                list.Add(new PersonaChoice(entry.Persona.Id, Label(entry, active), isActive));
            }

            return list;
        }

        /// <summary>Switches to a persona, or to the setup name for an empty id. Choosing the active one does nothing.</summary>
        public void Choose(string id)
        {
            var target = id ?? string.Empty;
            if (string.Equals(target, ActiveId, StringComparison.OrdinalIgnoreCase))
                return;
            if (target.Length == 0)
                _host.UseSetupName();
            else
                _host.UsePersona(target);
        }

        /// <summary>The active persona's display name, or empty while the setup name is used.</summary>
        public string ActiveDisplayName()
        {
            var active = ActiveId;
            if (active.Length == 0 || _host.PersonaEntries == null)
                return string.Empty;
            for (var i = 0; i < _host.PersonaEntries.Count; i++)
            {
                var entry = _host.PersonaEntries[i];
                if (entry != null && entry.Persona != null && string.Equals(entry.Persona.Id, active, StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrEmpty(entry.Persona.DisplayName) ? entry.Persona.Id : entry.Persona.DisplayName;
            }

            return string.Empty;
        }

        /// <summary>Same wording as the old Persona tab: "(active)", or "(saved)" for an edited built-in.</summary>
        public static string Label(PersonaEntry entry, string activeId)
        {
            var persona = entry.Persona;
            var label = string.IsNullOrEmpty(persona.DisplayName) ? persona.Id : persona.DisplayName;
            if (string.Equals(persona.Id, activeId, StringComparison.OrdinalIgnoreCase))
                return label + " (active)";
            if (entry.UserFile && entry.BuiltIn)
                return label + " (saved)";
            return label;
        }
    }
}
