using System;
using System.Collections.Generic;
using AICompanion.Core;

namespace AICompanion.NewUI.VisualNovel
{
    public enum VnMode
    {
        /// <summary>Plays every line in order by itself.</summary>
        Auto = 0,
        /// <summary>Waits on each line; Next, a click or Space advances exactly one line.</summary>
        Step = 1,
    }

    /// <summary>Her voice as the player sees it. The real one goes through VoiceSpeaker with the reply's privacy origin.</summary>
    public interface IVnVoice
    {
        /// <summary>False while muted, paused or with speech off: lines are then read, not spoken.</summary>
        bool CanSpeak { get; }

        /// <summary>True while a line is being synthesized, is queued, or is playing.</summary>
        bool IsSpeaking { get; }

        /// <summary>True when <see cref="Prefetch"/> can synthesize a line without playing it yet.</summary>
        bool SupportsPrefetch { get; }

        /// <summary>Synthesizes and plays one line now.</summary>
        void Speak(string text);

        /// <summary>Synthesizes one line and holds it until <see cref="PlayPrefetched"/>.</summary>
        void Prefetch(string text);

        /// <summary>Plays the line held by <see cref="Prefetch"/>.</summary>
        void PlayPrefetched();

        /// <summary>Stops speech and drops a held line.</summary>
        void Stop();
    }

    /// <summary>Where a line's cues go: the animation director and her mood (JUH-103).</summary>
    public interface IVnCueSink
    {
        void Fire(AnimationCue cue);
    }

    /// <summary>Reading-time pacing for lines that are not spoken.</summary>
    public struct VnTiming
    {
        public float NarrationSecondsPerChar;
        public float SilentSpeechSecondsPerChar;
        public float MinSeconds;
        public float MaxSeconds;
        /// <summary>A spoken line that never starts within this time is read instead (speech blocked).</summary>
        public float VoiceStartGrace;

        public static VnTiming From(HappiTheme theme)
        {
            return new VnTiming
            {
                NarrationSecondsPerChar = theme.NarrationSecondsPerChar,
                SilentSpeechSecondsPerChar = theme.SilentSpeechSecondsPerChar,
                MinSeconds = theme.NarrationMinSeconds,
                MaxSeconds = theme.NarrationMaxSeconds,
                VoiceStartGrace = 1.5f
            };
        }

        public float ReadSeconds(VnSegment segment)
        {
            var perChar = segment.Kind == VnSegmentKind.Narration ? NarrationSecondsPerChar : SilentSpeechSecondsPerChar;
            var seconds = segment.Text.Length * perChar;
            if (seconds < MinSeconds)
                seconds = MinSeconds;
            if (seconds > MaxSeconds)
                seconds = MaxSeconds;
            return seconds;
        }
    }

    /// <summary>
    /// JUH-108 sequenced playback of one reply. Lines appear one at a time with the current one
    /// highlighted. Speech lines are voiced; narration only with <see cref="VoiceNarration"/>.
    /// Each line's cues fire when that line starts, not when the reply starts. While the user
    /// reads, the next spoken line is synthesized ahead (prefetch) so it plays at once.
    /// Pure logic: the view calls <see cref="Tick"/> every frame.
    /// </summary>
    public sealed class VnPlayer
    {
        enum LineState
        {
            None,
            WaitingForVoice,
            Reading,
            Done,
        }

        readonly IVnVoice _voice;
        readonly IVnCueSink _cues;
        readonly List<VnSegment> _segments = new List<VnSegment>();
        readonly List<bool> _fired = new List<bool>();
        VnTiming _timing;
        LineState _state;
        int _current = -1;
        int _prefetched = -1;
        float _elapsed;
        float _remaining;
        bool _voiceSeen;
        bool _final;
        bool _showAll;
        bool _pendingAdvance;
        bool _anyCueFired;
        bool _endHandled;
        IReadOnlyList<AnimationCue> _trailing = Array.Empty<AnimationCue>();

        public VnPlayer(IVnVoice voice, IVnCueSink cues, VnTiming timing)
        {
            _voice = voice ?? throw new ArgumentNullException(nameof(voice));
            _cues = cues ?? throw new ArgumentNullException(nameof(cues));
            _timing = timing;
        }

