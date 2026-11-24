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

        /// <summary>Optional model-assigned type, importance and confidence, parallel to Memories.</summary>
        public readonly List<SessionMemory> Details = new List<SessionMemory>();

        /// <summary>True when the provider returned a usable reply, even if every line was filtered.</summary>
        public bool Answered;

        /// <summary>A new open thread in her own life (JUH-117 <see cref="WorldThreads"/>), or null.</summary>
        public MemoryEntry Thread;

        /// <summary>JUH-141: the memories the summary was shown (m1, m2, …).</summary>
        public readonly List<KnownMemory> Known = new List<KnownMemory>();

        /// <summary>JUH-141: entries the summary corrected ("update m1: …").</summary>
        public readonly List<MemoryUpdate> Updates = new List<MemoryUpdate>();

        /// <summary>JUH-141: entry ids the summary closed ("resolved m2").</summary>
        public readonly List<string> Resolved = new List<string>();
    }

    /// <summary>One parsed session line, with optional model metadata (JUH-134).</summary>
    public sealed class SessionMemory
    {
        public string Text = string.Empty;
        public string Kind = string.Empty;
        public int Importance;
        public int Confidence;
        public bool HasMetadata;

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(Text); }
        }
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

        /// <summary>True when the 18+ gate was open at any send of this session.</summary>
        public bool Intimate { get; private set; }

        /// <summary>One send of the session, with its private flag.</summary>
        public void Note(bool keepsLocal)
        {
            if (keepsLocal)
                KeptLocal = true;
        }

        public void NoteMoment(bool intimate)
        {
            if (intimate)
                Intimate = true;
        }

        /// <summary>The private flag for the session-end request: the sticky one, or the current one.</summary>
        public bool KeepsLocal(bool keepsLocalNow)
        {
            return KeptLocal || keepsLocalNow;
        }

        public void Reset()
        {
            KeptLocal = false;
            Intimate = false;
        }
    }

    /// <summary>
    /// Which provider and moment kind a finished chapter uses (JUH-189). A Private/Public
    /// switch closes the chapter that just ended, so it uses the privacy that chapter
    /// recorded. The mode that was just turned on is not that chapter. Other ends (a pause,
    /// quitting, a persona change) still follow the mode that is on.
    /// </summary>
    public struct SessionClosePlan
    {
        /// <summary>Public lines may be summarised by the public provider.</summary>
        public bool UsePublicProvider;

        /// <summary>The chapter itself held an 18+ moment.</summary>
        public bool Intimate;

        public string MomentKind
        {
            get { return Intimate ? "intimate" : string.Empty; }
        }

        public static SessionClosePlan For(
            bool sessionKeptLocal, bool sessionIntimate, bool modeKeepsLocal, bool gateOpen, bool modeSwitch)
        {
            if (modeSwitch)
            {
                return new SessionClosePlan
                {
                    UsePublicProvider = !sessionKeptLocal,
                    Intimate = sessionIntimate,
                };
            }

            return new SessionClosePlan
            {
                UsePublicProvider = !modeKeepsLocal,
                Intimate = sessionIntimate || gateOpen,
            };
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
        /// <summary>Room for up to three memories plus a few update and resolved lines (JUH-141).</summary>
        public const int MaxTokens = 320;
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
            "- Put each memory on its own line. Start it with a metadata tag: [type|importance|confidence] then the sentence. type is fact, preference, boundary, promise, event, relationship or thread. importance is 1 to 5; confidence is 0 to 100. Example: [promise|5|95] The user promised to visit on Sunday. If you are unsure, omit the tag; the app will infer one.\n" +
            "- Each line is one full sentence in the third person about what happened, starting with \"The user\" or with her name. Past tense.\n" +
            "- Call the user \"the user\" and \"they\". Never guess their gender or name.\n" +
            "- Keep only what she would still care about next week: something the user told her about their life, something they did or decided together, a promise, a joke they shared, or a lasting change in how close they are.\n" +
            "- Name the concrete thing: the person, event, promise, preference or boundary. Never write \"something\", \"a moment\", \"an experience\" or \"what happened\" without naming it. A vague line is worse than no line.\n" +
            "- Write one sentence that still makes sense a month later without the transcript. If the only thing left is a passing mood, write nothing.\n" +
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
        public static ChatRequest BuildRequest(string transcript, string herName, bool keepsLocal, bool intimateActive, bool askThread = false,
            string knowledge = null)
        {
            var name = string.IsNullOrWhiteSpace(herName) ? "the companion" : herName.Trim();
            var system = SystemPrompt;
            // JUH-141: today's date, and the memories she already has with ids to update or resolve.
            if (!string.IsNullOrWhiteSpace(knowledge))
                system += "\n" + knowledge;
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
            var details = ParseDetailed(reply);
            var result = new List<string>();
            for (var i = 0; i < details.Count; i++)
                result.Add(details[i].Text);
            return result;
        }

        /// <summary>
        /// Parses the optional [type|importance|confidence] prefix. A model that omits it still
        /// produces a usable line; the timeline falls back to its local classifier.
        /// </summary>
        public static List<SessionMemory> ParseDetailed(string reply)
        {
            var result = new List<SessionMemory>();
            if (string.IsNullOrWhiteSpace(reply))
                return result;
            var lines = WithoutObject(reply).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length && result.Count < MaxMemories; i++)
            {
                // JUH-141: "update m1: …" and "resolved m2" are changes, not new memories.
                if (SessionKnowledge.IsChangeLine(lines[i]))
                    continue;
                var parsed = ParseLine(lines[i]);
                if (parsed == null || !parsed.IsValid)
                    continue;
                var duplicate = false;
                for (var j = 0; j < result.Count; j++)
                {
                    if (string.Equals(result[j].Text, parsed.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    result.Add(parsed);
            }

            return result;
        }

        /// <summary>JUH-141: the sentence of an "update m1: …" line, with its optional [type|importance|confidence] tag.</summary>
        public static SessionMemory ParseLineForUpdate(string raw)
        {
            return ParseLine(raw);
        }

        static SessionMemory ParseLine(string raw)
        {
            var line = Clean(raw);
            if (line.Length == 0)
                return null;

            var memory = new SessionMemory();
            if (line[0] == '[')
            {
                var end = line.IndexOf(']');
                if (end > 1)
                {
                    var tag = line.Substring(1, end - 1);
                    var body = line.Substring(end + 1).Trim();
                    ApplyTag(memory, tag);
                    line = body;
                }
            }

            if (line.Length < 12 || line.EndsWith(":", StringComparison.Ordinal) || line.IndexOf('<') >= 0
                || line.IndexOf('{') >= 0 || line.IndexOf('}') >= 0 || line.IndexOf("newThread", StringComparison.OrdinalIgnoreCase) >= 0
                || line.StartsWith("`", StringComparison.Ordinal))
                return null;
            if (line.Length > MaxMemoryLength)
                line = line.Substring(0, MaxMemoryLength).TrimEnd();
            memory.Text = line;
            return memory;
        }

        static void ApplyTag(SessionMemory memory, string tag)
        {
            var parts = (tag ?? string.Empty).Split('|');
            if (parts.Length == 0)
                return;
            var kind = NormalizeKind(parts[0]);
            var importance = 0;
            var confidence = 0;
            var ok = !string.IsNullOrEmpty(kind);
            if (parts.Length > 1)
                ok &= int.TryParse(parts[1], out importance) && importance >= 1 && importance <= 5;
            if (parts.Length > 2)
                ok &= int.TryParse(parts[2], out confidence) && confidence >= 0 && confidence <= 100;
            if (!ok)
                return;
            memory.Kind = kind;
            memory.Importance = importance;
            memory.Confidence = confidence;
            memory.HasMetadata = true;
        }

        static string NormalizeKind(string value)
        {
            var kind = (value ?? string.Empty).Trim().ToLowerInvariant();
            switch (kind)
            {
                case "fact": return MemoryDurability.Fact;
                case "preference": return MemoryDurability.Preference;
                case "boundary": return MemoryDurability.Boundary;
                case "promise": return MemoryDurability.Promise;
                case "event": return MemoryDurability.Event;
                case "relationship":
                case "relationship_change":
                    return MemoryDurability.RelationshipChange;
                case "thread":
                case "open_thread":
                    return MemoryDurability.OpenThread;
                default: return string.Empty;
            }
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
            bool askThread = false,
            DateTime? sessionLocalDate = null,
            string moment = null)
        {
            var capture = new SessionCapture();
            var stored = capture.Memories;
            if (store == null || !Qualifies(userMessages) || string.IsNullOrWhiteSpace(transcript))
                return capture;
            if (provider == null || !provider.IsConfigured || !PisaraReader.MayUse(provider, keepsLocal))
                return capture;

            // JUH-141: show the summary what she already remembers about this talk (a cloud
            // summary sees only entries that are not private) and today's date.
            var timeline = RelationshipTimeline.ForStore(store);
            capture.Known.AddRange(SessionKnowledge.For(timeline.Entries, transcript, keepsLocal, DateTime.UtcNow));
            var knowledge = SessionKnowledge.PromptBlock(capture.Known, sessionLocalDate ?? DateTime.Now);

            ChatResult reply;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    var call = provider.StreamChat(BuildRequest(transcript, herName, keepsLocal, intimateActive, askThread, knowledge), _ => { }, linked.Token);
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
            SessionKnowledge.ParseChanges(reply.Text, capture.Known, capture.Updates, capture.Resolved);
            var memories = ParseDetailed(reply.Text);
            var now = DateTime.UtcNow;
            for (var i = 0; i < memories.Count; i++)
            {
                var memory = memories[i];
                if (memory == null || !memory.IsValid || !MemoryDurability.IsDurable(memory.Text))
                    continue;
                var before = store.Count;
                WorldMemory.AddShortTerm(store, memory.Text, now, keepsLocal, moment);
                if (store.Count > before)
                {
                    stored.Add(memory.Text);
                    capture.Details.Add(memory);
                }
            }

            // JUH-188: an intimate session must leave one memory of the event, not the
            // physical details. The private copy is for private mode; the public copy is a
            // deliberately vague marker so public mode is not blind after a restart.
            if (string.Equals(moment, "intimate", StringComparison.OrdinalIgnoreCase))
            {
                var name = string.IsNullOrWhiteSpace(herName) ? "her" : herName.Trim();
                var marker = keepsLocal
                    ? "The user and " + name + " were intimate."
                    : "The user and " + name + " shared a private, intimate moment.";
                var before = store.Count;
                WorldMemory.AddShortTerm(store, marker, now, keepsLocal, "intimate");
                if (store.Count > before && !stored.Contains(marker))
                    stored.Add(marker);
            }

            if (askThread)
                capture.Thread = WorldThreads.Add(store, ParseThread(reply.Text), now, keepsLocal);
            return capture;
        }
    }
}
