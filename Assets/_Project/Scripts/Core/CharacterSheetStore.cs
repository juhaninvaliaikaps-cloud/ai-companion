using System.IO;

namespace AICompanion.Core
{
    /// <summary>
    /// The character sheet on disk (JUH-117): one encrypted file per persona
    /// (<c>sheet.json</c>, or <c>sheet-&lt;persona&gt;.json</c>), through
    /// <see cref="ProtectedLocalFile"/> like her mood. Wipe and export include it
    /// (<see cref="LocalDataInventory.IsSheetName"/>).
    /// </summary>
    public static class CharacterSheetStore
    {
        public const string FileName = "sheet.json";

        /// <summary>The sheet file for a persona in the ProjectLeia folder, or null without a folder.</summary>
        public static string FileFor(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return Path.Combine(folder, id.Length == 0 ? FileName : "sheet-" + id + ".json");
        }

        /// <summary>The saved sheet, or null when there is none or it cannot be read.</summary>
        public static CharacterSheet Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            var read = ProtectedLocalFile.Read(path);
            return read.Status == LocalReadStatus.Ready ? CharacterSheet.FromJson(read.Text) : null;
        }

        /// <summary>
        /// The saved sheet when it was read from this prompt; otherwise null, which means a new
        /// reading is due.
        /// </summary>
        public static CharacterSheet LoadFor(string path, string personaPrompt)
        {
            var sheet = Load(path);
            return sheet != null && sheet.IsFor(personaPrompt) ? sheet : null;
        }

        public static bool Save(string path, CharacterSheet sheet)
        {
            if (string.IsNullOrEmpty(path) || sheet == null)
                return false;
            return ProtectedLocalFile.Write(path, sheet.ToJson());
        }
    }
}
