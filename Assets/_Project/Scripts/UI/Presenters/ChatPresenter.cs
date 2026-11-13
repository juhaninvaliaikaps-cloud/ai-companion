using System;
using AICompanion.Core;

namespace AICompanion.UI.Presenters
{
    /// <summary>What the chat's main button (or Enter) did.</summary>
    public enum ChatActionResult
    {
        /// <summary>Nothing happened: empty text, a reply already running, or a look in progress.</summary>
        Rejected = 0,
        /// <summary>A new message was sent.</summary>
        Sent = 1,
        /// <summary>The streaming reply was cancelled.</summary>
        CancelledReply = 2,
        /// <summary>Her speech was stopped.</summary>
        StoppedSpeech = 3,
    }

    /// <summary>What the "What do you see?" button did.</summary>
    public enum ChatLookResult
    {
        /// <summary>Window capture is not part of this session.</summary>
        Missing = 0,
        Started = 1,
        Stopped = 2,
    }

    /// <summary>
    /// JUH-104 PR B: the chat panel's button behaviour and labels, without any uGUI. The old
    /// <see cref="ChatPanel"/> and the new chat view both call this, so Send/Stop, Mute, the
    /// screen look and Escape behave the same in both.
    ///
    /// The send itself (provider routing, private mode, speech origin, cues, memory) stays in
    /// ChatPanel: open PR #63 rewrites those lines, so this presenter calls ChatPanel's
    /// existing entry points instead of moving them (docs/ui-overhaul/DECISIONS.md, B1).
    /// </summary>
    public sealed class ChatPresenter
    {
        public const string SendLabel = "Send";
        public const string StopLabel = "Stop";
        public const string MuteOffLabel = "Mute";
        public const string MuteOnLabel = "Muted";
        public const string LookLabelIdle = "What do you see?";
        public const string LookLabelBusy = "Stop look";
        public const string MicIdleLabel = "Mic";
        public const string MicRecordingLabel = "Rec";
        public const string MicWaitingLabel = "...";
        public const string ScreenLookMissing = "Window capture is not available in this session.";

        readonly Func<bool> _replyBusy;
        readonly Func<ISpeechOutput> _speech;
        readonly Func<IScreenLook> _look;
        readonly Func<string, bool> _send;
        readonly Action _cancelReply;

        /// <param name="replyBusy">True while a reply streams.</param>
        /// <param name="speech">Her voice, or null when there is none.</param>
        /// <param name="look">The screen look, or null when this session has none.</param>
        /// <param name="send">Sends one user line; false when it was not sent.</param>
        /// <param name="cancelReply">Stops speech and cancels the streaming reply.</param>
        public ChatPresenter(Func<bool> replyBusy, Func<ISpeechOutput> speech, Func<IScreenLook> look, Func<string, bool> send, Action cancelReply)
        {
            _replyBusy = replyBusy ?? (() => false);
            _speech = speech ?? (() => null);
            _look = look ?? (() => null);
            _send = send ?? (_ => false);
            _cancelReply = cancelReply ?? (() => { });
        }

        public bool ReplyBusy => _replyBusy();

        public bool Speaking
        {
            get
            {
                var speech = _speech();
                return speech != null && speech.IsSpeaking;
            }
        }

        public bool Muted
        {
            get
            {
                var speech = _speech();
                return speech != null && speech.IsMuted;
            }
        }

        public bool LookBusy
        {
            get
            {
                var look = _look();
                return look != null && look.IsBusy;
            }
        }

        /// <summary>The look button is usable (a look can start, or a running one can be stopped).</summary>
        public bool LookInteractable
        {
            get
            {
                var look = _look();
                return look == null || look.LookButtonInteractable;
            }
        }

        public string ActionLabel => ActionLabelFor(ReplyBusy, Speaking);

        public string MuteLabel => MuteLabelFor(Muted);

        public string LookLabel => LookLabelFor(LookBusy);

        /// <summary>
        /// The main button: cancel a streaming reply, else stop her speech, else send the draft.
        /// Same order as ChatPanel.OnActionClick on main.
        /// </summary>
        public ChatActionResult PrimaryAction(string draft)
        {
            if (ReplyBusy)
            {
                _cancelReply();
                return ChatActionResult.CancelledReply;
            }

            var speech = _speech();
            if (speech != null && speech.IsSpeaking)
            {
                speech.Cancel();
                return ChatActionResult.StoppedSpeech;
            }

            return Send(draft);
        }

        /// <summary>Enter in the message field: send only, never stop.</summary>
        public ChatActionResult Send(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ChatActionResult.Rejected;
            return _send(text) ? ChatActionResult.Sent : ChatActionResult.Rejected;
        }

        /// <summary>Flips mute. Returns the new state; false when there is no voice.</summary>
        public bool ToggleMute()
        {
            var speech = _speech();
            if (speech == null)
                return false;
            speech.SetMuted(!speech.IsMuted);
            return speech.IsMuted;
        }

        /// <summary>The "What do you see?" button: start a look, or stop the running one.</summary>
        public ChatLookResult Look()
        {
            var look = _look();
            if (look == null)
                return ChatLookResult.Missing;
            if (look.IsBusy)
            {
                look.Stop();
                return ChatLookResult.Stopped;
            }

            look.RequestLook();
            return ChatLookResult.Started;
        }

        /// <summary>
        /// Escape in the chat: stop a running look, then cancel the reply when one streams,
        /// otherwise stop her speech. Same as ChatPanel.Update on main.
        /// </summary>
        public void Escape()
        {
            var look = _look();
            if (look != null && look.IsBusy)
                look.Stop();
            if (ReplyBusy)
            {
                _cancelReply();
                return;
            }

            var speech = _speech();
            if (speech != null)
                speech.Cancel();
        }

        public static string ActionLabelFor(bool replyBusy, bool speaking)
        {
            return replyBusy || speaking ? StopLabel : SendLabel;
        }

        public static string MuteLabelFor(bool muted)
        {
            return muted ? MuteOnLabel : MuteOffLabel;
        }

        public static string LookLabelFor(bool lookBusy)
        {
            return lookBusy ? LookLabelBusy : LookLabelIdle;
        }

        public static string MicLabelFor(bool recording, bool transcribing)
        {
            return recording ? MicRecordingLabel : transcribing ? MicWaitingLabel : MicIdleLabel;
        }
    }
}
