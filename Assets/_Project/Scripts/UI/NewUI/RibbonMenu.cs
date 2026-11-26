using System;
using AICompanion.Core;
using AICompanion.NewUI.Wheel;
using AICompanion.Overlay;
using AICompanion.Settings;
using AICompanion.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Opt-in Ribbon menu. Uses the same presenters as the existing Happi views.</summary>
    sealed class RibbonMenu : MonoBehaviour, IOverlayKeepAwake
    {
        public const string LaunchArgument = "--ribbon-ui";
        public static bool Enabled { get; } = Array.IndexOf(Environment.GetCommandLineArgs(), LaunchArgument) >= 0;
        internal static readonly Color Ink = new Color32(16, 19, 24, 255);
        internal static readonly Color Cyan = new Color32(38, 230, 255, 255);
        static readonly Color Yellow = new Color32(255, 202, 34, 255);
        const float HoverGrace = 0.22f;
        const float PaperRevealSeconds = 0.21f;
        const float TriggerSize = 30f;
        const float MainWidth = 170f;
        const float MainRowHeight = 35f;
        const float MainRowPitch = 39f;
        const float CloseGap = 7f;
        const float FlyoutRowHeight = 31f;
        const float FlyoutRowPitch = 35f;
        const float FlyoutMinWidth = 158f;
        const float FlyoutPreferredWidth = 660f;

        internal static int PreferredSideRoom(float scale)
        {
            return Mathf.CeilToInt((FlyoutPreferredWidth + MainWidth * 0.5f + 36f) * scale);
        }

        Canvas _canvas;
        HeadLocator _head;
        OverlayWindow _window;
        IScreenCursor _cursor;
        ChatPanel _chat;
        SettingsPanel _settings;
        HappiSettingsView _settingsView;
        HappiWheelHost _wheel;
        HappiButton _trigger;
        RibbonGlyph _menuGlyph;
        RectTransform _roomPreview;
        RectTransform _roomHint;
        RibbonClickAccent _clickAccent;
        Action _pendingSelection;
        float _selectionReadyAt;
        HappiButton[] _rows;
        RibbonGlyph[] _rowIcons;
        readonly Rect[] _rowBounds = new Rect[6];
        readonly Rect[] _destinationBounds = new Rect[AiPages.Length];
        RibbonGlyph _profileArrow;
        RibbonGlyph _aiArrow;
        RectTransform _menu;
        RectTransform _flyout;
        Image _paper;
        RibbonShape _paperShape;
        RibbonScrollArt _painting;
        RawImage _scrollRoll;
        float _flyoutShownAt;
        Texture2D _profileArt;
        Texture2D _aiArt;
        HappiButton[] _destinations;
        enum Flyout { None, Profile, Ai }
        Flyout _activeFlyout;

        static readonly ProfilePanel.Tab[] ProfileTabs =
        {
            ProfilePanel.Tab.Feelings, ProfilePanel.Tab.Personality,
            ProfilePanel.Tab.World, ProfilePanel.Tab.Memories
        };
        static readonly string[] ProfileLabels = { "Feelings", "Personality", "Her world", "Memories" };
        static readonly SettingsPage[] AiPages =
        {
            SettingsPage.AiChat, SettingsPage.AiPrivate, SettingsPage.VoiceEngine,
            SettingsPage.VoiceListening, SettingsPage.SensesScreen, SettingsPage.AiPisara
        };
        static readonly string[] AiLabels =
        {
            "Chat model", "Private model", "Voice engine", "Listening", "Vision", "Pisara"
        };

        Rect _triggerBounds;
        Rect _roomBounds;
        Rect _menuBounds;
        Rect _visiblePaperBounds;
        Rect _openPaperBounds;
        bool _flyoutOpensRight;
        Vector2 _flyoutPointerOrigin;
        float _flyoutPointerAt;
        float _closeAt;
        bool _closedOverTrigger;
        bool _leftWasHeld;
        readonly RibbonKeyboard _keyboard = new RibbonKeyboard();
        Vector2 _keyboardPointer;
        RectTransform _keyboardMark;

        public bool KeepFrameRateActive => _menu != null && _menu.gameObject.activeInHierarchy;

        public void Attach(ChatPanel chat, SettingsPanel settings)
        {
            _chat = chat;
            _settings = settings;
            _settingsView = GetComponent<HappiSettingsView>();
            _wheel = GetComponent<HappiWheelHost>();
            _head = new HeadLocator(gameObject);
            _window = GetComponent<OverlayWindow>();
            _cursor = GetComponent<IScreenCursor>();
            var root = HappiUiKit.CreateCanvas(transform, "HappiRibbon", 65, out _canvas).transform;
            _clickAccent = RibbonClickAccent.Create(root);
            _trigger = Button(root, "Ribbon menu", null);
            _trigger.Button.onClick.AddListener(() =>
            {
                if (!_keyboard.Active)
                    OpenWithKeyboard();
            });
            _keyboardMark = HappiUiKit.Panel(root, "Ribbon keyboard focus", Cyan, false).rectTransform;
            _trigger.Label.gameObject.SetActive(false);
            _menuGlyph = RibbonGlyph.Add(_trigger.Rect, "Menu glyph", RibbonGlyph.Mark.Menu, Cyan);
            HappiUiKit.Stretch(_menuGlyph.rectTransform, 6f, 6f, 6f, 6f);
            var triggerColors = _trigger.Button.colors;
            triggerColors.normalColor = new Color32(16, 19, 24, 105);
            _trigger.Button.colors = triggerColors;

            var room = HappiUiKit.Panel(root, "Her Room preview", new Color32(16, 19, 24, 110), false);
            _roomPreview = room.rectTransform;
            RibbonShape.Apply(room, false);
            var door = RibbonGlyph.Add(_roomPreview, "Door glyph", RibbonGlyph.Mark.Door, new Color32(176, 191, 197, 230));
            HappiUiKit.Stretch(door.rectTransform, 6f, 6f, 6f, 6f);
            var hint = HappiUiKit.Panel(root, "Her Room preview hint", Ink, false);
            _roomHint = hint.rectTransform;
            RibbonShape.Apply(hint, false);
            var hintText = HappiUiKit.Text(_roomHint, "Label", "Her Room · Planned", 13f,
                HappiTheme.Current.Text, TextAlignmentOptions.Center, false);
            HappiUiKit.Stretch(hintText.rectTransform, 4f, 0f, 4f, 0f);
            _roomHint.gameObject.SetActive(false);
            _menu = HappiUiKit.Rect(root, "Ribbon tabs");
            _rows = new[]
            {
                Button(_menu, "Mute", () => _chat.Presenter.ToggleMute()),
                Button(_menu, "Look now", () => _settings.ScreenLook.LookNow()),
                Button(_menu, "Profile", () => OpenProfile(ProfilePanel.Tab.Feelings)),
                Button(_menu, "AI", () => ShowFlyout(Flyout.Ai)),
                Button(_menu, "Settings", () => { Close(); _settings.Open(); }),
                Button(_menu, "Close", Close)
            };

            _rowIcons = new[]
            {
                AddRowGlyph(_rows[0], RibbonGlyph.Mark.Voice),
                AddRowGlyph(_rows[1], RibbonGlyph.Mark.Screen),
                AddRowGlyph(_rows[2], RibbonGlyph.Mark.Profile),
                AddRowGlyph(_rows[3], RibbonGlyph.Mark.Model),
                AddRowGlyph(_rows[4], RibbonGlyph.Mark.Settings),
                AddRowGlyph(_rows[5], RibbonGlyph.Mark.Close)
            };
            _profileArrow = AddRowGlyph(_rows[2], RibbonGlyph.Mark.Chevron);
            _aiArrow = AddRowGlyph(_rows[3], RibbonGlyph.Mark.Chevron);

            foreach (var row in _rows)
            {
                row.Label.alignment = TextAlignmentOptions.MidlineLeft;
                row.Label.overflowMode = TextOverflowModes.Ellipsis;
                var accent = HappiUiKit.Panel(row.Rect, "Mint edge", new Color32(114, 238, 207, 255), false);
                var edge = accent.rectTransform;
                edge.anchorMin = new Vector2(0.04f, 0.15f);
                edge.anchorMax = new Vector2(0.09f, 0.85f);
                edge.offsetMin = Vector2.zero;
                edge.offsetMax = Vector2.zero;
                accent.transform.SetAsFirstSibling();
                RibbonShape.Apply(accent, true);
            }

            _profileArt = Resources.Load<Texture2D>("RibbonArt/Profile");
            _aiArt = Resources.Load<Texture2D>("RibbonArt/AI");
            _flyout = HappiUiKit.Rect(root, "Illustrated flyout");
            _paper = HappiUiKit.Panel(_flyout, "Yellow paper", Yellow, false);
            _paperShape = RibbonShape.Apply(_paper, RibbonShape.Kind.ScrollPaper);
            _paper.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _painting = HappiUiKit.Rect(_paper.transform, "Painting").gameObject.AddComponent<RibbonScrollArt>();
            _painting.raycastTarget = false;
            _scrollRoll = HappiUiKit.Rect(_flyout, "Illustrated scroll roll").gameObject.AddComponent<RawImage>();
            _scrollRoll.texture = Resources.Load<Texture2D>("RibbonArt/ScrollRoll");
            _scrollRoll.enabled = _scrollRoll.texture != null;
            _scrollRoll.raycastTarget = false;

            _destinations = new HappiButton[AiPages.Length];
            for (var i = 0; i < _destinations.Length; i++)
            {
                var index = i;
                var destination = Button(_flyout, string.Empty, () => OpenDestination(index));
                destination.Label.alignment = TextAlignmentOptions.MidlineLeft;
                destination.Label.overflowMode = TextOverflowModes.Truncate;
                _destinations[i] = destination;
            }
            Close();
            _window?.RefreshClickTargets();
        }

        HappiButton Button(Transform parent, string label, Action action)
        {
            var button = HappiUiKit.Button(parent, label, label, 15f, null);
            if (action != null)
            {
                button.Button.onClick.AddListener(() =>
                {
                    if (_pendingSelection != null)
                        return;
                    _clickAccent.Play(button);
                    _pendingSelection = action;
                    _selectionReadyAt = Time.unscaledTime + RibbonClickAccent.ReadableSeconds;
                });
            }
            RibbonShape.Apply(button.Image, true);
            button.Label.fontStyle |= FontStyles.Bold | FontStyles.Italic;
            var colors = button.Button.colors;
            colors.normalColor = Ink;
            colors.highlightedColor = Cyan;
            colors.pressedColor = Yellow;
            colors.selectedColor = Ink;
            button.Button.colors = colors;
            var hover = button.Button.GetComponent<HappiHoverText>();
            hover.Hover = Ink;
            return button;
        }

        static RibbonGlyph AddRowGlyph(HappiButton row, RibbonGlyph.Mark mark)
        {
            var glyph = RibbonGlyph.Add(row.Rect, mark.ToString(), mark, row.Label.color);
            glyph.Label = row.Label;
            return glyph;
        }

        void OpenProfile(ProfilePanel.Tab tab)
        {
            Close();
            if (_wheel != null)
                _wheel.OpenProfileFromRibbon(tab);
        }

        void OpenDestination(int index)
        {
            if (_activeFlyout == Flyout.Profile && index < ProfileTabs.Length)
            {
                OpenProfile(ProfileTabs[index]);
                return;
            }

            if (_activeFlyout != Flyout.Ai || index >= AiPages.Length)
                return;
            var page = AiPages[index];
            Close();
            if (_settingsView != null)
                _settingsView.TryOpenPage(page);
        }

        void ShowFlyout(Flyout flyout)
        {
            if (_activeFlyout == flyout)
                return;
            SetParentTabHighlight(_activeFlyout, false);
            _activeFlyout = flyout;
            _flyoutPointerAt = float.NegativeInfinity;
            SetParentTabHighlight(flyout, true);
            _flyoutShownAt = Time.unscaledTime;
            var profile = flyout == Flyout.Profile;
            var labels = profile ? ProfileLabels : AiLabels;
            _painting.texture = profile ? _profileArt : _aiArt;
            _painting.enabled = _painting.texture != null;
            for (var i = 0; i < _destinations.Length; i++)
            {
                var visible = i < labels.Length;
                _destinations[i].Button.gameObject.SetActive(visible);
                if (visible)
                    _destinations[i].Label.text = labels[i];
            }
            _flyout.gameObject.SetActive(true);
        }

        void SetParentTabHighlight(Flyout flyout, bool active)
        {
            if (flyout == Flyout.None)
                return;

            var parent = _rows[flyout == Flyout.Profile ? 2 : 3];
            var colors = parent.Button.colors;
            colors.normalColor = active ? Cyan : Ink;
            colors.selectedColor = colors.normalColor;
            parent.Button.colors = colors;
            parent.Button.GetComponent<HappiHoverText>().HoldHoverColor = active;
        }

        void HideFlyout()
        {
            SetParentTabHighlight(_activeFlyout, false);
            _activeFlyout = Flyout.None;
            _flyout.gameObject.SetActive(false);
        }

        void Close()
        {
            _pendingSelection = null;
            _clickAccent.Stop();
            EndKeyboard();
            _closedOverTrigger = true;
            _menu.gameObject.SetActive(false);
            HideFlyout();
        }

        void OpenWithKeyboard(int row = 0)
        {
            GetComponent<ITypingFocus>()?.FocusForTyping();
            EventSystem.current?.SetSelectedGameObject(null);
            _keyboard.Open(row);
            _keyboardPointer = ReadPointer();
            _closedOverTrigger = false;
            _menu.gameObject.SetActive(true);
            _closeAt = Time.unscaledTime + HoverGrace;
            HideFlyout();
            ShowKeyboardMark();
        }

        void EndKeyboard()
        {
            _keyboard.Close();
            _keyboardMark.gameObject.SetActive(false);
        }

        Vector2 ReadPointer()
        {
            var pointer = (Vector2)Input.mousePosition;
            if (_cursor != null && _cursor.TryGetUnityScreenCursor(out var x, out var y))
                pointer = new Vector2(x, y);
            return new Vector2(pointer.x, Screen.height - pointer.y);
        }

        void UpdateKeyboard(Rect area, float scale)
        {
            var parentRow = _keyboard.Row;
            var inFlyout = _keyboard.Destination >= 0;
            var openRight = RibbonMenuPlacement.OpensRight(area, _menuBounds, FlyoutPreferredWidth * scale);
            var enterSide = openRight ? KeyCode.RightArrow : KeyCode.LeftArrow;
            var backSide = openRight ? KeyCode.LeftArrow : KeyCode.RightArrow;
            var confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

            if (Input.GetKeyDown(KeyCode.Escape) || (inFlyout && Input.GetKeyDown(backSide)))
            {
                if (inFlyout)
                {
                    _keyboard.LeaveFlyout();
                    HideFlyout();
                    ShowKeyboardMark();
                }
                else
                    Close();
                return;
            }

            var direction = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : Input.GetKeyDown(KeyCode.DownArrow) ? 1 : 0;
            var destinationCount = _activeFlyout == Flyout.Profile ? ProfileTabs.Length : AiPages.Length;
            if (direction != 0)
            {
                _keyboardPointer = ReadPointer();
                _keyboard.Move(direction, _rows.Length, destinationCount, _rows[1].Button.interactable);
            }
            else if (!inFlyout && (parentRow == 2 || parentRow == 3) && (confirm || Input.GetKeyDown(enterSide)))
            {
                ShowFlyout(parentRow == 2 ? Flyout.Profile : Flyout.Ai);
                _keyboard.EnterFlyout();
                PlaceFlyout(area, scale);
            }
            else if (confirm)
            {
                var choice = inFlyout ? _destinations[_keyboard.Destination] : _rows[parentRow];
                if (choice.Button.IsInteractable())
                    choice.Button.onClick.Invoke();
            }

            ShowKeyboardMark();
        }

        void ShowKeyboardMark()
        {
            if (!_keyboard.Active)
                return;
            var row = _keyboard.Destination >= 0 ? _destinations[_keyboard.Destination] : _rows[_keyboard.Row];
            if (_keyboardMark.parent != row.Rect)
            {
                _keyboardMark.SetParent(row.Rect, false);
                _keyboardMark.anchorMin = new Vector2(0.08f, 0f);
                _keyboardMark.anchorMax = new Vector2(0.92f, 0f);
                _keyboardMark.offsetMin = new Vector2(0f, 2f);
                _keyboardMark.offsetMax = new Vector2(0f, 5f);
            }
            _keyboardMark.gameObject.SetActive(true);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && _keyboard.Active && _canvas != null)
                Close();
        }

        void Update()
        {
            if (_canvas == null)
                return;
            var chrome = _window != null ? _window.Chrome : null;
            var left = chrome != null && chrome.IsSupported ? chrome.IsLeftButtonHeld() : Input.GetMouseButton(0);
            var leftDown = left && !_leftWasHeld;
            _leftWasHeld = left;
            if ((_window != null && !_window.IsCompanionVisible) || !_head.TryGet(Time.unscaledDeltaTime, out var head))
            {
                Close();
                _trigger.Button.gameObject.SetActive(false);
                _roomPreview.gameObject.SetActive(false);
                _roomHint.gameObject.SetActive(false);
                return;
            }

            HappiUiKit.FollowCamera(_canvas, 0.42f);
            _trigger.Button.gameObject.SetActive(true);
            _roomPreview.gameObject.SetActive(true);
            var visible = OverlayVisibleArea.Get(_window);
            var area = new Rect(visible.xMin, Screen.height - visible.yMax, visible.width, visible.height);
            var mainHeight = (_rows.Length - 1) * MainRowPitch + MainRowHeight + CloseGap;
            var scale = RibbonMenuPlacement.FitScale(area.size, new Vector2(MainWidth + FlyoutMinWidth, mainHeight + TriggerSize),
                HappiTheme.Current.UiScale(Screen.dpi));
            Place(head, area, mainHeight, scale);

            // Keep the selected row and its flyout on screen until the stamp has registered.
            if (_pendingSelection != null)
            {
                if (_activeFlyout != Flyout.None)
                    PlaceFlyout(area, scale);
                if (Time.unscaledTime < _selectionReadyAt)
                    return;

                var selected = _pendingSelection;
                _pendingSelection = null;
                selected();
                return;
            }
            var pointer = ReadPointer();
            var overTrigger = _triggerBounds.Contains(pointer);
            var keyboardShortcut = Input.GetKeyDown(KeyCode.DownArrow) && CanTakeKeyboard()
                && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
            if (keyboardShortcut || (leftDown && overTrigger))
            {
                // Windows can deliver the initial press before the overlay receives Unity UI focus.
                OpenWithKeyboard();
                return;
            }
            var glyphColor = overTrigger ? Ink : Cyan;
            if (_menuGlyph.color != glyphColor)
                _menuGlyph.color = glyphColor;
            _roomHint.gameObject.SetActive(_roomBounds.Contains(pointer) && !_menu.gameObject.activeSelf);
            if (!overTrigger)
                _closedOverTrigger = false;
            if (!_keyboard.Active && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            if (overTrigger && !_closedOverTrigger)
            {
                _menu.gameObject.SetActive(true);
                _closeAt = Time.unscaledTime + HoverGrace;
            }
            if (!_menu.gameObject.activeSelf)
                return;

            var muted = _chat.Presenter.Muted;
            var muteLabel = muted ? "Unmute" : "Mute";
            _rowIcons[0].Shape = muted ? RibbonGlyph.Mark.Muted : RibbonGlyph.Mark.Voice;
            if (_rows[0].Label.text != muteLabel)
                _rows[0].Label.text = muteLabel;
            _rows[1].Button.interactable = _settings.ScreenLook.Enabled;
            var overMain = _menuBounds.Contains(pointer);
            var pointerRow = overMain ? MainRowAt(pointer) : -1;
            if (_activeFlyout != Flyout.None)
                PlaceFlyout(area, scale);
            var overFlyout = _activeFlyout != Flyout.None && _visiblePaperBounds.Contains(pointer);
            if (leftDown && !overTrigger && pointerRow < 0 && !OverDestination(pointer))
            {
                Close();
                return;
            }

            var verticalKey = (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                && CanTakeKeyboard();
            if (!_keyboard.Active && verticalKey)
                OpenWithKeyboard(Mathf.Max(0, pointerRow));
            if (_keyboard.Active && !verticalKey && !overTrigger
                && (pointer - _keyboardPointer).sqrMagnitude > 144f)
            {
                EndKeyboard();
                _closeAt = Time.unscaledTime + HoverGrace;
            }
            if (_keyboard.Active)
            {
                if (Application.isFocused)
                    UpdateKeyboard(area, scale);
                return;
            }

            if (overMain || overFlyout)
                _closeAt = Time.unscaledTime + HoverGrace;
            else if (!overTrigger && Time.unscaledTime >= _closeAt)
            {
                Close();
                return;
            }

            if (pointerRow < 0)
                return;

            var target = pointerRow switch { 2 => Flyout.Profile, 3 => Flyout.Ai, _ => Flyout.None };
            if (target != _activeFlyout && _activeFlyout != Flyout.None
                && Time.unscaledTime - _flyoutPointerAt < HoverGrace
                && RibbonMenuPlacement.AimsAtFlyout(_flyoutPointerOrigin, pointer, _openPaperBounds, _flyoutOpensRight, 8f * scale))
                return;

            if (target == Flyout.None)
            {
                if (_activeFlyout != Flyout.None)
                    HideFlyout();
                return;
            }

            if (_activeFlyout != target)
            {
                ShowFlyout(target);
                PlaceFlyout(area, scale);
            }
            _flyoutPointerOrigin = pointer;
            _flyoutPointerAt = Time.unscaledTime;
        }

        bool CanTakeKeyboard()
        {
            if (!Application.isFocused || (_settingsView != null && _settingsView.IsOpen) || (_wheel != null && _wheel.IsOpen))
                return false;

            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected == null || !selected.TryGetComponent<TMP_InputField>(out var input) || !input.isFocused;
        }

        bool OverDestination(Vector2 pointer)
        {
            var count = _activeFlyout == Flyout.Profile ? ProfileTabs.Length : _activeFlyout == Flyout.Ai ? AiPages.Length : 0;
            for (var i = 0; i < count; i++)
                if (_destinationBounds[i].Contains(pointer))
                    return true;
            return false;
        }

        int MainRowAt(Vector2 pointer)
        {
            for (var i = 0; i < _rowBounds.Length; i++)
            {
                if (_rowBounds[i].Contains(pointer))
                    return i;
            }
            return -1;
        }

        void Place(Vector2 head, Rect area, float mainHeight, float scale)
        {
            var trigger = TriggerSize * scale;
            var menuWidth = MainWidth * scale;
            var menuHeight = mainHeight * scale;
            var clusterWidth = trigger * 2f + 4f * scale;
            var rightOfHead = head.x + 24f * scale;
            var triggerX = rightOfHead + clusterWidth <= area.xMax
                ? Fit(rightOfHead, area.xMin, area.xMax - clusterWidth)
                : Fit(head.x - 24f * scale - clusterWidth, area.xMin, area.xMax - clusterWidth);
            var (triggerY, menuY) = RibbonMenuPlacement.Stack(Screen.height - head.y, area, trigger, menuHeight);
            _triggerBounds = new Rect(triggerX, triggerY, trigger, trigger);
            HappiUiKit.Place(_trigger.Rect, triggerX, triggerY, trigger, trigger);
            var roomX = triggerX + trigger + 4f * scale;
            _roomBounds = new Rect(roomX, triggerY, trigger, trigger);
            HappiUiKit.Place(_roomPreview, roomX, triggerY, trigger, trigger);
            var hintWidth = 142f * scale;
            var hintX = Fit(roomX + trigger * 0.5f - hintWidth * 0.5f, area.xMin, area.xMax - hintWidth);
            var hintY = Fit(triggerY + trigger, area.yMin, area.yMax - 22f * scale);
            HappiUiKit.Place(_roomHint, hintX, hintY, hintWidth, 22f * scale);
            var menuX = RibbonMenuPlacement.MenuX(area, triggerX, menuWidth, FlyoutMinWidth * scale);
            _menuBounds = new Rect(menuX, menuY, menuWidth, menuHeight);
            HappiUiKit.Place(_menu, menuX, menuY, menuWidth, menuHeight);
            for (var i = 0; i < _rows.Length; i++)
            {
                var stagger = (i % 2) * 7f * scale;
                var rowTop = (i * MainRowPitch + (i == _rows.Length - 1 ? CloseGap : 0f)) * scale;
                var row = _rows[i];
                var rowWidth = menuWidth - stagger;
                var rowHeight = MainRowHeight * scale;
                _rowBounds[i] = new Rect(menuX + stagger, menuY + rowTop, rowWidth, rowHeight);
                HappiUiKit.Place(row.Rect, stagger, rowTop, rowWidth, rowHeight);
                HappiUiKit.Place(_rowIcons[i].rectTransform, 15f * scale, 8f * scale, 19f * scale, 19f * scale);
                var trailing = (i == 2 || i == 3) ? 30f : 12f;
                HappiUiKit.Stretch(row.Label.rectTransform, 42f * scale, 0f, trailing * scale, 0f);
                row.Label.fontSize = 18f * scale;
            }

            var openRight = RibbonMenuPlacement.OpensRight(area, _menuBounds, FlyoutPreferredWidth * scale);
            PlaceArrow(_profileArrow, _rows[2], openRight, scale);
            PlaceArrow(_aiArrow, _rows[3], openRight, scale);
        }

        static void PlaceArrow(RibbonGlyph arrow, HappiButton row, bool openRight, float scale)
        {
            var rect = arrow.rectTransform;
            const float size = 13f;
            var inset = openRight ? 23f : 23f - size;
            HappiUiKit.Place(rect, row.Rect.rect.width - inset * scale, 11f * scale, size * scale, size * scale);
            rect.localScale = new Vector3(openRight ? 1f : -1f, 1f, 1f);
        }

        void PlaceFlyout(Rect area, float scale)
        {
            var preferredWidth = FlyoutPreferredWidth * scale;
            var openRight = RibbonMenuPlacement.OpensRight(area, _menuBounds, preferredWidth);
            var count = _activeFlyout == Flyout.Profile ? ProfileTabs.Length : AiPages.Length;
            var height = (count * FlyoutRowPitch + 13f) * scale;
            var row = _activeFlyout == Flyout.Profile ? 2 : 3;
            var bounds = RibbonMenuPlacement.Flyout(area, _menuBounds, new Vector2(preferredWidth, height),
                row * MainRowPitch * scale, openRight);
            _openPaperBounds = bounds;
            _flyoutOpensRight = openRight;
            var width = bounds.width;
            HappiUiKit.Place(_flyout, bounds.x, bounds.y, width, height);
            var rollWidth = _scrollRoll.enabled ? Mathf.Min(height * 0.34f, width * 0.23f) : 0f;
            var (rollOverlap, paperEdge) = RibbonMenuPlacement.ScrollJoin(rollWidth);
            var curlWidthInFlyout = rollWidth - rollOverlap;
            var buttonWidth = Mathf.Min(width, Mathf.Max(FlyoutMinWidth * scale,
                Mathf.Max(width * 0.34f, curlWidthInFlyout + 146f * scale)));
            var remaining = 1f - Mathf.Clamp01((Time.unscaledTime - _flyoutShownAt) / PaperRevealSeconds);
            var eased = 1f - remaining * remaining * remaining;
            // Leave room for the tapered outer edge beyond the buttons in the opening frame.
            var initialWidth = Mathf.Min(width, buttonWidth + 32f * scale);
            var shownWidth = Mathf.Lerp(initialWidth, width, eased);
            var revealX = openRight ? 0f : width - shownWidth;
            _visiblePaperBounds = new Rect(bounds.x + revealX, bounds.y, shownWidth, height);
            _paperShape.SetScrollJoin(!openRight, paperEdge);
            HappiUiKit.Place(_paper.rectTransform, revealX, 0f, shownWidth, height);
            PlaceFlyoutPainting(width, height, buttonWidth, revealX, openRight, scale);
            if (_scrollRoll.enabled)
            {
                var roll = _scrollRoll.rectTransform;
                HappiUiKit.Place(roll, openRight ? -rollOverlap : width + rollOverlap, 0f, rollWidth, height);
                roll.localScale = new Vector3(openRight ? 1f : -1f, 1f, 1f);
            }
            var curlInset = Mathf.Max(4f * scale, curlWidthInFlyout - 3f * scale);
            var actionX = openRight ? curlInset : width - buttonWidth + 4f * scale;
            var actionWidth = Mathf.Max(1f, buttonWidth - curlInset - 4f * scale);
            var actionHeight = FlyoutRowHeight * scale;
            for (var i = 0; i < count; i++)
            {
                var destination = _destinations[i];
                destination.Label.fontSize = 17f * scale;
                HappiUiKit.Stretch(destination.Label.rectTransform, 12f * scale, 0f, 10f * scale, 0f);
                var actionY = (8f + i * FlyoutRowPitch) * scale;
                _destinationBounds[i] = new Rect(bounds.x + actionX, bounds.y + actionY, actionWidth, actionHeight);
                HappiUiKit.Place(destination.Rect, actionX, actionY, actionWidth, actionHeight);
            }
        }

        void PlaceFlyoutPainting(float width, float height, float buttonWidth, float revealX, bool openRight, float scale)
        {
            var visible = _painting.texture != null && width - buttonWidth >= 55f * scale;
            _painting.enabled = visible;
            if (!visible)
                return;

            // The artwork is fixed to the open scroll. The moving paper mask supplies the reveal.
            var seam = buttonWidth - 16f * scale;
            var artWidth = width - seam;
            var aspect = (float)_painting.texture.width / _painting.texture.height;
            var profile = _activeFlyout == Flyout.Profile;
            _painting.SetFadeSide(openRight);
            _painting.uvRect = RibbonMenuPlacement.IllustrationUv(aspect, artWidth / height,
                profile ? 0.64f : 0.71f, profile ? 0.30f : 0.17f);
            HappiUiKit.Place(_painting.rectTransform, (openRight ? seam : 0f) - revealX, 0f, artWidth, height);
        }

        static float Fit(float value, float min, float max) => Mathf.Clamp(value, min, Mathf.Max(min, max));

        void OnDestroy()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);
            _window?.RefreshClickTargets();
        }
    }
}
