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

        /// <summary>
        /// "Start over with this persona" (JUH-117): clears the active persona's chat history,
        /// everything she remembers (her own life is seeded again from her character sheet),
        /// her mood, and anything else that implements <see cref="ICompanionStartOver"/> (the
        /// relationship). Her persona, its description and other personas are not touched.
        /// </summary>
        void ConfirmStartOver()
        {
            var name = StartOverName;
            var persona = _settings != null ? _settings.PersonaId ?? string.Empty : string.Empty;
            ConfirmClear();

            var forgotten = 0;
            try
            {
                forgotten = MemoryRuntime.Store.DeleteAll();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Start over: memories could not be cleared (" + e.GetType().Name + ")");
            }

            var parts = GetComponents<MonoBehaviour>();
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i] as ICompanionStartOver;
                if (part == null)
                    continue;
                try
                {
                    part.StartOver();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Start over: " + parts[i].GetType().Name + " failed (" + e.GetType().Name + ")");
                }
            }

            Debug.Log("Start over: " + (persona.Length == 0 ? "(default)" : CompanionMoodStore.PersonaFileId(persona))
                + ", " + forgotten + " memories forgotten, chat history cleared.");
            SetStatus(StartOverText.Status(name));
            _rebuild = true;
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
