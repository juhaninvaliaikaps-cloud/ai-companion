using System.Collections;
using AICompanion.AI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    public partial class SettingsPanel
    {
        InputField _privateKeyInput;
        bool _privateProbeBusy;
        int _privateProbeSerial;

        void BuildPrivateEndpoint()
        {
            AddLabel("Private endpoint");
            var custom = PrivateEndpointTypes.IsCustom(_settings.PrivateEndpointType);
            AddButton(custom ? "Local Ollama" : "Local Ollama (selected)", () =>
            {
                _settings.PrivateEndpointType = PrivateEndpointTypes.Local;
                ApplyLive();
                _rebuild = true;
            });
            AddButton(custom ? "Custom server (selected)" : "Custom server", () =>
            {
                _settings.PrivateEndpointType = PrivateEndpointTypes.Custom;
                ApplyLive();
                _rebuild = true;
            });

            if (!custom)
            {
                Note("Uses the Local model and Local address above. Nothing leaves this PC.");
                return;
            }

            AddField("Custom base URL", _settings.PrivateBaseUrl, false, value =>
            {
                _settings.PrivateBaseUrl = value;
                ApplyLive();
                _rebuild = true;
            });
            Note("The OpenAI-compatible base URL, for example https://gpu.example.com/v1. vLLM and Ollama both work.");
            AddField("Custom model", _settings.PrivateModel, false, value =>
            {
                _settings.PrivateModel = value;
                ApplyLive();
                _rebuild = true;
            });

            var warning = PrivateEndpointRules.HttpWarning(_settings.PrivateBaseUrl);
            if (!string.IsNullOrEmpty(warning))
                Note(warning);

            var addressError = PrivateEndpointRules.ValidateAddress(_settings.PrivateBaseUrl);
            if (!string.IsNullOrEmpty(addressError) && !string.IsNullOrWhiteSpace(_settings.PrivateBaseUrl))
                Note(addressError);

            Note(HasPrivateKey()
                ? "A private API key is saved in the encrypted local store."
                : "No private API key is saved. Most self-hosted servers do not need one.");
            _privateKeyInput = AddField("Paste a private API key", string.Empty, true, _ => { });
            AddButton("Save private key", SavePrivateKey);
            AddButton("Remove private key", RemovePrivateKey);
            AddButton(_privateProbeBusy ? "Testing..." : "Test connection", () =>
            {
                if (!_privateProbeBusy)
                    TestPrivateConnection();
            });
            Note("Test connection calls /models. The key stays in the request header and is never written to the log.");
        }

        void SavePrivateKey()
        {
            var text = _privateKeyInput != null ? _privateKeyInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("Paste a private API key before saving.");
                return;
            }

            try
            {
                PrivateApiKeyStore.Save(text);
                SetStatus("Private API key saved in the encrypted local store.");
            }
            catch (System.Exception)
            {
                SetStatus("The private API key was not saved.");
            }

            _rebuild = true;
        }

        void RemovePrivateKey()
        {
            PrivateApiKeyStore.Delete();
            SetStatus("Private API key removed.");
            _rebuild = true;
        }

        static bool HasPrivateKey()
        {
            string key;
            return PrivateApiKeyStore.TryLoad(out key) && !string.IsNullOrWhiteSpace(key);
        }

        void TestPrivateConnection()
        {
            var error = PrivateEndpointRules.ValidateAddress(_settings.PrivateBaseUrl);
            if (!string.IsNullOrEmpty(error))
            {
                SetStatus(error);
                return;
            }

            _privateProbeSerial++;
            _privateProbeBusy = true;
            SetStatus("Testing the custom server...");
            _rebuild = true;
            StartCoroutine(ProbePrivate(_privateProbeSerial));
        }

        IEnumerator ProbePrivate(int serial)
        {
            var warning = PrivateEndpointRules.HttpWarning(_settings.PrivateBaseUrl);
            var url = PrivateEndpointRules.ModelsUrl(_settings.PrivateBaseUrl);
            string key;
            var hasKey = PrivateApiKeyStore.TryLoad(out key) && !string.IsNullOrWhiteSpace(key);
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 20;
                if (hasKey)
                    request.SetRequestHeader("Authorization", "Bearer " + key.Trim());
                key = null;
                yield return request.SendWebRequest();
                if (serial != _privateProbeSerial)
                    yield break;

                var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                var status = ConnectionProbe.FormatResult((int)request.responseCode, request.error, body);
                if (!string.IsNullOrEmpty(warning))
                    status += " " + warning;
                SetStatus(status);
                Debug.Log("Private endpoint test: " + status);
            }

            _privateProbeBusy = false;
            if (_open)
                _rebuild = true;
        }
    }
}
