using System.Collections.Generic;
using AICompanion.Settings.Presenters;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-104 PR B: this panel is the host the presenters change settings through, so the
    /// old tabs, the new settings view and the radial wheel all apply and save the same way
    /// (<see cref="ApplyLive"/>, encrypted settings.json). Its own file, so the open PRs that
    /// edit SettingsPanel*.cs merge around it.
    /// </summary>
    public sealed partial class SettingsPanel : ISettingsHost, IPersonaHost
    {
        SettingsPresenter _settingsPresenter;
        PersonaPresenter _personaPresenter;
        ScreenLookPresenter _screenPresenter;
        MemoryPresenter _memoryPresenter;

        public SettingsPresenter Presenter
        {
            get
            {
                if (_settingsPresenter == null)
                    _settingsPresenter = new SettingsPresenter(this);
                return _settingsPresenter;
            }
        }

        public PersonaPresenter Personas
        {
            get
            {
                if (_personaPresenter == null)
                    _personaPresenter = new PersonaPresenter(this);
                return _personaPresenter;
            }
        }

        public ScreenLookPresenter ScreenLook
        {
            get
            {
                if (_screenPresenter == null)
                {
                    _screenPresenter = new ScreenLookPresenter(Presenter, () =>
                    {
                        var look = GetComponent<ScreenLookController>();
                        return look != null ? new ScreenLookControllerPort(look) : null;
                    });
                }

                return _screenPresenter;
            }
        }

        public MemoryPresenter Memories
        {
            get
            {
                if (_memoryPresenter == null)
                    _memoryPresenter = new MemoryPresenter(() => new MemoryStorePort());
                return _memoryPresenter;
            }
        }

        /// <summary>The ProjectLeia data folder (settings, history, personas, exports start here).</summary>
        public string DataFolder => Folder();

        /// <summary>True while the old settings panel is open.</summary>
        public bool IsOpen => _open;

        CompanionSettings ISettingsHost.Settings
        {
            get
            {
                if (!_started)
                    StartCompanion();
                return _settings;
            }
        }

        void ISettingsHost.ApplyAndSave()
        {
            ApplyLive();
        }

        void ISettingsHost.SetStatus(string message)
        {
            SetStatus(message);
        }

        string ISettingsHost.Status => _statusMessage ?? string.Empty;

        void ISettingsHost.RequestRefresh()
        {
            if (_open)
                _rebuild = true;
        }

        string ISettingsHost.ActiveModelText => ActiveModelText();

        IReadOnlyList<PersonaEntry> IPersonaHost.PersonaEntries => PersonaCatalog.LoadDefault(Folder()).Entries;

        string IPersonaHost.ActivePersonaId => _settings != null ? _settings.PersonaId ?? string.Empty : string.Empty;

        void IPersonaHost.UsePersona(string id)
        {
            UsePersona(id);
        }

        void IPersonaHost.UseSetupName()
        {
            ClearActivePersona();
        }

        /// <summary>Export for the new settings view: same checks and messages as the Privacy tab.</summary>
        public string ExportLocalData(string destination, bool zip)
        {
            var status = LocalDataPresenter.Export(Folder(), destination, zip, LocalDataPortal.ExportFolder, LocalDataPortal.ExportZip);
            SetStatus(status);
            return status;
        }

        /// <summary>Clear chat history for the new settings view (after its own confirm step).</summary>
        public void ClearChatHistoryConfirmed()
        {
            ConfirmClear();
        }

        /// <summary>Wipe all local data for the new settings view (after its own confirm step).</summary>
        public void WipeAllConfirmed()
        {
            ConfirmWipe();
        }

        // ---- JUH-104 PR E: the new settings view reaches the same actions as the old tabs ----

        /// <summary>The status line text.</summary>
        public string StatusMessage => _statusMessage ?? string.Empty;

        public void SetStatusMessage(string message)
        {
            SetStatus(message);
        }

        /// <summary>Same as "Save key" on the AI tab: the key goes to the secret store, never settings.json.</summary>
        public void SaveApiKey(string key)
        {
            var trimmed = (key ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                SetStatus("Enter a key to save. A saved key stays masked.");
                return;
            }

            try
            {
                AICompanion.AI.ApiKeyFile.SavePreferred(trimmed);
                SetStatus(ApiKeyMask.SavedLabel + ". It is in the secret store, not in settings.");
            }
            catch (System.Exception)
            {
                SetStatus("Could not save the API key.");
            }
        }

        /// <summary>Same as "Remove saved key" on the AI tab.</summary>
        public void RemoveApiKey()
        {
            RemoveKey();
        }

        public bool ApiKeySaved => FileHasKey();

        public bool ApiKeyFromEnvironment => EnvOverrides();

        public bool ConnectionTestRunning => _probeBusy;

        /// <summary>Same as "Test connection" on the AI tab (the key and header are never logged).</summary>
        public void TestConnectionNow()
        {
            if (!_probeBusy)
                TestConnection();
        }

        /// <summary>Same as "Save chat history" on the Privacy tab.</summary>
        public void SetChatHistory(bool on)
        {
            _settings.ChatHistory = on;
            if (on)
                SaveHistory();
            ApplyLive();
        }

        /// <summary>The persona list read from disk now (built-ins and saved files).</summary>
        public PersonaCatalog PersonaCatalogNow()
        {
            return PersonaCatalog.LoadDefault(Folder());
        }

        /// <summary>Same as editing a field on the Persona tab.</summary>
        public void SavePersona(string id, System.Action<PersonaDefinition> change)
        {
            SaveActive(id, change);
        }

        /// <summary>Same as "New persona".</summary>
        public void NewPersona()
        {
            CreatePersona();
        }

        /// <summary>Same as "Delete saved copy" / "Delete persona".</summary>
        public void DeletePersona()
        {
            DeleteActivePersona();
        }

        /// <summary>
        /// Saves a look-only setting (chat box position, spoken-text glow) without re-applying
        /// everything: a full apply rebuilds the voice provider, which must not happen on every drag.
        /// </summary>
        public void SaveLayoutSetting(System.Action<CompanionSettings> change)
        {
            if (change == null || _settings == null)
                return;
            change(_settings);
            _settings.Normalize();
            SaveSettings();
        }

        /// <summary>Same as "Reset rotation" on the Avatar tab.</summary>
        public void ResetAvatarRotation()
        {
            var mount = GetComponent<AICompanion.Avatar.AvatarMount>();
            if (mount != null)
                mount.ResetRotation();
            SetStatus("Rotation reset.");
        }

        /// <summary>
        /// While set, Open() (F12, the tray, the wheel) shows the new settings view instead of
        /// this panel. Return true when it handled the request. Null keeps this panel.
        /// </summary>
        public System.Func<bool> OpenOverride;

        /// <summary>Opens this classic panel even while the new settings view is on ("Classic settings").</summary>
        public void OpenClassic()
        {
            var redirect = OpenOverride;
            OpenOverride = null;
            try
            {
                Open();
            }
            finally
            {
                OpenOverride = redirect;
            }
        }

        public ScreenLookController ScreenController => GetComponent<ScreenLookController>();

        /// <summary>"Run setup again" for the new settings view.</summary>
        public void RequestSetupReplay()
        {
            if (SetupReplayRequested != null)
                SetupReplayRequested();
        }
    }
}
