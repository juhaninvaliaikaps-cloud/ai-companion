using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using AICompanion.UI;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-150: what the Persona Studio needs from settings: read personas, save a whole draft
    /// with its picture, and switch to it. The Studio holds the draft until Save.
    /// </summary>
    public sealed partial class SettingsPanel
    {
        public string PersonasFolderPath
        {
            get { return SettingsLocations.PersonasFolder(Folder()); }
        }

        /// <summary>The personas on this PC, built-in and the user's own.</summary>
        public IReadOnlyList<PersonaEntry> StudioEntries()
        {
            return PersonaCatalog.LoadDefault(Folder()).Entries;
        }

        /// <summary>
        /// A copy of a persona to edit. An old file has no description yet: its personality
        /// text becomes the description.
        /// </summary>
        public PersonaDefinition StudioLoad(string id)
        {
            var persona = PersonaCatalog.LoadDefault(Folder()).Find(id);
            if (persona == null)
                return null;
            var copy = persona.Copy();
            if (string.IsNullOrWhiteSpace(copy.Description))
                copy.Description = copy.Personality;
            return copy;
        }

        /// <summary>A free id for a new persona, from her name.</summary>
        public string StudioNewId(string name)
        {
            var catalog = PersonaCatalog.LoadDefault(Folder());
            var seed = string.IsNullOrWhiteSpace(name) ? "custom" : name;
            return PersonaIds.Unique(seed, catalog.Contains);
        }

        public byte[] StudioPicture(string id)
        {
            return PersonaPictureStore.Load(PersonasFolderPath, id);
        }

        /// <summary>JUH-156: her saved lorebook, or null.</summary>
        public Lorebook StudioLore(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : LorebookStore.Load(PersonasFolderPath, id);
        }

        /// <summary>
        /// JUH-156: saves her lorebook (null or empty removes it). The active persona's chat uses
        /// the new one at once.
        /// </summary>
        public bool StudioSaveLore(string id, Lorebook book)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;
            var saved = LorebookStore.Save(PersonasFolderPath, id, book);
            if (saved && string.Equals(_settings.PersonaId, PersonaIds.Normalize(id), StringComparison.OrdinalIgnoreCase))
                LoreNow.Set(book);
            return saved;
        }

        /// <summary>
        /// Saves the whole draft. The chat text comes from the description
        /// (<see cref="PersonaDescription.ModelText"/>). A changed picture is saved or removed.
        /// <paramref name="use"/> switches to her afterwards; editing the active persona
        /// reapplies her either way.
        /// </summary>
        public bool StudioSave(PersonaDefinition draft, bool pictureChanged, byte[] picturePng, bool use, out string savedId, out string message)
        {
            return StudioSave(draft, pictureChanged, picturePng, null, use, out savedId, out message);
        }

        /// <summary>
        /// JUH-151: as above, and a character sheet from the Studio's Pisara step is stored for
        /// her with the hash of her chat prompt, so the keeper uses it instead of reading again.
        /// </summary>
        public bool StudioSave(PersonaDefinition draft, bool pictureChanged, byte[] picturePng, CharacterSheet sheet, bool use, out string savedId, out string message)
        {
            message = string.Empty;
            savedId = string.Empty;
            if (draft == null)
            {
                message = "Nothing to save.";
                return false;
            }

            var copy = draft.Copy();
            if (string.IsNullOrWhiteSpace(copy.Id))
                copy.Id = StudioNewId(copy.DisplayName);
            PersonaDescription.ApplyTo(copy);

            var folder = PersonasFolderPath;
            if (pictureChanged)
            {
                if (picturePng != null && picturePng.Length > 0)
                {
                    if (!PersonaPictureStore.Save(folder, copy.Id, picturePng))
                    {
                        message = "Couldn't save her picture. The rest was not saved either.";
                        return false;
                    }

                    copy.HasPicture = true;
                }
                else
                {
                    PersonaPictureStore.Delete(folder, copy.Id);
                    copy.HasPicture = false;
                }
            }

            if (!PersonaStore.Save(folder, copy))
            {
                message = "Couldn't save the persona on this PC.";
                return false;
            }

            savedId = PersonaIds.Normalize(copy.Id);
            if (sheet != null)
                SaveStudioSheet(savedId, sheet);
            var name = string.IsNullOrWhiteSpace(copy.DisplayName) ? copy.Id : copy.DisplayName;
            if (use || string.Equals(_settings.PersonaId, copy.Id, StringComparison.OrdinalIgnoreCase))
                UsePersona(copy.Id);
            message = "Saved " + name + ".";
            SetStatus(message);
            SettingsRevision++;
            return true;
        }

        /// <summary>The prompt the chat model gets for this persona, the same as the keeper reads.</summary>
        string ChatPromptFor(string id)
        {
            var persona = PersonaCatalog.LoadDefault(Folder()).Find(id);
            return persona == null ? null : PersonaText.Build(persona, _settings.CompanionName, _settings.Relationship);
        }

        void SaveStudioSheet(string id, CharacterSheet sheet)
        {
            var prompt = ChatPromptFor(id);
            if (prompt == null)
                return;
            var copy = CharacterSheet.FromJson(sheet.ToJson()) ?? sheet;
            PisaraReading.Finish(copy, prompt, CharacterSheet.SourceModel, DateTime.UtcNow);
            if (!CharacterSheetStore.Save(CharacterSheetStore.FileFor(PersonaRuntime.DefaultSettingsFolder(), id), copy))
                UnityEngine.Debug.LogWarning("Persona Studio: the character sheet could not be saved.");
        }

        /// <summary>Her stored character sheet for her current prompt, as a copy to edit, or null.</summary>
        public CharacterSheet StudioSheet(string id)
        {
            var prompt = ChatPromptFor(id);
            if (prompt == null)
                return null;
            var sheet = CharacterSheetStore.LoadFor(CharacterSheetStore.FileFor(PersonaRuntime.DefaultSettingsFolder(), id), prompt);
            return sheet == null ? null : CharacterSheet.FromJson(sheet.ToJson());
        }

        /// <summary>
        /// JUH-152: her first greeting in an empty chat: after launch, a persona switch, or
        /// clearing the history. A chat that already has lines is left alone.
        /// </summary>
        void GreetIfEmpty()
        {
            var chat = GetComponent<ChatPanel>();
            if (chat == null || _settings == null || string.IsNullOrWhiteSpace(_settings.PersonaId))
                return;
            var persona = PersonaCatalog.LoadDefault(Folder()).Find(_settings.PersonaId);
            if (persona != null && !string.IsNullOrWhiteSpace(persona.Greeting))
                chat.ShowGreeting(persona.Greeting);
        }

        /// <summary>JUH-152: switch to a saved persona from the Studio.</summary>
        public void StudioUse(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
                UsePersona(PersonaIds.Normalize(id));
        }

        /// <summary>The Pisara reader for the Studio, or null.</summary>
        public PisaraReadingKeeper StudioReader
        {
            get { return GetComponent<PisaraReadingKeeper>(); }
        }

        /// <summary>The text of a .txt or .md file to import, at most the description limit.</summary>
        public static bool StudioReadText(string path, out string text, out string message)
        {
            text = null;
            message = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    message = "That file wasn't found.";
                    return false;
                }

                var info = new FileInfo(path);
                if (info.Length > 4L * PersonaDefinition.MaxDescriptionLength)
                {
                    message = "That file is too large for a description.";
                    return false;
                }

                text = File.ReadAllText(path);
                if (text.Length > PersonaDefinition.MaxDescriptionLength)
                    text = text.Substring(0, PersonaDefinition.MaxDescriptionLength);
                return true;
            }
            catch (Exception ex)
            {
                message = "Couldn't read that file (" + ex.GetType().Name + ").";
                return false;
            }
        }
    }
}
