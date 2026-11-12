using System.Collections.Generic;

namespace AICompanion.AI
{
    public static class ChatDefaults
    {
        public const string GrokId = "grok";
        public const string LocalId = "local";

        /// <summary>Checked against the xAI streaming docs on 2026-10-21.</summary>
        public const string Model = "grok-4.7";
        public const string BaseUrl = "https://api.x.ai/v1";
        public const string LocalModel = "llama3.2";
        public const string LocalBaseUrl = "http://127.0.0.1:11434/v1";
        public const float Temperature = 0.7f;
        public const int MaxTokens = 512;
        public const float TimeoutSeconds = 120f;
        public const string SystemPrompt = "You are a warm, friendly desktop companion sitting beside the user. Reply in one or two short, conversational sentences. Stay kind, light, and appropriate for any audience.";
    }

    public enum ChatRole
    {
        System = 0,
        User = 1,
        Assistant = 2
    }

    public readonly struct ChatMessage
    {
        public ChatRole Role { get; }
        public string Content { get; }

        public ChatMessage(ChatRole role, string content)
        {
            Role = role;
            Content = content ?? string.Empty;
        }
    }

    public sealed class ChatRequest
    {
        public string SystemPrompt;
        public List<ChatMessage> Messages = new List<ChatMessage>();
        public string Model;
        public float Temperature = ChatDefaults.Temperature;
        public int MaxTokens = ChatDefaults.MaxTokens;

        /// <summary>
        /// One JPEG for this request only. It is not copied into <see cref="Messages"/>
        /// and must be wiped after the body is built. Null for ordinary chat.
        /// </summary>
        public ChatImageAttachment Image;

        /// <summary>
        /// When set, an attached image must not be posted. Private mode sets this
        /// so a cloud vision call cannot leave the machine.
        /// </summary>
        public bool BlockCloud;
    }

    /// <summary>
    /// Options for one send. Ordinary chat leaves this null.
    /// </summary>
    public sealed class ChatTurnOptions
    {
        public ChatImageAttachment Image;
        public bool SingleTurn;
        public bool Ephemeral;
        public bool BlockCloud;
    }

    public enum ChatErrorKind
    {
        None = 0,
        MissingApiKey = 1,
        InvalidApiKey = 2,
        RateLimited = 3,
        Unreachable = 4,
        Timeout = 5,
        Cancelled = 6,
        Malformed = 7,
        UnknownProvider = 8
    }

    public readonly struct ChatResult
    {
        public bool Ok { get; }
        public string Text { get; }
        public ChatErrorKind Error { get; }
        public string UserMessage { get; }

        /// <summary>
        /// Console line for this failure. Already scrubbed. Empty when the
        /// on-screen message is enough.
        /// </summary>
        public string LogMessage { get; }

        public ChatResult(bool ok, string text, ChatErrorKind error, string userMessage, string logMessage)
        {
            Ok = ok;
            Text = text ?? string.Empty;
            Error = error;
            UserMessage = userMessage ?? string.Empty;
            LogMessage = logMessage ?? string.Empty;
        }

        public static ChatResult Success(string text)
        {
            return new ChatResult(true, text, ChatErrorKind.None, string.Empty, string.Empty);
        }

        public static ChatResult Fail(ChatErrorKind error, string userMessage, string logMessage = null)
        {
            return new ChatResult(false, string.Empty, error, userMessage, logMessage);
        }
    }
}
