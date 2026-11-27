using System;
using AICompanion.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    public readonly struct RibbonTemperamentLayout
    {
        public readonly float Scale;
        public readonly float Width;
        public readonly float TrackX;
        public readonly float TrackWidth;
        public readonly float LabelHeight;
        public readonly float TickHeight;
        public readonly bool Compact;
        readonly float _rowHeight;

        public RibbonTemperamentLayout(float width, float scale, bool editable)
        {
            Scale = scale;
            Width = width;
            Compact = width < 420f * scale;
            TrackX = (Compact ? 25f : 138f) * scale;
            TrackWidth = Mathf.Max(1f, Mathf.Min((Compact ? 300f : 200f) * scale,
                width - TrackX - (Compact ? 0f : 112f * scale)));
            LabelHeight = 21f * scale;
            TickHeight = (editable ? 27f : 20f) * scale;
            _rowHeight = (Compact ? LabelHeight + TickHeight : Mathf.Max(LabelHeight, TickHeight)) + 2f * scale;
        }

        public float Height => 17f * Scale + _rowHeight * TemperamentAxes.Count;
        public float Pitch => TrackWidth / TemperamentSteps.Max;
        public float LabelY(int axis) => 17f * Scale + axis * _rowHeight;
        public float TrackY(int axis) => Compact ? LabelY(axis) + LabelHeight
            : LabelY(axis) + (Mathf.Max(LabelHeight, TickHeight) - TickHeight) * 0.5f;

        public static bool IsPink(int step, int reached) => step >= 8 && step <= reached;
    }

    /// <summary>The same ten-step track for read-only Profile rows and the Settings editor.</summary>
    sealed class RibbonTemperamentScale
    {
        static readonly Color32 Mint = new(113, 236, 212, 255);
        static readonly Color32 Cyan = new(41, 211, 221, 255);
        static readonly Color32 Yellow = new(255, 204, 45, 255);
        static readonly Color32 Pink = new(255, 117, 186, 255);
        static readonly Color32 Empty = new(65, 79, 95, 255);
        static readonly Color32 TickLabel = new(167, 186, 204, 255);

        public RectTransform Root { get; }
        public float Height => _layout.Height;
        readonly RibbonTemperamentLayout _layout;

        public RibbonTemperamentScale(Transform parent, HappiTheme theme, float width, float scale,
            Func<int, float> valueAt, Action<int, int> chooseStep = null)
        {
            _layout = new RibbonTemperamentLayout(width, scale, chooseStep != null);
            Root = HappiUiKit.Rect(parent, "Temperament scale");
            for (var i = TemperamentSteps.Min; i <= TemperamentSteps.Max; i++)
            {
                var number = HappiUiKit.Text(Root, "Step " + i, i.ToString(), 10.5f * scale, TickLabel,
                    TextAlignmentOptions.Center, true);
                HappiUiKit.Place(number.rectTransform, _layout.TrackX + (i - 1) * _layout.Pitch, 0f,
                    _layout.Pitch, 16f * scale);
            }

            Slider previous = null;
            for (var i = 0; i < TemperamentAxes.Count; i++)
            {
                var current = AddRow(theme, i, valueAt(i), chooseStep);
                if (current == null)
                    continue;
                var navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = previous };
                current.navigation = navigation;
                if (previous != null)
                {
                    var linked = previous.navigation;
                    linked.selectOnDown = current;
                    previous.navigation = linked;
                }
                previous = current;
            }
        }

        Slider AddRow(HappiTheme theme, int axis, float rawValue, Action<int, int> chooseStep)
        {
            var s = _layout.Scale;
            var name = TemperamentAxes.Names[axis];
            var labelY = _layout.LabelY(axis);
            var icon = RibbonGlyph.Add(Root, name + " icon", RibbonGlyph.TemperamentMark(axis), Mint);
            HappiUiKit.Place(icon.rectTransform, 0f, labelY + 2f * s, 17f * s, 17f * s);
            var caption = HappiUiKit.Text(Root, name, name, 13f * s, theme.Text, TextAlignmentOptions.MidlineLeft, false);
            var wordX = _layout.Compact ? _layout.Width * 0.52f : _layout.TrackX + _layout.TrackWidth + 6f * s;
            var captionEnd = _layout.Compact ? wordX : _layout.TrackX - 4f * s;
            HappiUiKit.Place(caption.rectTransform, 24f * s, labelY, captionEnd - 24f * s, _layout.LabelHeight);
            var descriptor = HappiUiKit.Text(Root, name + " description", string.Empty, 12.3f * s, theme.Text,
                TextAlignmentOptions.MidlineRight, true);
            descriptor.enableAutoSizing = true;
            descriptor.fontSizeMin = 9f * s;
            descriptor.fontSizeMax = 12.3f * s;
            descriptor.textWrappingMode = TextWrappingModes.NoWrap;
            descriptor.overflowMode = TextOverflowModes.Ellipsis;
            HappiUiKit.Place(descriptor.rectTransform, wordX, labelY, _layout.Width - wordX, _layout.LabelHeight);

            var trackY = _layout.TrackY(axis);
            var track = HappiUiKit.Rect(Root, name + " track");
            HappiUiKit.Place(track, _layout.TrackX, trackY, _layout.TrackWidth, _layout.TickHeight);
            var editable = chooseStep != null;
            var hover = editable ? HappiUiKit.Panel(track, "Step control", Color.white, true) : null;
            if (hover != null)
                HappiUiKit.Stretch(hover.rectTransform, 0f, 0f, 0f, 0f);
            var tiles = new Image[TemperamentSteps.Max];
            for (var i = 0; i < tiles.Length; i++)
                tiles[i] = HappiUiKit.Panel(track, "Step " + (i + 1), Empty, false);

            var step = TemperamentSteps.DisplayStep(rawValue);
            void Paint(float value)
            {
                descriptor.text = ProfilePersonality.SliderWord(axis, value);
                for (var i = 0; i < tiles.Length; i++)
                {
                    var point = i + 1;
                    var height = (point == step ? 11f : 7f) * s;
                    var width = _layout.Pitch * 0.75f;
                    tiles[i].color = RibbonTemperamentLayout.IsPink(point, step) ? Pink
                        : point == step ? Yellow : point < step ? Cyan : Empty;
                    HappiUiKit.Place(tiles[i].rectTransform, i * _layout.Pitch + (_layout.Pitch - width) * 0.5f,
                        (_layout.TickHeight - height) * 0.5f, width, height);
                }
            }
            Paint(rawValue);
            if (!editable)
                return null;

            // The slider travels between the first and last tile centers. Its hit area also covers
            // the outer half tiles, so clicking either end selects its exact step.
            var travel = HappiUiKit.Rect(track, "Step travel");
            var halfPitch = _layout.Pitch * 0.5f;
            HappiUiKit.Stretch(travel, halfPitch, 0f, halfPitch, 0f);
            var handle = HappiUiKit.Rect(travel, "Step pointer");
            handle.sizeDelta = Vector2.zero;
            var slider = track.gameObject.AddComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = hover;
            var colors = slider.colors;
            colors.normalColor = new Color32(25, 31, 42, 150);
            colors.highlightedColor = new Color32(36, 65, 80, 210);
            colors.selectedColor = new Color32(40, 91, 104, 235);
            colors.pressedColor = colors.selectedColor;
            slider.colors = colors;
            slider.minValue = TemperamentSteps.Min;
            slider.maxValue = TemperamentSteps.Max;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(step);
            slider.onValueChanged.AddListener(value =>
            {
                var selected = Mathf.RoundToInt(value);
                if (selected == step)
                    return;
                step = selected;
                chooseStep(axis, selected);
                Paint(TemperamentSteps.ValueForStep(selected));
            });
            return slider;
        }
    }
}
