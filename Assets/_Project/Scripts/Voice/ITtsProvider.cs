using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.Voice
{
    public sealed class TtsRequest
    {
        public string Text;
        public string VoiceId;
        public string Language;
        public float Speed = 1f;
        public int SampleRate = 24000;
    }

    public readonly struct TtsResult
    {
        public bool Ok { get; }
        public byte[] Audio { get; }
        public string UserMessage { get; }
        public string LogMessage { get; }
        public bool StopQueue { get; }
        public bool Quiet { get; }

        public TtsResult(bool ok, byte[] audio, string userMessage, string logMessage, bool stopQueue, bool quiet)
        {
            Ok = ok;
            Audio = audio;
            UserMessage = userMessage ?? string.Empty;
            LogMessage = logMessage ?? string.Empty;
            StopQueue = stopQueue;
            Quiet = quiet;
        }

        public static TtsResult Success(byte[] audio)
        {
            return new TtsResult(true, audio, string.Empty, string.Empty, false, false);
        }

        public static TtsResult Fail(string userMessage, string logMessage = null, bool stopQueue = false)
        {
            return new TtsResult(false, null, userMessage, logMessage, stopQueue, false);
        }

        public static TtsResult Cancelled()
        {
            return new TtsResult(false, null, string.Empty, string.Empty, false, true);
        }
    }

    public sealed class TtsHttpResult
    {
        public int StatusCode;
        public byte[] Body;
        public string TransportDetail;
        public bool TimedOut;
        public bool Cancelled;
        public bool NetworkFailed;

        public static TtsHttpResult Ok(int statusCode, byte[] body)
        {
            return new TtsHttpResult { StatusCode = statusCode == 0 ? 200 : statusCode, Body = body ?? new byte[0] };
        }

        public static TtsHttpResult Http(int statusCode, byte[] body)
        {
            return new TtsHttpResult { StatusCode = statusCode, Body = body ?? new byte[0] };
        }

        public static TtsHttpResult Timeout()
        {
            return new TtsHttpResult { TimedOut = true };
        }

        public static TtsHttpResult Cancel()
        {
            return new TtsHttpResult { Cancelled = true };
        }

        public static TtsHttpResult Network(string transportDetail = null)
        {
            return new TtsHttpResult { NetworkFailed = true, TransportDetail = transportDetail ?? string.Empty };
        }
    }

    /// <summary>
    /// HTTP POST for one spoken sentence. Tests supply a fake.
    /// </summary>
    public interface ITtsTransport
    {
        Task<TtsHttpResult> PostAsync(string url, string json, string bearerToken, float timeoutSeconds, CancellationToken cancellationToken);
    }

    /// <summary>
    /// One swappable text-to-speech backend.
    /// </summary>
    public interface ITtsProvider
    {
        string ProviderId { get; }

        Task<TtsResult> Synthesize(TtsRequest request, CancellationToken cancellationToken);
    }
}
