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
        public string ChatModel;
        public string LocalVisionModel;
        public bool Busy;
        public bool Automatic;
        public bool MonitorRequested;
        public WindowTarget Target;
    }

    public enum ScreenLookRoute
    {
        None = 0,
        Cloud = 1,
        Local = 2
    }

    public readonly struct ScreenLookDecision
    {
        public bool AllowPreview { get; }
        public ScreenLookRoute Route { get; }
        public string Message { get; }

        public bool AllowSend
        {
            get { return Route != ScreenLookRoute.None; }
        }

        /// <summary>
        /// The What do you see? and Look at screen controls use this.
        /// A look that is already running stays clickable so Stop still works.
        /// </summary>
        public bool SendButtonEnabled
        {
            get { return AllowSend; }
        }

        public ScreenLookDecision(bool allowPreview, ScreenLookRoute route, string message)
        {
            AllowPreview = allowPreview;
            Route = route;
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
        public const string ConsentRequired = "Cloud screen analysis is off, and no local vision model is set. What do you see? stays off until you set a local vision model or allow cloud screen analysis. Nothing was sent.";
        public const string Stale = "That frame was dropped because a newer look started. Nothing was sent.";
        public const string Blank = "The capture was empty or black. Protected and exclusive fullscreen windows often do this. Nothing was sent.";
        public const string CaptureFailed = "That window could not be captured. Exclusive fullscreen, a protected window, or a closed window can do this. Nothing was sent.";
        public const string PlayerOnly = "Window capture runs in the Windows player. This session cannot grab another window.";
        public const string MissingLocalVision = "No local vision model is set. The text model does not read pictures. Set one under Settings, Screen. Nothing was sent, and the app did not switch to a cloud model.";

        public static ScreenLookDecision Evaluate(ScreenLookInput input)
        {
            input = input ?? new ScreenLookInput();
            if (!input.FeatureEnabled)
                return new ScreenLookDecision(false, ScreenLookRoute.None, FeatureOff);
            if (input.Busy)
                return new ScreenLookDecision(false, ScreenLookRoute.None, Busy);
            if (input.Automatic && !ScreenLookSchedule.AutomaticSamplingEnabled)
                return new ScreenLookDecision(false, ScreenLookRoute.None, AutomaticRefused);
            if (input.MonitorRequested && !CapturePlan.MonitorCaptureAllowed)
                return new ScreenLookDecision(false, ScreenLookRoute.None, MonitorRefused);

            var validity = WindowTargetRules.Check(input.Target);
            if (validity == TargetValidity.Missing)
                return new ScreenLookDecision(false, ScreenLookRoute.None, NeedWindow);
            if (validity == TargetValidity.Desktop)
                return new ScreenLookDecision(false, ScreenLookRoute.None, DesktopRefused);
            if (validity == TargetValidity.OwnProcess)
                return new ScreenLookDecision(false, ScreenLookRoute.None, OwnWindow);
            if (validity == TargetValidity.Closed)
                return new ScreenLookDecision(false, ScreenLookRoute.None, Closed);
            if (validity == TargetValidity.Minimized)
                return new ScreenLookDecision(false, ScreenLookRoute.None, Minimized);

            if (UseLocalVision(input))
                return LocalRoute(input.LocalVisionModel, input.ChatModel);

            if (!VisionCapability.CloudModelAcceptsImages(input.CloudModel))
                return new ScreenLookDecision(true, ScreenLookRoute.None, VisionCapability.ModelRefusal);
            if (!input.CloudConsent)
                return new ScreenLookDecision(true, ScreenLookRoute.None, ConsentRequired);

            return new ScreenLookDecision(true, ScreenLookRoute.Cloud, string.Empty);
        }

        public static bool LookButtonInteractable(bool sendEnabled, bool busy)
        {
            return busy || sendEnabled;
        }

        /// <summary>
        /// Local vision does not need the cloud switch. A configured vision model
        /// with cloud analysis off stays on this PC, including when chat is still Grok.
        /// </summary>
        public static bool UseLocalVision(ScreenLookInput input)
        {
            input = input ?? new ScreenLookInput();
            if (input.PrivateMode || !input.AllowCloud)
                return true;
            if (string.Equals(input.EffectiveProviderId, ChatDefaults.LocalId, System.StringComparison.OrdinalIgnoreCase))
                return true;
            return !input.CloudConsent && HasVisionModel(input.LocalVisionModel);
        }

        public static bool HasVisionModel(string visionModel)
        {
            return !string.IsNullOrWhiteSpace(visionModel);
        }

        static ScreenLookDecision LocalRoute(string visionModel, string chatModel)
        {
            var vision = visionModel == null ? string.Empty : visionModel.Trim();
            var chat = chatModel == null ? string.Empty : chatModel.Trim();
            if (vision.Length == 0)
                return new ScreenLookDecision(true, ScreenLookRoute.None, MissingLocalVision);
            if (chat.Length > 0 && string.Equals(vision, chat, System.StringComparison.OrdinalIgnoreCase))
                return new ScreenLookDecision(true, ScreenLookRoute.None, LocalVisionExchange.SameAsChat);
            return new ScreenLookDecision(true, ScreenLookRoute.Local, string.Empty);
        }
    }
}
