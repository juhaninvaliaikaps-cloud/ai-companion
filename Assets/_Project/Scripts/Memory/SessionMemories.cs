using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.Memory
{
    /// <summary>
    /// Session memories (JUH-118 section 3, docs/PISARA.md): at the end of a session the model
    /// writes one to three short memories of it, from her side. Each is stored with
    /// <see cref="WorldMemory.AddShortTerm"/>; fading and promotion are plain C# there.
    /// <para>
    /// Privacy: with <c>keepsLocal</c> (Private mode or an open intimate gate) only the local
    /// model or the private server is asked; a cloud provider is refused before anything is
    /// sent, and the memories are stored as private. <see cref="IntimateBoundaries"/> is part
    /// of the prompt.
    /// </para>
    /// </summary>
    public static class SessionMemories
    {
        /// <summary>Sessions with fewer user messages than this are not worth a memory.</summary>
        public const int MinUserMessages = 4;
        public const int MaxMemories = 3;
        public const int MaxMemoryLength = 200;
        public const int MaxTranscriptCharacters = 6000;
        public const int MaxTokens = 220;
        public const float Temperature = 0.3f;

        /// <summary>A session ends after this long without a message.</summary>
        public const float IdleSeconds = 600f;

        /// <summary>On quit the model gets this long; after that the memories are skipped.</summary>
        public const float QuitTimeoutSeconds = 4f;
        public const float DefaultTimeoutSeconds = 90f;

        public const string SystemPrompt =
            "You write what a desktop companion will remember from one conversation with the user. " +
            "Read the conversation and reply with 1 to 3 short lines, one memory per line, nothing else.\n" +
            "Rules:\n" +
            "- Put each memory on its own line. Each line is one full sentence in the third person about what happened, starting with \"The user\" or with her name. Past tense.\n" +
            "- Call the user \"the user\" and \"they\". Never guess their gender or name.\n" +
            "- Keep what she would still care about next week: something the user told her about their life, something they did or decided together, a promise, a joke they shared, how the talk left her feeling.\n" +
            "- One line when little happened, three only when three different things matter. No greetings, no small talk, nothing about how she speaks, and nothing about the user arriving, leaving or coming back.\n" +
            "- Only what the user actually said or did. A question she asked is not something the user did.\n" +
            "- Only what is in the conversation. Invent nothing: no trips, plans or reasons the user did not mention. If she guessed something about the user, it is not a memory.\n" +
            "- No numbering, no bullets, no quotes, no tags, no text before or after the lines.";

        public static bool Qualifies(int userMessages)
        {
            return userMessages >= MinUserMessages;
        }

        /// <summary>
        /// The conversation as text for the request: "User: ..." and "<paramref name="herName"/>: ..."
        /// lines, newest kept when it is longer than <see cref="MaxTranscriptCharacters"/>.
        /// </summary>
        public static string BuildTranscript(IReadOnlyList<ChatMessage> messages, string herName)
        {
            if (messages == null || messages.Count == 0)
                return string.Empty;
            var name = string.IsNullOrWhiteSpace(herName) ? "Companion" : herName.Trim();
            var lines = new List<string>();
            var total = 0;
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                if (message.Role == ChatRole.System || string.IsNullOrWhiteSpace(message.Content))
                    continue;
                var line = (message.Role == ChatRole.User ? "User" : name) + ": " + message.Content.Trim();
                if (total + line.Length > MaxTranscriptCharacters && lines.Count > 0)
                    break;
                lines.Add(line);
                total += line.Length + 1;
            }

            lines.Reverse();
            return string.Join("\n", lines);
        }

        public static ChatRequest BuildRequest(string transcript, string herName, bool keepsLocal, bool intimateActive)
        {
            var name = string.IsNullOrWhiteSpace(herName) ? "the companion" : herName.Trim();
            return new ChatRequest
            {
                SystemPrompt = SystemPrompt + "\n" + IntimateBoundaries.PromptLine(intimateActive),
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, "Her name: " + name + "\nThe conversation:\n" + (transcript ?? string.Empty)),
                },
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                BlockCloud = keepsLocal,
                NoMemory = true,
            };
        }

        /// <summary>
        /// The memories in a model reply: at most <see cref="MaxMemories"/> lines, with bullets,
        /// numbers and quotes stripped. Lines that are a heading, a tag or too short are skipped.
        /// </summary>
        public static List<string> Parse(string reply)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(reply))
                return result;
            var lines = reply.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length && result.Count < MaxMemories; i++)
            {
                var line = Clean(lines[i]);
                if (line.Length < 12 || line.EndsWith(":", StringComparison.Ordinal) || line.IndexOf('<') >= 0)
                    continue;
                if (line.Length > MaxMemoryLength)
                    line = line.Substring(0, MaxMemoryLength).TrimEnd();
                if (!result.Contains(line))
                    result.Add(line);
            }

            return result;
        }

        static string Clean(string line)
        {
            var text = (line ?? string.Empty).Trim();
            var start = 0;
            while (start < text.Length && (text[start] == '-' || text[start] == '*' || text[start] == '•' || char.IsDigit(text[start])
                || text[start] == '.' || text[start] == ')' || text[start] == ' '))
                start++;
            text = text.Substring(start).Trim();
            if (text.Length >= 2 && (text[0] == '"' || text[0] == '\'') && text[text.Length - 1] == text[0])
                text = text.Substring(1, text.Length - 2).Trim();
            return text;
        }

        /// <summary>
        /// Asks the model and stores what it wrote. Returns the stored memories (empty when the
        /// session was too short, the provider was refused or missing, the model did not answer
        /// in time, or the reply held nothing usable). Never throws.
        /// </summary>
        public static async Task<List<string>> CaptureAsync(
            IChatProvider provider,
            string transcript,
            string herName,
            int userMessages,
            bool keepsLocal,
            bool intimateActive,
            MemoryStore store,
            float timeoutSeconds,
            CancellationToken cancellation)
        {
            var stored = new List<string>();
            if (store == null || !Qualifies(userMessages) || string.IsNullOrWhiteSpace(transcript))
                return stored;
            if (provider == null || !provider.IsConfigured || !PisaraReader.MayUse(provider, keepsLocal))
                return stored;

            ChatResult reply;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    var call = provider.StreamChat(BuildRequest(transcript, herName, keepsLocal, intimateActive), _ => { }, linked.Token);
                    var limit = Task.Delay(TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds)), linked.Token);
                    var first = await Task.WhenAny(call, limit);
                    if (first != call)
                    {
                        linked.Cancel();
                        call.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        return stored;
                    }

                    reply = await call;
                    linked.Cancel();
                }
                catch (Exception)
                {
                    return stored;
                }
            }

            if (!reply.Ok)
                return stored;

            var memories = Parse(reply.Text);
            var now = DateTime.UtcNow;
            for (var i = 0; i < memories.Count; i++)
            {
                var before = store.Count;
                WorldMemory.AddShortTerm(store, memories[i], now, keepsLocal);
                if (store.Count > before)
                    stored.Add(memories[i]);
            }

            return stored;
        }
    }
}
