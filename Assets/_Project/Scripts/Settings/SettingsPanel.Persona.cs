using System;
using System.IO;
using AICompanion.AI;
using AICompanion.UI;
using AICompanion.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    /// <summary>
    /// Persona picker on the Settings window. Built-ins ship with the app. Edits
    /// are written under the per-user personas folder and applied immediately.
    /// </summary>
    public sealed partial class SettingsPanel
    {
        void BuildPersona()
        {
            var catalog = PersonaCatalog.LoadDefault(Folder());
            Note("Built-ins ship with the app. Edits save outside the repo.");
            AddLabel("Active now: " + ActiveModelText());
            Note(VoiceRoute.UsesCloudTts(_settings.PrivateMode, _settings.ProviderId)
                ? "Cloud voice is used while chat is Grok."
                : "A local voice is used while chat is local or private: Piper, or Kokoro when you picked it. That audio stays on this PC.");
            Note("Cue instructions are added after the persona on every reply.");
            Note("Each persona keeps its own chat history, so switching characters starts a different conversation.");
            var active = _settings.PersonaId ?? string.Empty;
            AddButton(string.IsNullOrEmpty(active) ? "Setup name (active)" : "Use setup name", ClearActivePersona);

            for (var i = 0; i < catalog.Entries.Count; i++)
                AddPersonaButton(catalog.Entries[i], active);

            AddButton("New persona", CreatePersona);
            if (string.IsNullOrEmpty(active))
            {
                Note("Pick a persona, or keep the name from setup.");
                Note("A blank voice, model, or avatar keeps the current one.");
                return;
            }

            var current = catalog.FindEntry(active);
            if (current == null || current.Persona == null)
            {
                Note("The selected persona file is missing. Voice and model stay as they are.");
                return;
            }

            var persona = current.Persona;
            if (current.BuiltIn)
                Note(current.UserFile ? "Editing your saved copy." : "Editing saves a copy. The built-in file stays.");
            Note("Id " + persona.Id);
            var idFixed = persona.Id;
            AddField("Display name", persona.DisplayName, false, value => SaveActive(idFixed, item => item.DisplayName = value));
            AddArea("Personality", persona.Personality, 78f, PersonaDefinition.MaxPersonalityLength, value => SaveActive(idFixed, item => item.Personality = value));
            AddArea("Speaking style", persona.SpeakingStyle, 52f, PersonaDefinition.MaxSpeakingStyleLength, value => SaveActive(idFixed, item => item.SpeakingStyle = value));
            AddField("Piper voice id (local)", persona.PiperVoiceId, false, value => SaveActive(idFixed, item => item.PiperVoiceId = value));
            AddField("Kokoro voice id (local)", persona.KokoroVoiceId, false, value => SaveActive(idFixed, item => item.KokoroVoiceId = value));
            AddField("Cloud voice id (Grok)", persona.CloudVoiceId, false, value => SaveActive(idFixed, item => item.CloudVoiceId = value));
            Note("Blank voice, model, or avatar keeps the current one.");
            AddButton(PersonaChoiceLabel("Grok", persona.ProviderId, ChatDefaults.GrokId), () => SaveActive(idFixed, item => item.ProviderId = ChatDefaults.GrokId));
            AddButton(PersonaChoiceLabel("Local Ollama", persona.ProviderId, ChatDefaults.LocalId), () => SaveActive(idFixed, item => item.ProviderId = ChatDefaults.LocalId));
            AddButton(string.IsNullOrEmpty(persona.ProviderId) ? "Leave provider unchanged (selected)" : "Leave provider unchanged", () => SaveActive(idFixed, item => item.ProviderId = string.Empty));
            AddField("Model name", persona.Model, false, value => SaveActive(idFixed, item => item.Model = value));
            Note("That name is the Grok model, or the local Ollama tag, for the provider above.");
            AddButton(PersonaChoiceLabel("Avatar auto", persona.AvatarSource, AvatarSourceNames.Auto), () => SaveActive(idFixed, item => item.AvatarSource = AvatarSourceNames.Auto));
            AddButton(PersonaChoiceLabel("Avatar local", persona.AvatarSource, AvatarSourceNames.Local), () => SaveActive(idFixed, item => item.AvatarSource = AvatarSourceNames.Local));
            AddButton(PersonaChoiceLabel("Avatar VRM", persona.AvatarSource, AvatarSourceNames.Vrm), () => SaveActive(idFixed, item => item.AvatarSource = AvatarSourceNames.Vrm));
            AddButton(PersonaChoiceLabel("Avatar FBX", persona.AvatarSource, AvatarSourceNames.Fbx), () => SaveActive(idFixed, item => item.AvatarSource = AvatarSourceNames.Fbx));
            AddButton(string.IsNullOrEmpty(persona.AvatarSource) ? "Leave avatar unchanged (selected)" : "Leave avatar unchanged", () => SaveActive(idFixed, item => item.AvatarSource = string.Empty));
            AddField("Portrait folder", persona.PortraitFolder, false, value => SaveActive(idFixed, item => item.PortraitFolder = value));
            Note("A missing portrait folder uses the standard portraits folder.");
            AddTemperamentPicker(idFixed, persona.Temperament);
            if (current.UserFile)
                AddButton(current.BuiltIn ? "Delete saved copy" : "Delete persona", DeleteActivePersona);
        }

        /// <summary>
        /// JUH-103 phase 3: a few temperament presets, never raw numbers. The choice is saved in
        /// the persona file and takes effect at once.
        /// </summary>
        void AddTemperamentPicker(string id, string current)
        {
            // JUH-118: what the model read from her description, then the picker. A pick overrides the reading.
            var view = PisaraView();
            Note("Temperament: how quickly her moods build and fade. Auto reads it from her description; a pick below overrides it.");
            Note(view.TemperamentLine);
            Note(view.RelationshipLine);
            Note(view.Status);
            AddButton(view.Reading ? "Re-read (reading...)" : "Re-read", RereadPersona);
            AddButton(view.Manual ? "Temperament: Auto" : "Temperament: Auto (selected)", () => SetPersonaTemperamentAuto(id));
            for (var i = 0; i < AICompanion.Core.PersonaTemperaments.Ids.Length; i++)
            {
                var preset = AICompanion.Core.PersonaTemperaments.Get(AICompanion.Core.PersonaTemperaments.Ids[i]);
                var stored = AICompanion.Core.PersonaTemperaments.Normalize(preset.Id);
                var selected = view.Manual && string.Equals(current ?? string.Empty, stored, StringComparison.OrdinalIgnoreCase);
                AddButton("Temperament: " + preset.Name + (selected ? " (selected)" : string.Empty),
                    () => SetPersonaTemperament(id, stored));
            }
        }

        int _pisaraRevision = -1;

        /// <summary>Rebuilds the open Persona tab when a reading starts or comes back.</summary>
        void TrackPisara()
        {
            var keeper = GetComponent<PisaraReadingKeeper>();
            var revision = keeper != null ? keeper.Revision : 0;
            if (revision == _pisaraRevision)
                return;
            if (_pisaraRevision < 0)
            {
                _pisaraRevision = revision;
                return;
            }

            // While a field has the keyboard the rebuild waits, so typing is not interrupted.
            if (AnyFieldFocused())
                return;
            _pisaraRevision = revision;
            _rebuild = true;
        }

        void ReloadMoodTemperament()
        {
            var keeper = GetComponent<CompanionMoodKeeper>();
            if (keeper != null)
                keeper.ReloadTemperament();
        }

        string ActiveModelText()
        {
            var effective = OutboundPrivacy.ChatProviderId(_settings.PrivateMode, _settings.ProviderId, _settings.PrivateEndpointType);
            return ActiveChatLabel.Format(effective, _settings.PrivateMode, _settings.Model, _settings.LocalModel, _settings.PrivateModel);
        }

        static string ProviderButton(string name, bool remembered, bool overridden)
        {
            if (!remembered)
                return name;
            return overridden ? name + " (remembered)" : name + " (active)";
        }

        void AddPersonaButton(PersonaEntry entry, string activeId)
        {
            if (entry == null || entry.Persona == null)
                return;

            var id = entry.Persona.Id;
            AddButton(PersonaButtonLabel(entry, activeId), () => UsePersona(id));
        }

        void UsePersona(string id)
        {
            var persona = PersonaCatalog.LoadDefault(Folder()).Find(id);
            if (persona == null)
            {
                SetStatus("That persona could not be read. Keeping the current setup.");
                _rebuild = true;
                return;
            }

            SwitchPersonaHistory(persona.Id);
            var beforeVoice = _settings.PiperVoice;
            PersonaApply.Overlay(_settings, persona, File.Exists, Directory.Exists);
            ApplyLive();
            // The same id can come back with another temperament (a deleted saved copy of a
            // built-in). A different id is loaded by the keeper with its mood.
            ReloadMoodTemperament();
            _rebuild = true;
            SetStatus(UseStatus(persona, beforeVoice));
        }

        void SaveActive(string id, Action<PersonaDefinition> change)
        {
            if (change == null || string.IsNullOrEmpty(id))
                return;

            var catalog = PersonaCatalog.LoadDefault(Folder());
            var current = catalog.Find(id);
            if (current == null)
            {
                SetStatus("That persona is not on this PC anymore.");
                _rebuild = true;
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
                _rebuild = true;
                return;
            }

            if (string.Equals(_settings.PersonaId, saved.Id, StringComparison.OrdinalIgnoreCase))
            {
                var beforeVoice = _settings.PiperVoice;
                PersonaApply.Overlay(_settings, saved, File.Exists, Directory.Exists);
                ApplyLive();
                ReloadMoodTemperament();
                SetStatus(SavedStatus(saved, beforeVoice));
            }
            else
            {
                SetStatus("Saved " + PersonaName(saved) + ".");
            }

            _rebuild = true;
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
            var id = _settings.PersonaId;
            var entry = PersonaCatalog.LoadDefault(Folder()).FindEntry(id);
            if (entry == null || !entry.UserFile)
                return;

            PersonaStore.Delete(SettingsLocations.PersonasFolder(Folder()), id);
            var restored = PersonaCatalog.LoadDefault(Folder()).Find(id);
            if (restored != null)
            {
                UsePersona(id);
                return;
            }

            ClearActivePersona();
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

            _settings.PersonaId = newId;

            var chat = GetComponent<ChatPanel>();
            if (chat != null)
                chat.ClearTranscript();

            if (_settings.ChatHistory)
                LoadHistory();
        }

        void ClearActivePersona()
        {
            SwitchPersonaHistory(string.Empty);
            _settings.PortraitFolder = string.Empty;
            ApplyLive();
            _rebuild = true;
            SetStatus("Using the setup name. Voice and model stay as they are.");
        }

        string UseStatus(PersonaDefinition persona, string piperVoiceBefore)
        {
            var name = PersonaName(persona);
            if (PiperMissed(persona, piperVoiceBefore))
                return "Using " + name + ". That Piper voice was not found, so the current voice file stays.";
            return "Using " + name + ".";
        }

        string SavedStatus(PersonaDefinition persona, string piperVoiceBefore)
        {
            var name = PersonaName(persona);
            if (PiperMissed(persona, piperVoiceBefore))
                return "Saved " + name + ". That Piper voice was not found, so the current voice file stays.";
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

        static string PersonaButtonLabel(PersonaEntry entry, string activeId)
        {
            var persona = entry.Persona;
            var label = string.IsNullOrEmpty(persona.DisplayName) ? persona.Id : persona.DisplayName;
            if (string.Equals(persona.Id, activeId, StringComparison.OrdinalIgnoreCase))
                return label + " (active)";
            if (entry.UserFile && entry.BuiltIn)
                return label + " (saved)";
            return label;
        }

        static string PersonaChoiceLabel(string caption, string current, string value)
        {
            return string.Equals(current, value, StringComparison.OrdinalIgnoreCase)
                ? caption + " (selected)"
                : caption;
        }

        InputField AddArea(string placeholder, string value, float height, int characterLimit, Action<string> onEnd)
        {
            var field = new GameObject("Area");
            field.transform.SetParent(_body, false);
            var rect = field.AddComponent<RectTransform>();
            Place(rect, height);
            var image = field.AddComponent<Image>();
            image.sprite = _white;
            image.color = new Color(0.18f, 0.16f, 0.22f, 1f);
            var input = field.AddComponent<InputField>();
            input.lineType = InputField.LineType.MultiLineNewline;
            input.characterLimit = characterLimit;
            input.targetGraphic = image;
            var text = CreateText(field.transform, _font, 14, TextAnchor.UpperLeft, string.Empty);
            Stretch(text.rectTransform, 6f, 4f, 6f, 4f);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var hint = CreateText(field.transform, _font, 13, TextAnchor.UpperLeft, placeholder);
            Stretch(hint.rectTransform, 6f, 4f, 6f, 4f);
            hint.fontStyle = FontStyle.Italic;
            hint.color = new Color(0.75f, 0.70f, 0.78f, 0.9f);
            hint.supportRichText = false;
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            input.textComponent = text;
            input.placeholder = hint;
            input.text = value ?? string.Empty;
            var generation = _fieldGeneration;
            input.onEndEdit.AddListener(textValue =>
            {
                if (_building || generation != _fieldGeneration || onEnd == null)
                    return;
                onEnd(textValue ?? string.Empty);
            });
            _fields.Add(input);
            return input;
        }
    }
}