        public VnMode Mode { get; set; } = VnMode.Auto;

        /// <summary>JUH-108 "Voice narration" setting. Off: narration is shown but not voiced.</summary>
        public bool VoiceNarration { get; set; }

        public IReadOnlyList<VnSegment> Segments => _segments;

        /// <summary>The highlighted line, or -1 before the first one.</summary>
        public int Current => _current;

        /// <summary>Lines 0 .. Revealed-1 are shown.</summary>
        public int Revealed => _showAll ? _segments.Count : _current + 1;

        /// <summary>True after <see cref="ShowAll"/>, or when the last line of a finished reply is done.</summary>
        public bool Finished => _final && (_showAll || (_current == _segments.Count - 1 && _state == LineState.Done) || _segments.Count == 0);

        /// <summary>True while a reply is still being shown or spoken.</summary>
        public bool Active => (_current >= 0 || _segments.Count > 0) && !Finished;

        /// <summary>The current line is done and Step mode waits for Next.</summary>
        public bool WaitingForNext => Mode == VnMode.Step && _state == LineState.Done && !Finished;

        public bool HasNext => _current + 1 < _segments.Count;

        /// <summary>The current line is being spoken (or synthesized) right now, not read or finished.</summary>
        public bool CurrentVoicing => _current >= 0 && !_showAll && _state == LineState.WaitingForVoice;

        public int PrefetchedIndex => _prefetched;

        public void SetTiming(VnTiming timing)
        {
            _timing = timing;
        }

        /// <summary>A new reply begins. The previous one is dropped.</summary>
        public void Begin()
        {
            StopVoice();
            _segments.Clear();
            _fired.Clear();
            _current = -1;
            _state = LineState.None;
            _final = false;
            _showAll = false;
            _pendingAdvance = false;
            _anyCueFired = false;
            _endHandled = false;
            _trailing = Array.Empty<AnimationCue>();
        }

        /// <summary>The reply so far (parsed again as it streams). Closed lines never change.</summary>
        public void Update(VnScript script, bool final)
        {
            if (script == null)
                script = VnScript.Empty;

            var segments = script.Segments;
            for (var i = _segments.Count; i < segments.Count; i++)
            {
                _segments.Add(segments[i]);
                _fired.Add(false);
            }

            if (final)
            {
                _final = true;
                _trailing = script.TrailingCues;
            }

            if (_showAll)
            {
                RevealAllCues();
                HandleEnd();
                return;
            }

            if (_current < 0 && _segments.Count > 0)
                Start(0);
            else if (_pendingAdvance && HasNext)
            {
                _pendingAdvance = false;
                Start(_current + 1);
            }
            else if (_state == LineState.Done && Mode == VnMode.Auto && HasNext)
                Start(_current + 1);

            HandleEnd();
        }

        public void Tick(float deltaTime)
        {
            if (_current < 0 || _showAll)
                return;

            switch (_state)
            {
                case LineState.WaitingForVoice:
                    _elapsed += deltaTime;
                    if (_voice.IsSpeaking)
                        _voiceSeen = true;
                    if (_voiceSeen && !_voice.IsSpeaking)
                        LineDone();
                    else if (!_voiceSeen && _elapsed >= _timing.VoiceStartGrace)
                    {
                        // Speech never started (paused, suppressed, blocked route): read it instead.
                        _state = LineState.Reading;
                        _remaining = Math.Max(0f, _timing.ReadSeconds(_segments[_current]) - _elapsed);
                    }

                    break;
                case LineState.Reading:
                    _remaining -= deltaTime;
                    TryPrefetch();
                    if (_remaining <= 0f)
                        LineDone();
                    break;
                case LineState.Done:
                    TryPrefetch();
                    break;
            }
        }

        /// <summary>Next button, a click or Space: advances exactly one line.</summary>
        public void Next()
        {
            if (_showAll)
                return;
            if (!HasNext)
            {
                if (!_final)
                    _pendingAdvance = true;
                return;
            }

            Start(_current + 1);
        }

        /// <summary>Skips the current line in either mode.</summary>
        public void Skip()
        {
            Next();
        }

