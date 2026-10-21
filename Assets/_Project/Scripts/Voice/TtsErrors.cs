using System.Text;
using AICompanion.AI;

namespace AICompanion.Voice
{
    public static class TtsErrors
    {
        public const string Unplayable = "The voice reply could not be played.";
        public const string EmptyAudio = "The voice sent an empty reply.";

        public static TtsResult FromHttp(TtsHttpResult http)
        {
            if (http == null)
                return TtsResult.Fail(ChatErrors.Unreachable("the voice"));
            if (http.Cancelled)
                return TtsResult.Cancelled();
            var contentType = http.ContentType ?? string.Empty;
            var byteCount = http.Body != null ? http.Body.Length : 0;
            var head = SpeechAudio.DescribeHead(http.Body);
            if (!http.TimedOut && !http.NetworkFailed && http.StatusCode >= 200 && http.StatusCode < 300)
            {
                if (http.Body == null || http.Body.Length == 0)
                    return TtsResult.Fail(EmptyAudio, null, false, http.StatusCode, contentType, byteCount, head);
                return TtsResult.Success(http.Body, http.StatusCode, contentType);
            }

            var body = Decode(http.Body);
            var mapped = ChatErrors.FromTransport(
                "the voice",
                new ChatHttpResult(http.StatusCode, http.TimedOut, http.Cancelled, http.NetworkFailed, body, http.TransportDetail),
                string.Empty,
                false,
                true);
            var log = mapped.LogMessage ?? string.Empty;
            if (log.StartsWith("Chat error:", System.StringComparison.Ordinal))
                log = "Voice error:" + log.Substring("Chat error:".Length);

            var stop = mapped.Error == ChatErrorKind.InvalidApiKey
                || mapped.Error == ChatErrorKind.MissingApiKey
                || mapped.Error == ChatErrorKind.RateLimited
                || http.StatusCode == 401
                || http.StatusCode == 403;
            return TtsResult.Fail(mapped.UserMessage, log, stop, http.StatusCode, contentType, byteCount, head);
        }

        public static TtsResult MissingKey()
        {
            return TtsResult.Fail(ChatErrors.MissingApiKey, "Voice error: " + ChatErrors.MissingApiKey, true);
        }

        static string Decode(byte[] body)
        {
            if (body == null || body.Length == 0)
                return string.Empty;
            return Encoding.UTF8.GetString(body);
        }
    }
}
