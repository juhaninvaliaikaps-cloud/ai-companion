using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;

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

        /// <summary>
        /// Saves the whole draft. The chat text comes from the description
        /// (<see cref="PersonaDescription.ModelText"/>). A changed picture is saved or removed.
        /// <paramref name="use"/> switches to her afterwards; editing the active persona
        /// reapplies her either way.
        /// </summary>
        public bool StudioSave(PersonaDefinition draft, bool pictureChanged, byte[] picturePng, bool use, out string savedId, out string message)
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
            if (!string.IsNullOrWhiteSpace(copy.Description))
                copy.Personality = PersonaDescription.ModelText(copy.Description);

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
            var name = string.IsNullOrWhiteSpace(copy.DisplayName) ? copy.Id : copy.DisplayName;
            if (use || string.Equals(_settings.PersonaId, copy.Id, StringComparison.OrdinalIgnoreCase))
                UsePersona(copy.Id);
            message = "Saved " + name + ".";
            SetStatus(message);
            _rebuild = true;
            return true;
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
