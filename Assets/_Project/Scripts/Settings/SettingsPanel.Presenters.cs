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

        /// <summary>"Run setup again" for the new settings view.</summary>
        public void RequestSetupReplay()
        {
            if (SetupReplayRequested != null)
                SetupReplayRequested();
        }
    }
}
