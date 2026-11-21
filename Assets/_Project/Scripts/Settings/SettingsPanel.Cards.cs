using System;
using System.IO;

namespace AICompanion.Settings
{
    /// <summary>JUH-153: exporting a persona as a character card, a card JSON or a zip.</summary>
    public sealed partial class SettingsPanel
    {
        /// <summary>
        /// Asks where to save and writes the persona in <paramref name="format"/>. Her 18+ notes go
        /// in only with <paramref name="includeAdult"/>. The result is shown as the status line.
        /// </summary>
        public bool ExportPersona(string id, CardExportFormat format, bool includeAdult, out string message)
        {
            message = string.Empty;
            var persona = StudioLoad(id);
            if (persona == null)
            {
                message = "That persona couldn't be read.";
                SetStatus(message);
                return false;
            }

            string path;
            if (!SaveFileDialog.TryPick("Export " + (string.IsNullOrWhiteSpace(persona.DisplayName) ? persona.Id : persona.DisplayName),
                    SaveFileDialog.FilterFor(format), SaveFileDialog.ExtensionFor(format), CharacterCards.FileName(persona, format), out path))
            {
                message = "Export cancelled.";
                SetStatus(message);
                return false;
            }

            return ExportPersonaTo(persona, format, includeAdult, path, out message);
        }

        /// <summary>Writes the export to a path the user chose (also used by tests).</summary>
        public bool ExportPersonaTo(PersonaDefinition persona, CardExportFormat format, bool includeAdult, string path, out string message)
        {
            try
            {
                var picture = persona.HasPicture ? StudioPicture(persona.Id) : null;
                // JUH-159: her lorebook goes in too (an 18+ one only with includeAdult).
                var lore = StudioLore(persona.Id);
                var bytes = CharacterCards.Export(persona, picture, format, includeAdult, lore);
                File.WriteAllBytes(path, bytes);
                var parts = new System.Collections.Generic.List<string>();
                if (includeAdult && !string.IsNullOrWhiteSpace(persona.AdultNotes))
                    parts.Add("her 18+ notes");
                if (CharacterCards.ExportsLorebook(lore, includeAdult))
                    parts.Add("her lorebook");
                message = "Exported " + Path.GetFileName(path)
                          + (parts.Count > 0 ? ", with " + string.Join(" and ", parts.ToArray()) + "." : ".")
                          + (lore != null && lore.Adult && !lore.IsEmpty && !includeAdult ? " Her 18+ lorebook was left out." : string.Empty);
                SetStatus(message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Couldn't write that file (" + ex.GetType().Name + ").";
                SetStatus(message);
                return false;
            }
        }
    }
}
