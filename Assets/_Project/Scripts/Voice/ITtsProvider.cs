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
        public int StatusCode { get; }
        public string ContentType { get; }
        public int ByteCount { get; }
        public string Head { get; }

        public TtsResult(bool ok, byte[] audio, string userMessage, string logMessage, bool stopQueue, bool quiet, int statusCode = 0, string contentType = null, int byteCount = 0, string head = null)
        {
            Ok = ok;
            Audio = audio;
            UserMessage = userMessage ?? string.Empty;
            LogMessage = logMessage ?? string.Empty;
            StopQueue = stopQueue;
            Quiet = quiet;
            StatusCode = statusCode;
            ContentType = contentType ?? string.Empty;
            ByteCount = byteCount;
            Head = head ?? string.Empty;
        }

        public static TtsResult Success(byte[] audio, int statusCode = 200, string contentType = null)
        {
            var bytes = audio ?? new byte[0];
            return new TtsResult(true, bytes, string.Empty, string.Empty, false, false, statusCode, contentType, bytes.Length, SpeechAudio.DescribeHead(bytes));
        }

        public static TtsResult Fail(string userMessage, string logMessage = null, bool stopQueue = false, int statusCode = 0, string contentType = null, int byteCount = 0, string head = null)
        {
            return new TtsResult(false, null, userMessage, logMessage, stopQueue, false, statusCode, contentType, byteCount, head);
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
        public string ContentType;
        public string TransportDetail;
        public bool TimedOut;
        public bool Cancelled;
        public bool NetworkFailed;

        public static TtsHttpResult Ok(int statusCode, byte[] body, string contentType = null)
        {
            return new TtsHttpResult
            {
                StatusCode = statusCode == 0 ? 200 : statusCode,
                Body = body ?? new byte[0],
                ContentType = contentType ?? string.Empty
            };
        }

        public static TtsHttpResult Http(int statusCode, byte[] body, string contentType = null)
        {
            return new TtsHttpResult
            {
                StatusCode = statusCode,
                Body = body ?? new byte[0],
                ContentType = contentType ?? string.Empty
            };
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
