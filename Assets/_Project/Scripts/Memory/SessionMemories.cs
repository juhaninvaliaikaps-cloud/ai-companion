using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>What one session-end request stored.</summary>
    public sealed class SessionCapture
    {
        public readonly List<string> Memories = new List<string>();

        /// <summary>True when the provider returned a usable reply, even if every line was filtered.</summary>
        public bool Answered;

        /// <summary>A new open thread in her own life (JUH-117 <see cref="WorldThreads"/>), or null.</summary>
        public MemoryEntry Thread;
    }

    /// <summary>
    /// Whether one chat session had to stay local (JUH-118 review of #91). Every send of the
    /// session notes <see cref="IntimateExpressions.KeepsLocal"/> at that moment, and the flag
    /// sticks: a session that was private at any point stays private at its end, even when
    /// Private mode was turned off (or the intimate gate closed) before the session ended. The
    /// session-end request then goes only to the local model or the private server, and its
    /// memories are stored as private. <see cref="Reset"/> when a new session starts.
    /// </summary>
    public sealed class SessionPrivacy
    {
        /// <summary>True when any send of this session had to stay local.</summary>
        public bool KeptLocal { get; private set; }

        /// <summary>One send of the session, with its private flag.</summary>
        public void Note(bool keepsLocal)
        {
            if (keepsLocal)
                KeptLocal = true;
        }

        /// <summary>The private flag for the session-end request: the sticky one, or the current one.</summary>
        public bool KeepsLocal(bool keepsLocalNow)
        {
            return KeptLocal || keepsLocalNow;
        }

        public void Reset()
        {
            KeptLocal = false;
        }
    }

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

        /// <summary>On quit the model gets this long; if it misses, the handoff is retried on the next start.</summary>
        public const float QuitTimeoutSeconds = 4f;
        public const float DefaultTimeoutSeconds = 90f;

        public const string SystemPrompt =
            "You write what a desktop companion will remember from one conversation with the user. " +
            "Read the conversation and reply with 1 to 3 short lines, one memory per line, nothing else.\n" +
            "Rules:\n" +
            "- Put each memory on its own line. Each line is one full sentence in the third person about what happened, starting with \"The user\" or with her name. Past tense.\n" +
            "- Call the user \"the user\" and \"they\". Never guess their gender or name.\n" +
            "- Keep only what she would still care about next week: something the user told her about their life, something they did or decided together, a promise, a joke they shared, or a lasting change in how close they are.\n" +
            "- Do not write a passing feeling, a physical reaction, a gesture, or a description of the scene. \"She trembled\" and \"the room went quiet\" are not memories. A feeling is allowed only when it reveals a lasting trigger, preference or boundary.\n" +
            "- Do not repeat something already known about the user or already in her memories. One clear durable line is better than three overlapping ones.\n" +
            "- No greetings, no small talk, nothing about how she speaks, and nothing about the user arriving, leaving or coming back.\n" +
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

        /// <summary>
        /// With <paramref name="askThread"/> the request also asks for one new thread in her own
        /// life (<see cref="WorldThreads.Instruction"/>), written as a JSON object after the lines.
        /// </summary>
        public static ChatRequest BuildRequest(string transcript, string herName, bool keepsLocal, bool intimateActive, bool askThread = false)
        {
            var name = string.IsNullOrWhiteSpace(herName) ? "the companion" : herName.Trim();
            var system = SystemPrompt;
            if (askThread)
                system += "\n" + ThreadRule + " " + WorldThreads.Instruction;
            return new ChatRequest
            {
                SystemPrompt = system + "\n" + IntimateBoundaries.PromptLine(intimateActive),
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

        /// <summary>Keeps the thread apart from the memory lines, so its JSON is never stored as a memory.</summary>
        public const string ThreadRule = "After the memory lines, on a line of its own, you may add one JSON object.";

        /// <summary>
        /// The new thread in a reply, or null. Reads <c>{"newThread": {...}}</c>, and also a bare
        /// <c>{"text": ..., "goodOutcome": ..., "badOutcome": ...}</c>, which local models often write.
        /// </summary>
        public static NewThread ParseThread(string reply)
        {
            var thread = WorldThreads.TryParse(reply);
            if (thread != null)
                return thread;
            var json = CharacterSheet.ExtractObject(reply);
            if (json == null || json.IndexOf("\"text\"", StringComparison.Ordinal) < 0)
                return null;
            return WorldThreads.TryParse("{\"newThread\": " + json + "}");
        }

        /// <summary>The reply without its JSON object, so only the memory lines are left.</summary>
        public static string WithoutObject(string reply)
        {
            if (string.IsNullOrEmpty(reply))
                return string.Empty;
            var json = CharacterSheet.ExtractObject(reply);
            if (json == null)
                return reply;
            var at = reply.IndexOf(json, StringComparison.Ordinal);
            if (at < 0)
            {
                // ExtractObject cleaned it (a code fence or a trailing comma): cut from the first brace.
                at = reply.IndexOf('{');
                return at < 0 ? reply : reply.Substring(0, at);
            }

            return reply.Substring(0, at) + reply.Substring(at + json.Length);
        }

        /// <summary>
        /// The memories in a model reply: at most <see cref="MaxMemories"/> lines, with bullets,
        /// numbers and quotes stripped. Lines that are a heading, a tag, JSON or too short are skipped.
        /// </summary>
        public static List<string> Parse(string reply)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(reply))
                return result;
            var lines = WithoutObject(reply).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length && result.Count < MaxMemories; i++)
            {
                var line = Clean(lines[i]);
                if (line.Length < 12 || line.EndsWith(":", StringComparison.Ordinal) || line.IndexOf('<') >= 0
                    || line.IndexOf('{') >= 0 || line.IndexOf('}') >= 0 || line.IndexOf("newThread", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.StartsWith("`", StringComparison.Ordinal))
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
        /// Asks the model and stores what it wrote. Returns the stored memories and the new
        /// thread (both empty when the session was too short, the provider was refused or
        /// missing, the model did not answer in time, or the reply held nothing usable). A thread
        /// is asked for only with <paramref name="askThread"/>, and stored only while "Her own
        /// world" and "Life while you are away" are on (<see cref="WorldThreads.Add"/>). Never throws.
        /// </summary>
        public static async Task<SessionCapture> CaptureAsync(
            IChatProvider provider,
            string transcript,
            string herName,
            int userMessages,
            bool keepsLocal,
            bool intimateActive,
            MemoryStore store,
            float timeoutSeconds,
            CancellationToken cancellation,
            bool askThread = false)
        {
            var capture = new SessionCapture();
            var stored = capture.Memories;
            if (store == null || !Qualifies(userMessages) || string.IsNullOrWhiteSpace(transcript))
                return capture;
            if (provider == null || !provider.IsConfigured || !PisaraReader.MayUse(provider, keepsLocal))
                return capture;

            ChatResult reply;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    var call = provider.StreamChat(BuildRequest(transcript, herName, keepsLocal, intimateActive, askThread), _ => { }, linked.Token);
                    var limit = Task.Delay(TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds)), linked.Token);
                    var first = await Task.WhenAny(call, limit);
                    if (first != call)
                    {
                        linked.Cancel();
                        call.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        Debug.LogWarning("Session memories: timed out after " + timeoutSeconds.ToString("0") + "s");
                        return capture;
                    }

                    reply = await call;
                    linked.Cancel();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Session memories: provider call failed (" + e.GetType().Name + ")");
                    return capture;
                }
            }

            if (!reply.Ok)
            {
                Debug.LogWarning("Session memories: provider reply failed (" + reply.Error + "): " + reply.LogMessage);
                return capture;
            }

            capture.Answered = true;
            var memories = Parse(reply.Text);
            var now = DateTime.UtcNow;
            for (var i = 0; i < memories.Count; i++)
            {
                if (!MemoryDurability.IsDurable(memories[i]))
                    continue;
                var before = store.Count;
                WorldMemory.AddShortTerm(store, memories[i], now, keepsLocal);
                if (store.Count > before)
                    stored.Add(memories[i]);
            }

            if (askThread)
                capture.Thread = WorldThreads.Add(store, ParseThread(reply.Text), now, keepsLocal);
            return capture;
        }
    }
}
