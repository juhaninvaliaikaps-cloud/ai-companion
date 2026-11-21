using System;
using System.Threading;
using AICompanion.Voice;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-165: the Whisper model in Settings > Voice: where it was found, and "Download speech
    /// model", which saves it to her data folder (ProjectLeia\Whisper) so no build has to carry it.
    /// </summary>
    public sealed partial class SettingsPanel
    {
        CancellationTokenSource _modelDownload;
        float _modelProgress;

        public bool SpeechModelDownloading => _modelDownload != null;

        public float SpeechModelProgress => _modelProgress;

        /// <summary>One line: which model, and where it is or that it's missing.</summary>
        public string SpeechModelStatus()
        {
            var listener = GetComponent<SpeechListener>();
            var file = SttSettings.SanitizeModelFile(_settings != null ? _settings.WhisperModel : null);
            if (listener == null)
                return "Speech input isn't set up in this build.";
            var path = listener.FindModel();
            var chosen = WhisperModelFiles.ChosenPath(_settings != null ? _settings.WhisperModelPath : null);
            if (chosen != null && string.Equals(path, chosen, StringComparison.OrdinalIgnoreCase))
                return "Speech model: " + System.IO.Path.GetFileName(chosen) + ", the file you chose.";
            var lost = chosen != null ? "The file you chose isn't there or isn't a model. " : string.Empty;
            if (path == null)
                return lost + "Speech model " + file + ": missing. Choose the file or download it below.";
            var inBuild = path.StartsWith(Application.streamingAssetsPath, StringComparison.OrdinalIgnoreCase);
            return lost + "Speech model " + file + ": " + (inBuild ? "included in this build." : "in your data folder.");
        }

        public bool SpeechModelFound()
        {
            var listener = GetComponent<SpeechListener>();
            return listener != null && listener.FindModel() != null;
        }

        public async void DownloadSpeechModel()
        {
            if (_modelDownload != null)
                return;
            var file = SttSettings.SanitizeModelFile(_settings != null ? _settings.WhisperModel : null);
            _modelDownload = new CancellationTokenSource();
            _modelProgress = 0f;
            SetStatusMessage("Downloading " + file + "…");
            WhisperDownloadResult result;
            try
            {
                result = await WhisperModelDownloader.DownloadAsync(Folder(), file, p => _modelProgress = p, _modelDownload.Token);
            }
            catch (Exception ex)
            {
                result = new WhisperDownloadResult { Message = "The download failed (" + ex.GetType().Name + ")." };
            }

            if (this == null)
                return;
            _modelDownload.Dispose();
            _modelDownload = null;
            if (result.Ok)
            {
                var listener = GetComponent<SpeechListener>();
                if (listener != null)
                    listener.ApplyRecognitionSettings();
            }

            SetStatusMessage(result.Message);
            _rebuild = true;
        }

        public void CancelSpeechModelDownload()
        {
            if (_modelDownload != null)
                _modelDownload.Cancel();
        }
    }
}
