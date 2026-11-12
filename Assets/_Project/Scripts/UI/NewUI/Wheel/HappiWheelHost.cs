using System;
using System.Collections.Generic;
using AICompanion.Core;
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
            if (OpenSettings == null)
                OpenSettings = () => { if (_settings != null) _settings.Open(); };
            AvatarContextMenu.Handler = OnRightClickOnHer;
            _attached = true;
            Debug.Log("New UI: radial wheel on (right-click her; Shift+right-click rotates)");
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
            if (!_attached || ModifierKeys.ShiftHeld())
                return false;

            _clickPoint = unityScreen;
            _back.Clear();
            Vector2 anchor;
            var part = BodyPartWheels.Resolve(unityScreen, out anchor);
            if (part != null)
            {
                _partWheel = true;
                _partAnchor = anchor;
                Show(() => part);
            }
            else
            {
                _partWheel = false;
                Show(BuildMain);
            }

            _open = true;
            _leftWas = true;
            _rightWas = true;
            var typing = GetComponent<ITypingFocus>();
            if (typing != null)
                typing.FocusForTyping();
            return true;
        }

        void Update()
        {
            if (!_attached || !_open || _canvasObject == null)
                return;

            HappiUiKit.FollowCamera(_canvas, PlaneDistance);
            var pointer = Pointer();
            Place();
            if (_wheel != null)
                _wheel.Tick(_card.Visible ? (Vector2?)null : pointer, Time.unscaledDeltaTime);

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
            _leftWas = left;
            _rightWas = right;
            if (!leftDown && !rightDown)
                return;

            var onCard = _card.Visible && pointer.HasValue && _card.Contains(pointer.Value);
            var onWheel = !_card.Visible && _wheel != null && pointer.HasValue && _wheel.Contains(pointer.Value);
            if (onCard)
                return;
            if (!onWheel)
            {
                Close();
                return;
            }

            if (rightDown)
                return;

            var hit = _wheel.HitTest(pointer.Value);
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
            if (!slice.Enabled)
            {
                Note(slice.Label, string.IsNullOrEmpty(slice.DisabledReason) ? "Not now." : slice.DisabledReason);
                return;
            }

            if (slice.OnChoose != null)
                slice.OnChoose();
        }

        // ---- Showing ---------------------------------------------------------------------

        void Show(Func<WheelDefinition> build)
        {
            _card.Hide();
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
            var personaId = settings != null ? settings.PersonaId : string.Empty;
            var definition = new WheelDefinition { CenterLabel = HappiNames.Companion(_settings), CenterHint = "right-click" };
            for (var i = 0; i < MainWheel.Order.Count; i++)
            {
                var id = MainWheel.Order[i];
                var gate = WheelSliceGates.Evaluate(id, CompanionMoodNow.Shown, CompanionMoodNow.Strength, personaId);
                definition.Slices.Add(new WheelSlice
                {
                    Id = id,
                    Label = MainWheel.Label(id, paused),
                    Enabled = gate.Enabled,
                    DisabledReason = gate.Reason,
                    OnChoose = MainAction(id, paused)
                });
            }

            definition.OnCenter = Close;
            return definition;
        }

        Action MainAction(string id, bool paused)
        {
            switch (id)
            {
                case MainWheel.Talk:
                    return () => Do(() => { if (_chat != null) _chat.ShowChat(true); });
                case MainWheel.WatchScreen:
                    return () => Push(BuildScreen);
                case MainWheel.Mood:
                    return ShowMood;
                case MainWheel.Persona:
                    return () => Push(BuildPersona);
                case MainWheel.DoSomething:
                    return () => Push(() => BuildDoSomething(0));
                case MainWheel.LeaveMeAlone:
                    if (paused)
                        return () => Do(() => _settings.Presenter.SetPaused(false));
                    return () => Push(BuildLeaveMeAlone);
                case MainWheel.Memories:
                    return ShowMemories;
                case MainWheel.Settings:
                    return () => Do(OpenSettings);
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
            definition.Slices.Add(new WheelSlice { Id = "screen_settings", Label = "Screen settings", OnChoose = () => Do(OpenSettings) });
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

        void ShowMood()
        {
            var keeper = GetComponent<CompanionMoodKeeper>();
            var top3 = keeper != null && keeper.State != null ? keeper.State.DescribeTop3() : string.Empty;
            var text = MoodPresenter.Describe(CompanionMoodNow.Shown, CompanionMoodNow.Strength, top3, IntimateExpressions.Active, DateTime.Now.Hour + DateTime.Now.Minute / 60f);
            var settings = _settings != null ? _settings.CurrentSettings : null;
            var debug = settings != null && settings.ShowMoodDebug;
            ShowCard("Mood", text, new[]
            {
                new CardButton(debug ? "Hide mood readout" : "Show mood readout", () => { _settings.Presenter.SetShowMoodDebug(!debug); ShowMood(); }),
                new CardButton("Back", Back),
            });
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

        static string StripActive(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;
            return label.Replace(" (active)", string.Empty).Replace(" (saved)", string.Empty);
        }

        WheelDefinition BuildDoSomething(int page)
        {
            var definition = Sub("Do");
            var video = GetComponent<VideoAvatar>();
            var playing = video != null && video.State == VideoAvatarState.Playing;
            var choices = VideoAvatarPresenter.DoSomething(playing, AvatarReactionCatalog.Names);
            var perPage = MaxSlicesPerWheel - 1;
            var start = page * perPage;
            var end = Mathf.Min(choices.Count, start + (choices.Count - start > MaxSlicesPerWheel ? perPage : MaxSlicesPerWheel));
            for (var i = start; i < end; i++)
            {
                var choice = choices[i];
                definition.Slices.Add(new WheelSlice { Id = "do_" + i, Label = choice.Label, OnChoose = () => Perform(choice) });
            }

            if (end < choices.Count)
            {
                var next = page + 1;
                definition.Slices.Add(new WheelSlice { Id = "do_more", Label = "More…", OnChoose = () => Push(() => BuildDoSomething(next)) });
            }

            return definition;
        }

        void Perform(DoSomethingChoice choice)
        {
            var video = GetComponent<VideoAvatar>();
            switch (choice.Kind)
            {
                case DoSomethingKind.Reaction:
                    if (video == null || video.State != VideoAvatarState.Playing)
                    {
                        Note("Do something", VideoAvatarPresenter.NotPlaying);
                        return;
                    }

                    var status = VideoAvatarPresenter.ReactionStatus(video.RequestReaction(choice.Name), choice.Name, VideoAvatarDirector.PendingSeconds);
                    if (status.Length > 0)
                        Note("Do something", status);
                    else
                        Close();
                    return;
                case DoSomethingKind.Walk:
                    Do(() => { if (video != null) video.TestWalk(choice.Value); });
                    return;
                case DoSomethingKind.Stop:
                    Do(() => { if (video != null) video.TestStop(); });
                    return;
                case DoSomethingKind.Front:
                    Do(() => { if (video != null) video.TestFront(); });
                    return;
                default:
                    Do(() =>
                    {
                        var animator = GetComponent<IAvatarAnimator>();
                        if (animator != null)
                            animator.SetEmotion(choice.Emotion, choice.Value);
                    });
                    return;
            }
        }

        WheelDefinition BuildLeaveMeAlone()
        {
            var settings = _settings.CurrentSettings;
            var muted = settings != null && settings.Muted;
            var definition = Sub("Quiet");
            definition.Slices.Add(new WheelSlice
            {
                Id = "quiet_pause",
                Label = "Focus mode",
                OnChoose = () => Do(() => _settings.Presenter.SetPaused(true))
            });
            definition.Slices.Add(new WheelSlice
            {
                Id = "quiet_mute",
                Label = muted ? "Unmute her" : "Mute her",
                OnChoose = () => Do(() => _settings.Presenter.SetMuted(!muted))
            });
            definition.Slices.Add(new WheelSlice
            {
                Id = "quiet_hide",
                Label = "Hide her",
                OnChoose = () => Do(() =>
                {
                    var tray = GetComponent<CompanionTray>();
                    if (tray != null)
                        tray.HideCompanion();
                })
            });
            return definition;
        }

        void ShowMemories()
        {
            var memories = _settings.Memories;
            if (memories.Confirm.HasPending)
            {
                ShowCard("Forget?", memories.PendingPrompt(), new[]
                {
                    new CardButton("Forget", () => { memories.ConfirmPending(); ShowMemories(); }, true),
                    new CardButton("Keep", () => { memories.Confirm.Cancel(); ShowMemories(); }),
                });
                return;
            }

            var page = memories.Page(6);
            var buttons = new List<CardButton>();
            for (var i = 0; i < page.Count; i++)
            {
                var id = page[i].Id;
                buttons.Add(new CardButton("Forget: " + page[i].Label, () => { memories.AskForget(id); ShowMemories(); }));
            }

            if (memories.Count > page.Count)
                buttons.Add(new CardButton("All memories (Settings)", () => Do(OpenSettings)));
            buttons.Add(new CardButton("Back", Back));
            ShowCard("What she remembers", memories.Summary, buttons);
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
