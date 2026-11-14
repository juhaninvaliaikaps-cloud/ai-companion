using System;
using System.IO;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// Her mood on disk: one encrypted file per persona (<c>mood.json</c>, or
    /// <c>mood-&lt;persona&gt;.json</c>), through <see cref="ProtectedLocalFile"/> like memory.
    /// Wipe and export include it (<see cref="LocalDataInventory.IsMoodName"/>).
    /// </summary>
    public static class CompanionMoodStore
    {
        public const string FileName = "mood.json";

        /// <summary>The mood file for a persona in the ProjectLeia folder, or null without a folder.</summary>
        public static string FileFor(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var id = PersonaFileId(personaId);
            return Path.Combine(folder, id.Length == 0 ? FileName : "mood-" + id + ".json");
        }

        /// <summary>The same cleaning as the memory file name: lower case, letters, digits, '-' and '_', at most 40.</summary>
        public static string PersonaFileId(string personaId)
        {
            if (string.IsNullOrWhiteSpace(personaId))
                return string.Empty;

            var trimmed = personaId.Trim();
            var sb = new StringBuilder(trimmed.Length);
            for (var i = 0; i < trimmed.Length && sb.Length < 40; i++)
            {
                var c = char.ToLowerInvariant(trimmed[i]);
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                        sb.Append('-');
                    continue;
                }

                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// The saved mood with the closed time already faded, or a fresh Content state when
        /// there is none or it cannot be read.
        /// </summary>
        public static CompanionMoodState Load(string path, DateTime nowUtc)
        {
            return Load(path, nowUtc, null);
        }

        /// <summary>The persona's mood, faded with its temperament for the time it was closed.</summary>
        public static CompanionMoodState Load(string path, DateTime nowUtc, PersonaTemperament temperament)
        {
            var read = ProtectedLocalFile.Read(path);
            if (read.Status != LocalReadStatus.Ready)
                return new CompanionMoodState { Temperament = temperament };
            return CompanionMoodState.FromJson(read.Text, nowUtc, temperament) ?? new CompanionMoodState { Temperament = temperament };
        }

        /// <summary>
        /// When this persona's mood was last saved (JUH-118: the end of her last session), or
        /// null when there is no file or it has no time. The keeper saves once a minute and on
        /// quit, so this is at most a minute early.
        /// </summary>
        public static DateTime? SavedAtUtc(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            var read = ProtectedLocalFile.Read(path);
            if (read.Status != LocalReadStatus.Ready)
                return null;
            return CompanionMoodState.SavedAtUtc(read.Text);
        }

        public static bool Save(string path, CompanionMoodState state, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(path) || state == null)
                return false;
            return ProtectedLocalFile.Write(path, state.ToJson(nowUtc));
        }
    }
}
