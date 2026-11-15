using System;
using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.Memory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>Everything the Profile panel shows, gathered by the wheel host when it opens.</summary>
    public sealed class ProfileData
    {
        public string Name = string.Empty;
        public string RoleLine = string.Empty;
        public ProfileFeelings Feelings;
        public ProfilePersonality Personality;
        public ProfileWorld World;
        public string MemoriesSummary = string.Empty;
        public readonly List<string> Memories = new List<string>();
        public int MemoryCount;
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

        readonly HappiTheme _theme;
        readonly RectTransform _root;
        readonly TextMeshProUGUI _name;
        readonly TextMeshProUGUI _role;
        readonly HappiButton _close;
        readonly List<HappiButton> _tabs = new List<HappiButton>();
        readonly ScrollRect _scroll;
        readonly RectTransform _viewport;
        readonly RectTransform _content;

        Func<ProfileData> _source;
        ProfileData _data;
        Tab _tab = Tab.Feelings;
        bool _allBackstory;
        float _scale = 1f;
        float _y;
        float _inner;

        /// <summary>The close button and Escape.</summary>
        public Action OnClose;

        /// <summary>"Manage in Settings" on the Memories tab.</summary>
        public Action OpenSettings;

        public ProfilePanel(Transform parent, HappiTheme theme)
        {
            _theme = theme;
            var panel = HappiUiKit.Panel(parent, "ProfilePanel", theme.PanelBackground, true);
            _root = panel.rectTransform;
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            _name = HappiUiKit.Text(_root, "Name", string.Empty, theme.FontTitle, theme.Text, TextAlignmentOptions.TopLeft, true);
            _role = HappiUiKit.Text(_root, "Role", string.Empty, theme.FontSmall, theme.Text, TextAlignmentOptions.TopLeft, false);
            _close = HappiUiKit.Button(_root, "Close", "X", theme.FontTitle, () => { if (OnClose != null) OnClose(); });
            for (var i = 0; i < TabLabels.Length; i++)
            {
                var index = i;
                _tabs.Add(HappiUiKit.Button(_root, "Tab" + TabLabels[i], TabLabels[i], theme.FontSmall, () => Select((Tab)index)));
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
            _root.gameObject.SetActive(false);
        }

        public bool Visible => _root.gameObject.activeSelf;

        public void Show(Func<ProfileData> source, Vector2 center, float scale, Vector2 screen)
        {
            _source = source;
            _scale = Mathf.Max(1f, scale);
            _allBackstory = false;
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
            HappiUiKit.Place(_name.rectTransform, pad, pad, _inner - closeSize - gap, nameH);
            HappiUiKit.Place(_role.rectTransform, pad, pad + nameH, _inner - closeSize - gap, roleH);

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
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Select(_tab);
        }

        public void Hide()
        {
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

        void Select(Tab tab)
        {
            _tab = tab;
            _data = _source != null ? _source() : null;
            if (_data == null)
                _data = new ProfileData();
            _name.text = HappiUiKit.Literal(_data.Name);
            _role.text = HappiUiKit.Literal(_data.RoleLine);
            for (var i = 0; i < _tabs.Count; i++)
            {
                var colors = _tabs[i].Button.colors;
                colors.normalColor = i == (int)tab ? _theme.Selected : _theme.Button;
                colors.selectedColor = colors.normalColor;
                _tabs[i].Button.colors = colors;
            }

            Rebuild();
        }

        void Rebuild()
        {
            for (var i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _y = 0f;
            switch (_tab)
            {
                case Tab.Personality: BuildPersonality(); break;
                case Tab.World: BuildWorld(); break;
                case Tab.Memories: BuildMemories(); break;
                default: BuildFeelings(); break;
            }

            _content.sizeDelta = new Vector2(0f, _y + 8f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        // ---- Tabs ------------------------------------------------------------------------

        void BuildFeelings()
        {
            var f = _data.Feelings;
            if (f == null)
            {
                Paragraph("Not available yet.", false);
                return;
            }

            var box = Begin();
            Heading("How she feels about you");
            Row("Affection", f.AffectionValue);
            AffectionBar(f.AffectionMarker);
            Row("Familiarity", f.FamiliarityValue);
            FillBar(f.FamiliarityFill, _theme.Hover);
            End(box);

            box = Begin();
            Heading(f.MoodTitle);
            for (var i = 0; i < f.Moods.Count; i++)
            {
                Row(f.Moods[i].Label, f.Moods[i].Value);
                FillBar(f.Moods[i].Fill, _theme.Hover);
            }

            End(box);

            box = Begin();
            Heading("Why it moved recently");
            if (f.Reasons.Count == 0)
                Paragraph(f.ReasonsNote, true);
            for (var i = 0; i < f.Reasons.Count; i++)
                Paragraph("• " + f.Reasons[i], false);
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
            if (p.HasReading)
                Radar(p.Axes);
            if (p.Reason.Length > 0)
                Paragraph(p.Reason, !p.HasReading);
            End(box);

            if (p.ShowInnocence)
            {
                box = Begin();
                Heading("Innocent to Perverted (18+)");
                Row("Innocent", p.InnocenceValue + "   Perverted");
                FillBar(p.Innocence / 100f, _theme.Intimate);
                Paragraph("Set it on Settings → Persona → Who she is.", true);
                End(box);
            }
        }

        void BuildWorld()
        {
            var w = _data.World;
            if (w == null || w.IsEmpty)
            {
                Paragraph("Her world has not been written yet. It is written from her description while \"Her own world\" is on (Settings → Persona → Her life).", true);
                return;
            }

            if (w.RightNow.Length > 0)
            {
                var box = Begin();
                Heading("Right now");
                Paragraph(w.RightNow, false);
                End(box);
            }

            if (w.Waiting.Count > 0 || w.WhileAway.Count > 0)
            {
                var box = Begin();
                Heading("Things she is waiting for");
                for (var i = 0; i < w.Waiting.Count; i++)
                    Paragraph("• " + w.Waiting[i], false);
                if (w.WhileAway.Count > 0)
                {
                    SubHeading("While you were away");
                    for (var i = 0; i < w.WhileAway.Count; i++)
                        Paragraph(w.WhileAway[i].When + ": " + w.WhileAway[i].Text, false);
                }

                End(box);
            }

            if (w.Week.Count > 0)
            {
                var box = Begin();
                Heading("Her last days");
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
                    Button(_allBackstory ? "Show fewer" : "Show all " + w.Backstory.Count, () => { _allBackstory = !_allBackstory; Rebuild(); });
                End(box);
            }
        }

        void BuildMemories()
        {
            var box = Begin();
            Heading("What she remembers about you");
            Paragraph(_data.MemoriesSummary, true);
            for (var i = 0; i < _data.Memories.Count; i++)
                Paragraph("• " + _data.Memories[i], false);
            if (_data.MemoryCount > _data.Memories.Count)
                Paragraph("And " + (_data.MemoryCount - _data.Memories.Count) + " more.", true);
            Button("Manage in Settings", () => { if (OpenSettings != null) OpenSettings(); });
            End(box);
        }

        // ---- Building blocks -------------------------------------------------------------

        float Pad => _theme.Padding * _scale;
        float Gap => _theme.Spacing * _scale;

        /// <summary>Starts an inset box; its background is sized by <see cref="End"/>.</summary>
        RectTransform Begin()
        {
            var bg = HappiUiKit.Panel(_content, "Box", _theme.PanelInset, false);
            HappiUiKit.Place(bg.rectTransform, 0f, _y, _inner, 0f);
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
            AddText(text, _theme.FontBody, _theme.Text, true);
        }

        void SubHeading(string text)
        {
            AddText(text, _theme.FontSmall, _theme.Text, true);
        }

        void Paragraph(string text, bool muted)
        {
            if (string.IsNullOrEmpty(text))
                return;
            AddText(text, _theme.FontSmall, muted ? _theme.TextMuted : _theme.Text, false);
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
            var size = _theme.FontSmall * _scale;
            var height = size * 1.45f;
            var left = HappiUiKit.Text(_content, "RowLabel", HappiUiKit.Literal(label), size, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            HappiUiKit.Place(left.rectTransform, Pad, _y, TextWidth * 0.4f, height);
            var right = HappiUiKit.Text(_content, "RowValue", HappiUiKit.Literal(value), size, _theme.Text, TextAlignmentOptions.MidlineRight, true);
            HappiUiKit.Place(right.rectTransform, Pad + TextWidth * 0.3f, _y, TextWidth * 0.7f, height);
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
                var bar = HappiUiKit.Panel(_content, "Fill", color, false);
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
            var width = Mathf.Min(TextWidth, 200f * _scale);
            HappiUiKit.Place(button.Rect, Pad, _y, width, rowH);
            _y += rowH + Gap;
        }
    }
}
