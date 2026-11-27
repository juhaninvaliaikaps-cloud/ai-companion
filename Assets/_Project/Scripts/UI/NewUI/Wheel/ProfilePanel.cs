using System;
using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.Memory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>Everything the Profile panel shows, gathered by the wheel host when it opens.</summary>
    public sealed class ProfileData
    {
        public string Name = string.Empty;
        public string RoleLine = string.Empty;

        /// <summary>JUH-154: her picture, or null; shown round or square.</summary>
        public Texture Picture;
        public bool PictureRound = true;
        public ProfileFeelings Feelings;
        public ProfilePersonality Personality;

        /// <summary>JUH-123: her secrets, shown on the Personality tab. Null when the relationship is not loaded.</summary>
        public ProfileSecrets Secrets;

        /// <summary>JUH-125: her 18+ secrets. Null while the 18+ gate is closed.</summary>
        public ProfileIntimateSecrets IntimateSecrets;
        public ProfileWorld World;
        public string MemoriesSummary = string.Empty;
        public readonly List<string> Memories = new List<string>();
        public int MemoryCount;
        public readonly List<string> Timeline = new List<string>();
        public int TimelineCount;
    }

    /// <summary>
    /// JUH-121: her Profile, opened from the wheel (for now from the Mood slice; Jussi decides
    /// the wheel later). Four tabs: Feelings (option A, gauges), Personality (temperament radar,
    /// innocence while 18+ is on), Her world (what Pisara wrote about her life) and Memories.
    /// Built like <see cref="WheelCard"/>: plain UGUI objects under the wheel's canvas. A tab is
    /// rebuilt only when it is opened, never per frame.
    /// </summary>
    public sealed class ProfilePanel
    {
        public enum Tab
        {
            Feelings = 0,
            Personality = 1,
            World = 2,
            Memories = 3,
        }

        static readonly string[] TabLabels = { "Feelings", "Personality", "Her world", "Memories" };
        const int MaxBackstoryCollapsed = 6;
        static readonly Color32 RibbonInk = new(14, 18, 24, 255);
        static readonly Color32 RibbonCard = new(36, 42, 53, 255);
        static readonly Color32 RibbonMint = new(114, 238, 207, 255);

        readonly HappiTheme _theme;
        readonly bool _ribbon;
        readonly RawImage _ribbonArt;
        readonly RibbonShape _ribbonArtShape;
        readonly RibbonGlyph _ribbonClose;
        readonly Scrollbar _ribbonScroll;
        readonly RectTransform _root;
        readonly TextMeshProUGUI _name;
        readonly TextMeshProUGUI _role;
        readonly HappiButton _close;
        readonly List<HappiButton> _tabs = new List<HappiButton>();
        readonly ScrollRect _scroll;
        readonly RectTransform _viewport;
        readonly RectTransform _content;
        readonly RectTransform _tip;
        readonly TextMeshProUGUI _tipText;
        readonly Image _tipBackground;

        Func<ProfileData> _source;
        ProfileData _data;
        Tab _tab = Tab.Feelings;
        bool _allBackstory;
        readonly float[] _tabScrollOffsets = new float[TabLabels.Length];
        float _scale = 1f;
        float _y;
        float _inner;

        /// <summary>The close button and Escape.</summary>
        public Action OnClose;

        /// <summary>"Manage in Settings" on the Memories tab.</summary>
        public Action OpenSettings;
        public Action EditTemperament;

        public ProfilePanel(Transform parent, HappiTheme theme)
        {
            _theme = theme;
            _ribbon = RibbonMenu.Enabled;
            var panel = HappiUiKit.Panel(parent, "ProfilePanel", _ribbon ? (Color)RibbonInk : theme.PanelBackground, true);
            _root = panel.rectTransform;
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            if (_ribbon)
            {
                RibbonShape.Apply(panel, RibbonShape.Kind.ChatFrame);
                _ribbonArt = HappiUiKit.Rect(_root, "Happi-Mood banner").gameObject.AddComponent<RawImage>();
                _ribbonArt.texture = Resources.Load<Texture2D>("RibbonArt/Happi-Mood");
                _ribbonArt.color = _ribbonArt.texture != null ? Color.white : new Color32(255, 202, 34, 255);
                _ribbonArt.raycastTarget = false;
                _ribbonArtShape = RibbonShape.Apply(_ribbonArt, RibbonShape.Kind.ChatHeader);
            }
            else
            {
                var outline = panel.gameObject.AddComponent<Outline>();
                outline.effectColor = theme.Border;
                outline.effectDistance = new Vector2(1f, -1f);
                _picture = PersonaPictureCache.Frame(_root, "Picture", theme, out _pictureFrame);
                _pictureFrame.gameObject.SetActive(false);
                _name = HappiUiKit.Text(_root, "Name", string.Empty, theme.FontTitle, theme.Text, TextAlignmentOptions.TopLeft, true);
                _role = HappiUiKit.Text(_root, "Role", string.Empty, theme.FontSmall, theme.Text, TextAlignmentOptions.TopLeft, false);
            }
            _close = HappiUiKit.Button(_root, "Close", "X", theme.FontTitle, () => { if (OnClose != null) OnClose(); });
            if (_ribbon)
            {
                StyleRibbonButton(_close);
                _close.Label.gameObject.SetActive(false);
                _ribbonClose = RibbonGlyph.Add(_close.Rect, "Close profile", RibbonGlyph.Mark.Close, theme.Text);
                _ribbonClose.Label = _close.Label;
            }
            for (var i = 0; i < TabLabels.Length; i++)
            {
                var index = i;
                var tab = HappiUiKit.Button(_root, "Tab" + TabLabels[i], TabLabels[i], theme.FontSmall, () => Select((Tab)index));
                if (_ribbon)
                {
                    StyleRibbonButton(tab);
                    tab.Label.enableAutoSizing = true;
                    tab.Label.overflowMode = TextOverflowModes.Ellipsis;
                }
                _tabs.Add(tab);
            }

            var body = HappiUiKit.Panel(_root, "Body", new Color(0f, 0f, 0f, 0f), true);
            _scroll = body.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;
            _viewport = HappiUiKit.Rect(body.transform, "Viewport");
            HappiUiKit.Stretch(_viewport, 0f, 0f, 0f, 0f);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = _viewport;
            _content = HappiUiKit.Rect(_viewport, "Content");
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = new Vector2(0f, 8f);
            _scroll.content = _content;

            if (_ribbon)
            {
                var track = HappiUiKit.Panel(_root, "Profile scrollbar", new Color32(39, 49, 60, 255), true);
                _ribbonScroll = track.gameObject.AddComponent<Scrollbar>();
                var trackArea = HappiUiKit.Rect(track.transform, "Track");
                HappiUiKit.Stretch(trackArea, 0f, 0f, 0f, 0f);
                var thumb = HappiUiKit.Panel(trackArea, "Thumb", new Color32(114, 238, 207, 225), true);
                HappiUiKit.Stretch(thumb.rectTransform, 0f, 0f, 0f, 0f);
                _ribbonScroll.targetGraphic = thumb;
                _ribbonScroll.handleRect = thumb.rectTransform;
                _ribbonScroll.direction = Scrollbar.Direction.BottomToTop;
                _scroll.verticalScrollbar = _ribbonScroll;
                _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }

            // JUH-133: a readable popup for the small secret/kink chips. It lives on the
            // profile root, outside the masked scroll body, so it is not clipped.
            var tip = HappiUiKit.Panel(_root, "SecretTip", theme.PanelBackground, false);
            _tipBackground = tip;
            _tip = tip.rectTransform;
            _tip.anchorMin = _tip.anchorMax = new Vector2(0.5f, 0.5f);
            _tip.pivot = new Vector2(0.5f, 0f);
            _tipText = HappiUiKit.Text(_tip, "TipText", string.Empty, theme.FontBody, theme.Text, TextAlignmentOptions.TopLeft, false);
            _tipText.textWrappingMode = TextWrappingModes.Normal;
            _tipText.overflowMode = TextOverflowModes.Overflow;
            _tipText.raycastTarget = false;
            _tip.gameObject.SetActive(false);
            if (_ribbon)
                RibbonChatFrameEdge.Add(_root);
            _root.gameObject.SetActive(false);
        }

        public bool Visible => _root.gameObject.activeSelf;

        // JUH-154: her picture left of her name and role line.
        Image _pictureFrame;
        RawImage _picture;
        float _headerPad;
        float _headerGap;
        float _headerNameH;
        float _headerRoleH;
        float _headerClose;

        void PlaceHeader(bool picture)
        {
            var size = _headerNameH + _headerRoleH;
            var x = _headerPad;
            if (picture)
            {
                HappiUiKit.Place(_pictureFrame.rectTransform, _headerPad, _headerPad, size, size);
                x += size + _headerGap;
            }

            var width = _inner - _headerClose - _headerGap - (x - _headerPad);
            HappiUiKit.Place(_name.rectTransform, x, _headerPad, width, _headerNameH);
            HappiUiKit.Place(_role.rectTransform, x, _headerPad + _headerNameH, width, _headerRoleH);
        }

        public void Show(Func<ProfileData> source, Vector2 center, float scale, Vector2 screen, Tab? initialTab = null,
            Rect? visibleArea = null)
        {
            _source = source;
            _scale = Mathf.Max(1f, scale);
            _allBackstory = false;
            if (_ribbon)
            {
                Array.Clear(_tabScrollOffsets, 0, _tabScrollOffsets.Length);
                ShowRibbon(center, scale, visibleArea ?? new Rect(Vector2.zero, screen));
            }
            else
                ShowClassic(center, screen);

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Select(initialTab ?? _tab, rememberScroll: false);
            if (_ribbon)
                _tabs[(int)_tab].Button.Select();
        }

        void ShowClassic(Vector2 center, Vector2 screen)
        {
            var s = _scale;
            var pad = _theme.Padding * s;
            var gap = _theme.Spacing * s;
            var width = Mathf.Min(420f * s, screen.x - 16f);
            var height = Mathf.Min(620f * s, screen.y - 16f);
            _inner = width - 2f * pad;
            _root.sizeDelta = new Vector2(width, height);
            var x = Mathf.Clamp(center.x, width * 0.5f + 8f, Mathf.Max(width * 0.5f + 8f, screen.x - width * 0.5f - 8f));
            var y = Mathf.Clamp(center.y, height * 0.5f + 8f, Mathf.Max(height * 0.5f + 8f, screen.y - height * 0.5f - 8f));
            _root.anchoredPosition = new Vector2(x, y);

            var closeSize = _theme.ButtonHeight * s;
            HappiUiKit.Place(_close.Rect, width - pad - closeSize, pad, closeSize, closeSize);
            _close.Label.fontSize = _theme.FontTitle * s;
            _name.fontSize = _theme.FontTitle * s;
            _role.fontSize = _theme.FontSmall * s;
            var nameH = _theme.FontTitle * s * 1.35f;
            var roleH = _theme.FontSmall * s * 1.4f;
            _headerPad = pad;
            _headerGap = gap;
            _headerNameH = nameH;
            _headerRoleH = roleH;
            _headerClose = closeSize;
            PlaceHeader(false);

            var tabsY = pad + nameH + roleH + gap;
            var rowH = _theme.ButtonHeight * s;
            var tabW = (_inner - gap * (_tabs.Count - 1)) / _tabs.Count;
            for (var i = 0; i < _tabs.Count; i++)
            {
                HappiUiKit.Place(_tabs[i].Rect, pad + i * (tabW + gap), tabsY, tabW, rowH);
                _tabs[i].Label.fontSize = _theme.FontSmall * s;
            }

            var bodyY = tabsY + rowH + gap;
            HappiUiKit.Place((RectTransform)_scroll.transform, pad, bodyY, _inner, height - bodyY - pad);
        }

        void ShowRibbon(Vector2 center, float scale, Rect visibleArea)
        {
            var layout = new RibbonProfileLayout(visibleArea, center, scale);
            _scale = layout.Scale;
            var s = _scale;
            var frame = layout.Frame;
            var pad = layout.Padding;
            _inner = frame.width - 2f * pad;
            _root.sizeDelta = frame.size;
            _root.anchoredPosition = frame.center;
            _ribbonArtShape.SetHeaderCorners(RibbonShape.ChatFrameCorners(frame.width, frame.height));
            HappiUiKit.Place(_ribbonArt.rectTransform, 0f, 0f, frame.width, layout.HeaderHeight);
            var close = layout.Close;
            HappiUiKit.Place(_close.Rect, close.x, close.y, close.width, close.height);
            HappiUiKit.Place(_ribbonClose.rectTransform, 5f * s, 5f * s, 20f * s, 20f * s);

            for (var i = 0; i < _tabs.Count; i++)
            {
                var tab = layout.Tab(i);
                HappiUiKit.Place(_tabs[i].Rect, tab.x, tab.y, tab.width, tab.height);
                _tabs[i].Label.fontSizeMin = 10f * s;
                _tabs[i].Label.fontSizeMax = 14f * s;
            }

            HappiUiKit.Place((RectTransform)_scroll.transform, pad, layout.BodyTop, _inner,
                frame.height - layout.BodyTop - pad);
            HappiUiKit.Place((RectTransform)_ribbonScroll.transform, frame.width - pad + 4f * s, layout.BodyTop,
                5f * s, frame.height - layout.BodyTop - pad);
        }

        static void StyleRibbonButton(HappiButton button)
        {
            RibbonShape.Apply(button.Image, true);
            var colors = button.Button.colors;
            colors.normalColor = RibbonInk;
            colors.highlightedColor = RibbonMenu.Cyan;
            colors.pressedColor = new Color32(255, 202, 34, 255);
            colors.selectedColor = colors.normalColor;
            button.Button.colors = colors;
            button.Button.GetComponent<HappiHoverText>().Hover = RibbonMenu.Ink;
        }

        public void Hide()
        {
            if (_ribbon && Visible)
            {
                var events = EventSystem.current;
                var selected = events != null ? events.currentSelectedGameObject : null;
                if (selected != null && selected.transform.IsChildOf(_root))
                    events.SetSelectedGameObject(null);
            }
            HideTip();
            _root.gameObject.SetActive(false);
        }

        public bool Contains(Vector2 unityScreen)
        {
            if (!Visible)
                return false;
            var size = _root.sizeDelta;
            var center = _root.anchoredPosition;
            return Mathf.Abs(unityScreen.x - center.x) <= size.x * 0.5f + 2f && Mathf.Abs(unityScreen.y - center.y) <= size.y * 0.5f + 2f;
        }

        void Select(Tab tab, bool rememberScroll = true)
        {
            if (_ribbon && rememberScroll)
            {
                if (tab == _tab)
                    return;
                _tabScrollOffsets[(int)_tab] = _content.anchoredPosition.y;
            }
            HideTip();
            _tab = tab;
            _data = _source != null ? _source() : null;
            if (_data == null)
                _data = new ProfileData();
            if (!_ribbon)
            {
                _name.text = HappiUiKit.Literal(_data.Name);
                _role.text = HappiUiKit.Literal(_data.RoleLine);
                PersonaPictureCache.Show(_pictureFrame, _picture, _data.Picture, _data.PictureRound);
                PlaceHeader(_data.Picture != null);
            }
            for (var i = 0; i < _tabs.Count; i++)
            {
                var colors = _tabs[i].Button.colors;
                var selected = i == (int)tab;
                colors.normalColor = _ribbon ? (selected ? RibbonMenu.Cyan : (Color)RibbonInk)
                    : selected ? _theme.Selected : _theme.Button;
                colors.selectedColor = colors.normalColor;
                _tabs[i].Button.colors = colors;
                if (_ribbon)
                    _tabs[i].Button.GetComponent<HappiHoverText>().HoldHoverColor = selected;
            }

            Rebuild(_ribbon ? _tabScrollOffsets[(int)tab] : 0f);
        }

        void Rebuild(float scrollOffset)
        {
            HideTip();
            for (var i = _content.childCount - 1; i >= 0; i--)
            {
                var oldRow = _content.GetChild(i).gameObject;
                oldRow.SetActive(false);
                UnityEngine.Object.Destroy(oldRow);
            }
            _scroll.StopMovement();
            _y = 0f;
            switch (_tab)
            {
                case Tab.Personality: BuildPersonality(); break;
                case Tab.World: BuildWorld(); break;
                case Tab.Memories: BuildMemories(); break;
                default: BuildFeelings(); break;
            }

            _content.sizeDelta = new Vector2(0f, _y + 8f);
            if (_ribbon)
            {
                SetScrollOffset(scrollOffset);
                _ribbonScroll.gameObject.SetActive(ScrollRange() > 0f);
            }
            else
                _scroll.verticalNormalizedPosition = 1f;
        }

        float ScrollRange() => Mathf.Max(0f, _content.rect.height - _viewport.rect.height);

        void SetScrollOffset(float offset)
        {
            _scroll.StopMovement();
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, Mathf.Clamp(offset, 0f, ScrollRange()));
        }

        internal void CycleTab(int direction)
        {
            var next = ((int)_tab + direction + _tabs.Count) % _tabs.Count;
            Select((Tab)next);
            _tabs[next].Button.Select();
        }

        internal void ScrollPage(int direction) =>
            SetScrollOffset(_content.anchoredPosition.y + direction * _viewport.rect.height * 0.88f);

        internal void ScrollToEdge(bool bottom) => SetScrollOffset(bottom ? ScrollRange() : 0f);

        // ---- Tabs ------------------------------------------------------------------------

        void BuildFeelings()
        {
            var f = _data.Feelings;
            if (f == null)
            {
                Paragraph("Not available yet.", false);
                return;
            }

            if (_ribbon)
                BuildMood(f);
            var box = Begin();
            Heading(_ribbon ? "Your relationship" : "How she feels about you");
            Row("Affection", f.AffectionValue);
            AffectionBar(f.AffectionMarker);
            Row("Familiarity", f.FamiliarityValue);
            FillBar(f.FamiliarityFill, _theme.Hover);
            End(box);

            if (!_ribbon)
                BuildMood(f);

            box = Begin();
            Heading(_ribbon ? "Recent changes" : "Why it moved recently");
            if (f.Reasons.Count == 0)
                Paragraph(f.ReasonsNote, true);
            for (var i = 0; i < f.Reasons.Count; i++)
                Paragraph("• " + f.Reasons[i], false);
            End(box);
        }

        void BuildMood(ProfileFeelings f)
        {
            var box = Begin();
            Heading(f.MoodTitle);
            for (var i = 0; i < f.Moods.Count; i++)
            {
                Row(f.Moods[i].Label, f.Moods[i].Value);
                FillBar(f.Moods[i].Fill, _theme.Hover);
            }
            End(box);
        }

        void BuildPersonality()
        {
            var p = _data.Personality;
            if (p == null)
            {
                Paragraph("Not available yet.", false);
                return;
            }

            var box = Begin();
            Heading(p.Title);
            if (_ribbon)
            {
                Paragraph(p.Reason, !p.HasReading);
                if (EditTemperament != null)
                    Button("Edit temperament", EditTemperament);
                End(box);
                if (p.HasSliders || p.HasReading)
                {
                    box = Begin();
                    Heading("Temperament");
                    if (p.HasSliders)
                    {
                        for (var i = 0; i < TemperamentAxes.Count; i++)
                        {
                            var value = p.Sliders[i];
                            var step = TemperamentSteps.DisplayStep(value);
                            Row(TemperamentAxes.Names[i], $"{ProfilePersonality.SliderWord(i, value)} · {step}/{TemperamentSteps.Max}");
                        }
                        Paragraph("Curiosity does not affect her mood yet.", true);
                    }
                    else
                        Radar(p.Axes);
                    End(box);
                }
            }
            else
                BuildClassicTemperament(p, box);

            BuildSecrets(_data.Secrets);

            if (p.ShowInnocence)
            {
                box = Begin();
                Heading("Innocent to Perverted (18+)");
                Row("Innocent", p.InnocenceValue + "   Perverted");
                FillBar(p.Innocence / 100f, _theme.Intimate);
                if (p.ShowDesire)
                {
                    Row("Desire", p.DesireValue);
                    FillBar(p.Desire / 100f, _theme.Intimate);
                    Row("Libido", p.LibidoValue);
                    FillBar(p.Libido / 100f, _theme.Hover);
                }

                Paragraph("Set innocence and libido on Settings → 18+ → Her. Desire moves on its own.", true);
                End(box);
            }

            BuildIntimateSecrets(_data.IntimateSecrets);
        }

        void BuildClassicTemperament(ProfilePersonality p, RectTransform box)
        {
            if (p.HasSliders)
            {
                // JUH-136 (Jussi): the nine sliders she runs on, instead of the 5-axis radar.
                for (var i = 0; i < TemperamentAxes.Count; i++)
                {
                    var value = p.Sliders[i];
                    Row(TemperamentAxes.Names[i], ProfilePersonality.SliderWord(i, value) + "  " + Mathf.RoundToInt(value));
                    FillBar(value / 100f, i == TemperamentAxes.Curiosity ? _theme.Disabled : _theme.Hover);
                }

                Paragraph("Each runs from 0 to 100. Curiosity does not change her mood yet. Set them on Settings → Her → Personality.", true);
            }
            else if (p.HasReading)
            {
                Radar(p.Axes);
            }

            if (p.Reason.Length > 0)
                Paragraph(p.Reason, !p.HasReading);
            End(box);
        }

        /// <summary>
        /// JUH-123: "Her secrets · 3 of 8 found", two groups of chips. A found one shows its
        /// text, one not found yet is a locked chip, and the newest find has a gold outline.
        /// </summary>
        void BuildSecrets(ProfileSecrets s)
        {
            if (s == null)
                return;
            var box = Begin();
            Heading(s.Total == 0 ? "Her secrets" : s.Title);
            if (s.Total == 0)
            {
                Paragraph("Her description has not been read yet, so she has no secrets to find.", true);
                End(box);
                return;
            }

            Paragraph("Find out what she likes and dislikes by talking with her.", true);
            SecretGroupChips("What stings her", s, SecretGroup.Stings);
            SecretGroupChips("What wins her over", s, SecretGroup.WinsHerOver);
            End(box);
        }

        struct Chip
        {
            public string Text;
            public bool Found;
            public bool Newest;
        }

        readonly List<Chip> _chips = new List<Chip>(8);

        void SecretGroupChips(string title, ProfileSecrets s, SecretGroup group)
        {
            _chips.Clear();
            for (var i = 0; i < s.Items.Count; i++)
            {
                if (s.Items[i].Group == group)
                    _chips.Add(new Chip { Text = s.Items[i].Text, Found = s.Items[i].Found, Newest = s.Items[i].Newest });
            }

            Chips(title);
        }

        void IntimateGroupChips(string title, ProfileIntimateSecrets s, IntimateSecretGroup group)
        {
            _chips.Clear();
            for (var i = 0; i < s.Items.Count; i++)
            {
                if (s.Items[i].Group == group)
                    _chips.Add(new Chip { Text = s.Items[i].Text, Found = s.Items[i].Found, Newest = s.Items[i].Newest });
            }

            Chips(title);
        }

        /// <summary>A sub-heading and a flowing row of chips from <see cref="_chips"/>. Nothing when it is empty.</summary>
        void Chips(string title)
        {
            if (_chips.Count == 0)
                return;
            SubHeading(title);
            var size = _theme.FontSmall * _scale;
            var height = size * 1.9f;
            var padX = 10f * _scale;
            var x = 0f;
            for (var i = 0; i < _chips.Count; i++)
            {
                var item = _chips[i];
                var text = HappiUiKit.Text(_content, "SecretText", HappiUiKit.Literal(item.Text), size,
                    item.Found ? _theme.Text : _theme.TextMuted, TextAlignmentOptions.Center, item.Newest);
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = true;
                var hover = text.gameObject.AddComponent<ProfileChipHover>();
                hover.Text = item.Found
                    ? title + "\n" + item.Text
                    : title + "\nNot found yet. Talk with her to uncover this.";
                hover.Enter = OnChipEnter;
                hover.Exit = OnChipExit;
                var width = Mathf.Min(TextWidth, text.GetPreferredValues(text.text, 0f, 0f).x + 2f * padX);
                if (x > 0f && x + width > TextWidth)
                {
                    x = 0f;
                    _y += height + Gap * 0.5f;
                }

                var border = HappiUiKit.Panel(_content, "Secret", item.Newest ? SecretGold : (item.Found ? _theme.Border : _theme.Gap), false);
                HappiUiKit.Place(border.rectTransform, Pad + x, _y, width, height);
                var thick = (item.Newest ? 2f : 1f) * _scale;
                var fill = HappiUiKit.Panel(_content, "SecretFill", item.Found ? _theme.Button : _theme.PanelInset, false);
                HappiUiKit.Place(fill.rectTransform, Pad + x + thick, _y + thick, width - 2f * thick, height - 2f * thick);
                text.transform.SetAsLastSibling();
                HappiUiKit.Place(text.rectTransform, Pad + x, _y, width, height);
                x += width + Gap * 0.5f;
            }

            _y += height + Gap;
        }

        /// <summary>
        /// JUH-125: "Her 18+ secrets · 1 of 6 found", only while the gate is open: Kinks,
        /// Sensitive areas, Shy to do, and how shy she is now.
        /// </summary>
        // ---- Secret / kink tooltip ------------------------------------------------------

        void OnChipEnter(ProfileChipHover chip)
        {
            if (chip == null || _tip == null || string.IsNullOrEmpty(chip.Text))
                return;
            var s = _scale;
            var pad = 10f * s;
            var width = Mathf.Min(360f * s, _root.sizeDelta.x - 2f * pad);
            _tipText.fontSize = _theme.FontBody * s;
            _tipText.text = HappiUiKit.Literal(chip.Text);
            var contentWidth = Mathf.Max(20f, width - 2f * pad);
            var height = Mathf.Max(24f * s, _tipText.GetPreferredValues(_tipText.text, contentWidth, 0f).y + 2f * pad);
            _tip.sizeDelta = new Vector2(width, height);
            _tipText.rectTransform.anchorMin = _tipText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _tipText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _tipText.rectTransform.sizeDelta = new Vector2(contentWidth, height - 2f * pad);
            _tipText.rectTransform.anchoredPosition = Vector2.zero;

            var anchor = chip.Rect;
            if (anchor == null)
                return;
            var canvas = _root.GetComponentInParent<Canvas>();
            var camera = canvas != null ? canvas.worldCamera : null;
            var world = anchor.TransformPoint(anchor.rect.center);
            var screen = RectTransformUtility.WorldToScreenPoint(camera, world);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, camera, out local))
                return;

            var halfW = _root.sizeDelta.x * 0.5f;
            var halfH = _root.sizeDelta.y * 0.5f;
            local.x = Mathf.Clamp(local.x, -halfW + width * 0.5f + 4f, halfW - width * 0.5f - 4f);
            local.y = Mathf.Clamp(local.y + anchor.rect.height * 0.5f + 8f * s,
                0f, halfH - height * 0.5f - 4f);
            _tip.anchoredPosition = local;
            _tip.SetAsLastSibling();
            _tip.gameObject.SetActive(true);
        }

        void OnChipExit(ProfileChipHover chip)
        {
            HideTip();
        }

        void HideTip()
        {
            if (_tip != null && _tip.gameObject.activeSelf)
                _tip.gameObject.SetActive(false);
        }

        sealed class ProfileChipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public string Text;
            public RectTransform Rect;
            public Action<ProfileChipHover> Enter;
            public Action<ProfileChipHover> Exit;

            void Awake()
            {
                Rect = transform as RectTransform;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Enter != null)
                    Enter(this);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (Exit != null)
                    Exit(this);
            }
        }

        void BuildIntimateSecrets(ProfileIntimateSecrets s)
        {
            if (s == null)
                return;
            var box = Begin();
            Heading(s.Total == 0 ? "Her 18+ secrets" : s.Title);
            if (s.Status.Length > 0)
                Paragraph(s.Status, true);
            if (s.Total > 0)
            {
                Paragraph("Found during intimate moments. Doing one of her kinks or touching a sensitive area again always pleases her a little more.", true);
                IntimateGroupChips("Kinks", s, IntimateSecretGroup.Kink);
                IntimateGroupChips("Sensitive areas", s, IntimateSecretGroup.Sensitive);
                IntimateGroupChips("Shy to do", s, IntimateSecretGroup.Shy);
                Paragraph(s.ShyLine, true);
            }

            End(box);
        }

        /// <summary>The gold of a secret found (the same as its floating piece).</summary>
        static readonly Color SecretGold = HappiTheme.Hex("#FFD166");

        void BuildWorld()
        {
            var w = _data.World;
            if (w == null || w.IsEmpty)
            {
                Paragraph("Her world has not been written yet. It is written from her description while \"Her own world\" is on (Settings → Her → Her life).", true);
                return;
            }

            if (w.RightNow.Length > 0)
            {
                var box = Begin();
                Heading("Right now");
                Paragraph(w.RightNow, false);
                End(box);
            }

            if (w.Waiting.Count > 0 || (!_ribbon && w.WhileAway.Count > 0))
            {
                var box = Begin();
                Heading(_ribbon ? "Open threads" : "Things she is waiting for");
                for (var i = 0; i < w.Waiting.Count; i++)
                    Paragraph("• " + w.Waiting[i], false);
                if (!_ribbon && w.WhileAway.Count > 0)
                {
                    SubHeading("While you were away");
                    for (var i = 0; i < w.WhileAway.Count; i++)
                        Paragraph(w.WhileAway[i].When + ": " + w.WhileAway[i].Text, false);
                }

                End(box);
            }

            if (_ribbon && w.WhileAway.Count > 0)
            {
                var box = Begin();
                Heading("While you were away");
                for (var i = 0; i < w.WhileAway.Count; i++)
                    Paragraph(w.WhileAway[i].When + ": " + w.WhileAway[i].Text, false);
                End(box);
            }

            if (w.Week.Count > 0)
            {
                var box = Begin();
                Heading(_ribbon ? "Recent days" : "Her last days");
                for (var i = 0; i < w.Week.Count; i++)
                    Paragraph(w.Week[i].When + ": " + w.Week[i].Text, false);
                End(box);
            }

            if (w.Backstory.Count > 0)
            {
                var box = Begin();
                Heading("Her backstory");
                var shown = _allBackstory ? w.Backstory.Count : Mathf.Min(MaxBackstoryCollapsed, w.Backstory.Count);
                for (var i = 0; i < shown; i++)
                    Paragraph("• " + w.Backstory[i], false);
                if (w.Backstory.Count > MaxBackstoryCollapsed)
                    Button(_allBackstory ? "Show fewer" : "Show all " + w.Backstory.Count, () =>
                    {
                        _allBackstory = !_allBackstory;
                        Rebuild(_ribbon ? _content.anchoredPosition.y : 0f);
                    });
                End(box);
            }
        }

        void BuildMemories()
        {
            var box = Begin();
            Heading("What she remembers about you");
            Paragraph(_data.MemoriesSummary, true);
            if (_ribbon && OpenSettings != null)
                Button("Manage memories", OpenSettings);
            for (var i = 0; i < _data.Memories.Count; i++)
                Paragraph("• " + _data.Memories[i], false);
            if (_data.MemoryCount > _data.Memories.Count)
                Paragraph("And " + (_data.MemoryCount - _data.Memories.Count) + " more.", true);

            if (_ribbon)
            {
                End(box);
                box = Begin();
            }
            Heading("Relationship timeline");
            if (_data.TimelineCount == 0)
            {
                Paragraph("Nothing dated yet. A session summary becomes a timeline entry when a conversation ends.", true);
            }
            else
            {
                for (var i = 0; i < _data.Timeline.Count; i++)
                    Paragraph("• " + _data.Timeline[i], false);
                if (_data.TimelineCount > _data.Timeline.Count)
                    Paragraph("And " + (_data.TimelineCount - _data.Timeline.Count) + " earlier.", true);
            }

            if (!_ribbon)
                Button("Manage in Settings", () => { if (OpenSettings != null) OpenSettings(); });
            End(box);
        }

        // ---- Building blocks -------------------------------------------------------------

        float Pad => _theme.Padding * _scale;
        float Gap => _theme.Spacing * _scale;

        /// <summary>Starts an inset box; its background is sized by <see cref="End"/>.</summary>
        RectTransform Begin()
        {
            var bg = HappiUiKit.Panel(_content, "Box", _ribbon ? (Color)RibbonCard : _theme.PanelInset, false);
            HappiUiKit.Place(bg.rectTransform, 0f, _y, _inner, 0f);
            if (_ribbon)
            {
                RibbonShape.Apply(bg, RibbonShape.Kind.Panel);
                var accent = HappiUiKit.Panel(bg.transform, "Section accent", RibbonMint, false);
                HappiUiKit.Place(accent.rectTransform, 0f, 0f, 3f * _scale, 30f * _scale);
            }
            _y += Pad;
            return bg.rectTransform;
        }

        void End(RectTransform box)
        {
            var top = -box.anchoredPosition.y;
            _y += Pad - Gap;
            box.sizeDelta = new Vector2(_inner, _y - top);
            _y += Gap * 1.5f;
        }

        float TextWidth => _inner - 2f * Pad;

        void Heading(string text)
        {
            AddText(text, _ribbon ? 17f : _theme.FontBody, _theme.Text, true);
        }

        void SubHeading(string text)
        {
            AddText(text, _theme.FontSmall, _theme.Text, true);
        }

        void Paragraph(string text, bool muted)
        {
            if (string.IsNullOrEmpty(text))
                return;
            AddText(text, _ribbon ? 14f : _theme.FontSmall, muted ? _theme.TextMuted : _theme.Text, false);
        }

        void AddText(string value, float size, Color color, bool bold)
        {
            var text = HappiUiKit.Text(_content, "Text", HappiUiKit.Literal(value), size * _scale, color, TextAlignmentOptions.TopLeft, bold);
            text.overflowMode = TextOverflowModes.Overflow;
            var height = text.GetPreferredValues(text.text, TextWidth, 0f).y;
            HappiUiKit.Place(text.rectTransform, Pad, _y, TextWidth, height);
            _y += height + Gap * 0.6f;
        }

        /// <summary>A label on the left and its value on the right, on one line.</summary>
        void Row(string label, string value)
        {
            var size = (_ribbon ? 14f : _theme.FontSmall) * _scale;
            var height = size * 1.45f;
            var left = HappiUiKit.Text(_content, "RowLabel", HappiUiKit.Literal(label), size, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            var labelWidth = _ribbon ? 0.44f : 0.4f;
            HappiUiKit.Place(left.rectTransform, Pad, _y, TextWidth * labelWidth, height);
            var right = HappiUiKit.Text(_content, "RowValue", HappiUiKit.Literal(value), size, _theme.Text, TextAlignmentOptions.MidlineRight, true);
            if (_ribbon)
            {
                right.textWrappingMode = TextWrappingModes.NoWrap;
                right.overflowMode = TextOverflowModes.Ellipsis;
                right.enableAutoSizing = true;
                right.fontSizeMin = 10f * _scale;
                right.fontSizeMax = size;
            }
            var split = _ribbon ? 0.46f : 0.3f;
            HappiUiKit.Place(right.rectTransform, Pad + TextWidth * split, _y, TextWidth * (1f - split), height);
            _y += height + Gap * 0.3f;
        }

        void FillBar(float fill, Color color)
        {
            var height = 8f * _scale;
            var track = HappiUiKit.Panel(_content, "Track", _theme.Gap, false);
            HappiUiKit.Place(track.rectTransform, Pad, _y, TextWidth, height);
            var amount = Mathf.Clamp01(float.IsNaN(fill) ? 0f : fill);
            if (amount > 0.005f)
            {
                var bar = HappiUiKit.Panel(_content, "Fill", _ribbon && color == _theme.Hover ? RibbonMenu.Cyan : color, false);
                HappiUiKit.Place(bar.rectTransform, Pad, _y, Mathf.Max(height, TextWidth * amount), height);
            }

            _y += height + Gap;
        }

        /// <summary>−100 to +100: filled from the middle toward the marker, pink when she likes you, grey when not.</summary>
        void AffectionBar(float marker)
        {
            var height = 8f * _scale;
            var knob = 16f * _scale;
            var top = _y + (knob - height) * 0.5f;
            var track = HappiUiKit.Panel(_content, "Track", _theme.Gap, false);
            HappiUiKit.Place(track.rectTransform, Pad, top, TextWidth, height);
            var amount = Mathf.Clamp01(float.IsNaN(marker) ? 0.5f : marker);
            var middle = Pad + TextWidth * 0.5f;
            var at = Pad + TextWidth * amount;
            var fill = HappiUiKit.Panel(_content, "Fill", amount >= 0.5f ? _theme.Intimate : _theme.Disabled, false);
            HappiUiKit.Place(fill.rectTransform, Mathf.Min(middle, at), top, Mathf.Max(2f, Mathf.Abs(at - middle)), height);
            var tick = HappiUiKit.Panel(_content, "Zero", _theme.Text, false);
            HappiUiKit.Place(tick.rectTransform, middle - 1f, _y, 2f, knob);
            var dot = HappiUiKit.Rect(_content, "Marker");
            var image = dot.gameObject.AddComponent<Image>();
            image.sprite = HappiUiKit.Circle();
            image.color = _theme.Text;
            image.raycastTarget = false;
            HappiUiKit.Place(dot, at - knob * 0.5f, _y, knob, knob);
            _y += knob + Gap * 0.3f;

            var size = _theme.FontSmall * 0.9f * _scale;
            var scale = HappiUiKit.Text(_content, "Scale", "−100 can't stand you", size, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, false);
            HappiUiKit.Place(scale.rectTransform, Pad, _y, TextWidth * 0.5f, size * 1.4f);
            var end = HappiUiKit.Text(_content, "ScaleEnd", "adores you 100", size, _theme.TextMuted, TextAlignmentOptions.MidlineRight, false);
            HappiUiKit.Place(end.rectTransform, Pad + TextWidth * 0.5f, _y, TextWidth * 0.5f, size * 1.4f);
            _y += size * 1.4f + Gap;
        }

        void Radar(float[] axes)
        {
            var side = Mathf.Min(TextWidth, 260f * _scale);
            var x = Pad + (TextWidth - side) * 0.5f;
            var rect = HappiUiKit.Rect(_content, "Radar");
            HappiUiKit.Place(rect, x, _y, side, side);
            var radar = rect.gameObject.AddComponent<RadarGraphic>();
            radar.Set(axes, HappiTheme.WithAlpha(_theme.Hover, 0.35f), _theme.Hover, HappiTheme.WithAlpha(_theme.Border, 0.4f), 2f * _scale);

            var labelW = 90f * _scale;
            var labelH = _theme.FontSmall * _scale * 1.4f;
            for (var i = 0; i < ProfilePersonality.AxisNames.Length && i < axes.Length; i++)
            {
                // Point() is local to the radar's top-left pivot, with y up; the content runs y down.
                var p = radar.Point(i, 1.22f);
                var label = HappiUiKit.Text(_content, "Axis", ProfilePersonality.AxisNames[i], _theme.FontSmall * _scale, _theme.Text, TextAlignmentOptions.Center, true);
                HappiUiKit.Place(label.rectTransform, x + p.x - labelW * 0.5f, _y - p.y - labelH * 0.5f, labelW, labelH);
            }

            _y += side + Gap;
        }

        void Button(string label, Action onClick)
        {
            var rowH = _theme.ButtonHeight * _scale;
            var button = HappiUiKit.Button(_content, "Button", HappiUiKit.Literal(label), _theme.FontSmall * _scale, onClick);
            if (_ribbon)
                StyleRibbonButton(button);
            var width = Mathf.Min(TextWidth, 200f * _scale);
            HappiUiKit.Place(button.Rect, Pad, _y, width, rowH);
            _y += rowH + Gap;
        }
    }
}
