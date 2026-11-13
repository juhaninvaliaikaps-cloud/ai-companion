using AICompanion.Core;

namespace AICompanion.Voice
{
    /// <summary>
    /// JUH-108 prefetch for the new chat: a held line is synthesized and queued but waits until
    /// the hold is released, so "Next" plays it immediately. Routing, privacy, mute and pause
    /// are unchanged: the line still went through <see cref="BeginUtterance(SpeechOrigin)"/>.
    /// <see cref="Cancel"/>, mute, pause and a new utterance clear the hold (SilencePlayback).
    /// Nothing holds playback while the new UI is off.
    /// </summary>
    public sealed partial class VoiceSpeaker
    {
        bool _held;

        public bool PlaybackHeld => _held;

        public void SetPlaybackHeld(bool held)
        {
            if (_held == held)
                return;

            _held = held;
            if (held || _playback.Playing || _clips.Count == 0)
                return;

            // Released with audio already waiting: start it now.
            _playback.Pump();
            PlayHead();
            ApplyTalking();
        }
    }
}
