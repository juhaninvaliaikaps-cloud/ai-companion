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
            var handler = new StreamingDownloadHandler(www, onBodyChunk);
            try
            {
                var body = Encoding.UTF8.GetBytes(request.Body ?? string.Empty);
                www.uploadHandler = new UploadHandlerRaw(body);
                www.downloadHandler = handler;
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
                    handler.ReleaseHeld();
                    completion.TrySetResult(ChatHttpResult.Ok(code == 0 ? 200 : code));
                    yield break;
                }

                if (code >= 400)
                {
                    completion.TrySetResult(ChatHttpResult.Http(code, handler.ErrorText));
                    yield break;
                }

                completion.TrySetResult(ChatHttpResult.Network(error));
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
            const int Cap = 4096;

            readonly UnityWebRequest _request;
            readonly Action<string> _onChunk;
            readonly Utf8ChunkDecoder _decoder = new Utf8ChunkDecoder();
            readonly StringBuilder _held = new StringBuilder();
            readonly StringBuilder _error = new StringBuilder();
            bool _streamed;

            public StreamingDownloadHandler(UnityWebRequest request, Action<string> onChunk)
                : base(new byte[2048])
            {
                _request = request;
                _onChunk = onChunk;
            }

            public string ErrorText => _error.ToString();

            public void ReleaseHeld()
            {
                if (_streamed || _held.Length == 0 || _onChunk == null)
                    return;
                _onChunk(_held.ToString());
                _held.Length = 0;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                    return true;

                var text = _decoder.Take(data, dataLength);
                if (text.Length == 0)
                    return true;

                var code = (int)_request.responseCode;
                if (code >= 400)
                {
                    AppendCap(_error, text);
                    return true;
                }

                if (code >= 200 && code < 300)
                {
                    if (_held.Length > 0 && _onChunk != null)
                    {
                        _onChunk(_held.ToString());
                        _held.Length = 0;
                    }

                    if (_onChunk != null)
                        _onChunk(text);
                    _streamed = true;
                    return true;
                }

                AppendCap(_held, text);
                AppendCap(_error, text);
                return true;
            }

            static void AppendCap(StringBuilder sb, string text)
            {
                if (sb.Length >= Cap || string.IsNullOrEmpty(text))
                    return;
                var room = Cap - sb.Length;
                sb.Append(text.Length <= room ? text : text.Substring(0, room));
            }
        }
    }
}
