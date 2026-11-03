namespace AICompanion.Settings
{
    /// <summary>
    /// Picks the open frame or the talk frame while she is speaking.
    /// Lip-sync mouth level drives it when that level actually moves.
    /// Otherwise the two frames take turns on a short random hold.
    /// </summary>
    public struct PortraitTalkMotion
    {
        public const float AmplitudeThreshold = 0.2f;
        public const float MinHoldSeconds = 0.09f;
        public const float MaxHoldSeconds = 0.16f;
        public const float FallbackMinSeconds = 0.11f;
        public const float FallbackMaxSeconds = 0.2f;
        public const float AmplitudeWaitSeconds = 0.25f;

        float _hold;
        float _silentFor;
        bool _followAmplitude;

        public bool ShowTalk { get; private set; }

        public bool FollowsAmplitude
        {
            get { return _followAmplitude; }
        }

        public PortraitTalkMotion Tick(float delta, bool speaking, bool hasTalkFrame, bool hasAmplitude, float amplitude, float unit)
        {
            if (delta < 0f || float.IsNaN(delta))
                delta = 0f;
            if (float.IsNaN(amplitude) || amplitude < 0f)
                amplitude = 0f;

            if (!speaking || !hasTalkFrame)
            {
                ShowTalk = false;
                _hold = 0f;
                _silentFor = 0f;
                _followAmplitude = false;
                return this;
            }

            if (hasAmplitude && amplitude >= AmplitudeThreshold)
                _followAmplitude = true;

            if (!_followAmplitude && hasAmplitude && _silentFor < AmplitudeWaitSeconds)
            {
                _silentFor += delta;
                ShowTalk = false;
                if (_silentFor < AmplitudeWaitSeconds)
                    return this;
            }

            if (_hold > 0f)
            {
                _hold -= delta;
                if (_hold > 0f)
                    return this;
            }

            if (_followAmplitude)
            {
                ShowTalk = amplitude >= AmplitudeThreshold;
                _hold = PortraitMath.Lerp(MinHoldSeconds, MaxHoldSeconds, unit);
                return this;
            }

            ShowTalk = !ShowTalk;
            _hold = PortraitMath.Lerp(FallbackMinSeconds, FallbackMaxSeconds, unit);
            return this;
        }
    }
}
