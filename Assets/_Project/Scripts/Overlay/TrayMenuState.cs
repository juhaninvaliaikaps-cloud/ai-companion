using System;

namespace AICompanion.Overlay
{
    public enum TrayCommand
    {
        None = 0,
        ToggleVisible = 1,
        OpenChat = 2,
        OpenSettings = 3,
        TogglePause = 4,
        ToggleStartup = 5,
        Quit = 6
    }

    public readonly struct TrayApplyResult
    {
        public bool Handled { get; }
        public bool VisibilityChanged { get; }
        public bool PauseChanged { get; }
        public bool StartupChanged { get; }
        public bool OpenChat { get; }
        public bool OpenSettings { get; }
        public bool Quit { get; }

        public TrayApplyResult(
            bool handled,
            bool visibilityChanged,
            bool pauseChanged,
            bool startupChanged,
            bool openChat,
            bool openSettings,
            bool quit)
        {
            Handled = handled;
            VisibilityChanged = visibilityChanged;
            PauseChanged = pauseChanged;
            StartupChanged = startupChanged;
            OpenChat = openChat;
            OpenSettings = openSettings;
            Quit = quit;
        }

        public static TrayApplyResult NotHandled => default;
    }

    public struct TrayMenuItem
    {
        public TrayCommand Command;
        public string Label;
        public bool Checked;
        public bool SeparatorBefore;
    }

    /// <summary>
    /// Labels, checks, and toggles for the tray menu. Safe to read from the menu thread.
    /// </summary>
    public sealed class TrayMenuState
    {
        public const string OpenChatLabel = "Open chat";
        public const string SettingsLabel = "Settings";
        public const string PauseLabel = "Pause";
        public const string StartupLabel = "Start with Windows";
        public const string QuitLabel = "Quit";

        /// <summary>
        /// JUH-55 stub. The settings window does not exist yet.
        /// </summary>
        public const string SettingsStubMessage = "JUH-55 stub: the settings window is not implemented yet.";

        readonly object _gate = new object();
        bool _companionVisible = true;
        bool _paused;
        bool _startWithWindows;

        /// <summary>
        /// JUH-55 stub hook. The tray logs <see cref="SettingsStubMessage"/> from here
        /// until a settings window subscribes and replaces that behavior.
        /// </summary>
        public event Action SettingsRequested;

        public bool CompanionVisible
        {
            get
            {
                lock (_gate)
                    return _companionVisible;
            }
            set
            {
                lock (_gate)
                    _companionVisible = value;
            }
        }

        public bool Paused
        {
            get
            {
                lock (_gate)
                    return _paused;
            }
            set
            {
                lock (_gate)
                    _paused = value;
            }
        }

        public bool StartWithWindows
        {
            get
            {
                lock (_gate)
                    return _startWithWindows;
            }
            set
            {
                lock (_gate)
                    _startWithWindows = value;
            }
        }

        public string VisibilityLabel
        {
            get { return VisibilityLabelFor(CompanionVisible); }
        }

        public static string VisibilityLabelFor(bool companionVisible)
        {
            return companionVisible ? "Hide companion" : "Show companion";
        }

        public void CopyFlags(out bool companionVisible, out bool paused, out bool startWithWindows)
        {
            lock (_gate)
            {
                companionVisible = _companionVisible;
                paused = _paused;
                startWithWindows = _startWithWindows;
            }
        }

        public TrayApplyResult Apply(TrayCommand command)
        {
            switch (command)
            {
                case TrayCommand.ToggleVisible:
                    CompanionVisible = !CompanionVisible;
                    return new TrayApplyResult(true, true, false, false, false, false, false);
                case TrayCommand.TogglePause:
                    Paused = !Paused;
                    return new TrayApplyResult(true, false, true, false, false, false, false);
                case TrayCommand.ToggleStartup:
                    StartWithWindows = !StartWithWindows;
                    return new TrayApplyResult(true, false, false, true, false, false, false);
                case TrayCommand.OpenChat:
                    return new TrayApplyResult(true, false, false, false, true, false, false);
                case TrayCommand.OpenSettings:
                    return new TrayApplyResult(true, false, false, false, false, true, false);
                case TrayCommand.Quit:
                    return new TrayApplyResult(true, false, false, false, false, false, true);
                default:
                    return TrayApplyResult.NotHandled;
            }
        }

        public void RaiseSettings()
        {
            var handler = SettingsRequested;
            if (handler != null)
                handler();
        }

        public static TrayMenuItem[] Build(bool companionVisible, bool paused, bool startWithWindows)
        {
            return new[]
            {
                Item(TrayCommand.ToggleVisible, VisibilityLabelFor(companionVisible), false, false),
                Item(TrayCommand.OpenChat, OpenChatLabel, false, false),
                Item(TrayCommand.OpenSettings, SettingsLabel, false, false),
                Item(TrayCommand.TogglePause, PauseLabel, paused, true),
                Item(TrayCommand.ToggleStartup, StartupLabel, startWithWindows, false),
                Item(TrayCommand.Quit, QuitLabel, false, true)
            };
        }

        static TrayMenuItem Item(TrayCommand command, string label, bool check, bool separatorBefore)
        {
            return new TrayMenuItem
            {
                Command = command,
                Label = label,
                Checked = check,
                SeparatorBefore = separatorBefore
            };
        }
    }

    /// <summary>
    /// Left click versus right click for a Shell_NotifyIcon callback.
    /// NOTIFYICON_VERSION_4 packs the event in the low word and the icon id in the high word.
    /// </summary>
    public static class TrayClick
    {
        public const uint LeftButtonUp = 0x0202;
        public const uint RightButtonUp = 0x0205;
        public const uint ContextMenu = 0x007B;
        public const uint Select = 0x0400;
        public const uint KeySelect = 0x0401;

        public static uint EventCode(uint packedOrRaw, bool version4)
        {
            return version4 ? packedOrRaw & 0xFFFFu : packedOrRaw;
        }

        public static TrayClickKind Classify(uint eventCode)
        {
            if (eventCode == LeftButtonUp || eventCode == Select || eventCode == KeySelect)
                return TrayClickKind.Primary;
            if (eventCode == RightButtonUp || eventCode == ContextMenu)
                return TrayClickKind.Context;
            return TrayClickKind.None;
        }

        public static bool Allow(ref int previous, int now, int windowMs, ref bool hasPrevious)
        {
            if (windowMs < 0)
                windowMs = 0;
            if (hasPrevious && unchecked(now - previous) < windowMs)
                return false;
            hasPrevious = true;
            previous = now;
            return true;
        }
    }

    public enum TrayClickKind
    {
        None = 0,
        Primary = 1,
        Context = 2
    }

    public static class TrayPreferences
    {
        public const string PauseKey = "ai-companion.paused";
        public const string HideTaskbarKey = "ai-companion.hide-taskbar";

        public static bool ResolveHideTaskbar(bool inspectorDefault, bool hasPref, int prefValue)
        {
            if (hasPref)
                return prefValue != 0;
            return inspectorDefault;
        }
    }
}
