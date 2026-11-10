using AICompanion.AI;

namespace AICompanion.ScreenLook
{
    public sealed class ScreenLookInput
    {
        public bool FeatureEnabled;
        public bool CloudConsent;
        public bool PrivateMode;
        public bool AllowCloud;
        public string EffectiveProviderId;
        public string CloudModel;
        public bool Busy;
        public bool Automatic;
        public bool MonitorRequested;
        public WindowTarget Target;
    }

    public readonly struct ScreenLookDecision
    {
        public bool AllowPreview { get; }
        public bool AllowSend { get; }
        public string Message { get; }

        public ScreenLookDecision(bool allowPreview, bool allowSend, string message)
        {
            AllowPreview = allowPreview;
            AllowSend = allowSend;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// Consent, private mode, and target checks for one manual frame.
    /// A blocked send does not change the provider.
    /// </summary>
    public static class ScreenLookPolicy
    {
        public const bool RememberObservations = false;

        public const string FeatureOff = "Read a window is off. Turn it on under Settings, Screen. Nothing is captured.";
        public const string NeedWindow = "Choose a window first. The desktop is not captured.";
        public const string DesktopRefused = "That target is the desktop or the taskbar. Capture stays on a window you select.";
        public const string OwnWindow = "That window is this companion. Choose a different window.";
        public const string Minimized = "That window is minimised or has no area. Restore it, then try again. Nothing was sent.";
        public const string Closed = "That window has closed. Choose another. Nothing was sent.";
        public const string Busy = "A look is already in progress.";
        public const string AutomaticRefused = "Automatic commentary is not on. Use What do you see? for one frame.";
        public const string MonitorRefused = "Capture does not switch to the whole screen.";
        public const string ConsentRequired = "Cloud screen analysis is off. Allowing it sends the selected window image to the configured cloud vision provider (Grok). Nothing was sent.";
        public const string Stale = "That frame was dropped because a newer look started. Nothing was sent.";
        public const string Blank = "The capture was empty or black. Protected and exclusive fullscreen windows often do this. Nothing was sent.";
        public const string CaptureFailed = "That window could not be captured. Exclusive fullscreen, a protected window, or a closed window can do this. Nothing was sent.";
        public const string PlayerOnly = "Window capture runs in the Windows player. This session cannot grab another window.";

        public static ScreenLookDecision Evaluate(ScreenLookInput input)
        {
            input = input ?? new ScreenLookInput();
            if (!input.FeatureEnabled)
                return new ScreenLookDecision(false, false, FeatureOff);
            if (input.Busy)
                return new ScreenLookDecision(false, false, Busy);
            if (input.Automatic && !ScreenLookSchedule.AutomaticSamplingEnabled)
                return new ScreenLookDecision(false, false, AutomaticRefused);
            if (input.MonitorRequested && !CapturePlan.MonitorCaptureAllowed)
                return new ScreenLookDecision(false, false, MonitorRefused);

            var validity = WindowTargetRules.Check(input.Target);
            if (validity == TargetValidity.Missing)
                return new ScreenLookDecision(false, false, NeedWindow);
            if (validity == TargetValidity.Desktop)
                return new ScreenLookDecision(false, false, DesktopRefused);
            if (validity == TargetValidity.OwnProcess)
                return new ScreenLookDecision(false, false, OwnWindow);
            if (validity == TargetValidity.Closed)
                return new ScreenLookDecision(false, false, Closed);
            if (validity == TargetValidity.Minimized)
                return new ScreenLookDecision(false, false, Minimized);

            if (input.PrivateMode || !input.AllowCloud)
                return new ScreenLookDecision(true, false, VisionCapability.PrivateRefusal);

            var local = string.Equals(input.EffectiveProviderId, ChatDefaults.LocalId, System.StringComparison.OrdinalIgnoreCase);
            if (local)
                return new ScreenLookDecision(true, false, VisionCapability.LocalRefusal);
            if (!VisionCapability.CloudModelAcceptsImages(input.CloudModel))
                return new ScreenLookDecision(true, false, VisionCapability.ModelRefusal);
            if (!input.CloudConsent)
                return new ScreenLookDecision(true, false, ConsentRequired);

            return new ScreenLookDecision(true, true, string.Empty);
        }
    }
}
