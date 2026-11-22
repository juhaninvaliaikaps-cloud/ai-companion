using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.UI
{
    public enum ChatLineKind
    {
        User = 0,
        Assistant = 1,
        System = 2
    }

    /// <summary>
    /// JUH-174: what a note line is, for its colour in the chat box. Notes stay out of every
    /// request whatever their tag.
    /// </summary>
    public enum ChatLineTag
    {
        /// <summary>A plain note, a user line or a reply.</summary>
        None = 0,

        /// <summary>Scene text: the first scene and "While you were away" (green).</summary>
        Scene = 1,

        /// <summary>A relationship or desire change, the readable copy of a floating piece.</summary>
        Change = 2,
    }

    public sealed class ChatLine
    {
        internal ChatLine(ChatLineKind kind, string text, bool streaming)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            Streaming = streaming;
        }

        public ChatLineKind Kind { get; }
        public string Text { get; private set; }
        public bool Streaming { get; private set; }

        /// <summary>
        /// Screen observations stay in the session transcript and are left out of the history file.
        /// </summary>
        public bool KeepInHistory { get; internal set; } = true;

        /// <summary>
        /// Adult interaction text. A later cloud request leaves the line out. A local or
        /// private request still includes it.
        /// </summary>
        public bool LocalOnly { get; internal set; }

        /// <summary>
        /// The reply hit a weak spot, kink or secret, so the chat should keep its pink
        /// feedback styling. UI only; the text sent to the model is unchanged.
        /// </summary>
        public bool HighlightFeedback { get; internal set; }

        /// <summary>JUH-174: a scene note or a change line; <see cref="ChatLineTag.None"/> otherwise.</summary>
        public ChatLineTag Tag { get; internal set; }

        /// <summary>JUH-174: for a change line, the piece's <c>FeedbackKind</c> as a number (it picks the colour).</summary>
        public int Change { get; internal set; }

        /// <summary>
        /// JUH-174: an 18+ change (desire, or one of her 18+ secrets). It is shown only while the
        /// 18+ gate is open (<see cref="ChatConversation.Shows"/>), and kept as a local-only line.
        /// </summary>
        public bool Adult { get; internal set; }

        internal void Append(string piece)
        {
            if (!string.IsNullOrEmpty(piece))
                Text += piece;
        }

        internal void UseIfEmpty(string text)
        {
            if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(text))
                Text = text;
        }

        internal void SetText(string text)
        {
            Text = text ?? string.Empty;
        }

        internal void Stop()
        {
            Streaming = false;
        }
    }

    /// <summary>
    /// Session transcript. The full list stays on screen. Outgoing requests
    /// drop the oldest turns once the cap is hit. System lines stay local.
    /// </summary>
    public sealed class ChatConversation
    {
        public const int DefaultMaxMessages = 12;
        public const int DefaultMaxCharacters = 4000;

        readonly List<ChatLine> _lines = new List<ChatLine>();
        readonly int _maxMessages;
        readonly int _maxCharacters;

        public ChatConversation(int maxMessages = DefaultMaxMessages, int maxCharacters = DefaultMaxCharacters)
        {
            _maxMessages = maxMessages < 1 ? 1 : maxMessages;
            _maxCharacters = maxCharacters < 1 ? 1 : maxCharacters;
        }

        public IReadOnlyList<ChatLine> Lines => _lines;

        /// <summary>How many messages and characters of history a request carries (JUH-129).</summary>
        public int MaxMessages => _maxMessages;
        public int MaxCharacters => _maxCharacters;
        public bool IsBusy { get; private set; }
        public int Revision { get; private set; }

        /// <summary>
        /// JUH-130: first line sent in requests. Chat sets this to the current session's
        /// start so earlier sessions stay in the transcript and memories, not in the raw
        /// prompt where they compete with the scene happening now. 0 sends everything.
        /// </summary>
        public int HistoryStart { get; set; }

        /// <summary>Increments on each accepted send so a late chunk cannot join the next reply.</summary>
        public int Generation { get; private set; }

        public bool TryBeginSend(string userText, string systemPrompt, float temperature, int maxTokens, out ChatRequest request)
        {
            return TryBeginSend(userText, systemPrompt, temperature, maxTokens, out request, null);
        }

        public bool TryBeginSend(string userText, string systemPrompt, float temperature, int maxTokens, out ChatRequest request, ChatTurnOptions options)
        {
            request = null;
            var text = (userText ?? string.Empty).Trim();
            if (IsBusy || text.Length == 0)
                return false;

            Generation++;
            var user = new ChatLine(ChatLineKind.User, text, false);
            var localOnly = options != null && options.LocalOnly;
            if (localOnly)
                user.LocalOnly = true;
            _lines.Add(user);
            var includeLocalOnly = options == null || !options.OmitLocalOnly;
            request = options != null && options.SingleTurn
                ? BuildSingle(systemPrompt, temperature, maxTokens, text)
                : BuildRequest(systemPrompt, temperature, maxTokens, includeLocalOnly);
            if (options != null)
            {
                request.Image = options.Image;
                request.BlockCloud = options.BlockCloud;
            }

            var assistant = new ChatLine(ChatLineKind.Assistant, string.Empty, true);
            if (localOnly)
                assistant.LocalOnly = true;
            _lines.Add(assistant);
            if (options != null && options.Ephemeral)
            {
                user.KeepInHistory = false;
                assistant.KeepInHistory = false;
            }

            IsBusy = true;
            Touch();
            return true;
        }

        public void PushDelta(string piece)
        {
            PushDelta(Generation, piece);
        }

        public void PushDelta(int generation, string piece)
        {
            if (generation != Generation || string.IsNullOrEmpty(piece))
                return;

            var line = StreamingAssistant();
            if (line == null)
                return;

            line.Append(piece);
            Touch();
        }

        /// <summary>
        /// Replaces the streaming assistant line. Used when the cue filter's final
        /// text is the source of truth, so a tag that slipped into a delta cannot
        /// stay in the transcript or the next request.
        /// </summary>
        public bool ReplaceStreamingAssistant(int generation, string text)
        {
            if (generation != Generation)
                return false;

            var line = StreamingAssistant();
            if (line == null)
                return false;

            var clean = text ?? string.Empty;
            if (line.Text == clean)
                return true;

            line.SetText(clean);
            Touch();
            return true;
        }

        public void Complete(ChatResult result)
        {
            IsBusy = false;
            if (result.Ok)
            {
                if (AssistantIsEmpty() && string.IsNullOrEmpty(result.Text))
                {
                    RemoveStreamingAssistant();
                    AddSystem("Empty reply.");
                }
                else
                {
                    SealAssistant(ScreenLookCue.Strip(result.Text));
                }

                return;
            }

            if (AssistantIsEmpty())
                RemoveStreamingAssistant();
            else
                SealAssistant(null);

            var note = result.UserMessage;
            if (string.IsNullOrEmpty(note))
                note = result.LogMessage;
            if (!string.IsNullOrEmpty(note))
                AddSystem(note);
        }

        /// <summary>A reply that was not triggered by a visible user turn, used by automatic looks.</summary>
        public void AddAssistant(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            _lines.Add(new ChatLine(ChatLineKind.Assistant, text.Trim(), false));
            Touch();
        }
        public void AddSystem(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            _lines.Add(new ChatLine(ChatLineKind.System, text.Trim(), false));
            Touch();
        }

        /// <summary>JUH-174: scene text (the first scene, "While you were away"), drawn green.</summary>
        public void AddScene(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            _lines.Add(new ChatLine(ChatLineKind.System, text.Trim(), false) { Tag = ChatLineTag.Scene });
            Touch();
        }

        /// <summary>
        /// JUH-174: the readable copy of a floating change ("+1.5 affection · Compliment"), so it
        /// can be read after the piece is gone. A note: it never goes to the model.
        /// </summary>
        public void AddChange(string text, int change, bool adult)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            _lines.Add(new ChatLine(ChatLineKind.System, text.Trim(), false)
            {
                Tag = ChatLineTag.Change,
                Change = change,
                Adult = adult,
                LocalOnly = adult,
            });
            Touch();
        }

        /// <summary>JUH-174: false for an 18+ change line while the gate is closed; such lines are not drawn or copied then.</summary>
        public static bool Shows(ChatLine line)
        {
            return line != null && (!line.Adult || IntimateExpressions.Active);
        }

        /// <summary>Marks the newest assistant reply as a pink feedback line.</summary>
        public void MarkLastAssistantFeedback()
        {
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                if (_lines[i] == null || _lines[i].Kind != ChatLineKind.Assistant)
                    continue;
                _lines[i].HighlightFeedback = true;
                Touch();
                return;
            }
        }

        /// <summary>True when the newest assistant reply is a pink feedback line.</summary>
        public bool LastAssistantHighlighted
        {
            get
            {
                for (var i = _lines.Count - 1; i >= 0; i--)
                {
                    if (_lines[i] != null && _lines[i].Kind == ChatLineKind.Assistant)
                        return _lines[i].HighlightFeedback;
                }

                return false;
            }
        }

        public ChatRequest BuildRequest(string systemPrompt, float temperature, int maxTokens)
        {
            return BuildRequest(systemPrompt, temperature, maxTokens, true);
        }

        /// <param name="includeLocalOnly">False on a cloud send, so adult interaction lines stay on this machine.</param>
        public ChatRequest BuildRequest(string systemPrompt, float temperature, int maxTokens, bool includeLocalOnly)
        {
            var messages = new List<ChatMessage>();
            var first = Math.Max(0, HistoryStart);
            for (var i = first; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (line.Kind == ChatLineKind.System || string.IsNullOrEmpty(line.Text))
                    continue;
                if (!includeLocalOnly && line.LocalOnly)
                    continue;

                var role = line.Kind == ChatLineKind.Assistant ? ChatRole.Assistant : ChatRole.User;
                messages.Add(new ChatMessage(role, line.Text));
            }

            while (messages.Count > _maxMessages || (messages.Count > 1 && Count(messages) > _maxCharacters))
                messages.RemoveAt(0);

            var request = new ChatRequest
            {
                SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? ChatDefaults.SystemPrompt : systemPrompt.Trim(),
                Temperature = temperature,
                MaxTokens = maxTokens,
                Messages = messages,
                Roleplay = true,
            };
            return request;
        }

        ChatRequest BuildSingle(string systemPrompt, float temperature, int maxTokens, string userText)
        {
            var request = new ChatRequest
            {
                SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? ChatDefaults.SystemPrompt : systemPrompt.Trim(),
                Temperature = temperature,
                MaxTokens = maxTokens,
                Messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, userText ?? string.Empty) },
                Roleplay = true,
            };
            return request;
        }

        public void ClearTranscript()
        {
            _lines.Clear();
            HistoryStart = 0;
            IsBusy = false;
            Generation++;
            Touch();
        }

        public bool ImportLine(ChatLineKind kind, string text)
        {
            return ImportLine(kind, text, false);
        }

        public bool ImportLine(ChatLineKind kind, string text, bool localOnly)
        {
            return ImportLine(kind, text, localOnly, ChatLineTag.None, 0, false);
        }

        /// <summary>JUH-174: as above, with a note's tag, change kind and 18+ flag from the history file.</summary>
        public bool ImportLine(ChatLineKind kind, string text, bool localOnly, ChatLineTag tag, int change, bool adult)
        {
            if (kind != ChatLineKind.User && kind != ChatLineKind.Assistant && kind != ChatLineKind.System)
                return false;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var clean = text.Trim();
            if (clean.Length > 4000)
                clean = clean.Substring(0, 4000);
            var line = new ChatLine(kind, clean, false);
            line.LocalOnly = localOnly || adult;
            if (kind == ChatLineKind.System)
            {
                // A history file from before JUH-174 has no tags: its scene notes are known by their start.
                if (tag == ChatLineTag.None && (clean.StartsWith("Scene: ", StringComparison.Ordinal)
                                                || clean.StartsWith("While you were away: ", StringComparison.Ordinal)))
                    tag = ChatLineTag.Scene;
                line.Tag = tag;
                line.Change = tag == ChatLineTag.Change ? change : 0;
                line.Adult = adult;
            }

            _lines.Add(line);
            Touch();
            return true;
        }

        public string FormatTranscript()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (line.Kind == ChatLineKind.Assistant && line.Streaming && line.Text.Length == 0)
                    continue;
                if (!Shows(line))
                    continue;

                if (sb.Length > 0)
                    sb.Append("\n\n");
                sb.Append(Label(line.Kind));
                sb.Append('\n');
                sb.Append(line.Text);
            }

            return sb.ToString();
        }

        static string Label(ChatLineKind kind)
        {
            switch (kind)
            {
                case ChatLineKind.User:
                    return "You";
                case ChatLineKind.Assistant:
                    return "Companion";
                default:
                    return "Note";
            }
        }

        void SealAssistant(string fallbackText)
        {
            var line = StreamingAssistant();
            if (line == null)
                return;

            line.UseIfEmpty(fallbackText);
            line.Stop();
            Touch();
        }

        bool AssistantIsEmpty()
        {
            var line = StreamingAssistant();
            return line == null || string.IsNullOrEmpty(line.Text);
        }

        void RemoveStreamingAssistant()
        {
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                if (_lines[i].Kind == ChatLineKind.Assistant && _lines[i].Streaming)
                {
                    _lines.RemoveAt(i);
                    Touch();
                    return;
                }
            }
        }

        ChatLine StreamingAssistant()
        {
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                if (_lines[i].Kind == ChatLineKind.Assistant && _lines[i].Streaming)
                    return _lines[i];
            }

            return null;
        }

        static int Count(List<ChatMessage> messages)
        {
            var count = 0;
            for (var i = 0; i < messages.Count; i++)
                count += messages[i].Content != null ? messages[i].Content.Length : 0;
            return count;
        }

        void Touch()
        {
            Revision++;
        }
    }

    /// <summary>
    /// One send through <see cref="IChatProvider"/>. Tests pass a fake provider.
    /// </summary>
    public sealed class ChatExchange
    {
        readonly ChatConversation _conversation;

        public ChatExchange(ChatConversation conversation)
        {
            _conversation = conversation ?? new ChatConversation();
        }

        public ChatConversation Conversation => _conversation;

        public Action<bool> Talking;

        public Action<string> SpokenDelta;

        /// <summary>JUH-108: every raw piece from the provider, cue tags included, before filtering. The new chat uses it to place cues on their own line.</summary>
        public Action<string> RawDelta;

        /// <summary>The cue parsed from the last successful reply.</summary>
        public AnimationCue LastCue { get; private set; }

        /// <summary>The tag as the model wrote it in the last reply, for the log. Empty when it wrote none.</summary>
        public string LastCueTag { get; private set; }

        /// <summary>How the last reply's tag was read: none, parsed, or not readable.</summary>
        public CueParseStatus LastCueStatus { get; private set; }

        /// <summary>
        /// Goes up by one each time a reply finishes and <see cref="LastCue"/> is read from it.
        /// Lets a listener act on each reply's cue exactly once.
        /// </summary>
        public int CueSerial { get; private set; }

        /// <summary>
        /// Added to the newest user message in each request, never to the transcript, the
        /// display or memory. Empty sends the message as typed.
        /// </summary>
        public string TurnReminder;

        CueStreamFilter _filter;
        int _generation;
        bool _sawPiece;

        public Task<ChatResult> Run(IChatProvider provider, string userText, string systemPrompt, float temperature, int maxTokens, CancellationToken token)
        {
            return Run(provider, userText, systemPrompt, temperature, maxTokens, token, null);
        }

        public Task<ChatResult> Run(IChatProvider provider, string userText, string systemPrompt, float temperature, int maxTokens, CancellationToken token, ChatTurnOptions options)
        {
            if (!_conversation.TryBeginSend(userText, systemPrompt, temperature, maxTokens, out var request, options))
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, "Type a message first."));

            if (token.IsCancellationRequested)
            {
                WipeImage(request);
                var cancelled = ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
                _conversation.Complete(cancelled);
                return Task.FromResult(cancelled);
            }

            if (provider == null)
            {
                WipeImage(request);
                var missing = ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable("the chat provider"));
                _conversation.Complete(missing);
                return Task.FromResult(missing);
            }

            AddTurnReminder(request, TurnReminder);
            var generation = _conversation.Generation;
            _generation = generation;
            _filter = new CueStreamFilter();
            _sawPiece = false;
            LastCue = AnimationCueParser.NeutralCue;
            LastCueTag = string.Empty;
            LastCueStatus = CueParseStatus.None;
            if (Talking != null)
                Talking(true);

            Task<ChatResult> pending;
            try
            {
                pending = provider.StreamChat(request, piece =>
                {
                    if (!string.IsNullOrEmpty(piece))
                        _sawPiece = true;
                    Raw(piece);

                    var shown = _filter.Push(piece);
                    if (shown.Length == 0)
                        return;

                    _conversation.PushDelta(generation, shown);
                    Speak(shown);
                }, token);
            }
            catch (Exception)
            {
                return Task.FromResult(Apply(null));
            }

            if (pending == null || pending.IsCompleted)
                return Task.FromResult(Apply(pending));

            return Finish(pending);
        }

        async Task<ChatResult> Finish(Task<ChatResult> pending)
        {
            try
            {
                ChatResult result;
                try
                {
                    result = await pending;
                }
                catch (OperationCanceledException)
                {
                    result = ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
                }
                catch (Exception)
                {
                    result = ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable("the chat provider"));
                }

                return Settle(result);
            }
            finally
            {
                if (Talking != null)
                    Talking(false);
            }
        }

        ChatResult Apply(Task<ChatResult> pending)
        {
            try
            {
                return Settle(Read(pending));
            }
            finally
            {
                if (Talking != null)
                    Talking(false);
            }
        }

        /// <summary>
        /// Releases the held-back tail, seals the visible text, and hands the cue to the
        /// director. The reply stored in the transcript is the text without the tag.
        /// A failed reply still releases a held suffix that was not a tag, and does not
        /// adopt a cue.
        /// </summary>
        ChatResult Settle(ChatResult result)
        {
            if (_filter != null)
            {
                if (result.Ok && !_sawPiece && !string.IsNullOrEmpty(result.Text))
                {
                    Raw(result.Text);
                    _filter.Push(result.Text);
                }

                var tail = _filter.Flush();
                if (tail.Length > 0)
                    _conversation.PushDelta(_generation, tail);

                if (result.Ok)
                {
                    if (!_sawPiece)
                    {
                        if (_filter.Text.Length > 0)
                            Speak(_filter.Text);
                    }
                    else if (tail.Length > 0)
                    {
                        Speak(tail);
                    }

                    LastCue = _filter.Cue;
                    LastCueTag = _filter.RawTag;
                    LastCueStatus = _filter.Status;
                    CueSerial++;
                    _conversation.ReplaceStreamingAssistant(_generation, _filter.Text);
                    result = ChatResult.Success(_filter.Text);
                }
            }

            _conversation.Complete(result);
            return result;
        }

        /// <summary>Appends <paramref name="reminder"/> to the last user message of the request only.</summary>
        public static void AddTurnReminder(ChatRequest request, string reminder)
        {
            if (request == null || request.Messages == null || string.IsNullOrWhiteSpace(reminder))
                return;

            for (var i = request.Messages.Count - 1; i >= 0; i--)
            {
                var message = request.Messages[i];
                if (message.Role != ChatRole.User)
                    continue;
                request.Messages[i] = new ChatMessage(ChatRole.User, message.Content + "\n\n" + reminder.Trim());
                return;
            }
        }

        void Raw(string piece)
        {
            if (RawDelta == null || string.IsNullOrEmpty(piece))
                return;

            try
            {
                RawDelta(piece);
            }
            catch (Exception)
            {
            }
        }

        void Speak(string piece)
        {
            if (SpokenDelta == null)
                return;

            try
            {
                SpokenDelta(piece);
            }
            catch (Exception)
            {
            }
        }

        static void WipeImage(ChatRequest request)
        {
            if (request == null || request.Image == null)
                return;
            request.Image.Wipe();
            request.Image = null;
        }

        static ChatResult Read(Task<ChatResult> pending)
        {
            if (pending == null || pending.IsFaulted)
            {
                var inner = pending != null && pending.Exception != null ? pending.Exception.GetBaseException() : null;
                if (inner is OperationCanceledException)
                    return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
                return ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable("the chat provider"));
            }

            if (pending.IsCanceled)
                return ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);

            return pending.GetAwaiter().GetResult();
        }
    }
}
