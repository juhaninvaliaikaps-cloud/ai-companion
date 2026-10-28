using System;
using System.IO;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>
    /// Personality that outlives a session. Loaded from
    /// <c>%APPDATA%\ProjectLeia\personality.json</c> on first use and written back on
    /// every change. A corrupt file falls back to the defaults instead of throwing.
    /// </summary>
    public sealed class PersonalityStore
    {
        readonly string _path;

        public PersonalityStore(string path, PersonalityState state)
        {
            _path = path;
            Current = state ?? new PersonalityState();
        }

        public PersonalityState Current { get; }

        public static PersonalityStore Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new PersonalityStore(path, new PersonalityState());

            try
            {
                var json = File.ReadAllText(path);
                var state = JsonUtility.FromJson<PersonalityState>(json);
                return new PersonalityStore(path, state);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Personality file could not be read, using defaults: " + e.GetType().Name);
                return new PersonalityStore(path, new PersonalityState());
            }
        }

        public static PersonalityStore CreateDefault()
        {
            return Load(MemoryPaths.PersonalityFile());
        }

        public void SetMood(string mood)
        {
            Current.mood = string.IsNullOrWhiteSpace(mood) ? "neutral" : mood.Trim();
            Current.Touch();
            Save();
        }

        public void SetPersona(string personaName, float warmth)
        {
            if (!string.IsNullOrWhiteSpace(personaName))
                Current.personaName = personaName.Trim();
            Current.warmth = Mathf.Clamp01(warmth);
            Current.Touch();
            Save();
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_path))
                return;

            try
            {
                var folder = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(_path, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Personality could not be saved: " + e.GetType().Name);
            }
        }
    }
}
