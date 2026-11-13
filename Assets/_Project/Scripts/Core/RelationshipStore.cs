using System;
using System.IO;

namespace AICompanion.Core
{
    /// <summary>
    /// Her relationship on disk: one encrypted file per persona
    /// (<c>relationship.json</c>, or <c>relationship-&lt;persona&gt;.json</c>), next to the
    /// mood files and through <see cref="ProtectedLocalFile"/>. Wipe and export include it
    /// (<see cref="LocalDataInventory.IsRelationshipName"/>).
    /// </summary>
    public static class RelationshipStore
    {
        public const string FileName = "relationship.json";

        /// <summary>The relationship file for a persona in the ProjectLeia folder, or null without a folder.</summary>
        public static string FileFor(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return Path.Combine(folder, id.Length == 0 ? FileName : "relationship-" + id + ".json");
        }

        public static RelationshipState Load(string path)
        {
            var read = ProtectedLocalFile.Read(path);
            return read.Status == LocalReadStatus.Ready
                ? RelationshipState.FromJson(read.Text)
                : new RelationshipState();
        }

        public static bool Save(string path, RelationshipState state, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(path) || state == null)
                return false;
            return ProtectedLocalFile.Write(path, state.ToJson(nowUtc));
        }
    }
}
