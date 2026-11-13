using System;
using AICompanion.AI;
using AICompanion.Overlay;

namespace AICompanion.Settings.Presenters
{
    /// <summary>
    /// JUH-104 PR B: settings read/write without uGUI. Every change goes through the host,
    /// which applies it live and saves it, exactly like a button on the old settings panel
    /// (change, <c>ApplyLive</c>, rebuild).
    /// </summary>
    public sealed class SettingsPresenter
    {
        readonly ISettingsHost _host;

        public SettingsPresenter(ISettingsHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public CompanionSettings Settings => _host.Settings;

        public string Status => _host.Status;

        public string ActiveModelText => _host.ActiveModelText;

        /// <summary>Changes the settings, applies them live, saves, and asks views to redraw.</summary>
        public void Change(Action<CompanionSettings> change)
        {
            var settings = _host.Settings;
            if (settings == null || change == null)
                return;
            change(settings);
            _host.ApplyAndSave();
            _host.RequestRefresh();
        }

        public void SetStatus(string message)
        {
            _host.SetStatus(message);
        }

        // ---- Model / provider switch (S-AI-02, S-AI-03) --------------------------------

        public bool GrokSelected => Settings != null && Settings.ProviderId == ChatDefaults.GrokId;

        /// <summary>Only Grok and the local model are valid; anything else means Grok, like Normalize.</summary>
        public void SetProvider(string providerId)
        {
            var id = NormalizeProvider(providerId);
            Change(s => s.ProviderId = id);
        }

        public static string NormalizeProvider(string providerId)
        {
            return string.Equals(providerId, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase)
                ? ChatDefaults.LocalId
                : ChatDefaults.GrokId;
        }

        // ---- Toggles used by the wheel and the new views -------------------------------

        public void SetPrivateMode(bool on) => Change(s => s.PrivateMode = on);
        public void SetMuted(bool on) => Change(s => s.Muted = on);
        public void SetPaused(bool on) => Change(s => s.Paused = on);
        public void SetAlwaysOnTop(bool on) => Change(s => s.AlwaysOnTop = on);
        public void SetShowMoodDebug(bool on) => Change(s => s.ShowMoodDebug = on);
        public void SetNewUiPreview(bool on) => Change(s => s.NewUiPreview = on);
        public void SetVoiceNarration(bool on) => Change(s => s.VoiceNarration = on);
        public void SetTtsEnabled(bool on) => Change(s => s.TtsEnabled = on);

        /// <summary>3D model or 2D video (S-AV-01, S-AV-02).</summary>
        public void SetAvatarMode(string mode) => Change(s => s.AvatarMode = mode);

        // ---- Click-through (S-OV-03) ---------------------------------------------------

        public void CycleClickThrough()
        {
            Change(s => s.ClickThrough = NextClickThrough(s.ClickThrough));
        }

        /// <summary>silhouette → panels only → capture the window → silhouette.</summary>
        public static string NextClickThrough(string current)
        {
            var mode = OverlayClickThroughPolicy.Parse(current);
            var next = mode == OverlayClickThrough.Silhouette
                ? OverlayClickThrough.PanelsOnly
                : mode == OverlayClickThrough.PanelsOnly
                    ? OverlayClickThrough.Capture
                    : OverlayClickThrough.Silhouette;
            return OverlayClickThroughPolicy.Name(next);
        }

        public static string ClickThroughLabel(string value)
        {
            switch (OverlayClickThroughPolicy.Parse(value))
            {
                case OverlayClickThrough.PanelsOnly:
                    return "panels only";
                case OverlayClickThrough.Capture:
                    return "capture the window";
                default:
                    return "silhouette";
            }
        }

        public static string ClickThroughHelp(string value)
        {
            switch (OverlayClickThroughPolicy.Parse(value))
            {
                case OverlayClickThrough.PanelsOnly:
                    return "Only panels catch clicks. The avatar clicks through. Applies immediately.";
                case OverlayClickThrough.Capture:
                    return "The whole window catches clicks. Applies immediately. Transparency stays.";
                default:
                    return "Empty space clicks through. The avatar and panels stay clickable. This is the default.";
            }
        }

        /// <summary>"Name: on" / "Name: off", the label every old toggle button uses.</summary>
        public static string ToggleLabel(string name, bool value)
        {
            return name + ": " + (value ? "on" : "off");
        }
    }
}
