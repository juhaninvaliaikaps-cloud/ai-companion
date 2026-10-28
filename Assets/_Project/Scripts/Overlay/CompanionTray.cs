using System;
using System.Collections.Concurrent;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Notification-area icon for the Windows player. The editor keeps the menu
    /// state and pause flag and does not call Shell_NotifyIcon.
    /// Left click toggles the overlay. Right click opens the menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionTray : MonoBehaviour, ICompanionStartup, ICompanionPause
    {
        [SerializeField] bool hideTaskbarButton = true;

        readonly TrayMenuState _menu = new TrayMenuState();
        readonly ConcurrentQueue<TrayCommand> _commands = new ConcurrentQueue<TrayCommand>();

        IStartupRegistration _startup;
        OverlayWindow _window;

        public event Action TrayStateChanged;

        public TrayMenuState Menu => _menu;

        public bool HideTaskbarButton => _hideTaskbar;
        string _executable = string.Empty;
        bool _hideTaskbar = true;
        bool _started;
        bool _quitStarted;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool _loggedIcon;
        WindowsTrayHost _tray;
#endif

        public bool IsPaused => _menu.Paused;

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            _hideTaskbar = TrayPreferences.ResolveHideTaskbar(
                hideTaskbarButton,
                PlayerPrefs.HasKey(TrayPreferences.HideTaskbarKey),
                PlayerPrefs.GetInt(TrayPreferences.HideTaskbarKey, 0));
            _menu.Paused = PlayerPrefs.GetInt(TrayPreferences.PauseKey, 0) != 0;
            _startup = CreateStartup();
            bool enabled;
            if (_startup.TryGetEnabled(WindowsStartupEntry.ValueName, out enabled))
                _menu.StartWithWindows = enabled;
            _executable = CurrentExecutable();
            ApplyPause();
            InstallTray();
            Application.quitting += OnQuitting;
        }

        void Start()
        {
            StartCompanion();
        }

        void Update()
        {
            if (!_started)
                return;

            DrainCommands();
            var chrome = Chrome();
            if (chrome != null && chrome.IsHandleLocked)
            {
                chrome.SetTaskbarButtonVisible(!_hideTaskbar);
                if (!_menu.CompanionVisible)
                    chrome.SetWindowVisible(false);
            }

            ReportIcon();
        }

        void OnDestroy()
        {
            if (_started)
                Application.quitting -= OnQuitting;
            RemoveTray();
        }

        public void SetPaused(bool paused)
        {
            if (!_started)
                StartCompanion();
            if (_menu.Paused == paused)
            {
                ApplyPause();
                return;
            }

            _menu.Paused = paused;
            PlayerPrefs.SetInt(TrayPreferences.PauseKey, paused ? 1 : 0);
            PlayerPrefs.Save();
            ApplyPause();
            RaiseTrayState();
        }

        public void ShowCompanion()
        {
            if (!_started)
                StartCompanion();
            if (_menu.CompanionVisible)
                return;

            _menu.CompanionVisible = true;
            PushVisibility();
        }

        /// <summary>
        /// Saves the startup preference on the tray checkmark. The Windows player
        /// writes HKCU. The Editor keeps the checkmark so it stays in sync with
        /// settings and lets the player apply the Run key later.
        /// </summary>
        public bool TrySetStartWithWindows(bool enabled)
        {
            if (!_started)
                StartCompanion();

            _menu.StartWithWindows = enabled;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            ApplyStartup();
#endif
            RaiseTrayState();
            return _menu.StartWithWindows == enabled;
        }

        /// <summary>
        /// Persists the taskbar-button setting. Default is hidden once the overlay
        /// handle is locked, so the player lives in the tray.
        /// </summary>
        public void SetHideTaskbarButton(bool hide)
        {
            _hideTaskbar = hide;
            hideTaskbarButton = hide;
            PlayerPrefs.SetInt(TrayPreferences.HideTaskbarKey, hide ? 1 : 0);
            PlayerPrefs.Save();
            RaiseTrayState();
        }

        void DrainCommands()
        {
            TrayCommand command;
            while (_commands.TryDequeue(out command))
                Dispatch(command);
        }

        void Dispatch(TrayCommand command)
        {
            var result = _menu.Apply(command);
            if (!result.Handled)
                return;

            if (result.VisibilityChanged)
                PushVisibility();
            if (result.PauseChanged)
            {
                PlayerPrefs.SetInt(TrayPreferences.PauseKey, _menu.Paused ? 1 : 0);
                PlayerPrefs.Save();
                ApplyPause();
                RaiseTrayState();
            }

            if (result.StartupChanged)
            {
                ApplyStartup();
                RaiseTrayState();
            }
            if (result.OpenChat)
                OpenChat();
            if (result.OpenSettings)
                _menu.RaiseSettings();
            if (result.Quit)
                Quit();
        }

        void PushVisibility()
        {
            var window = Window();
            if (window != null)
                window.SetCompanionVisible(_menu.CompanionVisible);
        }

        void OpenChat()
        {
            if (!_menu.CompanionVisible)
            {
                _menu.CompanionVisible = true;
                PushVisibility();
            }

            var chat = GetComponent<IChatWindow>();
            if (chat != null)
                chat.OpenChat();
        }

        void ApplyStartup()
        {
            var enabled = _menu.StartWithWindows;
            if (StartupToggle.TryApply(_startup, enabled, _executable))
                return;

            _menu.StartWithWindows = !enabled;
            Debug.Log("Start with Windows could not be updated.");
        }

        void ApplyPause()
        {
            var paused = _menu.Paused;
            var speech = GetComponent<ISpeechSuppression>();
            if (speech != null)
                speech.SetSuppressed(!CompanionPause.SpeechAllowed(false, paused));
            var reactions = GetComponent<IReactionSuppression>();
            if (reactions != null)
                reactions.SetSuppressed(!CompanionPause.ReactionsAllowed(paused));
        }

        void Quit()
        {
            if (_quitStarted)
                return;
            _quitStarted = true;
            RemoveTray();
            Application.Quit();
        }

        void RaiseTrayState()
        {
            var handler = TrayStateChanged;
            if (handler != null)
                handler();
        }

        void OnQuitting()
        {
            RemoveTray();
        }

        void InstallTray()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _tray = new WindowsTrayHost(_menu, _commands);
            _tray.Start();
#endif
        }

        void RemoveTray()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_tray == null)
                return;
            var tray = _tray;
            _tray = null;
            tray.Remove();
#endif
        }

        void ReportIcon()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_loggedIcon || _tray == null)
                return;
            var status = _tray.Status;
            if (status == 1)
            {
                _loggedIcon = true;
                Debug.Log("Tray icon installed.");
            }
            else if (status == 2)
            {
                _loggedIcon = true;
                Debug.LogWarning("Tray icon could not be installed.");
            }
#endif
        }

        IWindowChrome Chrome()
        {
            var window = Window();
            return window != null ? window.Chrome : null;
        }

        OverlayWindow Window()
        {
            if (_window == null)
                _window = GetComponent<OverlayWindow>();
            return _window;
        }

        static IStartupRegistration CreateStartup()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return new RegistryStartupRegistration();
#else
            return new UnavailableStartupRegistration();
#endif
        }

        static string CurrentExecutable()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                return module != null ? module.FileName : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
#else
            return string.Empty;
#endif
        }
    }
}
