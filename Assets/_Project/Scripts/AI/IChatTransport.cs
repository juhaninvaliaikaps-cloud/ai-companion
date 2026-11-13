using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.AI
{
    public sealed class ChatHttpRequest
    {
        public string Url;
        public string Body;
        public float TimeoutSeconds;
        /// <summary>False for Private custom endpoints: redirects must never replay the body or key.</summary>
        public bool FollowRedirects = true;
        public readonly List<KeyValuePair<string, string>> Headers = new List<KeyValuePair<string, string>>();
    }

    public readonly struct ChatHttpResult
    {
        public int StatusCode { get; }
        public bool TimedOut { get; }
        public bool Cancelled { get; }
        public bool NetworkFailed { get; }
        public string Body { get; }
        public string TransportDetail { get; }

        public ChatHttpResult(int statusCode, bool timedOut, bool cancelled, bool networkFailed, string body, string transportDetail)
        {
            StatusCode = statusCode;
            TimedOut = timedOut;
            Cancelled = cancelled;
            NetworkFailed = networkFailed;
            Body = body ?? string.Empty;
            TransportDetail = transportDetail ?? string.Empty;
        }

        public static ChatHttpResult Ok(int statusCode)
        {
            return new ChatHttpResult(statusCode, false, false, false, null, null);
        }

        public static ChatHttpResult Http(int statusCode, string body)
        {
            return new ChatHttpResult(statusCode, false, false, false, body, null);
        }

        public static ChatHttpResult Timeout()
        {
            return new ChatHttpResult(0, true, false, false, null, null);
        }

        public static ChatHttpResult Cancel()
        {
            return new ChatHttpResult(0, false, true, false, null, null);
        }

        public static ChatHttpResult Network(string transportDetail = null)
        {
            return new ChatHttpResult(0, false, false, true, null, transportDetail);
        }
    }

    /// <summary>
    /// How many characters of a download to keep before the HTTP status is known.
    /// A 200 response is not capped. An unknown status used to stop at 4096 and drop the rest.
    /// </summary>
    public static class DownloadHold
    {
        public const int ErrorCap = 4096;
        public const int PendingCap = 262144;

        public static int CapFor(int responseCode)
        {
            if (responseCode >= 400)
                return ErrorCap;
            if (responseCode >= 200 && responseCode < 300)
                return int.MaxValue;
            return PendingCap;
        }
    }

    /// <summary>
    /// HTTP POST used by chat providers. Tests supply a fake. The Unity player
    /// uses <see cref="UnityChatTransport"/>.
    /// </summary>
    public interface IChatTransport
    {
        Task<ChatHttpResult> PostAsync(ChatHttpRequest request, Action<string> onBodyChunk, CancellationToken cancellationToken);
    }
}
