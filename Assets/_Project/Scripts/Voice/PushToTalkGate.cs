namespace AICompanion.Voice
{
    public enum PushToTalkPhase
    {
        Idle = 0,
        Recording = 1,
        Transcribing = 2
    }

    public enum PushToTalkSignal
    {
        None = 0,
        Started = 1,
        Released = 2
    }

    /// <summary>
    /// Hold-to-talk. Recording lasts only while the key or the mic button is held.
    /// A new hold is ignored until the current transcription finishes and the
    /// previous hold has been released.
    /// </summary>
    public sealed class PushToTalkGate
    {
        bool _armed = true;

        public PushToTalkPhase Phase { get; private set; }

        public PushToTalkSignal Tick(bool keyHeld, bool buttonHeld, bool keyBlocked)
        {
            var held = buttonHeld || (keyHeld && !keyBlocked);
            if (Phase == PushToTalkPhase.Transcribing)
                return PushToTalkSignal.None;

            if (Phase == PushToTalkPhase.Idle)
            {
                if (!_armed)
                {
                    if (!held)
                        _armed = true;
                    return PushToTalkSignal.None;
                }

                if (!held)
                    return PushToTalkSignal.None;

                Phase = PushToTalkPhase.Recording;
                _armed = false;
                return PushToTalkSignal.Started;
            }

            if (!held)
            {
                Phase = PushToTalkPhase.Transcribing;
                return PushToTalkSignal.Released;
            }

            return PushToTalkSignal.None;
        }

        public PushToTalkSignal ForceRelease()
        {
            if (Phase != PushToTalkPhase.Recording)
                return PushToTalkSignal.None;

            Phase = PushToTalkPhase.Transcribing;
            return PushToTalkSignal.Released;
        }

        public void FinishTranscription(bool stillHeld)
        {
            if (Phase == PushToTalkPhase.Transcribing)
                Phase = PushToTalkPhase.Idle;
            _armed = !stillHeld;
        }

        public void Abort()
        {
            Phase = PushToTalkPhase.Idle;
            _armed = false;
        }
    }
}
