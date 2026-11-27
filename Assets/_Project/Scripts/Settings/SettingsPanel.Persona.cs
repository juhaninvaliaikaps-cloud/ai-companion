using System;
using System.IO;
using AICompanion.AI;
using AICompanion.UI;
using AICompanion.Voice;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// Persona picker on the Settings window. Built-ins ship with the app. Edits
    /// are written under the per-user personas folder and applied immediately.
    /// </summary>
    public sealed partial class SettingsPanel
    {
        void ReloadMoodTemperament()
        {
            var keeper = GetComponent<CompanionMoodKeeper>();
            if (keeper != null)
                keeper.ReloadTemperament();
        }

        string ActiveModelText()
        {
            var active = EffectiveSettings ?? _settings;
            var provider = OutboundPrivacy.ChatProviderId(active.PrivateMode, active.ProviderId, active.PrivateEndpointType);
            return ActiveChatLabel.Format(provider, active.PrivateMode, active.Model, active.LocalModel, active.PrivateModel);
        }

        void UsePersona(string id)
        {
            var persona = PersonaCatalog.LoadDefault(Folder()).Find(id);
            if (persona == null)
            {
                SetStatus("That persona could not be read. Keeping the current setup.");
                SettingsRevision++;
                return;
            }

            SwitchPersonaHistory(persona.Id);
            var beforeVoice = _settings.PiperVoice;
            ApplyLive();
            GreetIfEmpty();
            // The same id can come back with another temperament (a deleted saved copy of a
            // built-in). A different id is loaded by the keeper with its mood.
            ReloadMoodTemperament();
            SettingsRevision++;
            SetStatus(UseStatus(persona, beforeVoice));
        }

        int _temperamentRevision;

        /// <summary>Bumps when a temperament preset or slider is saved, so the new settings view can refresh after a drag.</summary>
        public int TemperamentRevision
        {
            get { return _temperamentRevision; }
        }

        void SaveActive(string id, Action<PersonaDefinition> change)
        {
            SaveActive(id, change, true, true);
        }

        /// <summary>
        /// <paramref name="applyLive"/> reapplies voice and the rest of the persona.
        /// <paramref name="rebuild"/> redraws this window. A slider passes false for both so the
        /// drag is not thrown away, and still reloads her mood.
        /// </summary>
        void SaveActive(string id, Action<PersonaDefinition> change, bool applyLive, bool rebuild)
        {
            if (change == null || string.IsNullOrEmpty(id))
                return;

            var catalog = PersonaCatalog.LoadDefault(Folder());
            var current = catalog.Find(id);
            if (current == null)
            {
                SetStatus("That persona is not on this PC anymore.");
                SettingsRevision++;
                return;
            }

            var copy = current.Copy();
            change(copy);
            if (!PersonaStore.Save(SettingsLocations.PersonasFolder(Folder()), copy))
            {
                SetStatus("Could not save the persona on this PC.");
                return;
            }

            var saved = PersonaCatalog.LoadDefault(Folder()).Find(id);
            if (saved == null)
            {
                SetStatus("Could not read the persona back.");
                SettingsRevision++;
                return;
            }

            if (string.Equals(_settings.PersonaId, saved.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (applyLive)
                {
                    var beforeVoice = _settings.PiperVoice;
                    ApplyLive();
                    SetStatus(SavedStatus(saved, beforeVoice));
                }

                ReloadMoodTemperament();
            }
            else if (applyLive)
            {
                SetStatus("Saved " + PersonaName(saved) + ".");
            }

            _temperamentRevision++;
            if (rebuild)
                SettingsRevision++;
        }

        void CreatePersona()
        {
            var catalog = PersonaCatalog.LoadDefault(Folder());
            var id = PersonaIds.Unique("custom", catalog.Contains);
            var persona = PersonaDefinition.CreateStarter(id);
            if (!PersonaStore.Save(SettingsLocations.PersonasFolder(Folder()), persona))
            {
                SetStatus("Could not save the new persona on this PC.");
                return;
            }

            UsePersona(id);
        }

        void DeleteActivePersona()
        {
            DeletePersonaById(_settings.PersonaId, out _);
        }

        /// <summary>
        /// Keeps conversations apart: the transcript of the old persona is saved under its own
        /// id, then the new persona's transcript is loaded (or the chat starts empty).
        /// </summary>
        void SwitchPersonaHistory(string newId)
        {
            var oldId = _settings.PersonaId;
            if (string.Equals(oldId ?? string.Empty, newId ?? string.Empty, StringComparison.Ordinal))
            {
                _settings.PersonaId = newId;
                return;
            }

            if (_settings.ChatHistory)
                SaveHistory();

            // JUH-118: the session with the old persona ends here, while her model, privacy and
            // memory store are still the active ones.
            var chat = GetComponent<ChatPanel>();
            if (chat != null)
                chat.EndSessionForSwitch();

            _settings.PersonaId = newId;

            if (chat != null)
                chat.ClearTranscript();

            if (_settings.ChatHistory)
                LoadHistory();
        }

        void ClearActivePersona()
        {
            SwitchPersonaHistory(string.Empty);
            ApplyLive();
            SettingsRevision++;
            SetStatus("Using the setup name and global voice and model defaults.");
        }

        string UseStatus(PersonaDefinition persona, string piperVoiceBefore)
        {
            var name = PersonaName(persona);
            if (PiperMissed(persona, piperVoiceBefore))
                return "Using " + name + ". That Piper voice was not found, so the global voice file is used.";
            return "Using " + name + ".";
        }

        string SavedStatus(PersonaDefinition persona, string piperVoiceBefore)
        {
            var name = PersonaName(persona);
            if (PiperMissed(persona, piperVoiceBefore))
                return "Saved " + name + ". That Piper voice was not found, so the global voice file is used.";
            return "Saved " + name + ".";
        }

        bool PiperMissed(PersonaDefinition persona, string piperVoiceBefore)
        {
            if (persona == null || string.IsNullOrEmpty(persona.PiperVoiceId))
                return false;
            return PersonaVoices.ResolveOrNull(persona.PiperVoiceId, piperVoiceBefore, _settings.PiperExe, File.Exists) == null;
        }

        static string PersonaName(PersonaDefinition persona)
        {
            if (persona == null)
                return "persona";
            return string.IsNullOrEmpty(persona.DisplayName) ? persona.Id : persona.DisplayName;
        }

    }
}
