using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// Keeps the active persona's character sheet (JUH-118, docs/PISARA.md). When a persona is
    /// selected or its prompt changes it loads the cached sheet; when there is none for this
    /// prompt it asks the model once, in the background, and saves the result encrypted next
    /// to her mood. Until the reading is back she runs on a neutral sheet.
    /// <para>
    /// The model is the one this persona's chat uses, picked by the same privacy rule
    /// (<see cref="PrivateChatRouting"/> with <see cref="IntimateExpressions.KeepsLocal"/>), so
    /// a private persona is never sent to a cloud model. The request does not go through the
    /// memory layer: remembered facts about the user are not part of a reading.
    /// </para>
    /// One call per event (persona created, prompt changed, Re-read), never per frame. Added
    /// by <see cref="SettingsPanel"/>.
    /// </summary>
    public sealed class PisaraReadingKeeper : MonoBehaviour, ICompanionStartOver
    {
        SettingsPanel _settings;
        ChatPanel _chat;
        ChatProviderRegistry _registry;
        ChatSettings _registryFor;

        string _personaId;
        string _prompt;
        bool _seen;
        CharacterSheet _sheet;
        bool _reading;
        string _failure = string.Empty;
        int _generation;
        CancellationTokenSource _cancel;
        readonly HashSet<string> _tried = new HashSet<string>(StringComparer.Ordinal);
        readonly PisaraApiKeySource _pisaraKeys = new PisaraApiKeySource();

        /// <summary>The prompt check is cheap, but there is no reason to do it sixty times a second.</summary>
        const float CheckSeconds = 0.25f;
        float _checkIn;

        /// <summary>Goes up whenever the sheet or the reading state changes, so settings views can refresh.</summary>
        public int Revision { get; private set; }

        /// <summary>True while a model call is running.</summary>
        public bool IsReading
        {
            get { return _reading; }
        }

        /// <summary>Unscaled time when the current reading started; 0 when none has run.</summary>
        public float ReadingStartedAt { get; private set; }

        /// <summary>The active persona's sheet. Null with the setup name (no persona), or before the first look.</summary>
        public CharacterSheet Sheet
        {
            get { return _sheet; }
        }

        /// <summary>JUH-237: the active persona's prompt (her description), as last checked. Read-only.</summary>
        public string Prompt
        {
            get { return _prompt ?? string.Empty; }
        }

        /// <summary>The sheet for a persona if it is the active one (looked up now), else null.</summary>
        public CharacterSheet SheetFor(string personaId)
        {
            Check();
            return string.Equals(personaId ?? string.Empty, _personaId ?? string.Empty, StringComparison.Ordinal) ? _sheet : null;
        }

        /// <summary>
        /// The user corrects who she is to them (JUH-117). The correction is locked into the
        /// sheet, survives a re-read and an edited prompt, and is applied to the current
        /// relationship at once. False when there is no sheet yet.
        /// </summary>
        public bool CorrectRelationship(string role, string label, float affection, float familiarity, bool sharedHistory)
        {
            if (_sheet == null)
                return false;
            PisaraCorrections.SetRelationship(_sheet, role, label, affection, familiarity, sharedHistory);
            var path = CharacterSheetStore.FileFor(Folder(), _personaId);
            if (!CharacterSheetStore.Save(path, _sheet))
                Debug.LogWarning("Pisara reading: the corrected sheet could not be saved.");
            var relationship = GetComponent<RelationshipKeeper>();
            if (relationship != null)
                relationship.SetByUser(_sheet.relationship.affection, _sheet.relationship.familiarity);
            Debug.Log("Pisara reading: " + (_personaId ?? string.Empty) + " relationship corrected by the user ("
                + PisaraReading.RelationshipLine(_sheet) + ")");
            Revision++;
            return true;
        }

        /// <summary>Drops the user's relationship correction and asks the model again.</summary>
        public void UseReadingAgain()
        {
            if (_sheet == null)
                return;
            PisaraCorrections.Unlock(_sheet);
            CharacterSheetStore.Save(CharacterSheetStore.FileFor(Folder(), _personaId), _sheet);
            Reread();
        }

        /// <summary>The line under the reading in persona settings.</summary>
        public string StatusLine
        {
            get
            {
                if (_reading)
                    return "Reading her description...";
                if (_sheet != null && _sheet.source != CharacterSheet.SourceModel)
                    return PisaraReading.FallbackStatus;
                var line = PisaraReading.SourceLine(_sheet);
                if (_sheet != null && _sheet.source == CharacterSheet.SourceModel && !string.IsNullOrWhiteSpace(_sheet.readBy))
                    line = "Read by " + _sheet.readBy + ".";
                if (_failure.Length == 0)
                    return line;
                return _sheet != null && _sheet.source == CharacterSheet.SourceModel
                    ? line + " The last Re-read did not work (" + _failure + ")."
                    : line + " (" + _failure + ")";
            }
        }

        void Update()
        {
            _checkIn -= Time.unscaledDeltaTime;
            if (_checkIn > 0f)
                return;
            _checkIn = CheckSeconds;
            Check();
            World();
        }

        // ---- Her world (JUH-118 sections 2 and 3) ------------------------------------------

        bool _worldDirty = true;
        string _arrivedFor;
        CompanionMoodPush[] _awayPushes;
        string _awayPushesFor;

        /// <summary>
        /// Her own life in memory, for the active persona. Runs only after something changed
        /// (a persona switch, a new sheet, Start over): no work and no allocation otherwise.
        /// <list type="number">
        /// <item>Once per arrival (app start or persona switch): short-term memories fade, and
        /// if the user was away long enough one open thread turns out well or badly.</item>
        /// <item>A sheet the model wrote is seeded into her memory once (after the shared-history
        /// guard), and its "right now" becomes the first scene.</item>
        /// </list>
        /// </summary>
        bool _worldSwitch = PisaraSwitches.World;
        bool _awaySwitch = PisaraSwitches.AwayEvents;

        void World()
        {
            DeliverAwayMood();
            // A switch flipped in settings: look again (turning one off freezes it, nothing is deleted).
            if (_worldSwitch != PisaraSwitches.World || _awaySwitch != PisaraSwitches.AwayEvents)
            {
                _worldSwitch = PisaraSwitches.World;
                _awaySwitch = PisaraSwitches.AwayEvents;
                _worldDirty = true;
            }

            if (!_worldDirty || !_seen)
                return;

            if (string.IsNullOrEmpty(_personaId))
            {
                // The setup name has no sheet and no world.
                _worldDirty = false;
                _arrivedFor = _personaId;
                WorldPromptNow.Reset();
                return;
            }

            var store = StoreFor(_personaId);
            if (store == null)
                return; // The memory store still belongs to the previous persona: try next frame.

            _worldDirty = false;
            WorldPromptNow.SetHerName(PersonaName(_personaId));
            var privacy = GetComponent<IPrivacyMode>();
            var keepsLocal = IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);

            if (!string.Equals(_arrivedFor, _personaId, StringComparison.Ordinal))
            {
                _arrivedFor = _personaId;
                Arrive(store, keepsLocal);
            }

            var sheet = _sheet;
            if (sheet == null || sheet.source != CharacterSheet.SourceModel || !PisaraSwitches.World)
            {
                // A fallback sheet has an empty world, and "Her own world" off means no seeding and no scene.
                WorldPromptNow.SetFirstScene(string.Empty);
                return;
            }

            if (WorldMemory.NeedsSeeding(store, sheet))
            {
                var dropped = new List<string>();
                var safe = WorldGuard.ForSeeding(sheet, dropped);
                // JUH-126: the sheet itself says whether it was read locally or in the cloud.
                var seedKeepsLocal = WorldMemory.SeedKeepsLocal(sheet);
                var added = safe == null ? 0 : WorldMemory.Seed(store, safe, DateTime.UtcNow, seedKeepsLocal);
                Debug.Log("Pisara world: " + PersonaLogId(_personaId) + " seeded " + added + " memories"
                    + (dropped.Count == 0 ? string.Empty : " (" + dropped.Count + " left out: they mention the user without a shared past)"));
                Revision++;
            }

            var scenePrompt = WorldGuard.FirstScene(sheet);
            WorldPromptNow.SetFirstScene(scenePrompt,
                scenePrompt.Length == 0 || sheet.world == null ? string.Empty : WorldGuard.Sentence(sheet.world.rightNow));
        }

        /// <summary>
        /// The user comes back to this persona. The time away is counted from her mood file,
        /// which is saved once a minute while she is active and when the app quits.
        /// </summary>
        void Arrive(MemoryStore store, bool keepsLocal)
        {
            WorldPromptNow.SetAway(string.Empty);
            var now = DateTime.UtcNow;
            var faded = WorldMemory.Fade(store, now);
            if (faded > 0)
                Debug.Log("Pisara world: " + PersonaLogId(_personaId) + " forgot " + faded + " short-term memories");

            if (!PisaraSwitches.AwayEvents)
                return;
            var saved = CompanionMoodStore.SavedAtUtc(CompanionMoodStore.FileFor(Folder(), _personaId));
            if (saved == null)
                return;
            var awaySeconds = (now - saved.Value).TotalSeconds;
            if (awaySeconds < WorldMemory.MinAwaySeconds)
                return;

            // A private thread may only come up when this session stays on the local model or the private server.
            var chat = Chat();
            var providerId = chat == null ? string.Empty : PrivateChatRouting.ProviderId(keepsLocal, chat.providerId, chat.privateEndpointType);
            var includePrivate = string.Equals(providerId, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(providerId, ChatDefaults.CustomId, StringComparison.OrdinalIgnoreCase);

            var away = WorldMemory.ResolveThread(store, awaySeconds, UnityEngine.Random.value, now, includePrivate);
            if (!away.Happened)
                return;

            WorldPromptNow.SetAway(away.PromptLine, away.Text);
            _awayPushes = away.MoodPushes;
            _awayPushesFor = _personaId;
            Debug.Log("Away: " + PersonaLogId(_personaId) + " = " + away.Text);
            Revision++;
        }

        /// <summary>The away event's mood nudges, once her mood for this persona is loaded.</summary>
        void DeliverAwayMood()
        {
            if (_awayPushes == null)
                return;
            if (!string.Equals(_awayPushesFor, _personaId, StringComparison.Ordinal))
            {
                _awayPushes = null;
                return;
            }

            var mood = GetComponent<CompanionMoodKeeper>();
            if (mood == null || mood.TryApplyPushes(_personaId, _awayPushes, "what happened while you were away"))
                _awayPushes = null;
        }

        /// <summary>
        /// "Start over" (JUH-117): her memories were just deleted, which also forgets which sheet
        /// her world came from. Seed it again from the cached sheet now, and drop a pending away
        /// line. The chat is empty again, so the first scene opens the next conversation.
        /// </summary>
        public void StartOver()
        {
            WorldPromptNow.SetAway(string.Empty);
            _awayPushes = null;
            _worldDirty = true;
            World();
        }

        /// <summary>The running memory store when it is this persona's file; null while it is still another one's.</summary>
        static MemoryStore StoreFor(string personaId)
        {
            var store = MemoryRuntime.Store;
            var expected = MemoryPaths.MemoryFile(personaId);
            if (store == null || string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(store.Path))
                return null;
            return string.Equals(System.IO.Path.GetFullPath(store.Path), System.IO.Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase)
                ? store
                : null;
        }

        static string PersonaLogId(string personaId)
        {
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return id.Length == 0 ? "(default)" : id;
        }

        void OnDisable()
        {
            CancelReading();
        }

        /// <summary>
        /// Notices a persona switch or a changed prompt. Cheap while nothing changed: two string
        /// comparisons that hit the same references, and no allocation.
        /// </summary>
        void Check()
        {
            var settings = CurrentSettings();
            var chat = Chat();
            if (settings == null || chat == null)
                return;

            var persona = settings.PersonaId ?? string.Empty;
            var prompt = chat.systemPrompt ?? string.Empty;
            if (_seen && string.Equals(persona, _personaId, StringComparison.Ordinal) && string.Equals(prompt, _prompt, StringComparison.Ordinal))
                return;

            _seen = true;
            _personaId = persona;
            _prompt = prompt;
            _worldDirty = true;
            Begin(false);
        }

        /// <summary>The "Re-read" button: asks the model again for the active persona.</summary>
        public void Reread()
        {
            Check();
            if (string.IsNullOrEmpty(_personaId))
                return;
            Begin(true);
        }

        void Begin(bool force)
        {
            CancelReading();
            _failure = string.Empty;
            if (string.IsNullOrEmpty(_personaId))
            {
                // The setup name is not a persona: no reading, and the temperament stays balanced.
                SetSheet(null);
                return;
            }

            var path = CharacterSheetStore.FileFor(Folder(), _personaId);
            var cached = force ? null : CharacterSheetStore.LoadFor(path, _prompt);
            if (cached != null && cached.source == CharacterSheet.SourceModel)
            {
                SetSheet(cached);
                return;
            }

            var key = _personaId + "\n" + CharacterSheet.HashPrompt(_prompt);
            if (!PisaraReading.ShouldRetryFallback(cached, force, ProviderReady(), _tried.Contains(key)))
            {
                SetSheet(cached);
                return;
            }

            // A fallback sheet is used while the one retry for this launch runs.
            SetSheet(cached ?? CharacterSheet.Neutral(_prompt));
            _tried.Add(key);
            Read(_personaId, _prompt, path);
        }

        async void Read(string persona, string prompt, string path)
        {
            var generation = ++_generation;
            var cancel = new CancellationTokenSource();
            _cancel = cancel;
            _reading = true;
            ReadingStartedAt = Time.unscaledTime;
            Revision++;

            PisaraReadResult result;
            PisaraModelChoice choice = default(PisaraModelChoice);
            var readKeepsLocal = true;
            try
            {
                choice = Choice();
                var privacy = GetComponent<IPrivacyMode>();
                var keepsLocal = choice.UseCloud
                    ? false
                    : IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
                readKeepsLocal = keepsLocal;
                var intimateActive = choice.UseCloud ? false : IntimateExpressions.Active;
                // JUH-157: her lorebook is read too; an 18+ one only on a local reading with the gate open.
                result = await PisaraReader.ReadAsync(
                    Provider(choice, keepsLocal),
                    PersonaName(persona),
                    LoreReading.Append(prompt, LoreNow.Book, keepsLocal && intimateActive),
                    keepsLocal,
                    intimateActive,
                    PisaraKeywords.Temperament,
                    cancel.Token);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Pisara reading failed (" + e.GetType().Name + ")");
                result = new PisaraReadResult
                {
                    Sheet = PisaraReading.Fallback(prompt, null, DateTime.UtcNow),
                    Failure = "the call failed",
                };
            }

            if (this == null || generation != _generation || cancel.IsCancellationRequested)
            {
                cancel.Dispose();
                return;
            }

            _reading = false;
            _cancel = null;
            cancel.Dispose();
            _failure = result.Failure ?? string.Empty;
            if (result.Sheet != null && result.Sheet.source == CharacterSheet.SourceModel)
            {
                result.Sheet.readInCloud = PisaraModelChoice.ReadInCloud(choice, readKeepsLocal);
                result.Sheet.readBy = ReadBy(choice, readKeepsLocal);
            }

            // The user's relationship correction survives a new reading, even of an edited prompt.
            PisaraCorrections.CarryOver(CharacterSheetStore.Load(path), result.Sheet);
            // A failed Re-read keeps the sheet the model wrote earlier for this prompt.
            var kept = PisaraReading.Keep(CharacterSheetStore.LoadFor(path, prompt), result.Sheet);
            if (ReferenceEquals(kept, result.Sheet) && !CharacterSheetStore.Save(path, kept))
                Debug.LogWarning("Pisara reading: the sheet could not be saved.");
            Debug.Log(PisaraReading.LogLine(persona, kept)
                + (_failure.Length == 0 ? string.Empty : " (" + _failure + (ReferenceEquals(kept, result.Sheet) ? ")" : "; kept the earlier reading)")));
            SetSheet(kept);
        }

        void SetSheet(CharacterSheet sheet)
        {
            _sheet = sheet;
            _worldDirty = true;
            Revision++;
            var mood = GetComponent<CompanionMoodKeeper>();
            if (mood != null)
                mood.ReloadTemperament();
        }

        void CancelReading()
        {
            _generation++;
            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
                _cancel = null;
            }

            if (_reading)
            {
                _reading = false;
                Revision++;
            }
        }

        // ---- JUH-151: the Persona Studio's Pisara step reads a draft the same way -----------

        bool DraftKeepsLocal(PisaraModelChoice choice)
        {
            var privacy = GetComponent<IPrivacyMode>();
            return choice.UseCloud ? false : IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
        }

        /// <summary>Who reads a Studio draft, such as "deepseek-flash (chat, cloud)", or why no one can.</summary>
        public string DraftReaderLabel(out bool ready)
        {
            var choice = Choice();
            var keepsLocal = DraftKeepsLocal(choice);
            var provider = Provider(choice, keepsLocal);
            ready = provider != null && provider.IsConfigured && PisaraReader.MayUse(provider, keepsLocal);
            return ready ? ReadBy(choice, keepsLocal) : "no model is ready";
        }

        /// <summary>
        /// Reads a draft that isn't saved yet. Same model, privacy rule and parser as a normal
        /// reading; nothing is stored here (the Studio saves the sheet with the persona).
        /// </summary>
        public Task<PisaraReadResult> ReadDraftAsync(string name, string text, CancellationToken token)
        {
            return ReadDraftAsync(name, text, null, token);
        }

        /// <summary>
        /// JUH-157: as above, with the draft's lorebook read too. An 18+ lorebook is added only
        /// when the reading stays local with the 18+ gate open.
        /// </summary>
        public async Task<PisaraReadResult> ReadDraftAsync(string name, string text, Lorebook lore, CancellationToken token)
        {
            var choice = Choice();
            var keepsLocal = DraftKeepsLocal(choice);
            var intimateActive = choice.UseCloud ? false : IntimateExpressions.Active;
            text = LoreReading.Append(text, lore, keepsLocal && intimateActive);
            var result = await PisaraReader.ReadAsync(Provider(choice, keepsLocal), name, text, keepsLocal, intimateActive,
                PisaraKeywords.Temperament, token);
            if (result != null && result.Sheet != null && result.Sheet.source == CharacterSheet.SourceModel)
            {
                result.Sheet.readInCloud = PisaraModelChoice.ReadInCloud(choice, keepsLocal);
                result.Sheet.readBy = ReadBy(choice, keepsLocal);
            }

            return result;
        }

        /// <summary>Condenses a long draft description with the same model and privacy rule.</summary>
        public Task<PersonaCondenseResult> CondenseDraftAsync(string name, string description, CancellationToken token)
        {
            var choice = Choice();
            var keepsLocal = DraftKeepsLocal(choice);
            var intimateActive = choice.UseCloud ? false : IntimateExpressions.Active;
            return PersonaCondenser.CondenseAsync(Provider(choice, keepsLocal), name, description, PersonaDescription.ModelBudget,
                keepsLocal, intimateActive, token);
        }

        /// <summary>What the chat path and the separate Pisara cloud setting say for this read.</summary>
        PisaraModelChoice Choice()
        {
            var privacy = GetComponent<IPrivacyMode>();
            var privateMode = privacy != null && privacy.PrivateMode;
            var ownKey = _pisaraKeys.TryGetOwn(out _);
            var grokKey = _pisaraKeys.TryGetGrok(out _);
            return PisaraModelChoice.For(CurrentSettings(), privateMode, IntimateExpressions.Active, ownKey, grokKey);
        }

        static string ReadBy(PisaraModelChoice choice, bool keepsLocal)
        {
            var model = string.IsNullOrWhiteSpace(choice.Model) ? "the chat model" : choice.Model;
            var where = choice.UseCloud ? "cloud" : keepsLocal ? "chat" : "chat, cloud";
            return model + " (" + where + ")";
        }

        string KeyFor(PisaraModelChoice choice)
        {
            string key;
            if (choice.KeySource == PisaraKeySource.Pisara && _pisaraKeys.TryGetOwn(out key))
                return key;
            if (choice.KeySource == PisaraKeySource.Grok && _pisaraKeys.TryGetGrok(out key))
                return key;
            return null;
        }

        /// <summary>
        /// The provider this reading may use. A Pisara cloud choice gets its own provider; every
        /// other choice keeps the persona's chat provider and the existing privacy rule.
        /// </summary>
        IChatProvider Provider(PisaraModelChoice choice, bool keepsLocal)
        {
            if (choice.UseCloud)
                return PisaraCloudProvider.Create(choice.BaseUrl, choice.Model, () => KeyFor(choice), new UnityChatTransport(this));

            var chat = Chat();
            if (chat == null)
                return null;
            if (_registry == null || !ReferenceEquals(_registryFor, chat))
            {
                _registry = ChatProviderFactory.Create(chat, new EnvironmentApiKeySource(), new UnityChatTransport(this), null);
                _registryFor = chat;
            }

            var id = PrivateChatRouting.ProviderId(keepsLocal, chat.providerId, chat.privateEndpointType);
            IChatProvider provider;
            return _registry.TryGet(id, out provider) ? provider : null;
        }

        /// <summary>
        /// JUH-171: the model for the everyday voice reading: the same as her description's, but
        /// never the cloud in private mode (only the description reading may go there, JUH-126).
        /// <paramref name="keepsLocal"/> says whether a cloud provider must be refused.
        /// </summary>
        public IChatProvider VoiceProvider(out bool keepsLocal, out string readBy)
        {
            var privacy = GetComponent<IPrivacyMode>();
            var privateMode = privacy != null && privacy.PrivateMode;
            var choice = privateMode ? default(PisaraModelChoice) : Choice();
            keepsLocal = choice.UseCloud ? false : IntimateExpressions.KeepsLocal(privateMode);
            readBy = ReadBy(choice, keepsLocal);
            return Provider(choice, keepsLocal);
        }

        /// <summary>JUH-171: the local model or the private server, for the 18+ voice reading.</summary>
        public IChatProvider LocalVoiceProvider(out string readBy)
        {
            readBy = ReadBy(default(PisaraModelChoice), true);
            return Provider(default(PisaraModelChoice), true);
        }

        /// <summary>The persona's display name, for the readings that run after this one.</summary>
        public string NameFor(string personaId)
        {
            return PersonaName(personaId);
        }

        /// <summary>True when a fallback can be retried without violating the privacy rule.</summary>
        bool ProviderReady()
        {
            var choice = Choice();
            var privacy = GetComponent<IPrivacyMode>();
            var keepsLocal = choice.UseCloud
                ? false
                : IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
            var provider = Provider(choice, keepsLocal);
            return provider != null && provider.IsConfigured && PisaraReader.MayUse(provider, keepsLocal);
        }

        string PersonaName(string personaId)
        {
            var settings = CurrentSettings();
            var persona = PersonaCatalog.LoadDefault(Folder()).Find(personaId);
            if (persona != null && !string.IsNullOrWhiteSpace(persona.DisplayName))
                return persona.DisplayName;
            return settings != null ? settings.CompanionName : string.Empty;
        }

        CompanionSettings CurrentSettings()
        {
            if (_settings == null)
                _settings = GetComponent<SettingsPanel>();
            return _settings != null ? _settings.EffectiveSettings ?? _settings.CurrentSettings : null;
        }

        ChatSettings Chat()
        {
            if (_chat == null)
                _chat = GetComponent<ChatPanel>();
            return _chat != null ? _chat.RuntimeChat : null;
        }

        static string Folder()
        {
            return PersonaRuntime.DefaultSettingsFolder();
        }
    }
}
