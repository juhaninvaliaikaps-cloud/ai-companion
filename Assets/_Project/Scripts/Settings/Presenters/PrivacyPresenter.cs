using System;
using System.IO;
using AICompanion.Core;

namespace AICompanion.Settings.Presenters
{
    /// <summary>A destructive action waiting for "Confirm" or "Cancel".</summary>
    public enum PrivacyConfirmKind
    {
        None = 0,
        ClearHistory = 1,
        WipeAll = 2,
        DeleteMemory = 3,
        DeleteAllMemories = 4,
        StartOver = 5,
    }

    /// <summary>
    /// JUH-104 PR B: the two-step confirm used by every destructive privacy action. One
    /// request is pending at a time; a new request replaces it, as on the old Privacy tab
    /// where Clear and Wipe cancel each other. Escape cancels a pending request first.
    /// </summary>
    public sealed class PrivacyConfirm
    {
        /// <summary>
        /// The question for <see cref="PrivacyConfirmKind.StartOver"/>; <paramref name="name"/> is
        /// the persona's name.
        /// </summary>
        public static string StartOverPrompt(string name)
        {
            return "Start over with " + StartOverText.Name(name) + "? This clears this persona's chat history, everything she remembers about you, "
                + "her mood and your relationship. Her persona, her description and her own life stay. This cannot be undone.";
        }

        public PrivacyConfirmKind Pending { get; private set; }

        /// <summary>The memory id for <see cref="PrivacyConfirmKind.DeleteMemory"/>.</summary>
        public string MemoryId { get; private set; } = string.Empty;

        public bool HasPending => Pending != PrivacyConfirmKind.None;

        public void Request(PrivacyConfirmKind kind, string memoryId = null)
        {
            Pending = kind;
            MemoryId = kind == PrivacyConfirmKind.DeleteMemory ? memoryId ?? string.Empty : string.Empty;
        }

        public void Cancel()
        {
            Pending = PrivacyConfirmKind.None;
            MemoryId = string.Empty;
        }

        /// <summary>Escape: cancels a pending request and returns true; false when nothing was pending.</summary>
        public bool Escape()
        {
            if (!HasPending)
                return false;
            Cancel();
            return true;
        }

        /// <summary>Takes the pending request (and clears it) so the caller can run it once.</summary>
        public PrivacyConfirmKind Take(out string memoryId)
        {
            var kind = Pending;
            memoryId = MemoryId;
            Cancel();
            return kind;
        }

        /// <summary>The question shown above Confirm / Cancel. Same wording as the old Privacy tab.</summary>
        public static string Prompt(PrivacyConfirmKind kind, int memoryCount, string memoryText)
        {
            switch (kind)
            {
                case PrivacyConfirmKind.ClearHistory:
                    return "Clear saved chat history on this PC? The API key and settings stay.";
                case PrivacyConfirmKind.WipeAll:
                    return "Wipe all local data on this PC? This deletes memories, chat history, saved personas, settings, screen-look choices, portraits in the ProjectLeia folder, and the saved API key. Built-in personas stay. This cannot be undone.";
                case PrivacyConfirmKind.DeleteMemory:
                    return string.IsNullOrEmpty(memoryText) ? "Delete this memory?" : "Delete \"" + memoryText + "\"?";
                case PrivacyConfirmKind.DeleteAllMemories:
                    return "Delete all " + memoryCount + " memories? This cannot be undone.";
                case PrivacyConfirmKind.StartOver:
                    return StartOverPrompt(memoryText);
                default:
                    return string.Empty;
            }
        }
    }

    /// <summary>"Start over with this persona" (JUH-117): what it clears and what it keeps.</summary>
    public static class StartOverText
    {
        public static string Button(string name)
        {
            return "Start over with " + Name(name);
        }

        public static string Status(string name)
        {
            return "Started over with " + Name(name) + ". She keeps her own life; this conversation and what she knew about you are gone.";
        }

        internal static string Name(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "her" : name.Trim();
        }
    }

    /// <summary>
    /// JUH-104 PR B: decrypted export of the ProjectLeia folder (S-PR-05, S-PR-06). Returns the
    /// status line. The old Privacy tab and the new settings view both call this.
    /// </summary>
    public static class LocalDataPresenter
    {
        public const string ChooseFolderMessage = "Choose a folder for the decrypted export.";
        public const string InsideRootMessage = "Export did not run. Choose a folder outside the ProjectLeia data folder.";
        public const string FailedMessage = "Could not export local data.";
        public const string ZipFileName = "ProjectLeia-export.zip";

        public static string Export(
            string dataFolder,
            string destination,
            bool zip,
            Func<string, string, LocalActionResult> exportFolder,
            Func<string, string, LocalActionResult> exportZip)
        {
            var dest = (destination ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(dest))
                return ChooseFolderMessage;

            try
            {
                if (LocalDataActions.DestinationInsideRoot(dataFolder, dest))
                    return InsideRootMessage;

                LocalActionResult result;
                if (zip)
                {
                    var where = ZipPath(dest);
                    result = exportZip != null ? exportZip(dataFolder, where) : null;
                }
                else
                {
                    result = exportFolder != null ? exportFolder(dataFolder, dest) : null;
                }

                if (result == null || !result.Ok)
                    return InsideRootMessage;

                var skipped = result.Notes.Count > 0 ? " Some files were skipped." : string.Empty;
                return "Exported " + result.Files.ToString() + " file(s)." + skipped;
            }
            catch (Exception)
            {
                return FailedMessage;
            }
        }

        /// <summary>A destination ending in .zip is used as is; a folder gets ProjectLeia-export.zip inside it.</summary>
        public static string ZipPath(string destination)
        {
            var dest = (destination ?? string.Empty).Trim();
            return dest.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? dest
                : Path.Combine(dest, ZipFileName);
        }
    }
}
