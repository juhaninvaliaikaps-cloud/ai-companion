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
    /// Posts one TTS request and keeps the response bytes.
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
            var limit = timeoutSeconds <= 0f ? 20f : timeoutSeconds;
            try
            {
                var body = Encoding.UTF8.GetBytes(json ?? string.Empty);
                www.uploadHandler = new UploadHandlerRaw(body);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.timeout = Mathf.Max(1, Mathf.CeilToInt(limit));
                www.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(bearerToken))
                    www.SetRequestHeader("Authorization", "Bearer " + bearerToken);

                var operation = www.SendWebRequest();
                var started = Time.realtimeSinceStartup;
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        www.Abort();
                        completion.TrySetResult(TtsHttpResult.Cancel());
                        yield break;
                    }

                    if (Time.realtimeSinceStartup - started >= limit)
                    {
                        www.Abort();
                        completion.TrySetResult(TtsHttpResult.Timeout());
                        yield break;
                    }

                    yield return null;
                }

                completion.TrySetResult(ReadFinished(www, cancellationToken));
            }
            finally
            {
                www.Dispose();
            }
        }

        static TtsHttpResult ReadFinished(UnityWebRequest www, CancellationToken cancellationToken)
        {
            try
            {
                if (www == null)
                    return TtsHttpResult.Network("Voice playback is not active.");
                if (cancellationToken.IsCancellationRequested)
                    return TtsHttpResult.Cancel();

                var code = (int)www.responseCode;
                var error = www.error ?? string.Empty;
                var data = Copy(www.downloadHandler != null ? www.downloadHandler.data : null);
                var contentType = ReadContentType(www);
                if (IsTimeout(error))
                    return TtsHttpResult.Timeout();
                if (www.result == UnityWebRequest.Result.Success || (code >= 200 && code < 300))
                    return TtsHttpResult.Ok(code == 0 ? 200 : code, data, contentType);
                if (code >= 400)
                    return TtsHttpResult.Http(code, data, contentType);
                return TtsHttpResult.Network(error);
            }
            catch (Exception ex)
            {
                return TtsHttpResult.Network(ex.Message);
            }
        }

        static string ReadContentType(UnityWebRequest www)
        {
            try
            {
                return www.GetResponseHeader("Content-Type") ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
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
