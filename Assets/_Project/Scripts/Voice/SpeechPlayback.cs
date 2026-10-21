namespace AICompanion.Voice
{
    /// <summary>
    /// Clip queue. Talking stays on across the handoff to the next clip
    /// and turns off when the queue is empty or stopped.
    /// </summary>
    public sealed class SpeechPlayback
    {
        int _queued;
        bool _playing;

        public int QueuedCount => _queued;

        public bool Playing => _playing;

        public bool ShouldTalk => _playing;

        public void EnqueueClip()
        {
            _queued++;
        }

        public void Pump()
        {
            if (_playing || _queued <= 0)
                return;
            _queued--;
            _playing = true;
        }

        public void ClipFinished()
        {
            _playing = false;
            Pump();
        }

        public void Stop()
        {
            _queued = 0;
            _playing = false;
        }
    }
}
