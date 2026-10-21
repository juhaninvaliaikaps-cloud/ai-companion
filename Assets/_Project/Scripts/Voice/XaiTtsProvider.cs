using System;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;

namespace AICompanion.Voice
{
    /// <summary>
    /// xAI REST text-to-speech. One WAV request per sentence.
    /// The key is an Authorization header only. It is never written into the JSON.
    /// </summary>
    public sealed class XaiTtsProvider : ITtsProvider
    {
        public const string Endpoint = "https://api.x.ai/v1/tts";

        readonly IApiKeySource _keys;
        readonly ITtsTransport _transport;
        readonly string _url;
        readonly float _timeoutSeconds;

        public XaiTtsProvider(IApiKeySource keys, ITtsTransport transport, float timeoutSeconds)
            : this(keys, transport, Endpoint, timeoutSeconds)
        {
        }

        public XaiTtsProvider(IApiKeySource keys, ITtsTransport transport, string url, float timeoutSeconds)
        {
            _keys = keys;
            _transport = transport;
            _url = string.IsNullOrWhiteSpace(url) ? Endpoint : url.Trim();
            _timeoutSeconds = timeoutSeconds <= 0f ? 20f : timeoutSeconds;
        }

        public string ProviderId => "xai";

        public Task<TtsResult> Synthesize(TtsRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
                return Task.FromResult(TtsResult.Fail("Nothing to speak."));
            if (_keys == null || !_keys.TryGet(out var key) || string.IsNullOrWhiteSpace(key))
                return Task.FromResult(TtsErrors.MissingKey());
            if (_transport == null)
                return Task.FromResult(TtsResult.Fail(ChatErrors.Unreachable("the voice")));
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(TtsResult.Cancelled());

            var json = TtsRequestJson.Write(request);
            Task<TtsHttpResult> pending;
            try
            {
                pending = _transport.PostAsync(_url, json, key.Trim(), _timeoutSeconds, cancellationToken);
            }
            catch (Exception ex)
            {
                return Task.FromResult(TransportFault(ex));
            }

            if (pending == null || pending.IsCompleted)
                return Task.FromResult(Read(pending));
            return Finish(pending);
        }

        async Task<TtsResult> Finish(Task<TtsHttpResult> pending)
        {
            try
            {
                return Read(await pending);
            }
            catch (OperationCanceledException)
            {
                return TtsResult.Cancelled();
            }
            catch (Exception ex)
            {
                return TransportFault(ex);
            }
        }

        static TtsResult Read(Task<TtsHttpResult> pending)
        {
            if (pending == null || pending.IsFaulted)
            {
                var inner = pending != null && pending.Exception != null ? pending.Exception.GetBaseException() : null;
                if (inner is OperationCanceledException)
                    return TtsResult.Cancelled();
                return TransportFault(inner);
            }

            if (pending.IsCanceled)
                return TtsResult.Cancelled();
            return TtsErrors.FromHttp(pending.GetAwaiter().GetResult());
        }

        static TtsResult TransportFault(Exception ex)
        {
            var detail = ex != null ? ChatErrors.Scrub(ex.Message) : string.Empty;
            var screen = ChatErrors.Unreachable("the voice");
            if (detail.Length == 0)
                return TtsResult.Fail(screen);
            return TtsResult.Fail(screen, "Voice error: " + screen + ": " + detail);
        }
    }
}
