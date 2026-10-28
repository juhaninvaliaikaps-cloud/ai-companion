namespace AICompanion.Settings
{
    /// <summary>
    /// Discrete portrait blink. Open, half, closed, half, open.
    /// Each frame holds 40–60 ms. The next blink waits 2–6 s.
    /// About one in five blinks plays twice, with a short open between them.
    /// The JUH-24 scheduler is a continuous eyelid curve and is not on main, so this
    /// stays a small frame timer for the drawn portraits.
    /// A long frame that ends the wait still shows the first half-closed frame.
    /// </summary>
    public struct PortraitBlinkScheduler
    {
        public const float MinFrameSeconds = 0.04f;
        public const float MaxFrameSeconds = 0.06f;
        public const float MinGapSeconds = 2f;
        public const float MaxGapSeconds = 6f;
        public const float DoubleChance = 0.2f;

        float _wait;
        float _into;
        float _frameSeconds;
        int _index;
        int _count;
        bool _double;

        public PortraitLid Lid { get; private set; }

        public bool IsWaiting
        {
            get { return _index == 0; }
        }

        public bool IsDouble
        {
            get { return _double && _index > 0; }
        }

        public static float Gap(float unit)
        {
            return PortraitMath.Lerp(MinGapSeconds, MaxGapSeconds, unit);
        }

        public static float FrameHold(float unit)
        {
            return PortraitMath.Lerp(MinFrameSeconds, MaxFrameSeconds, unit);
        }

        public static bool RollsDouble(float unit)
        {
            return PortraitMath.Clamp01(unit) < DoubleChance;
        }

        public static PortraitBlinkScheduler Start(float gapUnit)
        {
            return new PortraitBlinkScheduler
            {
                _wait = Gap(gapUnit),
                Lid = PortraitLid.Open
            };
        }

        public PortraitBlinkScheduler Tick(float delta, float gapUnit, float frameUnit, float doubleUnit)
        {
            if (delta < 0f || float.IsNaN(delta))
                delta = 0f;

            if (_index == 0)
            {
                Lid = PortraitLid.Open;
                _wait -= delta;
                if (_wait > 0f)
                    return this;

                _double = RollsDouble(doubleUnit);
                _frameSeconds = FrameHold(frameUnit);
                _count = _double ? 7 : 3;
                _index = 1;
                _into = 0f;
                Lid = LidAt(1);
                return this;
            }

            _into += delta;
            while (_index > 0 && _into >= _frameSeconds)
            {
                _into -= _frameSeconds;
                _index++;
                if (_index > _count)
                {
                    _index = 0;
                    _double = false;
                    _into = 0f;
                    Lid = PortraitLid.Open;
                    _wait = Gap(gapUnit);
                    return this;
                }

                Lid = LidAt(_index);
            }

            return this;
        }

        PortraitLid LidAt(int index)
        {
            switch (index)
            {
                case 1:
                case 3:
                case 5:
                case 7:
                    return PortraitLid.Half;
                case 2:
                case 6:
                    return PortraitLid.Closed;
                default:
                    return PortraitLid.Open;
            }
        }
    }
}
