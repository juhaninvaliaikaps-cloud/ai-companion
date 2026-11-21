using System;
using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.Overlay;
using AICompanion.Settings;
using AICompanion.Settings.Presenters;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>
    /// JUH-104 PR D: the radial wheels. Right-click on her (only over her: the overlay calls
    /// <see cref="AvatarContextMenu"/> only for a visible pixel, everywhere else stays
    /// click-through) opens the main wheel above her head with the eight fixed slices. A
    /// JUH-101 hit zone opens a body-part wheel instead, once one is registered. The wheel
    /// follows her, stays inside the window near monitor edges, and closes on Escape, a click
    /// outside it, or after an action. Every slice calls the PR B presenters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HappiWheelHost : MonoBehaviour
    {
        const float PlaneDistance = 0.42f;
        const int MaxSlicesPerWheel = 8;
        const float NoteSeconds = 4f;

        ChatPanel _chat;
        SettingsPanel _settings;
        OverlayWindow _window;
        HappiTheme _theme;
        HeadLocator _head;
        GameObject _canvasObject;
        Canvas _canvas;
        RadialWheelView _wheel;
        WheelCard _card;
        ProfilePanel _profile;
        AvatarInteractions _interactions;
        readonly Stack<Func<WheelDefinition>> _back = new Stack<Func<WheelDefinition>>();
        Func<WheelDefinition> _currentBuilder;
        bool _attached;
        bool _open;
        bool _partWheel;
        Vector2 _clickPoint;
        Vector2 _partAnchor;
        bool _leftWas;
        bool _rightWas;
        float _scale = 1f;
        float _noteUntil = -1f;

        /// <summary>Opens settings. The new settings view (PR E) replaces this; default is the old panel.</summary>
        public Action OpenSettings;

        public bool IsOpen => _open;

        public void Attach(ChatPanel chat, SettingsPanel settings)
        {
            if (_attached)
                return;
            _chat = chat;
            _settings = settings;
            _window = GetComponent<OverlayWindow>();
            _theme = HappiTheme.Current;
            _head = new HeadLocator(gameObject);
            _canvasObject = HappiUiKit.CreateCanvas(transform, "HappiWheelCanvas", 70, out _canvas);
            _card = new WheelCard(_canvasObject.transform, _theme);
            _profile = new ProfilePanel(_canvasObject.transform, _theme)
            {
                OnClose = Back,
                OpenSettings = () => Do(() => { if (OpenSettings != null) OpenSettings(); }),
            };
            if (OpenSettings == null)
                OpenSettings = () => { if (_settings != null) _settings.Open(); };
            _interactions = GetComponent<AvatarInteractions>();
            if (_interactions == null)
                _interactions = gameObject.AddComponent<AvatarInteractions>();
            AvatarContextMenu.Handler = OnRightClickOnHer;
            _attached = true;
            Debug.Log("New UI: radial wheel on (right-click head, Shift+right-click anywhere; body zones are quick touch)");
        }

        public void Detach()
        {
            if (!_attached)
                return;
            _attached = false;
            if (AvatarContextMenu.Handler == OnRightClickOnHer)
                AvatarContextMenu.Handler = null;
            Close();
            if (_canvasObject != null)
                Destroy(_canvasObject);
            _canvasObject = null;
            Debug.Log("New UI: radial wheel off");
        }

        void OnDestroy()
        {
            Detach();
        }

        /// <summary>Called by the overlay for a right-click on a visible pixel of her.</summary>
        bool OnRightClickOnHer(Vector2 unityScreen)
        {
            if (!_attached)
                return false;

            // Shift is the fallback full wheel; it used to be rotate mode, which the 2D
            // portrait does not need.
            if (ModifierKeys.ShiftHeld())
            {
                OpenAt(unityScreen, false, BuildMain);
                return true;
            }

            EnsureHitZones();
            var zones = BodyPartWheels.HitZones;
            BodyPart part;
            Vector2 anchor;
            if (zones != null && zones.TryHit(unityScreen, out part, out anchor))
            {
                if (part == BodyPart.Head)
                    OpenAt(unityScreen, false, BuildMain);
                else
                    OpenAt(anchor, true, () => BuildPartWheel(part));
                return true;
            }

            // A miss on the playing portrait must not fall through to rotate mode.
            // While the portrait is not playing, the 3D model still uses rotate.
            return ConsumeMissedPortraitClick(IsPortraitPlaying());
        }

        /// <summary>
        /// A right-click on the playing 2D portrait is consumed even when it misses a zone,
        /// so rotate mode cannot take the next left-drag. While the portrait is not playing,
        /// a miss is left for rotate on the 3D model.
        /// </summary>
        public static bool ConsumeMissedPortraitClick(bool portraitPlaying)
        {
            return portraitPlaying;
        }

        bool IsPortraitPlaying()
        {
            var video = GetComponent<VideoAvatar>();
            return video != null && video.State == VideoAvatarState.Playing;
        }

        void OpenAt(Vector2 click, bool partWheel, Func<WheelDefinition> build)
        {
            _clickPoint = click;
            _back.Clear();
            _partWheel = partWheel;
            _partAnchor = click;
            Show(build);
            _open = true;
            _leftWas = true;
            _rightWas = true;
            _pressed = NoPress;
            Debug.Log("Wheel: opened " + (partWheel ? "body-part wheel" : "main wheel"));
            var typing = GetComponent<ITypingFocus>();
            if (typing != null)
                typing.FocusForTyping();
        }

        void EnsureHitZones()
        {
            if (BodyPartWheels.HitZones != null)
                return;
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var zones = behaviours[i] as IBodyHitZones;
                if (zones != null)
                {
                    BodyPartWheels.HitZones = zones;
                    return;
                }
            }
        }

        void Update()
        {
            if (!_attached || !_open || _canvasObject == null)
                return;

            HappiUiKit.FollowCamera(_canvas, PlaneDistance);
            var pointer = Pointer();
            Place();
            if (_wheel != null)
                _wheel.Tick(PanelVisible ? (Vector2?)null : pointer, Time.unscaledDeltaTime);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Back();
                return;
            }

            if (_noteUntil > 0f && Time.unscaledTime > _noteUntil)
            {
                _noteUntil = -1f;
                Back();
                return;
            }

            var left = LeftHeld();
            var right = RightHeld();
            var leftDown = left && !_leftWas;
            var rightDown = right && !_rightWas;
            var leftUp = !left && _leftWas;
            _leftWas = left;
            _rightWas = right;

            // A slice acts when the button is released over the slice it was pressed on, so the
            // release cannot fall through into a panel the slice just opened.
            if (leftUp && _pressed != NoPress)
            {
                var pressed = _pressed;
                _pressed = NoPress;
                if (_wheel != null && !PanelVisible && pointer.HasValue && _wheel.HitTest(pointer.Value) == pressed)
                    Activate(pressed);
                return;
            }

            if (!leftDown && !rightDown)
                return;

            var onCard = PanelVisible && pointer.HasValue && (_card.Contains(pointer.Value) || _profile.Contains(pointer.Value));
            var onWheel = !PanelVisible && _wheel != null && pointer.HasValue && _wheel.Contains(pointer.Value);
            if (onCard)
                return;
            if (!onWheel)
            {
                Debug.Log("Wheel: closed by a click outside");
                Close();
                return;
            }

            if (rightDown)
                return;

            _pressed = _wheel.HitTest(pointer.Value);
        }

        const int NoPress = int.MinValue;
        int _pressed = NoPress;

        void Activate(int hit)
        {
            if (hit == RadialMath.Center)
            {
                var center = _wheel.Definition.OnCenter;
                if (center != null)
                    center();
                else
                    Back();
                return;
            }

            if (hit < 0 || hit >= _wheel.Slices.Count)
                return;
            Choose(_wheel.Slices[hit]);
        }

        void Choose(WheelSlice slice)
        {
            Debug.Log("Wheel: chose " + slice.Id + (slice.Enabled ? string.Empty : " (refused)"));
            if (!slice.Enabled)
            {
                Note(slice.Label, string.IsNullOrEmpty(slice.DisabledReason) ? "Not now." : slice.DisabledReason);
                return;
            }

            if (slice.OnChoose != null)
                slice.OnChoose();
        }

        // ---- Showing ---------------------------------------------------------------------

        /// <summary>A card or the Profile panel is up instead of the wheel.</summary>
        bool PanelVisible => (_card != null && _card.Visible) || (_profile != null && _profile.Visible);

        void Show(Func<WheelDefinition> build)
        {
            _card.Hide();
            _profile.Hide();
            _noteUntil = -1f;
            if (_wheel != null)
                _wheel.Destroy();
            _currentBuilder = build;
            var definition = build();
            _wheel = new RadialWheelView(_canvasObject.transform, definition, _partWheel ? WheelSize.Part : WheelSize.Main, IntimateExpressions.Active, _theme);
            _scale = -1f;
            Place();
        }

        void Push(Func<WheelDefinition> build)
        {
            if (_currentBuilder != null)
                _back.Push(_currentBuilder);
            Show(build);
        }

        void Back()
        {
            if (_profile.Visible)
            {
                _profile.Hide();
                if (_wheel != null)
                    _wheel.Root.gameObject.SetActive(true);
                return;
            }

            if (_card.Visible)
            {
                _card.Hide();
                _noteUntil = -1f;
                if (_wheel != null)
                    _wheel.Root.gameObject.SetActive(true);
                return;
            }

            if (_back.Count > 0)
            {
                Show(_back.Pop());
                return;
            }

            Close();
        }

        public void Close()
        {
            _open = false;
            _back.Clear();
            _currentBuilder = null;
            _noteUntil = -1f;
            if (_card != null)
                _card.Hide();
            if (_profile != null)
                _profile.Hide();
            if (_wheel != null)
                _wheel.Destroy();
            _wheel = null;
        }

        /// <summary>Runs an action and closes the wheel ("closes after an action").</summary>
        void Do(Action action)
        {
            Close();
            if (action != null)
                action();
        }

        void Note(string title, string text)
        {
            ShowCard(title, text, new[] { new CardButton("OK", Back) });
            _noteUntil = Time.unscaledTime + NoteSeconds;
        }

        void ShowCard(string title, string text, IReadOnlyList<CardButton> buttons)
        {
            _noteUntil = -1f;
            if (_wheel != null)
                _wheel.Root.gameObject.SetActive(false);
            var center = _wheel != null ? _wheel.Center : _clickPoint;
            _card.Show(title, text, buttons, center, Mathf.Max(1f, _scale), new Vector2(Screen.width, Screen.height));
        }

        void Place()
        {
            if (_wheel == null)
                return;
            var margin = _theme.WheelEdgeMargin;
            var outerBase = _partWheel ? _theme.PartWheelOuterRadius : _theme.WheelOuterRadius;
            var scale = RadialMath.FitScale(_theme.UiScale(Screen.dpi), outerBase + _theme.WheelGlow + _theme.WheelHoverPop, Screen.width, Screen.height, margin);
            var outer = (outerBase + _theme.WheelHoverPop) * scale;
            Vector2 center;
            if (_partWheel)
            {
                center = RadialMath.PlaceBeside(_partAnchor, _theme.PartWheelOffset * scale, outer, Screen.width, Screen.height, margin);
            }
            else
            {
                Vector2 head;
                if (!_head.TryGet(Time.unscaledDeltaTime, out head))
                    head = _clickPoint;
                center = RadialMath.PlaceAbove(head, _theme.WheelAboveHead * scale + 28f * scale, outer, Screen.width, Screen.height, margin);
            }

            // The window can reach past the screen edge (side room for the chat box): keep the wheel on screen.
            center = ChatBoxPlacement.Clamp(center, new Vector2(outer * 2f, outer * 2f), OverlayVisibleArea.Get(_window), margin);

            _scale = scale;
            _wheel.Layout(center, scale);
            _wheel.SetConnector(_partWheel ? (Vector2?)_partAnchor : null, true);
        }

        Vector2? Pointer()
        {
            var cursor = GetComponent<IScreenCursor>();
            float x;
            float y;
            if (cursor != null && cursor.TryGetUnityScreenCursor(out x, out y))
                return new Vector2(x, y);
            var mouse = Input.mousePosition;
            return new Vector2(mouse.x, mouse.y);
        }

        bool LeftHeld()
        {
            var chrome = _window != null ? _window.Chrome : null;
            if (chrome != null && chrome.IsSupported)
                return chrome.IsLeftButtonHeld();
            return Input.GetMouseButton(0);
        }

        bool RightHeld()
        {
            var chrome = _window != null ? _window.Chrome : null;
            if (chrome != null && chrome.IsSupported)
                return chrome.IsRightButtonHeld();
            return Input.GetMouseButton(1);
        }

        // ---- Main wheel ------------------------------------------------------------------

        WheelDefinition BuildMain()
        {
            var settings = _settings != null ? _settings.CurrentSettings : null;
            var paused = settings != null && settings.Paused;
            var chatVisible = _chat != null && _chat.ChatVisible;
            var personaId = settings != null ? settings.PersonaId : string.Empty;
            var definition = new WheelDefinition { CenterLabel = HappiNames.Companion(_settings), CenterHint = "head / Shift+right-click" };
            for (var i = 0; i < MainWheel.Order.Count; i++)
            {
                var id = MainWheel.Order[i];
                var gate = WheelSliceGates.Evaluate(id, CompanionMoodNow.Shown, CompanionMoodNow.Strength, personaId);
                definition.Slices.Add(new WheelSlice
                {
                    Id = id,
                    Label = MainWheel.Label(id, paused, chatVisible),
                    Enabled = gate.Enabled,
                    DisabledReason = gate.Reason,
                    RequiresIntimateGate = id == MainWheel.Intimate,
                    OnChoose = MainAction(id, paused, chatVisible)
                });
            }

            definition.OnCenter = Close;
            return definition;
        }

        /// <summary>
        /// JUH-121: each slice does exactly what its label says. The chat and pause slices
        /// toggle in one click; the wheel is rebuilt on every open, so their labels stay current.
        /// </summary>
        Action MainAction(string id, bool paused, bool chatVisible)
        {
            switch (id)
            {
                case MainWheel.Settings:
                    return () => Do(OpenSettings);
                case MainWheel.Talk:
                    return () => Do(() => { if (_chat != null) _chat.ShowChat(!chatVisible); });
                case MainWheel.Profile:
                    return ShowProfile;
                case MainWheel.WatchScreen:
                    return () => Push(BuildScreen);
                case MainWheel.Persona:
                    return () => Push(BuildPersona);
                case MainWheel.Interactions:
                    return () => Push(BuildInteractions);
                case MainWheel.Intimate:
                    return () => Push(BuildIntimate);
                case MainWheel.Pause:
                    return () => Do(() => _settings.Presenter.SetPaused(!paused));
                default:
                    return null;
            }
        }

        WheelDefinition Sub(string title)
        {
            return new WheelDefinition { CenterLabel = title, CenterHint = "back", OnCenter = Back };
        }

        // Watch my screen: toggle the screen look and pick a monitor (B7: never grants consent).
        WheelDefinition BuildScreen()
        {
            var look = _settings.ScreenLook;
            var definition = Sub("Screen");
            definition.Slices.Add(new WheelSlice
            {
                Id = "screen_toggle",
                Label = look.Enabled ? "Stop watching" : "Watch my screen",
                Enabled = look.Available,
                DisabledReason = ScreenLookPresenter.Unavailable,
                OnChoose = () => { look.Toggle(); Note("Screen", look.StatusText); }
            });
            definition.Slices.Add(new WheelSlice
            {
                Id = "screen_look",
                Label = look.Busy ? "Stop look" : "Look now",
                Enabled = look.Enabled,
                DisabledReason = "Turn screen watching on first.",
                OnChoose = () =>
                {
                    if (look.LookNow())
                        Close();
                    else
                        Note("Screen", "She cannot look right now. Pick a window or display in Settings > Screen. " + look.StatusText);
                }
            });
            definition.Slices.Add(new WheelSlice { Id = "screen_monitor", Label = "Pick a monitor", OnChoose = ShowMonitors });
            return definition;
        }

        void ShowMonitors()
        {
            string note;
            var monitors = _settings.ScreenLook.Monitors(out note);
            var buttons = new List<CardButton>();
            for (var i = 0; i < monitors.Count; i++)
            {
                var index = monitors[i].Index;
                buttons.Add(new CardButton((monitors[i].Selected ? "✓ " : string.Empty) + monitors[i].Label, () =>
                {
                    _settings.ScreenLook.SelectMonitor(index);
                    ShowMonitors();
                }));
            }

            if (monitors.Count == 0)
                buttons.Add(new CardButton("Open settings", () => Do(OpenSettings)));
            buttons.Add(new CardButton("Back", Back));
            ShowCard("Pick a monitor", note, buttons);
        }

        void ShowProfile()
        {
            _noteUntil = -1f;
            _card.Hide();
            if (_wheel != null)
                _wheel.Root.gameObject.SetActive(false);
            var center = _wheel != null ? _wheel.Center : _clickPoint;
            _profile.Show(BuildProfileData, center, Mathf.Max(1f, _scale), new Vector2(Screen.width, Screen.height));
            Debug.Log("Wheel: opened the Profile panel");
        }

        /// <summary>Gathers the Profile panel's data from the keepers. Runs when the panel or a tab opens, never per frame.</summary>
        ProfileData BuildProfileData()
        {
            var data = new ProfileData { Name = HappiNames.Companion(_settings) };
            // JUH-154: her picture, from the shared cache (loaded once per persona or picture change).
            var personaId = _settings != null && _settings.CurrentSettings != null ? _settings.CurrentSettings.PersonaId : string.Empty;
            if (!string.IsNullOrEmpty(personaId))
            {
                var persona = _settings.PersonaCatalogNow().Find(personaId);
                data.Picture = PersonaPictureCache.Get(_settings.PersonasFolderPath, persona);
                data.PictureRound = persona == null || PersonaPictureShapes.Normalize(persona.PictureShape) == PersonaPictureShapes.Round;
            }
            var reading = GetComponent<PisaraReadingKeeper>();
            var sheet = reading != null ? reading.Sheet : null;
            data.RoleLine = sheet != null ? ProfileHeader.RoleLine(sheet.relationship) : string.Empty;

            var moods = new CompanionMood[3];
            var strengths = new float[3];
            var mood = GetComponent<CompanionMoodKeeper>();
            if (mood != null && mood.State != null)
                mood.State.Top3(moods, strengths);
            var relationship = GetComponent<RelationshipKeeper>();
            var loaded = relationship != null && relationship.IsLoaded;
            data.Feelings = ProfileFeelings.Build(
                loaded ? relationship.State.Affection : 0f,
                loaded ? relationship.State.Familiarity : 0f,
                CompanionMoodNow.Shown, moods, strengths,
                loaded ? relationship.History : null,
                PisaraSwitches.Relationship);

            var innocence = GetComponent<InnocenceKeeper>();
            var gate = IntimateExpressions.Active && innocence != null && innocence.IsLoaded;
            var pisaraView = _settings != null ? _settings.PisaraView() : default(SettingsPanel.PisaraPersonaView);
            var temperamentLine = pisaraView.TemperamentLine ?? string.Empty;
            data.Personality = ProfilePersonality.Build(sheet, temperamentLine, gate, gate ? innocence.State.Current : InnocenceMath.Default, pisaraView.Axes);
            data.Secrets = loaded ? relationship.SecretsView() : null;
            var intimateSecrets = GetComponent<IntimateSecretsKeeper>();
            data.IntimateSecrets = gate && intimateSecrets != null ? intimateSecrets.View(innocence.State.Current) : null;

            var store = MemoryRuntime.Store;
            var entries = store != null ? store.Entries : null;
            data.World = ProfileWorld.Build(entries, sheet, DateTime.UtcNow);
            int total;
            data.Memories.AddRange(ProfileMemories.AboutYou(entries, ProfileMemories.MaxShown, out total));
            data.MemoryCount = total;
            data.MemoriesSummary = ProfileMemories.Summary(total);

            var timeline = RelationshipTimeline.ForStore(store);
            data.TimelineCount = timeline.Count;
            var recent = timeline.Page(6);
            for (var i = 0; i < recent.Count; i++)
            {
                var entry = recent[i];
                DateTime at;
                var when = DateTime.TryParse(entry.happenedAtUtc, out at)
                    ? at.ToLocalTime().ToString("d.M.yyyy")
                    : string.Empty;
                var kind = MemoryDurability.KindLabel(entry.kind);
                data.Timeline.Add((when.Length > 0 ? when + " — " : string.Empty)
                    + "[" + kind + "] " + entry.summary);
            }
            return data;
        }

        WheelDefinition BuildPersona()
        {
            var definition = Sub("Persona");
            var choices = _settings.Personas.Choices();
            var room = choices.Count > MaxSlicesPerWheel ? MaxSlicesPerWheel - 1 : choices.Count;
            for (var i = 0; i < room; i++)
            {
                var choice = choices[i];
                definition.Slices.Add(new WheelSlice
                {
                    Id = "persona_" + i,
                    Label = choice.IsSetupName ? "Setup name" : StripActive(choice.Label),
                    Enabled = !choice.Active,
                    DisabledReason = "Already active.",
                    OnChoose = () => Do(() => _settings.Personas.Choose(choice.Id))
                });
            }

            if (choices.Count > room)
                definition.Slices.Add(new WheelSlice { Id = "persona_more", Label = "More…", OnChoose = () => Do(OpenSettings) });
            return definition;
        }

        /// <summary>
        /// JUH-101: one flat menu of touch actions. The hold/let-go pair is stateful; every
        /// other item is a one-shot reaction that also sends its event line to the model.
        /// </summary>
        WheelDefinition BuildInteractions()
        {
            var definition = Sub("Interactions");
            var items = AvatarInteractionCatalog.All;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Adult)
                    continue;
                var available = _interactions != null && _interactions.CanRun(item.Id);
                definition.Slices.Add(new WheelSlice
                {
                    Id = item.Id,
                    Label = item.Label,
                    Enabled = available,
                    DisabledReason = item.Release ? "You are not holding her hand." : "She cannot do that right now.",
                    OnChoose = () => Do(() =>
                    {
                        if (_interactions != null)
                            _interactions.Run(item.Id);
                    })
                });
            }

            return definition;
        }

        /// <summary>The 18+ wheel. It only exists while the intimate gate is open.</summary>
        WheelDefinition BuildIntimate()
        {
            var definition = Sub("18+");
            var ids = new[]
            {
                AvatarInteractionCatalog.TouchBreast,
                AvatarInteractionCatalog.TouchVagina,
                AvatarInteractionCatalog.TouchButt,
                AvatarInteractionCatalog.FingerMouth,
            };
            for (var i = 0; i < ids.Length; i++)
            {
                var item = AvatarInteractionCatalog.Find(ids[i]);
                if (item == null)
                    continue;
                var backOnly = item.Id == AvatarInteractionCatalog.TouchButt;
                var canRun = _interactions != null && _interactions.CanRun(item.Id);
                definition.Slices.Add(new WheelSlice
                {
                    Id = item.Id,
                    Label = item.Label,
                    Enabled = canRun,
                    DisabledReason = backOnly ? "Turn her around first." : "Turn on Private mode and the adult portrait set first.",
                    RequiresIntimateGate = true,
                    OnChoose = () => Do(() =>
                    {
                        if (_interactions != null)
                            _interactions.Run(item.Id);
                    })
                });
            }

            return definition;
        }

        /// <summary>The local wheel for a body zone. The head uses the full wheel instead.</summary>
        WheelDefinition BuildPartWheel(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Hands:
                    return PartWheel("Hands", AvatarInteractionCatalog.HoldHand, AvatarInteractionCatalog.LetGo);
                case BodyPart.Lips:
                    return PartWheel("Lips", AvatarInteractionCatalog.Kiss, AvatarInteractionCatalog.FingerMouth);
                case BodyPart.Face:
                case BodyPart.Cheeks:
                    return PartWheel("Face", AvatarInteractionCatalog.CupCheek, AvatarInteractionCatalog.TouchFace);
                case BodyPart.Shoulder:
                    return PartWheel("Shoulder", AvatarInteractionCatalog.TouchShoulder);
                case BodyPart.Chest:
                    return PartWheel("18+", AvatarInteractionCatalog.TouchBreast);
                case BodyPart.Crotch:
                    return PartWheel("18+", AvatarInteractionCatalog.TouchVagina);
                case BodyPart.Butt:
                    return PartWheel("18+", AvatarInteractionCatalog.TouchButt);
                default:
                    return Sub("Touch");
            }
        }

        WheelDefinition PartWheel(string title, params string[] ids)
        {
            var definition = Sub(title);
            for (var i = 0; i < ids.Length; i++)
            {
                var item = AvatarInteractionCatalog.Find(ids[i]);
                if (item == null)
                    continue;
                var available = _interactions != null && _interactions.CanRun(item.Id);
                var slice = PartSlice(item, available);
                slice.OnChoose = () => Do(() =>
                {
                    if (_interactions != null)
                        _interactions.Run(item.Id);
                });
                definition.Slices.Add(slice);
            }

            return definition;
        }

        /// <summary>
        /// One quick-touch slice. Adult actions take the same 18+ gate as the main wheel,
        /// so they are omitted while that gate is closed instead of shown greyed out.
        /// </summary>
        public static WheelSlice PartSlice(AvatarInteractionDefinition item, bool available)
        {
            return new WheelSlice
            {
                Id = item.Id,
                Label = item.Label,
                Enabled = available,
                DisabledReason = item.Release ? "You are not holding her hand." : "She cannot do that right now.",
                RequiresIntimateGate = item.Adult,
            };
        }

        static string StripActive(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;
            return label.Replace(" (active)", string.Empty).Replace(" (saved)", string.Empty);
        }
    }

    /// <summary>Her name for headers and the wheel centre: persona display name, setup name, or Leia.</summary>
    public static class HappiNames
    {
        public static string Companion(SettingsPanel settings)
        {
            if (settings != null)
            {
                var persona = settings.Personas.ActiveDisplayName();
                if (!string.IsNullOrEmpty(persona))
                    return persona;
                var current = settings.CurrentSettings;
                if (current != null && !string.IsNullOrEmpty(current.CompanionName))
                    return current.CompanionName;
            }

            return CompanionPersona.DefaultName;
        }
    }
}
