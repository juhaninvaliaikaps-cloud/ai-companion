namespace AICompanion.ScreenLook
{
    /// <summary>
    /// Future sampling bounds from JUH-44. This slice does not start a timer.
    /// Focus and mute pauses are not implemented. The ~2% FPS target is not measured here.
    /// </summary>
    public static class ScreenLookSchedule
    {
        public const float FutureMinFps = 0.5f;
        public const float FutureMaxFps = 2f;
        public const bool AutomaticSamplingEnabled = true;
        public const bool FocusAndMutePauseImplemented = false;
        public const bool PerformanceImpactMeasured = false;
    }
}
