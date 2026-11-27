using System.Collections;
using System.Collections.Generic;
using AICompanion.AI;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Settings
{
    public partial class SettingsPanel
    {
        readonly List<string> _ollamaModels = new List<string>();
        bool _ollamaModelsLoading;
        bool _ollamaModelsLoaded;
        string _ollamaModelsError = string.Empty;
        int _ollamaModelsSerial;

        /// <summary>Installed model tags from the configured local Ollama server.</summary>
        public IReadOnlyList<string> OllamaModels
        {
            get { return _ollamaModels; }
        }

        public bool OllamaModelsLoading
        {
            get { return _ollamaModelsLoading; }
        }

        public string OllamaModelsStatus
        {
            get
            {
                if (_ollamaModelsLoading)
                    return "Looking for installed Ollama models...";
                if (_ollamaModelsError.Length > 0)
                    return _ollamaModelsError;
                if (!_ollamaModelsLoaded)
                    return string.Empty;
                return _ollamaModels.Count == 0
                    ? "No installed models were reported by Ollama."
                    : _ollamaModels.Count + " installed model" + (_ollamaModels.Count == 1 ? string.Empty : "s") + ".";
            }
        }

        /// <summary>Fetches once per page visit; a user Refresh can always retry.</summary>
        public void EnsureOllamaModelsLoaded()
        {
            if (_ollamaModelsLoaded || _ollamaModelsLoading || _ollamaModelsError.Length > 0)
                return;
            RefreshOllamaModels();
        }

        public void RefreshOllamaModels()
        {
            if (_ollamaModelsLoading)
                return;
            _ollamaModelsSerial++;
            _ollamaModelsError = string.Empty;
            _ollamaModelsLoading = true;
            SettingsRevision++;
            StartCoroutine(LoadOllamaModels(_ollamaModelsSerial));
        }

        /// <summary>The local address changed: forget the old server's list.</summary>
        public void InvalidateOllamaModels()
        {
            _ollamaModelsSerial++;
            _ollamaModels.Clear();
            _ollamaModelsLoaded = false;
            _ollamaModelsError = string.Empty;
            _ollamaModelsLoading = false;
        }

        IEnumerator LoadOllamaModels(int serial)
        {
            var url = OllamaModelCatalog.TagsUrl(_settings != null ? _settings.LocalBaseUrl : null);
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (serial != _ollamaModelsSerial)
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    _ollamaModels.Clear();
                    _ollamaModelsError = "Could not read installed models from " + url + " (" + request.error + ").";
                }
                else
                {
                    var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    var models = OllamaModelCatalog.Parse(body);
                    _ollamaModels.Clear();
                    _ollamaModels.AddRange(models);
                    _ollamaModelsLoaded = true;
                    _ollamaModelsError = string.Empty;
                }

                _ollamaModelsLoading = false;
                SettingsRevision++;
            }
        }
    }
}
