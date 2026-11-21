using System;
using System.Collections.Generic;

namespace AICompanion.Settings
{
    /// <summary>JUH-154: what the persona grid needs: duplicate, delete by id, history.</summary>
    public sealed partial class SettingsPanel
    {
        /// <summary>The persona cards for the grid, the active one marked.</summary>
        public List<PersonaCard> PersonaCards()
        {
            return PersonaGrid.Cards(PersonaCatalog.LoadDefault(Folder()).Entries, _settings != null ? _settings.PersonaId : string.Empty);
        }

        /// <summary>A copy of a persona with her picture, under a new id and "… copy" name. Doesn't switch to her.</summary>
        public bool DuplicatePersona(string id, out string message)
        {
            var copy = StudioLoad(id);
            if (copy == null)
            {
                message = "That persona couldn't be read.";
                SetStatus(message);
                return false;
            }

            var catalog = PersonaCatalog.LoadDefault(Folder());
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry != null && entry.Persona != null)
                    names.Add(entry.Persona.DisplayName ?? string.Empty);
            }

            var picture = copy.HasPicture ? StudioPicture(copy.Id) : null;
            copy.DisplayName = PersonaGrid.CopyName(copy.DisplayName, names.Contains);
            copy.Id = PersonaIds.Unique(copy.DisplayName, catalog.Contains);
            copy.HasPicture = false;
            if (picture != null && PersonaPictureStore.Save(PersonasFolderPath, copy.Id, picture))
                copy.HasPicture = true;
            if (!PersonaStore.Save(PersonasFolderPath, copy))
            {
                message = "Couldn't save the copy on this PC.";
                SetStatus(message);
                return false;
            }

            message = "Made " + copy.DisplayName + ".";
            SetStatus(message);
            _rebuild = true;
            return true;
        }

        /// <summary>
        /// Deletes her own file (and picture). A built-in comes back as it shipped. Deleting the
        /// active persona switches as "Delete persona" does today.
        /// </summary>
        public bool DeletePersonaById(string id, out string message)
        {
            var entry = PersonaCatalog.LoadDefault(Folder()).FindEntry(id);
            if (entry == null || !entry.UserFile)
            {
                message = "A built-in persona can't be deleted.";
                SetStatus(message);
                return false;
            }

            var name = string.IsNullOrWhiteSpace(entry.Persona.DisplayName) ? entry.Persona.Id : entry.Persona.DisplayName;
            if (string.Equals(_settings.PersonaId, entry.Persona.Id, StringComparison.OrdinalIgnoreCase))
            {
                PersonaPictureStore.Delete(PersonasFolderPath, entry.Persona.Id);
                DeleteActivePersona();
            }
            else
            {
                PersonaStore.Delete(PersonasFolderPath, entry.Persona.Id);
                PersonaPictureStore.Delete(PersonasFolderPath, entry.Persona.Id);
            }

            message = (entry.BuiltIn ? "Deleted your saved copy of " : "Deleted ") + name + ".";
            SetStatus(message);
            _rebuild = true;
            return true;
        }

        /// <summary>The kept earlier versions of a persona, newest first.</summary>
        public List<PersonaVersion> PersonaVersions(string id)
        {
            return PersonaHistory.List(PersonasFolderPath, id);
        }
    }
}
