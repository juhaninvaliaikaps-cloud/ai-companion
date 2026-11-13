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
            SetStatus(Presenters.LocalDataPresenter.Export(Folder(), _exportFolder, zip, LocalDataPortal.ExportFolder, LocalDataPortal.ExportZip));
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
