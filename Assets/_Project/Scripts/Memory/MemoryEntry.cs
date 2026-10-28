using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.Memory
{
    public enum MemoryKind
    {
        Fact = 0,
        Summary = 1,
    }

    /// <summary>
    /// One remembered item. <see cref="isPrivate"/> marks a memory created while
    /// private mode was on; those never go into a cloud prompt.
    /// </summary>
    [Serializable]
    public sealed class MemoryEntry
    {
        public string id = string.Empty;
        public string text = string.Empty;
        public MemoryKind kind = MemoryKind.Fact;
        public string createdAtUtc = string.Empty;
        public bool isPrivate;

        /// <summary>
        /// Identity-level facts (a name, how the user wants to be addressed). Core
        /// entries are always offered to the prompt, whatever the question was.
        /// </summary>
        public bool isCore;

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(text); }
        }
    }

    /// <summary>
    /// The on-disk shape of memory.json. JsonUtility needs the list wrapped in a
    /// class, and a version field lets a later migration read older files.
    /// </summary>
    [Serializable]
    public sealed class MemoryFile
    {
        public int version = 1;
        public List<MemoryEntry> entries = new List<MemoryEntry>();
    }

    /// <summary>
    /// The companion's personality across sessions. Nothing mutates it yet beyond the
    /// public setters, but the file is read on start so a later feature can rely on it.
    /// </summary>
    [Serializable]
    public sealed class PersonalityState
    {
        public int version = 1;
        public string personaName = "Leia";
        public string mood = "neutral";
        public float warmth = 0.6f;
        public string updatedUtc = string.Empty;

        public void Touch()
        {
            updatedUtc = DateTime.UtcNow.ToString("o");
        }
    }
}
