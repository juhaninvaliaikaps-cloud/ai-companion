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

        /// <summary>"Start over with this persona" for the new settings view (after its own confirm step).</summary>
        public void StartOverConfirmed()
        {
            ConfirmStartOver();
        }

        /// <summary>The active persona's name for the Start over button and its question.</summary>
        public string StartOverName
        {
            get { return _settings != null ? _settings.CompanionName : string.Empty; }
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

        /// <summary>What persona settings show about the reading (JUH-118).</summary>
        public readonly struct PisaraPersonaView
        {
            public readonly string TemperamentLine;
            public readonly string RelationshipLine;
            public readonly string Status;
            public readonly bool Manual;
            public readonly bool Reading;
            public readonly float ReadingStartedAt;

            public PisaraPersonaView(string temperamentLine, string relationshipLine, string status, bool manual, bool reading, float readingStartedAt)
            {
                TemperamentLine = temperamentLine ?? string.Empty;
                RelationshipLine = relationshipLine ?? string.Empty;
                Status = status ?? string.Empty;
                Manual = manual;
                Reading = reading;
                ReadingStartedAt = readingStartedAt;
            }
        }

        /// <summary>The reading of the active persona, as lines for the Persona tab (old and new).</summary>
        public PisaraPersonaView PisaraView()
        {
            var id = _settings != null ? _settings.PersonaId ?? string.Empty : string.Empty;
            var keeper = GetComponent<PisaraReadingKeeper>();
            var sheet = keeper != null ? keeper.SheetFor(id) : null;
            var choice = PersonaRuntime.TemperamentChoiceFor(PersonaCatalog.LoadDefault(Folder()), id, sheet);
            return new PisaraPersonaView(
                choice.Line,
                AICompanion.Core.PisaraReading.RelationshipLine(sheet) + AICompanion.Core.PisaraCorrections.Note(sheet),
                keeper != null ? keeper.StatusLine : AICompanion.Core.PisaraReading.SourceLine(sheet),
                !choice.Auto,
                keeper != null && keeper.IsReading,
                keeper != null ? keeper.ReadingStartedAt : 0f);
        }

        /// <summary>The "Re-read" button: asks the model again for the active persona's sheet.</summary>
        public void RereadPersona()
        {
            var keeper = GetComponent<PisaraReadingKeeper>();
            if (keeper == null)
                return;
            keeper.Reread();
            SetStatus("Reading her description again. This can take a minute with a local model.");
            _rebuild = true;
        }

        /// <summary>The active persona's character sheet, or null. Read only; correct it with <see cref="CorrectPersonaRelationship"/>.</summary>
        public AICompanion.Core.CharacterSheet ActiveSheet()
        {
            var keeper = GetComponent<PisaraReadingKeeper>();
            return keeper != null ? keeper.SheetFor(_settings != null ? _settings.PersonaId ?? string.Empty : string.Empty) : null;
        }

        /// <summary>
        /// JUH-117: the user corrects who she is to them. Locked against later readings, and
        /// applied to the current relationship at once.
        /// </summary>
        public void CorrectPersonaRelationship(string role, string label, float affection, float familiarity, bool sharedHistory)
        {
            var keeper = GetComponent<PisaraReadingKeeper>();
            if (keeper == null || !keeper.CorrectRelationship(role, label, affection, familiarity, sharedHistory))
            {
                SetStatus("Nothing to correct yet: her description has not been read.");
                return;
            }

            SetStatus("Relationship corrected. A new reading keeps your correction.");
            _rebuild = true;
        }

        /// <summary>JUH-116: the active persona's Innocent–Perverted keeper, or null. Adult mode only.</summary>
        public InnocenceKeeper Innocence
        {
            get { return GetComponent<InnocenceKeeper>(); }
        }

        /// <summary>JUH-116: the slider. Ignored while the 18+ gate is closed.</summary>
        public void SetInnocence(float value)
        {
            var keeper = GetComponent<InnocenceKeeper>();
            if (keeper != null && AICompanion.Core.IntimateExpressions.Active)
                keeper.SetByUser(value);
        }

        /// <summary>JUH-116: back to what the reading said.</summary>
        public void UseInnocenceReading()
        {
            var keeper = GetComponent<InnocenceKeeper>();
            if (keeper == null || !AICompanion.Core.IntimateExpressions.Active)
                return;
            keeper.UseReading();
            SetStatus("Innocence is back to the reading of her description.");
            _rebuild = true;
        }

        /// <summary>JUH-125: the active persona's 18+ secrets keeper, or null.</summary>
        public IntimateSecretsKeeper IntimateSecrets
        {
            get { return GetComponent<IntimateSecretsKeeper>(); }
        }

        /// <summary>JUH-125: read her 18+ secrets again with the local model. Found ones whose text stays keep found.</summary>
        public void ReadIntimateSecretsAgain()
        {
            var keeper = GetComponent<IntimateSecretsKeeper>();
            if (keeper == null || !AICompanion.Core.IntimateExpressions.Active)
                return;
            keeper.ReadAgain();
            SetStatus("Reading her 18+ secrets with the local model.");
            _rebuild = true;
        }

        /// <summary>JUH-116: read her innocence again with the local model.</summary>
        public void ReadInnocenceAgain()
        {
            var keeper = GetComponent<InnocenceKeeper>();
            if (keeper == null || !AICompanion.Core.IntimateExpressions.Active)
                return;
            keeper.ReadAgain();
            SetStatus("Reading her innocence with the local model.");
            _rebuild = true;
        }

        /// <summary>Drops the user's relationship correction and reads her description again.</summary>
        public void UseReadingForRelationship()
        {
            var keeper = GetComponent<PisaraReadingKeeper>();
            if (keeper == null)
                return;
            keeper.UseReadingAgain();
            SetStatus("Your correction is dropped. Reading her description again.");
            _rebuild = true;
        }

        /// <summary>Persona temperament: let the reading decide.</summary>
        public void SetPersonaTemperamentAuto(string id)
        {
            SaveActive(id, item => item.TemperamentMode = AICompanion.Core.TemperamentModes.Auto);
        }

        /// <summary>Persona temperament: a manual pick, which overrides the reading.</summary>
        public void SetPersonaTemperament(string id, string preset)
        {
            SaveActive(id, item =>
            {
                item.Temperament = preset;
                item.TemperamentMode = AICompanion.Core.TemperamentModes.Manual;
            });
        }

        /// <summary>Goes up when a reading starts or comes back.</summary>
        public int PisaraRevision
        {
            get
            {
                var keeper = GetComponent<PisaraReadingKeeper>();
                var innocence = GetComponent<InnocenceKeeper>();
                var intimate = GetComponent<IntimateSecretsKeeper>();
                return (keeper != null ? keeper.Revision : 0) + (innocence != null ? innocence.Revision : 0) + (intimate != null ? intimate.Revision : 0);
            }
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

        public ScreenLookController ScreenController => GetComponent<ScreenLookController>();

        /// <summary>"Run setup again" for the new settings view.</summary>
        public void RequestSetupReplay()
        {
            if (SetupReplayRequested != null)
                SetupReplayRequested();
        }
    }
}
