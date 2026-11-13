using System;
using System.Collections.Generic;
using System.Text;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.NewUI.VisualNovel
{
    /// <summary>
    /// The player's voice: one line at a time through the real voice (VoiceSpeaker), always with
    /// the speech origin the chat panel chose for this reply, so private mode, the intimate gate
    /// and the local-voice route apply exactly as before. Prefetch uses the voice's playback hold.
    /// </summary>
    public sealed class VnVoice : IVnVoice
    {
        readonly Func<ISpeechOutput> _real;
        readonly Func<bool> _speechEnabled;

        public VnVoice(Func<ISpeechOutput> real, Func<bool> speechEnabled)
        {
            _real = real ?? (() => null);
            _speechEnabled = speechEnabled ?? (() => true);
        }

        public SpeechOrigin Origin;
        public bool HasOrigin;

        /// <summary>A reply that was not spoken on main either (screen remarks without speech): shown only.</summary>
        public bool SilentReply;

        public bool CanSpeak
        {
            get
            {
                var real = _real();
                return !SilentReply && real != null && !real.IsMuted && _speechEnabled();
            }
        }

        public bool IsSpeaking
        {
            get
            {
                var real = _real();
                return real != null && real.IsSpeaking;
            }
        }

        public bool SupportsPrefetch => _real() is ISpeechPlaybackHold;

        public void Speak(string text)
        {
            var real = _real();
            if (real == null)
                return;
            Begin(real);
            real.Append(text);
            real.CompleteUtterance();
        }

        public void Prefetch(string text)
        {
            var real = _real();
            var hold = real as ISpeechPlaybackHold;
            if (real == null || hold == null)
                return;
            Begin(real);
            hold.SetPlaybackHeld(true);
            real.Append(text);
            real.CompleteUtterance();
        }

        public void PlayPrefetched()
        {
            var hold = _real() as ISpeechPlaybackHold;
            if (hold != null)
                hold.SetPlaybackHeld(false);
        }

        public void Stop()
        {
            var real = _real();
            if (real != null)
                real.Cancel();
        }

        void Begin(ISpeechOutput real)
        {
            if (HasOrigin)
                real.BeginUtterance(Origin);
            else
                real.BeginUtterance();
        }
    }

    /// <summary>Fires a line's cue: the animation director first, then her mood (as ChatPanel.ApplyCue on main).</summary>
    public sealed class VnCueSink : IVnCueSink
    {
        readonly Action<AnimationCue> _play;

        public VnCueSink(Action<AnimationCue> play)
        {
            _play = play;
        }

        public int Fired { get; private set; }

        public void Fire(AnimationCue cue)
        {
            Fired++;
            if (_play != null)
                _play(cue);
            CompanionMoodInputs.ReportCue(cue);
            Debug.Log("Chat line cue: " + EmotionNames.FormatFieldLog(cue.EmotionField));
        }
    }

    /// <summary>
    /// Stands in for the voice on the chat panel while the new chat is on. The panel keeps
    /// deciding what is said and where it may go (BeginUtterance with its origin); this sink
    /// turns the reply into visual-novel lines and lets <see cref="VnPlayer"/> speak them one by one.
    /// </summary>
    public sealed class VnSpeechSink : ISpeechOutput
    {
        readonly Func<ISpeechOutput> _real;
        readonly Func<string> _lastAssistant;
        readonly StringBuilder _raw = new StringBuilder();
        readonly StringBuilder _visible = new StringBuilder();
        bool _active;
        bool _final;

        public VnSpeechSink(VnPlayer player, VnVoice voice, Func<ISpeechOutput> real, Func<string> lastAssistant)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Voice = voice ?? throw new ArgumentNullException(nameof(voice));
            _real = real ?? (() => null);
            _lastAssistant = lastAssistant ?? (() => string.Empty);
        }

        public VnPlayer Player { get; }
        public VnVoice Voice { get; }

        /// <summary>Goes up by one for every reply, so views know when to redraw.</summary>
        public int ReplySerial { get; private set; }

        /// <summary>True from BeginUtterance until the reply is complete or cancelled.</summary>
        public bool Streaming => _active && !_final;

        public bool IsMuted
        {
            get
            {
                var real = _real();
                return real != null && real.IsMuted;
            }
        }

        public bool IsSpeaking
        {
            get
            {
                var real = _real();
                return Player.Active || (real != null && real.IsSpeaking);
            }
        }

        public void SetMuted(bool muted)
        {
            var real = _real();
            if (real != null)
                real.SetMuted(muted);
        }

        public void BeginUtterance()
        {
            Voice.HasOrigin = false;
            StartReply();
        }

        public void BeginUtterance(SpeechOrigin origin)
        {
            Voice.Origin = origin;
            Voice.HasOrigin = true;
            StartReply();
        }

        public void Append(string text)
        {
            if (!_active || _final || string.IsNullOrEmpty(text))
                return;
            _visible.Append(text);
            if (_raw.Length == 0)
                Reparse();
        }

        /// <summary>Raw provider piece (cue tags included). Only pieces of the reply in progress count.</summary>
        public void OnRawPiece(string piece)
        {
            if (!_active || _final || string.IsNullOrEmpty(piece))
                return;
            _raw.Append(piece);
            Reparse();
        }

        public void CompleteUtterance()
        {
            if (!_active || _final)
                return;
            _final = true;
            if (_raw.Length == 0 && _visible.Length == 0)
            {
                // Screen remarks are added to the transcript without passing through speech; on
                // main they are not spoken, so here they are shown but not voiced.
                var last = _lastAssistant();
                if (!string.IsNullOrEmpty(last))
                {
                    _visible.Append(last);
                    Voice.SilentReply = true;
                }
            }

            Reparse();
        }

        public void Cancel()
        {
            Player.Stop();
            _final = true;
        }

        void StartReply()
        {
            _raw.Length = 0;
            _visible.Length = 0;
            _active = true;
            _final = false;
            Voice.SilentReply = false;
            ReplySerial++;
            Player.Begin();
        }

        void Reparse()
        {
            var source = _raw.Length > 0 ? _raw.ToString() : _visible.ToString();
            Player.Update(VnScriptParser.Parse(source, _final), _final);
        }
    }

    /// <summary>Rich text for the dialogue box: revealed lines, narration in italics, the current line highlighted.</summary>
    public static class VnRender
    {
        /// <summary>
        /// Index of line <paramref name="index"/> first character in the rendered dialogue text
        /// (TMP counts characters, not rich-text tags; lines are joined by one line break).
        /// </summary>
        public static int LineStart(IReadOnlyList<VnSegment> segments, int index)
        {
            if (segments == null || index <= 0)
                return 0;
            var start = 0;
            for (var i = 0; i < index && i < segments.Count; i++)
                start += segments[i].Text.Length + 1;
            return start;
        }

        public static string Dialogue(IReadOnlyList<VnSegment> segments, int revealed, int current, HappiTheme theme)
        {
            if (segments == null || revealed <= 0)
                return string.Empty;
            var sb = new StringBuilder();
            var count = Math.Min(revealed, segments.Count);
            var narration = HappiUiKit.ColorTag(theme.Narration);
            var mark = HappiUiKit.ColorTag(theme.HighlightBackground);
            for (var i = 0; i < count; i++)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                var segment = segments[i];
                var highlighted = i == current;
                if (highlighted)
                    sb.Append("<mark=").Append(mark).Append('>');
                if (segment.Kind == VnSegmentKind.Narration)
                    sb.Append("<i><color=").Append(narration).Append('>').Append(HappiUiKit.Literal(segment.Text)).Append("</color></i>");
                else
                    sb.Append(HappiUiKit.Literal(segment.Text));
                if (highlighted)
                    sb.Append("</mark>");
            }

            return sb.ToString();
        }
    }
}
