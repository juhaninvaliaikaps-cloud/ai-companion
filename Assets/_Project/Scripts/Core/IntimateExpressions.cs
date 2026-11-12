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

        public static void Configure(bool enabled, bool privateMode, bool folderPresent)
        {
            Enabled = enabled;
            PrivateMode = privateMode;
            FolderPresent = folderPresent;
            Active = enabled && privateMode && folderPresent;
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
