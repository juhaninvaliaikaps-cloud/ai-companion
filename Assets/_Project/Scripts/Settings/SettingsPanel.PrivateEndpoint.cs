using System.Collections;
using AICompanion.AI;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Settings
{
    public partial class SettingsPanel
    {
        bool _privateProbeBusy;
        int _privateProbeSerial;

        public bool PrivateApiKeySaved => HasPrivateKey();

        public bool PrivateEndpointTestRunning => _privateProbeBusy;


        public void SavePrivateEndpointKey(string key)
        {
            var text = key ?? string.Empty;
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

            SettingsRevision++;
        }

        public void RemovePrivateEndpointKey()
        {
            RemovePrivateKey();
        }

        void RemovePrivateKey()
        {
            try
            {
                PrivateApiKeyStore.Delete();
                SetStatus("Private API key removed.");
            }
            catch (System.Exception)
            {
                SetStatus("The private API key could not be removed.");
            }

            SettingsRevision++;
        }

        /// <summary>Builds the Test connection line; the key is passed only to redact an echoed copy.</summary>
        public static string PrivateProbeStatus(int responseCode, string error, string body, string key, string warning)
        {
            var status = ConnectionProbe.FormatResult(responseCode, error, body, key);
            if (!string.IsNullOrEmpty(warning))
                status += " " + warning;
            return status;
        }

        static bool HasPrivateKey()
        {
            string key;
            return PrivateApiKeyStore.TryLoad(out key) && !string.IsNullOrWhiteSpace(key);
        }

        public void TestPrivateEndpointConnection()
        {
            TestPrivateConnection();
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
            SettingsRevision++;
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
                request.redirectLimit = 0;
                if (hasKey)
                    request.SetRequestHeader("Authorization", "Bearer " + key.Trim());
                yield return request.SendWebRequest();
                if (serial != _privateProbeSerial)
                {
                    key = null;
                    yield break;
                }

                var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                var status = PrivateProbeStatus((int)request.responseCode, request.error, body, key, warning);
                key = null;
                SetStatus(status);
                Debug.Log("Private endpoint test: " + status);
            }

            _privateProbeBusy = false;
            SettingsRevision++;
        }
    }
}
