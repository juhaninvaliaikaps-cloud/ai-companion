using System;
using System.Collections.Generic;

namespace AICompanion.Settings.Presenters
{
    /// <summary>The screen look as presenters see it. <see cref="ScreenLookControllerPort"/> wraps the real controller.</summary>
    public interface IScreenLookPort
    {
        bool IsBusy { get; }
        bool LookButtonInteractable { get; }
        string StatusText { get; }
        IReadOnlyList<string> MonitorLabels { get; }
        /// <summary>Index into <see cref="MonitorLabels"/>, or -1.</summary>
        int SelectedMonitorIndex { get; }
        void RefreshWindows();
        void RefreshMonitors();
        void DisableFeature();
        void SelectMonitor(int index);
        void RequestLook();
        void Stop();
    }

    /// <summary>One display in the "pick a monitor" list.</summary>
    public readonly struct MonitorChoice
    {
        public readonly int Index;
        public readonly string Label;
        public readonly bool Selected;

        public MonitorChoice(int index, string label, bool selected)
        {
            Index = index;
            Label = label ?? string.Empty;
            Selected = selected;
        }
    }

    /// <summary>
    /// JUH-104 PR B: "Watch my screen" (S-SC-01, S-SC-05, S-SC-16). Turning it on and off does
    /// the same as "Read a window" on the old Screen tab. Picking a monitor is offered only
    /// when whole-screen capture and its separate consent are already on in Settings: the
    /// wheel never grants a consent the user has not given there.
    /// </summary>
    public sealed class ScreenLookPresenter
    {
        public const string ConsentNeeded = "Turn on \"Whole screen instead of a window\" and \"Allow whole-screen capture\" in Settings > Screen to pick a monitor.";
        public const string FeatureOffNote = "Screen look is off. Nothing is captured.";
        public const string NoMonitors = "No displays were reported by Windows. Nothing is captured.";
        public const string Unavailable = "Window capture is not available in this session.";

        readonly SettingsPresenter _settings;
        readonly Func<IScreenLookPort> _port;

        public ScreenLookPresenter(SettingsPresenter settings, Func<IScreenLookPort> port)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _port = port ?? (() => null);
        }

        public bool Enabled => _settings.Settings != null && _settings.Settings.ScreenLookEnabled;

        public bool Available => _port() != null;

        public bool Busy
        {
            get
            {
                var port = _port();
                return port != null && port.IsBusy;
            }
        }

        public string StatusText
        {
            get
            {
                var port = _port();
                if (port == null)
                    return Unavailable;
                return Enabled ? port.StatusText ?? string.Empty : FeatureOffNote;
            }
        }

        /// <summary>Whole-screen mode and its consent are both on, so monitors can be picked.</summary>
        public bool CanPickMonitor
        {
            get
            {
                var s = _settings.Settings;
                return s != null && s.ScreenLookEnabled && s.ScreenCaptureWholeMonitor && s.ScreenWholeMonitorConsent;
            }
        }

        public void SetEnabled(bool on)
        {
            _settings.Change(s => s.ScreenLookEnabled = on);
            var port = _port();
            if (port == null)
                return;
            if (on)
                port.RefreshWindows();
            else
                port.DisableFeature();
        }

        public void Toggle()
        {
            SetEnabled(!Enabled);
        }

        /// <summary>The displays to pick from; empty (with <paramref name="note"/>) when picking is not allowed.</summary>
        public IReadOnlyList<MonitorChoice> Monitors(out string note)
        {
            var list = new List<MonitorChoice>();
            var port = _port();
            if (port == null)
            {
                note = Unavailable;
                return list;
            }

            if (!CanPickMonitor)
            {
                note = Enabled ? ConsentNeeded : FeatureOffNote;
                return list;
            }

            port.RefreshMonitors();
            var labels = port.MonitorLabels;
            if (labels == null || labels.Count == 0)
            {
                note = NoMonitors;
                return list;
            }

            var selected = port.SelectedMonitorIndex;
            for (var i = 0; i < labels.Count; i++)
                list.Add(new MonitorChoice(i, labels[i], i == selected));
            note = string.Empty;
            return list;
        }

        /// <summary>Selects a display. Refused (false) unless whole-screen consent is on.</summary>
        public bool SelectMonitor(int index)
        {
            var port = _port();
            if (port == null || !CanPickMonitor)
                return false;
            var labels = port.MonitorLabels;
            if (labels == null || index < 0 || index >= labels.Count)
                return false;
            port.SelectMonitor(index);
            return true;
        }

        /// <summary>"Look at screen" / "Stop look". False when the policy does not allow a look now.</summary>
        public bool LookNow()
        {
            var port = _port();
            if (port == null || !Enabled)
                return false;
            if (port.IsBusy)
            {
                port.Stop();
                return true;
            }

            if (!port.LookButtonInteractable)
                return false;
            port.RequestLook();
            return true;
        }
    }

    /// <summary>Adapter from <see cref="ScreenLookController"/> to <see cref="IScreenLookPort"/>.</summary>
    public sealed class ScreenLookControllerPort : IScreenLookPort
    {
        readonly ScreenLookController _look;
        readonly List<string> _labels = new List<string>();

        public ScreenLookControllerPort(ScreenLookController look)
        {
            _look = look;
        }

        public bool IsBusy => _look != null && _look.IsBusy;
        public bool LookButtonInteractable => _look != null && _look.LookButtonInteractable;
        public string StatusText => _look != null ? _look.StatusText : string.Empty;

        public IReadOnlyList<string> MonitorLabels
        {
            get
            {
                _labels.Clear();
                var monitors = _look != null ? _look.Monitors : null;
                if (monitors != null)
                {
                    for (var i = 0; i < monitors.Count; i++)
                        _labels.Add(monitors[i] != null ? monitors[i].Label : string.Empty);
                }

                return _labels;
            }
        }

        public int SelectedMonitorIndex
        {
            get
            {
                var monitors = _look != null ? _look.Monitors : null;
                if (monitors == null || _look.SelectedMonitor == null)
                    return -1;
                for (var i = 0; i < monitors.Count; i++)
                {
                    if (monitors[i] == _look.SelectedMonitor)
                        return i;
                }

                return -1;
            }
        }

        public void RefreshWindows()
        {
            if (_look != null)
                _look.RefreshWindows();
        }

        public void RefreshMonitors()
        {
            if (_look != null)
                _look.RefreshMonitors();
        }

        public void DisableFeature()
        {
            if (_look != null)
                _look.DisableFeature();
        }

        public void SelectMonitor(int index)
        {
            var monitors = _look != null ? _look.Monitors : null;
            if (monitors != null && index >= 0 && index < monitors.Count)
                _look.SelectMonitor(monitors[index]);
        }

        public void RequestLook()
        {
            if (_look != null)
                _look.RequestLook();
        }

        public void Stop()
        {
            if (_look != null)
                _look.Stop();
        }
    }
}
