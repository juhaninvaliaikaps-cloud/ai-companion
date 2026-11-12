using System;
using AICompanion.Core;
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
            var read = ProtectedLocalFile.Read(path);
            if (read.Status != LocalReadStatus.Ready || string.IsNullOrWhiteSpace(read.Text))
                return new PersonalityStore(path, new PersonalityState());

            try
            {
                var state = JsonUtility.FromJson<PersonalityState>(read.Text);
                if (state == null)
                {
                    ProtectedLocalFile.Quarantine(path);
                    Debug.LogWarning("Personality file could not be read, using defaults: invalid");
                    return new PersonalityStore(path, new PersonalityState());
                }

                return new PersonalityStore(path, state);
            }
            catch (Exception e)
            {
                ProtectedLocalFile.Quarantine(path);
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
                if (!ProtectedLocalFile.Write(_path, JsonUtility.ToJson(Current, true)))
                    Debug.LogWarning("Personality could not be saved: write");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Personality could not be saved: " + e.GetType().Name);
            }
        }
    }
}
