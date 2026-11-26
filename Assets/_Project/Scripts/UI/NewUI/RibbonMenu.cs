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
        const float MainWidth = 146f;
        const float FlyoutMinWidth = 124f;
        const float FlyoutPreferredWidth = 275f;

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
        RibbonGlyph _profileArrow;
        RibbonGlyph _aiArrow;
        RectTransform _menu;
        RectTransform _flyout;
        Image _paper;
        RectTransform _paperEdge;
        RawImage _painting;
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
        Rect _flyoutBounds;
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
            RibbonShape.Apply(_paper, false);
            var paperShadow = _paper.gameObject.AddComponent<Shadow>();
            paperShadow.effectColor = new Color32(0, 0, 0, 115);
            paperShadow.effectDistance = new Vector2(2f, -3f);
            _painting = HappiUiKit.Rect(_flyout, "Painting").gameObject.AddComponent<RawImage>();
            _painting.raycastTarget = false;
            _paperEdge = RibbonGlyph.Add(_flyout, "Rolled paper edge", RibbonGlyph.Mark.PaperEdge,
                new Color32(255, 228, 111, 255)).rectTransform;

            _destinations = new HappiButton[AiPages.Length];
            for (var i = 0; i < _destinations.Length; i++)
            {
                var index = i;
                _destinations[i] = Button(_flyout, string.Empty, () => OpenDestination(index));
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
            _activeFlyout = flyout;
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

        void HideFlyout()
        {
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
            var openRight = FlyoutOpensRight(area, scale);
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
            }
            else if (confirm)
            {
                var choice = inFlyout ? _destinations[_keyboard.Destination] : _rows[parentRow];
                if (choice.Button.IsInteractable())
                    choice.Button.onClick.Invoke();
            }

            if (_activeFlyout != Flyout.None)
                PlaceFlyout(area, scale);
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
            var area = OverlayVisibleArea.Get(_window);
            var scale = Mathf.Min(HappiTheme.Current.UiScale(Screen.dpi),
                Mathf.Max(1f, area.width) / (MainWidth + FlyoutMinWidth));
            Place(head, area, scale);

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
            var chrome = _window != null ? _window.Chrome : null;
            var left = chrome != null && chrome.IsSupported ? chrome.IsLeftButtonHeld() : Input.GetMouseButton(0);
            var leftDown = left && !_leftWasHeld;
            _leftWasHeld = left;
            var overTrigger = _triggerBounds.Contains(pointer);
            var keyboardShortcut = Application.isFocused && Input.GetKeyDown(KeyCode.DownArrow)
                && (_settingsView == null || !_settingsView.IsOpen) && (_wheel == null || !_wheel.IsOpen)
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
            var overFlyout = _activeFlyout != Flyout.None && _flyoutBounds.Contains(pointer);
            if (leftDown && !overTrigger && !overMain && !overFlyout)
            {
                Close();
                return;
            }

            var verticalKey = Application.isFocused
                && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow));
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

            if (pointerRow == 2 || pointerRow == 3)
                ShowFlyout(pointerRow == 2 ? Flyout.Profile : Flyout.Ai);
            else if (pointerRow >= 0)
                HideFlyout();

            if (_activeFlyout != Flyout.None)
                PlaceFlyout(area, scale);
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

        void Place(Vector2 head, Rect area, float scale)
        {
            var top = Screen.height - area.yMax;
            var bottom = Screen.height - area.yMin;
            var trigger = 30f * scale;
            var menuWidth = MainWidth * scale;
            var menuHeight = 198f * scale;
            var clusterWidth = trigger * 2f + 4f * scale;
            var rightOfHead = head.x + 24f * scale;
            var triggerX = rightOfHead + clusterWidth <= area.xMax
                ? Fit(rightOfHead, area.xMin, area.xMax - clusterWidth)
                : Fit(head.x - 24f * scale - clusterWidth, area.xMin, area.xMax - clusterWidth);
            var triggerY = Fit(Screen.height - head.y - trigger, top, bottom - trigger);
            _triggerBounds = new Rect(triggerX, triggerY, trigger, trigger);
            HappiUiKit.Place(_trigger.Rect, triggerX, triggerY, trigger, trigger);
            var roomX = triggerX + trigger + 4f * scale;
            _roomBounds = new Rect(roomX, triggerY, trigger, trigger);
            HappiUiKit.Place(_roomPreview, roomX, triggerY, trigger, trigger);
            var hintWidth = 142f * scale;
            var hintX = Fit(roomX + trigger * 0.5f - hintWidth * 0.5f, area.xMin, area.xMax - hintWidth);
            var hintY = Fit(triggerY + trigger, top, bottom - 22f * scale);
            HappiUiKit.Place(_roomHint, hintX, hintY, hintWidth, 22f * scale);
            var menuX = Fit(triggerX, area.xMin, area.xMax - menuWidth);
            var minimum = FlyoutMinWidth * scale;
            var right = area.xMax - menuX - menuWidth;
            var left = menuX - area.xMin;
            if (right < minimum && left < minimum)
                menuX = right >= left ? area.xMax - menuWidth - minimum : area.xMin + minimum;
            var below = bottom - (triggerY + trigger);
            var above = triggerY - top;
            var alignBelow = below >= menuHeight || below >= above;
            var menuY = Fit(alignBelow ? triggerY + trigger : triggerY - menuHeight, top, bottom - menuHeight);
            _menuBounds = new Rect(menuX, menuY, menuWidth, menuHeight);
            HappiUiKit.Place(_menu, menuX, menuY, menuWidth, menuHeight);
            for (var i = 0; i < _rows.Length; i++)
            {
                var stagger = (i % 2) * 5f * scale;
                var rowTop = (i * 32f + (i == 5 ? 6f : 0f)) * scale;
                var row = _rows[i];
                var rowWidth = menuWidth - stagger;
                var rowHeight = 29f * scale;
                _rowBounds[i] = new Rect(menuX + stagger, menuY + rowTop, rowWidth, rowHeight);
                HappiUiKit.Place(row.Rect, stagger, rowTop, rowWidth, rowHeight);
                HappiUiKit.Place(_rowIcons[i].rectTransform, 14f * scale, 7f * scale, 15f * scale, 15f * scale);
                var trailing = (i == 2 || i == 3) ? 30f : 12f;
                HappiUiKit.Stretch(row.Label.rectTransform, 37f * scale, 0f, trailing * scale, 0f);
                row.Label.fontSize = 14f * scale;
            }

            var openRight = FlyoutOpensRight(area, scale);
            PlaceArrow(_profileArrow, _rows[2], openRight, scale);
            PlaceArrow(_aiArrow, _rows[3], openRight, scale);
        }

        static void PlaceArrow(RibbonGlyph arrow, HappiButton row, bool openRight, float scale)
        {
            var rect = arrow.rectTransform;
            const float size = 11f;
            var inset = openRight ? 23f : 23f - size;
            HappiUiKit.Place(rect, row.Rect.rect.width - inset * scale, 9f * scale, size * scale, size * scale);
            rect.localScale = new Vector3(openRight ? 1f : -1f, 1f, 1f);
        }

        bool FlyoutOpensRight(Rect area, float scale)
        {
            var right = area.xMax - _menuBounds.xMax;
            var left = _menuBounds.xMin - area.xMin;
            return right >= FlyoutPreferredWidth * scale || right >= left;
        }

        void PlaceFlyout(Rect area, float scale)
        {
            var right = area.xMax - _menuBounds.xMax;
            var left = _menuBounds.xMin - area.xMin;
            var openRight = FlyoutOpensRight(area, scale);
            var width = Mathf.Min(FlyoutPreferredWidth * scale, Mathf.Max(1f, openRight ? right : left));
            var count = _activeFlyout == Flyout.Profile ? ProfileTabs.Length : AiPages.Length;
            var height = (count * 28f + 12f) * scale;
            var x = openRight ? _menuBounds.xMax : _menuBounds.xMin - width;
            var row = _activeFlyout == Flyout.Profile ? 2 : 3;
            var y = Fit(_menuBounds.y + row * 32f * scale, Screen.height - area.yMax, Screen.height - area.yMin - height);
            _flyoutBounds = new Rect(x, y, width, height);
            HappiUiKit.Place(_flyout, x, y, width, height);
            var buttonWidth = Mathf.Min(width, Mathf.Max(108f * scale, width * 0.4f));
            var progress = Mathf.Clamp01((Time.unscaledTime - _flyoutShownAt) / 0.18f);
            var eased = 1f - (1f - progress) * (1f - progress);
            var shownWidth = Mathf.Lerp(buttonWidth, width, eased);
            var revealX = openRight ? 0f : width - shownWidth;
            HappiUiKit.Place(_paper.rectTransform, revealX, 0f, shownWidth, height);
            if (_painting.texture != null)
            {
                var aspect = (float)_painting.texture.width / _painting.texture.height;
                var artHeight = Mathf.Min(height, width / aspect);
                var fraction = shownWidth / width;
                _painting.uvRect = openRight
                    ? new Rect(0f, 0f, fraction, 1f)
                    : new Rect(fraction, 0f, -fraction, 1f);
                HappiUiKit.Place(_painting.rectTransform, revealX, (height - artHeight) * 0.5f, shownWidth, artHeight);
            }
            var edgeWidth = Mathf.Min(13f * scale, shownWidth * 0.15f);
            var showEdge = shownWidth > buttonWidth + edgeWidth;
            _paperEdge.gameObject.SetActive(showEdge);
            if (showEdge)
            {
                var edgeX = openRight ? revealX + shownWidth - edgeWidth : revealX + edgeWidth;
                HappiUiKit.Place(_paperEdge, edgeX, 3f * scale, edgeWidth, height - 6f * scale);
                _paperEdge.localScale = new Vector3(openRight ? 1f : -1f, 1f, 1f);
            }
            for (var i = 0; i < count; i++)
            {
                var destination = _destinations[i];
                destination.Label.fontSize = 14f * scale;
                HappiUiKit.Place(destination.Rect, (openRight ? 0f : width - buttonWidth) + 4f * scale, (6f + i * 28f) * scale,
                    Mathf.Max(1f, buttonWidth - 8f * scale), 25f * scale);
            }
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
