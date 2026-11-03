using System;
using System.IO;
using AICompanion.AI;
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
            Note("Cue instructions are added after the persona on every reply.");
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
            AddField("Piper voice id", persona.PiperVoiceId, false, value => SaveActive(idFixed, item => item.PiperVoiceId = value));
            AddField("Cloud voice id", persona.CloudVoiceId, false, value => SaveActive(idFixed, item => item.CloudVoiceId = value));
            AddButton(PersonaChoiceLabel("Grok", persona.ProviderId, ChatDefaults.GrokId), () => SaveActive(idFixed, item => item.ProviderId = ChatDefaults.GrokId));
            AddButton(PersonaChoiceLabel("Local Ollama", persona.ProviderId, ChatDefaults.LocalId), () => SaveActive(idFixed, item => item.ProviderId = ChatDefaults.LocalId));
            AddButton(string.IsNullOrEmpty(persona.ProviderId) ? "Leave provider unchanged (selected)" : "Leave provider unchanged", () => SaveActive(idFixed, item => item.ProviderId = string.Empty));
            AddField("Model name", persona.Model, false, value => SaveActive(idFixed, item => item.Model = value));
            Note("Blank voice, model, or avatar keeps the current one.");
            AddButton(PersonaChoiceLabel("Avatar auto", persona.AvatarSource, AvatarSourceNames.Auto), () => SaveActive(idFixed, item => item.AvatarSource = AvatarSourceNames.Auto));
            AddButton(PersonaChoiceLabel("Avatar local", persona.AvatarSource, AvatarSourceNames.Local), () => SaveActive(idFixed, item => item.AvatarSource = AvatarSourceNames.Local));
            AddButton(PersonaChoiceLabel("Avatar VRM", persona.AvatarSource, AvatarSourceNames.Vrm), () => SaveActive(idFixed, item => item.AvatarSource = AvatarSourceNames.Vrm));
            AddButton(PersonaChoiceLabel("Avatar FBX", persona.AvatarSource, AvatarSourceNames.Fbx), () => SaveActive(idFixed, item => item.AvatarSource = AvatarSourceNames.Fbx));
            AddButton(string.IsNullOrEmpty(persona.AvatarSource) ? "Leave avatar unchanged (selected)" : "Leave avatar unchanged", () => SaveActive(idFixed, item => item.AvatarSource = string.Empty));
            AddField("Portrait folder", persona.PortraitFolder, false, value => SaveActive(idFixed, item => item.PortraitFolder = value));
            Note("A missing portrait folder uses the standard portraits folder.");
            if (current.UserFile)
                AddButton(current.BuiltIn ? "Delete saved copy" : "Delete persona", DeleteActivePersona);
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

            _settings.PersonaId = persona.Id;
            var beforeVoice = _settings.PiperVoice;
            PersonaApply.Overlay(_settings, persona, File.Exists, Directory.Exists);
            ApplyLive();
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

        void ClearActivePersona()
        {
            _settings.PersonaId = string.Empty;
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
