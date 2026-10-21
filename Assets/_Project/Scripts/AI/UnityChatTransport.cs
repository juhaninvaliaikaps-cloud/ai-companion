using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.AI
{
    /// <summary>
    /// Posts with <see cref="UnityWebRequest"/> and reports the body as it arrives.
    /// HTTP error bodies are not forwarded, so a 401 cannot be parsed as a reply.
    /// </summary>
    public sealed class UnityChatTransport : IChatTransport
    {
        readonly MonoBehaviour _host;

        public UnityChatTransport(MonoBehaviour host)
        {
            _host = host;
        }

        public Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ChatHttpResult>();
            if (_host == null || !_host.isActiveAndEnabled)
            {
                completion.SetResult(ChatHttpResult.Network());
                return completion.Task;
            }

            _host.StartCoroutine(Send(request, onBodyChunk, cancellationToken, completion));
            return completion.Task;
        }

        static IEnumerator Send(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken, TaskCompletionSource<ChatHttpResult> completion)
        {
            if (request == null)
            {
                completion.TrySetResult(ChatHttpResult.Network());
                yield break;
            }

            var www = new UnityWebRequest(request.Url, UnityWebRequest.kHttpVerbPOST);
            try
            {
                var body = Encoding.UTF8.GetBytes(request.Body ?? string.Empty);
                www.uploadHandler = new UploadHandlerRaw(body);
                www.downloadHandler = new StreamingDownloadHandler(www, onBodyChunk);
                var timeout = request.TimeoutSeconds <= 0f ? ChatDefaults.TimeoutSeconds : request.TimeoutSeconds;
                www.timeout = Mathf.CeilToInt(timeout);
                if (request.Headers != null)
                {
                    for (var i = 0; i < request.Headers.Count; i++)
                        www.SetRequestHeader(request.Headers[i].Key, request.Headers[i].Value);
                }

                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        www.Abort();
                        completion.TrySetResult(ChatHttpResult.Cancel());
                        yield break;
                    }

                    yield return null;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetResult(ChatHttpResult.Cancel());
                    yield break;
                }

                var code = (int)www.responseCode;
                var error = www.error ?? string.Empty;
                if (IsTimeout(error))
                {
                    completion.TrySetResult(ChatHttpResult.Timeout());
                    yield break;
                }

                if (www.result == UnityWebRequest.Result.Success || (code >= 200 && code < 300))
                {
                    completion.TrySetResult(ChatHttpResult.Ok(code == 0 ? 200 : code));
                    yield break;
                }

                if (code >= 400)
                {
                    completion.TrySetResult(ChatHttpResult.Ok(code));
                    yield break;
                }

                completion.TrySetResult(ChatHttpResult.Network());
            }
            finally
            {
                www.Dispose();
            }
        }

        static bool IsTimeout(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;
            return error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        sealed class StreamingDownloadHandler : DownloadHandlerScript
        {
            readonly UnityWebRequest _request;
            readonly Action<string> _onChunk;
            readonly Utf8ChunkDecoder _decoder = new Utf8ChunkDecoder();

            public StreamingDownloadHandler(UnityWebRequest request, Action<string> onChunk)
                : base(new byte[2048])
            {
                _request = request;
                _onChunk = onChunk;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (_onChunk == null || data == null || dataLength <= 0)
                    return true;

                var code = _request.responseCode;
                if (code >= 400)
                    return true;

                var text = _decoder.Take(data, dataLength);
                if (text.Length > 0)
                    _onChunk(text);
                return true;
            }
        }
    }
}
