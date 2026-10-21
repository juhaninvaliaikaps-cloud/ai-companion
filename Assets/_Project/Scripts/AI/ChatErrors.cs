namespace AICompanion.AI
{
    public static class ChatErrors
    {
        public const string InvalidApiKey = "Invalid API key";
        public const string RateLimited = "Rate limited, try again shortly";
        public const string Cancelled = "Request cancelled";
        public const string MissingApiKey = "No API key is set";

        public static string Unreachable(string displayName)
        {
            return "Couldn't reach " + Name(displayName);
        }

        public static string TimedOut(string displayName)
        {
            return Name(displayName) + " took too long to answer";
        }

        public static string Rejected(string displayName)
        {
            return Name(displayName) + " rejected the request";
        }

        public static string Unreadable(string displayName)
        {
            return Name(displayName) + " sent a reply this app could not read";
        }

        public static string UnknownProvider(string providerId)
        {
            var id = providerId ?? string.Empty;
            id = id.Trim();
            if (id.Length > 40)
                id = id.Substring(0, 40);
            if (id.Length == 0)
                return "No chat provider is selected.";
            return "No chat provider is named '" + id + "'.";
        }

        public static ChatResult FromTransport(string displayName, ChatHttpResult http, string text, bool done, bool malformed)
        {
            if (http.Cancelled)
                return ChatResult.Fail(ChatErrorKind.Cancelled, Cancelled);
            if (http.TimedOut)
                return ChatResult.Fail(ChatErrorKind.Timeout, TimedOut(displayName));
            if (http.NetworkFailed || http.StatusCode == 0)
                return ChatResult.Fail(ChatErrorKind.Unreachable, Unreachable(displayName));
            if (http.StatusCode == 401)
                return ChatResult.Fail(ChatErrorKind.InvalidApiKey, InvalidApiKey);
            if (http.StatusCode == 429)
                return ChatResult.Fail(ChatErrorKind.RateLimited, RateLimited);
            if (http.StatusCode >= 500)
                return ChatResult.Fail(ChatErrorKind.Unreachable, Unreachable(displayName));
            if (http.StatusCode >= 400)
                return ChatResult.Fail(ChatErrorKind.Malformed, Rejected(displayName));
            if (malformed && string.IsNullOrEmpty(text) && !done)
                return ChatResult.Fail(ChatErrorKind.Malformed, Unreadable(displayName));
            return ChatResult.Success(text);
        }

        static string Name(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "the chat provider";
            return displayName.Trim();
        }
    }
}
