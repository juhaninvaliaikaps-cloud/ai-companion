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
        public bool IsBusy { get; private set; }
        public int Revision { get; private set; }

        /// <summary>Increments on each accepted send so a late chunk cannot join the next reply.</summary>
        public int Generation { get; private set; }

        public bool TryBeginSend(string userText, string systemPrompt, float temperature, int maxTokens, out ChatRequest request)
        {
            request = null;
            var text = (userText ?? string.Empty).Trim();
            if (IsBusy || text.Length == 0)
                return false;

            Generation++;
            _lines.Add(new ChatLine(ChatLineKind.User, text, false));
            request = BuildRequest(systemPrompt, temperature, maxTokens);
            _lines.Add(new ChatLine(ChatLineKind.Assistant, string.Empty, true));
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
                    SealAssistant(result.Text);
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

        public void AddSystem(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            _lines.Add(new ChatLine(ChatLineKind.System, text.Trim(), false));
            Touch();
        }

        public ChatRequest BuildRequest(string systemPrompt, float temperature, int maxTokens)
        {
            var messages = new List<ChatMessage>();
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (line.Kind == ChatLineKind.System || string.IsNullOrEmpty(line.Text))
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
                Messages = messages
            };
            return request;
        }

        public string FormatTranscript()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (line.Kind == ChatLineKind.Assistant && line.Streaming && line.Text.Length == 0)
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

        /// <summary>The cue parsed from the last successful reply.</summary>
        public AnimationCue LastCue { get; private set; }

        CueStreamFilter _filter;
        int _generation;
        bool _sawPiece;

        public Task<ChatResult> Run(IChatProvider provider, string userText, string systemPrompt, float temperature, int maxTokens, CancellationToken token)
        {
            if (!_conversation.TryBeginSend(userText, systemPrompt, temperature, maxTokens, out var request))
                return Task.FromResult(ChatResult.Fail(ChatErrorKind.Malformed, "Type a message first."));

            if (token.IsCancellationRequested)
            {
                var cancelled = ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled);
                _conversation.Complete(cancelled);
                return Task.FromResult(cancelled);
            }

            if (provider == null)
            {
                var missing = ChatResult.Fail(ChatErrorKind.Unreachable, ChatErrors.Unreachable("the chat provider"));
                _conversation.Complete(missing);
                return Task.FromResult(missing);
            }

            var generation = _conversation.Generation;
            _generation = generation;
            _filter = new CueStreamFilter();
            _sawPiece = false;
            LastCue = AnimationCueParser.NeutralCue;
            if (Talking != null)
                Talking(true);

            Task<ChatResult> pending;
            try
            {
                pending = provider.StreamChat(request, piece =>
                {
                    if (!string.IsNullOrEmpty(piece))
                        _sawPiece = true;

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
                    _filter.Push(result.Text);

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
                    _conversation.ReplaceStreamingAssistant(_generation, _filter.Text);
                    result = ChatResult.Success(_filter.Text);
                }
            }

            _conversation.Complete(result);
            return result;
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
