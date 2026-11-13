using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    public sealed partial class SettingsPanel
    {
        void ChooseExportFolder()
        {
            string picked;
            if (ExportFolderDialog.TryPick(out picked) && !string.IsNullOrWhiteSpace(picked))
            {
                _exportFolder = picked.Trim();
                _rebuild = true;
                return;
            }

            SetStatus("Type an export folder. The folder window is unavailable here.");
        }

        void ExportLocal(bool zip)
        {
            var folder = Folder();
            var dest = (_exportFolder ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(dest))
            {
                SetStatus("Choose a folder for the decrypted export.");
                return;
            }

            try
            {
                LocalActionResult result;
                string where;
                if (LocalDataActions.DestinationInsideRoot(folder, dest))
                {
                    SetStatus("Export did not run. Choose a folder outside the ProjectLeia data folder.");
                    return;
                }

                if (zip)
                {
                    where = dest.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                        ? dest
                        : Path.Combine(dest, "ProjectLeia-export.zip");
                    result = LocalDataPortal.ExportZip(folder, where);
                }
                else
                {
                    where = dest;
                    result = LocalDataPortal.ExportFolder(folder, where);
                }

                if (result == null || !result.Ok)
                {
                    SetStatus("Export did not run. Choose a folder outside the ProjectLeia data folder.");
                    return;
                }

                var skipped = result.Notes.Count > 0 ? " Some files were skipped." : string.Empty;
                SetStatus("Exported " + result.Files.ToString() + " file(s)." + skipped);
            }
            catch (Exception)
            {
                SetStatus("Could not export local data.");
            }
        }

        void ConfirmWipe()
        {
            var folder = Folder();
            var result = LocalDataActions.Wipe(folder);
            if (result == null || !result.Ok)
            {
                SetStatus("Local data was not wiped.");
                return;
            }

            MemoryRuntime.Discard();
            _settings = CompanionSettings.CreateDefault();
            // After a full wipe she starts again as on a new install: Happi.
            _settings.PersonaId = PersonaDefaults.HappiId;
            PersonaRuntime.ApplySelected(_settings, folder);
            var chat = GetComponent<ChatPanel>();
            if (chat != null)
                chat.ClearTranscript();
            _historyRevision = chat != null ? chat.TranscriptRevision : 0;
            _confirmWipe = false;
            _applying = true;
            try
            {
                SettingsApplicator.Apply(_settings, this);
            }
            finally
            {
                _applying = false;
            }

            SetStatus("Local data wiped. Built-in personas are still available.");
            _rebuild = true;
        }
    }
}
