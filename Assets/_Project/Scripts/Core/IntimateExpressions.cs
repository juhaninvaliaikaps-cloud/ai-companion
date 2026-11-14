using System;

namespace AICompanion.Core
{
    /// <summary>
    /// The adult drawn portrait set is gated. It is listed, resolved and loaded only when the
    /// user has turned the setting on and the separate portraits-adult folder exists.
    /// </summary>
    public static class IntimateExpressions
    {
        public const string FolderName = "portraits-adult";
        public const string IdPrefix = "finger_";

        public static bool Active { get; private set; }

        public static bool Enabled { get; private set; }

        public static bool FolderPresent { get; private set; }

        /// <summary>Intimate portraits stay local: they are active only in Private mode.</summary>
        public static bool PrivateMode { get; private set; }

        /// <summary>
        /// She has taken her clothes off through an adult animation. While true she may only
        /// use naked animations until a <c>dress</c> cue clears it.
        /// </summary>
        public static bool Undressed { get; private set; }

        public static void Configure(bool enabled, bool privateMode, bool folderPresent)
        {
            Enabled = enabled;
            PrivateMode = privateMode;
            FolderPresent = folderPresent;
            Active = enabled && privateMode && folderPresent;
            if (!Active)
                Undressed = false;
        }

        /// <summary>Called by the video avatar when an undressing clip is accepted.</summary>
        public static void Undress()
        {
            if (Active)
                Undressed = true;
        }

        /// <summary>Called by the <c>dress</c> cue. There is no dressing animation.</summary>
        public static void Dress()
        {
            Undressed = false;
        }

        /// <summary>The adult animations that are valid while she is already naked.</summary>
        public static bool IsNakedCue(string value)
        {
            var name = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
            return name == "naked_idle" || name == "naked_idle_still"
                || name == "bend_over" || name == "bend_over_end";
        }

        public static void Reset()
        {
            Configure(false, false, false);
        }

        /// <summary>
        /// The private flag every outbound check should use. An open gate keeps chat, voice and
        /// vision local even if the caller's private flag is stale (JUH-112).
        /// </summary>
        public static bool KeepsLocal(bool privateMode)
        {
            return privateMode || Active;
        }

        /// <summary>True for a stem or catalog id in the gated set.</summary>
        public static bool IsIntimateId(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when this id belongs to the gated set and the gate is open.</summary>
        public static bool Allows(string value)
        {
            return Active && IsIntimateId(value);
        }
    }
}