        /// <summary>Shows every line now and stops the voice. The face takes the latest cue.</summary>
        public void ShowAll()
        {
            if (_showAll)
                return;
            _showAll = true;
            StopVoice();
            _state = LineState.Done;
            RevealAllCues();
            HandleEnd();
        }

        /// <summary>Esc / Stop / a new message: stops the voice and the sequence.</summary>
        public void Stop()
        {
            StopVoice();
            _showAll = true;
            _state = LineState.Done;
        }

        public bool IsVoiced(int index)
        {
            if (index < 0 || index >= _segments.Count)
                return false;
            return _segments[index].Kind == VnSegmentKind.Speech || VoiceNarration;
        }

        void Start(int index)
        {
            if (index < 0 || index >= _segments.Count)
                return;

            // Leaving a line that is still speaking stops it, unless the held line is a later one
            // that only needs to wait.
            if (_state == LineState.WaitingForVoice || (_prefetched >= 0 && _prefetched < index))
                StopVoice();

            _current = index;
            _elapsed = 0f;
            _voiceSeen = false;
            FireCues(index);

            var segment = _segments[index];
            if (IsVoiced(index) && _voice.CanSpeak)
            {
                if (_prefetched == index)
                {
                    _prefetched = -1;
                    _voice.PlayPrefetched();
                }
                else
                {
                    if (_prefetched >= 0)
                        StopVoice();
                    _voice.Speak(segment.Text);
                }

                _state = LineState.WaitingForVoice;
                _voiceSeen = _voice.IsSpeaking;
            }
            else
            {
                _state = LineState.Reading;
                _remaining = _timing.ReadSeconds(segment);
            }
        }

        void LineDone()
        {
            _state = LineState.Done;
            if (Mode == VnMode.Auto && HasNext)
            {
                Start(_current + 1);
                return;
            }

            if (_pendingAdvance && HasNext)
            {
                _pendingAdvance = false;
                Start(_current + 1);
                return;
            }

            TryPrefetch();
            HandleEnd();
        }

        /// <summary>
        /// While the reader is on a line that is not using the voice, synthesize the next spoken
        /// line so Next plays it at once.
        /// </summary>
        void TryPrefetch()
        {
            if (_prefetched >= 0 || !_voice.SupportsPrefetch || !_voice.CanSpeak || _voice.IsSpeaking)
                return;
            if (_state == LineState.WaitingForVoice)
                return;

            for (var i = _current + 1; i < _segments.Count; i++)
            {
                if (!IsVoiced(i))
                    continue;
                _prefetched = i;
                _voice.Prefetch(_segments[i].Text);
                return;
            }
        }

        void FireCues(int index)
        {
            if (index < 0 || index >= _fired.Count || _fired[index])
                return;
            _fired[index] = true;
            var cues = _segments[index].Cues;
            for (var i = 0; i < cues.Count; i++)
            {
                _anyCueFired = true;
                _cues.Fire(cues[i]);
            }
        }

        /// <summary>Show all: the face jumps to the last cue not yet shown instead of replaying every one.</summary>
        void RevealAllCues()
        {
            AnimationCue last = default;
            var found = false;
            for (var i = 0; i < _segments.Count; i++)
            {
                if (_fired[i])
                    continue;
                _fired[i] = true;
                var cues = _segments[i].Cues;
                if (cues.Count > 0)
                {
                    last = cues[cues.Count - 1];
                    found = true;
                }
            }

            if (found)
            {
                _anyCueFired = true;
                _cues.Fire(last);
            }
        }

        void HandleEnd()
        {
            if (!_final || _endHandled)
                return;
            if (!_showAll && !(_current == _segments.Count - 1 && _state == LineState.Done) && _segments.Count > 0)
                return;

            _endHandled = true;
            for (var i = 0; i < _trailing.Count; i++)
            {
                _anyCueFired = true;
                _cues.Fire(_trailing[i]);
            }

            // Same as the old chat: a reply without a cue tag returns her to neutral.
            if (!_anyCueFired)
                _cues.Fire(AnimationCueParser.NeutralCue);
        }

        void StopVoice()
        {
            _prefetched = -1;
            _voice.Stop();
        }
    }
}
