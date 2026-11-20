using System;
using System.Collections.Generic;
using System.Text;
using AICompanion.AI;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-129: keeps a long scene coherent. Chat only sends the last
    /// <see cref="ChatConversation.DefaultMaxMessages"/> messages, so in a long roleplay the
    /// place, the time and who is wearing or doing what fall out of the window, and a small
    /// local model invents a new setup. Every <see cref="EveryUserMessages"/> user messages,
    /// once the session no longer fits the window, the chat model writes a short digest of the
    /// scene so far. It is added to the system prompt until the next one replaces it.
    ///
    /// Kept in memory only, for the running session. It is written by the same model the
    /// session uses, with the same privacy: a digest of a session that stayed local is never
    /// sent to a cloud model, and adult lines never reach a cloud digest request.
    /// </summary>
    public sealed class SceneDigest
    {
        public const int EveryUserMessages = 10;
        public const int MaxTokens = 260;
        public const float Temperature = 0.3f;
        public const int MaxTranscriptCharacters = 9000;
        public const int MaxDigestCharacters = 1200;

        public const string Header = "The scene so far (from earlier in this conversation; the messages below continue it):";

        /// <summary>Always on the chat prompt (JUH-129 step 1): small models drift without it.</summary>
        public const string ContinuityRule =
            "Stay consistent with what this conversation has already established: the place, the time of day, " +
            "what each of you is wearing, holding and doing, and what just happened. Change them only when the story " +
            "clearly moves on.";

        public const string SystemPrompt =
            "You keep notes for an ongoing roleplay chat so it stays consistent. Read the earlier notes (if any) and the " +
            "conversation, then write the current state of the scene in at most 120 words, as plain sentences in English: " +
            "where they are and the time of day; what each person is wearing, holding, and doing right now; what has " +
            "happened so far in this scene, in order; anything promised, planned or unresolved inside the scene. " +
            "Use the names the conversation uses. Write only what the conversation shows; do not invent or judge. " +
            "No lists, no headings, no tags, no commentary.";

        public string Text { get; private set; } = string.Empty;

        /// <summary>True when this digest came from a session that had to stay local.</summary>
        public bool KeptLocal { get; private set; }

        /// <summary>The session's user-message count when the digest was last written (or started).</summary>
        public int WrittenAtUserMessages { get; private set; }

        public bool Running { get; private set; }

        /// <summary>Goes up on every reset, so a digest that finishes for an ended session is dropped.</summary>
        public int Generation { get; private set; }

        public void Reset()
        {
            Text = string.Empty;
            KeptLocal = false;
            WrittenAtUserMessages = 0;
            Running = false;
            Generation++;
        }

        /// <summary>
        /// Due when nothing is running, enough user messages came since the last digest, and the
        /// session no longer fits the chat window (otherwise the model still sees all of it).
        /// </summary>
        public bool IsDue(int sessionUserMessages, int sessionMessages, int sessionCharacters, int windowMessages, int windowCharacters)
        {
            if (Running || sessionUserMessages - WrittenAtUserMessages < EveryUserMessages)
                return false;
            return sessionMessages > windowMessages || sessionCharacters > windowCharacters;
        }

        /// <summary>Marks the request as started. Returns the generation to pass to <see cref="Finish"/>.</summary>
        public int Begin(int sessionUserMessages)
        {
            Running = true;
            WrittenAtUserMessages = sessionUserMessages;
            return Generation;
        }

        /// <summary>
        /// Stores the reply. Returns false when the session was reset meanwhile or the reply was
        /// empty; the old digest is kept then.
        /// </summary>
        public bool Finish(int generation, string reply, bool keptLocal)
        {
            if (generation != Generation)
                return false;
            Running = false;
            var clean = Clean(reply);
            if (clean.Length == 0)
                return false;
            Text = clean;
            KeptLocal = keptLocal;
            return true;
        }

        /// <summary>The previous digest this request may see: none from a local session for a cloud model.</summary>
        public string PreviousFor(bool keepsLocal)
        {
            return KeptLocal && !keepsLocal ? string.Empty : Text;
        }

        /// <summary>
        /// The request: the previous notes (if allowed) and the end of the session transcript.
        /// Sent with <see cref="ChatRequest.NoMemory"/>, so no memories go with it.
        /// </summary>
        public static ChatRequest BuildRequest(string previous, string transcript)
        {
            var body = new StringBuilder(MaxTranscriptCharacters + 600);
            if (!string.IsNullOrWhiteSpace(previous))
                body.Append("Earlier notes:\n").Append(previous.Trim()).Append("\n\n");
            var text = transcript ?? string.Empty;
            if (text.Length > MaxTranscriptCharacters)
            {
                var cut = text.Length - MaxTranscriptCharacters;
                var lineStart = text.IndexOf('\n', cut);
                text = lineStart > 0 && lineStart < text.Length - 1 ? text.Substring(lineStart + 1) : text.Substring(cut);
                body.Append("Conversation (the latest part):\n");
            }
            else
            {
                body.Append("Conversation:\n");
            }

            body.Append(text.Trim());
            return new ChatRequest
            {
                SystemPrompt = SystemPrompt,
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, body.ToString()) },
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                NoMemory = true,
            };
        }

        /// <summary>The block for the chat prompt, or empty (no digest, or a local digest on a cloud send).</summary>
        public string PromptBlock(bool keepsLocal)
        {
            if (Text.Length == 0 || (KeptLocal && !keepsLocal))
                return string.Empty;
            return Header + "\n" + Text;
        }

        /// <summary>Adds the continuity rule, and the digest when there is one, to a chat prompt.</summary>
        public string Append(string prompt, bool keepsLocal)
        {
            var builder = new StringBuilder((prompt ?? string.Empty).Length + 1600);
            builder.Append((prompt ?? string.Empty).TrimEnd());
            if (builder.Length > 0)
                builder.Append("\n\n");
            builder.Append(ContinuityRule);
            var block = PromptBlock(keepsLocal);
            if (block.Length > 0)
                builder.Append("\n\n").Append(block);
            return builder.ToString();
        }

        /// <summary>Plain sentences only: no cue tags, no "Notes:" label, no list marks, at most a short paragraph.</summary>
        public static string Clean(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply))
                return string.Empty;
            var builder = new StringBuilder(reply.Length);
            var depth = 0;
            for (var i = 0; i < reply.Length; i++)
            {
                var c = reply[i];
                if (c == '<')
                {
                    depth++;
                    continue;
                }

                if (c == '>' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (depth == 0)
                    builder.Append(c == '\r' ? ' ' : c);
            }

            var lines = builder.ToString().Split('\n');
            var joined = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim().TrimStart('-', '*', '•', '#', ' ').Trim();
                if (line.Length == 0)
                    continue;
                if (StartsWithLabel(line, out var rest))
                    line = rest;
                if (line.Length == 0)
                    continue;
                if (joined.Length > 0)
                    joined.Append(' ');
                joined.Append(line);
            }

            var text = joined.ToString().Trim();
            if (text.Length > MaxDigestCharacters)
            {
                var cut = text.LastIndexOf(". ", MaxDigestCharacters, StringComparison.Ordinal);
                text = cut > MaxDigestCharacters / 2 ? text.Substring(0, cut + 1) : text.Substring(0, MaxDigestCharacters).TrimEnd();
            }

            return text;
        }

        static readonly string[] Labels = { "notes:", "scene:", "summary:", "current scene:", "scene so far:", "updated notes:" };

        static bool StartsWithLabel(string line, out string rest)
        {
            var lower = line.ToLowerInvariant();
            for (var i = 0; i < Labels.Length; i++)
            {
                if (lower.StartsWith(Labels[i], StringComparison.Ordinal))
                {
                    rest = line.Substring(Labels[i].Length).Trim();
                    return true;
                }
            }

            rest = line;
            return false;
        }
    }
}
