using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.AI;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Settings
{
    public sealed class PersonaEntry
    {
        public PersonaEntry(PersonaDefinition persona, bool builtIn, bool userFile)
        {
            Persona = persona;
            BuiltIn = builtIn;
            UserFile = userFile;
        }

        public PersonaDefinition Persona { get; }
        public bool BuiltIn { get; }
        public bool UserFile { get; }
    }

    /// <summary>
    /// Built-in presets from Resources, then any JSON files under the per-user personas
    /// folder. A user file with the same id replaces the built-in. The built-in file
    /// in the repository is never written.
    /// </summary>
    public sealed class PersonaCatalog
    {
        readonly List<PersonaEntry> _entries;

        PersonaCatalog(List<PersonaEntry> entries)
        {
            _entries = entries ?? new List<PersonaEntry>();
        }

        public IReadOnlyList<PersonaEntry> Entries
        {
            get { return _entries; }
        }

        public static PersonaCatalog LoadDefault(string settingsFolder)
        {
            return Load(PersonaFiles.ReadBuiltInTexts(), SettingsLocations.PersonasFolder(settingsFolder));
        }

        public static PersonaCatalog Load(IEnumerable<string> builtInJson, string userDirectory)
        {
            var builtIns = new Dictionary<string, PersonaDefinition>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            if (builtInJson != null)
            {
                foreach (var json in builtInJson)
                {
                    var persona = PersonaJson.Parse(json);
                    if (persona == null)
                        continue;
                    if (!builtIns.ContainsKey(persona.Id))
                        order.Add(persona.Id);
                    builtIns[persona.Id] = persona;
                }
            }

            order.Sort(CompareIds);
            var users = ReadUserFiles(userDirectory);
            var entries = new List<PersonaEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < order.Count; i++)
            {
                var id = order[i];
                seen.Add(id);
                PersonaDefinition user;
                var hasUser = users.TryGetValue(id, out user);
                entries.Add(new PersonaEntry(hasUser ? user : builtIns[id], true, hasUser));
            }

            var extra = new List<string>();
            foreach (var pair in users)
            {
                if (!seen.Contains(pair.Key))
                    extra.Add(pair.Key);
            }

            extra.Sort(CompareIds);
            for (var i = 0; i < extra.Count; i++)
                entries.Add(new PersonaEntry(users[extra[i]], false, true));

            return new PersonaCatalog(entries);
        }

        public PersonaDefinition Find(string id)
        {
            var entry = FindEntry(id);
            return entry == null ? null : entry.Persona;
        }

        public PersonaEntry FindEntry(string id)
        {
            var key = PersonaIds.Normalize(id);
            if (key.Length == 0)
                return null;

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry != null && entry.Persona != null && string.Equals(entry.Persona.Id, key, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        public bool Contains(string id)
        {
            return Find(id) != null;
        }

        static Dictionary<string, PersonaDefinition> ReadUserFiles(string userDirectory)
        {
            var users = new Dictionary<string, PersonaDefinition>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(userDirectory) || !Directory.Exists(userDirectory))
                return users;

            string[] files;
            try
            {
                files = Directory.GetFiles(userDirectory, "*.json");
            }
            catch (Exception)
            {
                return users;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Length; i++)
            {
                var read = ProtectedLocalFile.Read(files[i]);
                if (read.Status != LocalReadStatus.Ready)
                    continue;

                PersonaDefinition persona = null;
                try
                {
                    persona = PersonaJson.Parse(read.Text);
                }
                catch (Exception)
                {
                    persona = null;
                }

                if (persona == null)
                {
                    ProtectedLocalFile.Quarantine(files[i]);
                    Debug.LogWarning("Persona file could not be read: " + Path.GetFileName(files[i]));
                    continue;
                }

                users[persona.Id] = persona;
            }

            return users;
        }

        static int CompareIds(string a, string b)
        {
            var rank = Rank(a) - Rank(b);
            if (rank != 0)
                return rank;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        static int Rank(string id)
        {
            if (string.Equals(id, PersonaDefaults.HappiId, StringComparison.OrdinalIgnoreCase))
                return -1;
            if (string.Equals(id, "friend", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(id, "coach", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (string.Equals(id, "calm", StringComparison.OrdinalIgnoreCase))
                return 2;
            return 10;
        }
    }

    public static class PersonaFiles
    {
        public const string ResourceFolder = "Personas";

        public static string BuiltInDirectory()
        {
            return Path.Combine(Application.dataPath, "_Project", "Resources", ResourceFolder);
        }

        public static List<string> ReadBuiltInTexts()
        {
            var list = new List<string>();
            var assets = Resources.LoadAll<TextAsset>(ResourceFolder);
            if (assets != null)
            {
                for (var i = 0; i < assets.Length; i++)
                {
                    if (assets[i] != null && !string.IsNullOrWhiteSpace(assets[i].text))
                        list.Add(assets[i].text);
                }
            }

            if (list.Count > 0)
                return list;

#if UNITY_EDITOR
            var directory = BuiltInDirectory();
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return list;

            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*.json");
            }
            catch (Exception)
            {
                return list;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Length; i++)
            {
                try
                {
                    var text = File.ReadAllText(files[i]);
                    if (!string.IsNullOrWhiteSpace(text))
                        list.Add(text);
                }
                catch (Exception)
                {
                    Debug.LogWarning("Built-in persona could not be read: " + Path.GetFileName(files[i]));
                }
            }
#endif
            return list;
        }
    }

    public static class PersonaStore
    {
        public static bool Save(string directory, PersonaDefinition persona)
        {
            if (persona == null || string.IsNullOrWhiteSpace(directory))
                return false;

            var copy = persona.Copy();
            if (!copy.Normalize())
                return false;

            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, copy.Id + ".json");
                var fullDir = Path.GetFullPath(directory);
                if (!fullDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    fullDir += Path.DirectorySeparatorChar;
                var fullPath = Path.GetFullPath(path);
                if (!fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
                    return false;

                var json = PersonaJson.Write(copy);
                if (CompanionSettingsSecrets.ContainsForbiddenSecretField(json))
                    return false;

                return ProtectedLocalFile.Write(path, json);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Delete(string directory, string id)
        {
            var key = PersonaIds.Normalize(id);
            if (key.Length == 0 || string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;

            var removed = false;
            try
            {
                var canonical = Path.Combine(directory, key + ".json");
                if (File.Exists(canonical))
                {
                    File.Delete(canonical);
                    removed = true;
                }

                var files = Directory.GetFiles(directory, "*.json");
                for (var i = 0; i < files.Length; i++)
                {
                    if (string.Equals(files[i], canonical, StringComparison.OrdinalIgnoreCase))
                        continue;

                    PersonaDefinition parsed = null;
                    try
                    {
                        var read = ProtectedLocalFile.Read(files[i]);
                        if (read.Status == LocalReadStatus.Ready)
                            parsed = PersonaJson.Parse(read.Text);
                    }
                    catch (Exception)
                    {
                        parsed = null;
                    }

                    if (parsed == null || !string.Equals(parsed.Id, key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    File.Delete(files[i]);
                    removed = true;
                }
            }
            catch (Exception)
            {
                return removed;
            }

            return removed;
        }
    }

    /// <summary>
    /// Copies the fields a persona owns onto settings. Blank voice, model, and avatar
    /// fields leave the current values. A blank or missing portrait folder clears the
    /// override so the standard portraits folder is used.
    /// </summary>
    public static class PersonaApply
    {
        public static void Overlay(CompanionSettings settings, PersonaDefinition persona, Func<string, bool> fileExists, Func<string, bool> directoryExists)
        {
            if (settings == null || persona == null)
                return;

            var source = persona.Copy();
            source.Normalize();

            string provider;
            if (PersonaProviders.TryNormalize(source.ProviderId, out provider))
                settings.ProviderId = provider;

            if (!string.IsNullOrEmpty(source.Model))
            {
                var slot = string.Equals(settings.ProviderId, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase)
                    ? ChatDefaults.LocalId
                    : ChatDefaults.GrokId;
                if (slot == ChatDefaults.LocalId)
                    settings.LocalModel = source.Model;
                else
                    settings.Model = source.Model;
            }

            if (!string.IsNullOrEmpty(source.CloudVoiceId))
                settings.VoiceId = source.CloudVoiceId;

            if (!string.IsNullOrEmpty(source.PiperVoiceId))
            {
                var resolved = PersonaVoices.ResolveOrNull(source.PiperVoiceId, settings.PiperVoice, settings.PiperExe, fileExists);
                if (!string.IsNullOrEmpty(resolved))
                    settings.PiperVoice = resolved;
            }

            if (!string.IsNullOrEmpty(source.KokoroVoiceId))
                settings.KokoroVoice = source.KokoroVoiceId.Trim();

            string avatar;
            if (PersonaAvatars.TryNormalize(source.AvatarSource, out avatar))
                settings.AvatarSource = avatar;

            if (!string.IsNullOrEmpty(source.PortraitFolder) && directoryExists != null && directoryExists(source.PortraitFolder))
                settings.PortraitFolder = source.PortraitFolder;
            else
                settings.PortraitFolder = string.Empty;

            if (!string.IsNullOrEmpty(source.DisplayName))
                settings.CompanionName = source.DisplayName;

            settings.Normalize();
        }
    }

    public static class PersonaVoices
    {
        public static string ResolveOrNull(string voiceId, string currentPath, string piperExe, Func<string, bool> fileExists)
        {
            if (fileExists == null || string.IsNullOrWhiteSpace(voiceId))
                return null;

            try
            {
                var id = voiceId.Trim();
                if (fileExists(id))
                    return id;

                var fileName = id.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ? id : id + ".onnx";
                var folders = new[] { DirectoryOf(currentPath), DirectoryOf(piperExe) };
                for (var i = 0; i < folders.Length; i++)
                {
                    if (string.IsNullOrEmpty(folders[i]))
                        continue;

                    var candidate = Path.Combine(folders[i], fileName);
                    if (fileExists(candidate))
                        return candidate;

                    if (!string.Equals(fileName, id, StringComparison.OrdinalIgnoreCase))
                    {
                        var plain = Path.Combine(folders[i], id);
                        if (fileExists(plain))
                            return plain;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        static string DirectoryOf(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                return Path.GetDirectoryName(path.Trim());
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static class PersonaRuntime
    {
        public static string DefaultSettingsFolder()
        {
            return SettingsLocations.Folder(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.persistentDataPath);
        }

        public static void ApplySelected(CompanionSettings settings, string settingsFolder)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.PersonaId))
                return;

            var persona = PersonaCatalog.LoadDefault(settingsFolder).Find(settings.PersonaId);
            if (persona == null)
                return;

            PersonaApply.Overlay(settings, persona, File.Exists, Directory.Exists);
        }

        /// <summary>
        /// The temperament of a persona (JUH-103 phase 3). No persona (the setup name), a missing
        /// persona, or a blank or unknown preset is the default (balanced) temperament.
        /// </summary>
        public static AICompanion.Core.PersonaTemperament TemperamentFor(PersonaCatalog catalog, string personaId)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(personaId))
                return AICompanion.Core.PersonaTemperament.Default;
            var persona = catalog.Find(personaId);
            return AICompanion.Core.PersonaTemperaments.Get(persona == null ? null : persona.Temperament);
        }

        public static string SystemPrompt(CompanionSettings settings, PersonaCatalog catalog)
        {
            if (settings == null)
                return ChatDefaults.SystemPrompt;
            if (catalog == null || string.IsNullOrWhiteSpace(settings.PersonaId))
                return CompanionPersona.Build(settings.CompanionName, settings.Relationship);

            var persona = catalog.Find(settings.PersonaId);
            if (persona == null)
                return CompanionPersona.Build(settings.CompanionName, settings.Relationship);
            return PersonaText.Build(persona, settings.CompanionName, settings.Relationship);
        }
    }
}
