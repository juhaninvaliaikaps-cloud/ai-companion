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

        // ---- JUH-117 (Pisara): her world. All default to "an ordinary lived memory", so
        // files written before these fields existed load unchanged.

        /// <summary>
        /// Where the memory comes from: "" for something lived with the user,
        /// <see cref="MemoryOrigins.Seeded"/> for her life before the user arrived,
        /// <see cref="MemoryOrigins.Away"/> for something that happened while the user was gone.
        /// </summary>
        public string origin = string.Empty;

        /// <summary>0 = long-term. 1 = short-term: it fades unless it is recalled often enough.</summary>
        public int horizon;

        /// <summary>When it happened in her world (ISO 8601 UTC), or empty when that is not known.</summary>
        public string happenedAtUtc = string.Empty;

        /// <summary>How often a short-term memory was brought into a prompt. Promotion counts these.</summary>
        public int recalls;

        /// <summary>True for something she is in the middle of, waiting for or dreading.</summary>
        public bool isThread;

        /// <summary>For a thread: one line each for how it could turn out.</summary>
        public string goodOutcome = string.Empty;
        public string badOutcome = string.Empty;

        public bool IsShortTerm
        {
            get { return horizon == MemoryHorizons.Short; }
        }

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(text); }
        }
    }

    public static class MemoryOrigins
    {
        public const string Lived = "";
        public const string Seeded = "seeded";
        public const string Away = "away";
    }

    public static class MemoryHorizons
    {
        public const int Long = 0;
        public const int Short = 1;
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

        /// <summary>
        /// JUH-117: the prompt hash of the character sheet her world was seeded from. Empty
        /// when nothing was seeded. A different hash means the persona prompt changed.
        /// </summary>
        public string seededFromHash = string.Empty;
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
