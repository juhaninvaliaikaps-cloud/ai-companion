using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Voice
{
    /// <summary>
    /// Posts one TTS request and keeps the WAV bytes.
    /// The Authorization header is set and never logged.
    /// </summary>
    public sealed class UnityTtsTransport : ITtsTransport
    {
        readonly MonoBehaviour _host;

        public UnityTtsTransport(MonoBehaviour host)
        {
            _host = host;
        }

        public Task<TtsHttpResult> PostAsync(string url, string json, string bearerToken, float timeoutSeconds, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<TtsHttpResult>();
            if (_host == null || !_host.isActiveAndEnabled || string.IsNullOrWhiteSpace(url))
            {
                completion.SetResult(TtsHttpResult.Network("Voice playback is not active."));
                return completion.Task;
            }

            _host.StartCoroutine(Send(url, json, bearerToken, timeoutSeconds, cancellationToken, completion));
            return completion.Task;
        }

        static IEnumerator Send(string url, string json, string bearerToken, float timeoutSeconds, CancellationToken cancellationToken, TaskCompletionSource<TtsHttpResult> completion)
        {
            var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            try
            {
                var body = Encoding.UTF8.GetBytes(json ?? string.Empty);
                www.uploadHandler = new UploadHandlerRaw(body);
                www.downloadHandler = new DownloadHandlerBuffer();
                var timeout = timeoutSeconds <= 0f ? 20f : timeoutSeconds;
                www.timeout = Mathf.Max(1, Mathf.CeilToInt(timeout));
                www.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(bearerToken))
                    www.SetRequestHeader("Authorization", "Bearer " + bearerToken);

                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        www.Abort();
                        completion.TrySetResult(TtsHttpResult.Cancel());
                        yield break;
                    }

                    yield return null;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetResult(TtsHttpResult.Cancel());
                    yield break;
                }

                var code = (int)www.responseCode;
                var error = www.error ?? string.Empty;
                var data = Copy(www.downloadHandler != null ? www.downloadHandler.data : null);
                if (IsTimeout(error))
                {
                    completion.TrySetResult(TtsHttpResult.Timeout());
                    yield break;
                }

                if (www.result == UnityWebRequest.Result.Success || (code >= 200 && code < 300))
                {
                    completion.TrySetResult(TtsHttpResult.Ok(code == 0 ? 200 : code, data));
                    yield break;
                }

                if (code >= 400)
                {
                    completion.TrySetResult(TtsHttpResult.Http(code, data));
                    yield break;
                }

                completion.TrySetResult(TtsHttpResult.Network(error));
            }
            finally
            {
                www.Dispose();
            }
        }

        static byte[] Copy(byte[] data)
        {
            if (data == null || data.Length == 0)
                return new byte[0];
            var copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            return copy;
        }

        static bool IsTimeout(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;
            return error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
