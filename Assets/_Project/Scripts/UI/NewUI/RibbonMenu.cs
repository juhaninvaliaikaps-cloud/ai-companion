using System;
using AICompanion.Core;
using AICompanion.NewUI.Wheel;
using AICompanion.Overlay;
using AICompanion.Settings;
using AICompanion.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Opt-in Ribbon menu. Uses the same presenters as the existing Happi views.</summary>
    sealed class RibbonMenu : MonoBehaviour
    {
        public const string LaunchArgument = "--ribbon-ui";
        public static bool Enabled { get; } = Array.IndexOf(Environment.GetCommandLineArgs(), LaunchArgument) >= 0;
        internal static readonly Color Ink = new Color32(16, 19, 24, 255);
        internal static readonly Color Cyan = new Color32(38, 230, 255, 255);
        static readonly Color Yellow = new Color32(255, 202, 34, 255);
        const float HoverGrace = 0.22f;

        Canvas _canvas;
        HeadLocator _head;
        OverlayWindow _window;
        IScreenCursor _cursor;
        ChatPanel _chat;
        SettingsPanel _settings;
        HappiButton _trigger;
        HappiButton[] _rows;
        RectTransform _menu;
        RectTransform _flyout;
        Image _paper;
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
        Rect _menuBounds;
        Rect _flyoutBounds;
        float _closeAt;
        bool _closedOverTrigger;

        public void Attach(ChatPanel chat, SettingsPanel settings)
        {
            _chat = chat;
            _settings = settings;
            _head = new HeadLocator(gameObject);
            _window = GetComponent<OverlayWindow>();
            _cursor = GetComponent<IScreenCursor>();
            var root = HappiUiKit.CreateCanvas(transform, "HappiRibbon", 65, out _canvas).transform;
            _trigger = Button(root, "☰", () =>
            {
                _closedOverTrigger = false;
                _menu.gameObject.SetActive(true);
                _closeAt = Time.unscaledTime + HoverGrace;
            });
            var triggerColors = _trigger.Button.colors;
            triggerColors.normalColor = new Color32(16, 19, 24, 105);
            _trigger.Button.colors = triggerColors;
            _menu = HappiUiKit.Rect(root, "Ribbon tabs");
            _rows = new[]
            {
                Button(_menu, "Mute", () => _chat.Presenter.ToggleMute()),
                Button(_menu, "Look now", () => _settings.ScreenLook.LookNow()),
                Button(_menu, "Profile  ›", () => OpenProfile(ProfilePanel.Tab.Feelings)),
                Button(_menu, "AI  ›", () => ShowFlyout(Flyout.Ai)),
                Button(_menu, "Settings", () => { Close(); _settings.Open(); }),
                Button(_menu, "Close", Close)
            };

            foreach (var row in _rows)
            {
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
            _painting = HappiUiKit.Rect(_flyout, "Painting").gameObject.AddComponent<RawImage>();
            _painting.raycastTarget = false;

            _destinations = new HappiButton[AiPages.Length];
            for (var i = 0; i < _destinations.Length; i++)
            {
                var index = i;
                _destinations[i] = Button(_flyout, string.Empty, () => OpenDestination(index));
            }
            Close();
        }

        static HappiButton Button(Transform parent, string label, Action action)
        {
            var button = HappiUiKit.Button(parent, label, label, 15f, action);
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

        void OpenProfile(ProfilePanel.Tab tab)
        {
            Close();
            var wheel = GetComponent<HappiWheelHost>();
            if (wheel != null)
                wheel.OpenProfileFromRibbon(tab);
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
            var settingsView = GetComponent<HappiSettingsView>();
            if (settingsView != null)
                settingsView.TryOpenPage(page);
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
            _closedOverTrigger = true;
            _menu.gameObject.SetActive(false);
            HideFlyout();
        }

        void Update()
        {
            if (_canvas == null)
                return;
            if ((_window != null && !_window.IsCompanionVisible) || !_head.TryGet(Time.unscaledDeltaTime, out var head))
            {
                Close();
                _trigger.Button.gameObject.SetActive(false);
                return;
            }

            HappiUiKit.FollowCamera(_canvas, 0.42f);
            _trigger.Button.gameObject.SetActive(true);
            var area = OverlayVisibleArea.Get(_window);
            var scale = HappiTheme.Current.UiScale(Screen.dpi);
            Place(head, area, scale);
            var pointer = (Vector2)Input.mousePosition;
            if (_cursor != null && _cursor.TryGetUnityScreenCursor(out var x, out var y))
                pointer = new Vector2(x, y);
            pointer.y = Screen.height - pointer.y;
            var overTrigger = _triggerBounds.Contains(pointer);
            if (!overTrigger)
                _closedOverTrigger = false;
            if (Input.GetKeyDown(KeyCode.Escape))
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

            var muteLabel = _chat.Presenter.Muted ? "Unmute" : "Mute";
            if (_rows[0].Label.text != muteLabel)
                _rows[0].Label.text = muteLabel;
            _rows[1].Button.interactable = _settings.ScreenLook.Enabled;
            var overMain = _menuBounds.Contains(pointer);
            var overFlyout = _activeFlyout != Flyout.None && _flyoutBounds.Contains(pointer);
            if (overMain || overFlyout)
                _closeAt = Time.unscaledTime + HoverGrace;
            else if (!overTrigger && Time.unscaledTime >= _closeAt)
            {
                Close();
                return;
            }

            if (overMain)
            {
                var yInMenu = pointer.y - _menuBounds.y;
                var row = Mathf.FloorToInt(yInMenu / (32f * scale));
                if (row == 2 || row == 3)
                    ShowFlyout(row == 2 ? Flyout.Profile : Flyout.Ai);
                else
                    HideFlyout();
            }

            if (_activeFlyout != Flyout.None)
                PlaceFlyout(area, scale);
        }

        void Place(Vector2 head, Rect area, float scale)
        {
            var top = Screen.height - area.yMax;
            var bottom = Screen.height - area.yMin;
            var trigger = 30f * scale;
            var menuWidth = 146f * scale;
            var menuHeight = 198f * scale;
            var triggerX = Fit(head.x + 24f * scale, area.xMin, area.xMax - trigger);
            var triggerY = Fit(Screen.height - head.y - trigger, top, bottom - trigger);
            _triggerBounds = new Rect(triggerX, triggerY, trigger, trigger);
            HappiUiKit.Place(_trigger.Rect, triggerX, triggerY, trigger, trigger);
            var menuX = Fit(triggerX, area.xMin, area.xMax - menuWidth);
            var menuY = Fit(triggerY + trigger, top, bottom - menuHeight);
            _menuBounds = new Rect(menuX, menuY, menuWidth, menuHeight);
            HappiUiKit.Place(_menu, menuX, menuY, menuWidth, menuHeight);
            for (var i = 0; i < _rows.Length; i++)
            {
                var stagger = (i % 2) * 5f * scale;
                var rowTop = (i * 32f + (i == 5 ? 6f : 0f)) * scale;
                HappiUiKit.Place(_rows[i].Rect, stagger, rowTop, menuWidth - stagger, 29f * scale);
            }
        }

        void PlaceFlyout(Rect area, float scale)
        {
            var right = area.xMax - _menuBounds.xMax;
            var left = _menuBounds.xMin - area.xMin;
            var openRight = right >= 275f * scale || right >= left;
            var width = Mathf.Min(275f * scale, Mathf.Max(1f, openRight ? right : left));
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
            for (var i = 0; i < count; i++)
                HappiUiKit.Place(_destinations[i].Rect, (openRight ? 0f : width - buttonWidth) + 4f * scale, (6f + i * 28f) * scale,
                    Mathf.Max(1f, buttonWidth - 8f * scale), 25f * scale);
        }

        static float Fit(float value, float min, float max) => Mathf.Clamp(value, min, Mathf.Max(min, max));

        void OnDestroy()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }
    }
}
