using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>Main wheel (8 slices above her head) or a smaller body-part wheel.</summary>
    public enum WheelSize
    {
        Main = 0,
        Part = 1,
    }

    /// <summary>
    /// Reusable radial wheel (JUH-104): any number of slices around a round centre button, a
    /// centre label with a small hint, a per-slice accent colour, and an optional thin connector
    /// line to an anchor point on her. It only draws and answers "which slice is here"; the host
    /// decides what a slice does. All sizes and colours come from <see cref="HappiTheme"/>.
    /// </summary>
    public sealed class RadialWheelView
    {
        sealed class SliceParts
        {
            public RectTransform Group;
            public Image Glow;
            public Image Fill;
            public Image Border;
            public TextMeshProUGUI Label;
        }

        readonly HappiTheme _theme;
        readonly WheelSize _size;
        readonly List<WheelSlice> _slices;
        readonly List<SliceParts> _parts = new List<SliceParts>();
        readonly RectTransform _root;
        readonly RectTransform _connector;
        readonly Image _connectorImage;
        readonly Image _hitDisk;
        readonly Image _centerDisk;
        readonly Image _centerRing;
        readonly TextMeshProUGUI _centerLabel;
        readonly TextMeshProUGUI _centerHint;
        readonly CanvasGroup _group;
        float _scale = -1f;
        float _openTime;
        int _hover = RadialMath.Outside;
        int _shownHover = int.MinValue;

        public RadialWheelView(Transform parent, WheelDefinition definition, WheelSize size, bool intimateGateOpen, HappiTheme theme)
        {
            _theme = theme ?? HappiTheme.Current;
            _size = size;
            Definition = definition ?? new WheelDefinition();
            _slices = WheelDefinition.Visible(Definition.Slices, intimateGateOpen);

            _connector = HappiUiKit.Rect(parent, "WheelConnector");
            _connectorImage = _connector.gameObject.AddComponent<Image>();
            _connectorImage.color = _theme.Intimate;
            _connectorImage.raycastTarget = false;
            _connector.gameObject.SetActive(false);

            _root = HappiUiKit.Rect(parent, size == WheelSize.Main ? "MainWheel" : "PartWheel");
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // Transparent disk under the whole wheel: uGUI raycasts hit it, so the overlay stops
            // clicking through while the pointer is on the wheel, and only there.
            _hitDisk = _root.gameObject.AddComponent<Image>();
            _hitDisk.sprite = HappiUiKit.Circle();
            _hitDisk.color = new Color(0f, 0f, 0f, 0f);
            _hitDisk.raycastTarget = true;

            for (var i = 0; i < _slices.Count; i++)
            {
                var group = HappiUiKit.Rect(_root, "Slice " + _slices[i].Id);
                group.anchorMin = group.anchorMax = new Vector2(0.5f, 0.5f);
                group.pivot = new Vector2(0.5f, 0.5f);
                var parts = new SliceParts
                {
                    Group = group,
                    Glow = SliceImage(group, "Glow"),
                    Fill = SliceImage(group, "Fill"),
                    Border = SliceImage(group, "Border"),
                };
                _parts.Add(parts);
            }

            for (var i = 0; i < _slices.Count; i++)
            {
                var label = HappiUiKit.Text(_root, "Label " + _slices[i].Id, HappiUiKit.Literal(_slices[i].Label), _theme.FontSlice, _theme.Text, TextAlignmentOptions.Center, true);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _parts[i].Label = label;
            }

            var centerRect = HappiUiKit.Rect(_root, "Center");
            centerRect.anchorMin = centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            _centerDisk = centerRect.gameObject.AddComponent<Image>();
            _centerDisk.sprite = HappiUiKit.Circle();
            _centerDisk.raycastTarget = false;
            var ringRect = HappiUiKit.Rect(_root, "CenterRing");
            ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            _centerRing = ringRect.gameObject.AddComponent<Image>();
            _centerRing.raycastTarget = false;
            _centerRing.color = _theme.Border;

            _centerLabel = HappiUiKit.Text(_root, "CenterLabel", HappiUiKit.Literal(Definition.CenterLabel), _theme.FontSliceCenter, _theme.Text, TextAlignmentOptions.Center, true);
            _centerLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _centerLabel.overflowMode = TextOverflowModes.Ellipsis;
            _centerLabel.rectTransform.anchorMin = _centerLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _centerHint = HappiUiKit.Text(_root, "CenterHint", HappiUiKit.Literal(Definition.CenterHint), _theme.FontSliceHint, _theme.TextMuted, TextAlignmentOptions.Center, false);
            _centerHint.textWrappingMode = TextWrappingModes.NoWrap;
            _centerHint.rectTransform.anchorMin = _centerHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        }

        static Image SliceImage(RectTransform parent, string name)
        {
            var rect = HappiUiKit.Rect(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            return image;
        }

        public WheelDefinition Definition { get; }

        public IReadOnlyList<WheelSlice> Slices => _slices;

        public RectTransform Root => _root;

        /// <summary>Slice under the pointer, <see cref="RadialMath.Center"/>, or <see cref="RadialMath.Outside"/>.</summary>
        public int Hover => _hover;

        public Vector2 Center { get; private set; }

        public float OuterRadius => BaseOuter * Scale;
        public float InnerRadius => BaseInner * Scale;
        public float CenterRadius => BaseCenter * Scale;

        float Scale => _scale > 0f ? _scale : 1f;
        float BaseOuter => _size == WheelSize.Main ? _theme.WheelOuterRadius : _theme.PartWheelOuterRadius;
        float BaseInner => _size == WheelSize.Main ? _theme.WheelInnerRadius : _theme.PartWheelInnerRadius;
        float BaseCenter => _size == WheelSize.Main ? _theme.WheelCenterRadius : _theme.PartWheelCenterRadius;

        /// <summary>Places the wheel (Unity screen pixels, origin bottom-left) and lays out its parts at a scale.</summary>
        public void Layout(Vector2 center, float scale)
        {
            Center = center;
            _root.anchoredPosition = center;
            if (Mathf.Approximately(scale, _scale))
                return;

            _scale = scale;
            var outer = OuterRadius;
            var inner = InnerRadius;
            var centerR = CenterRadius;
            var glow = _theme.WheelGlow * scale;
            var pop = _theme.WheelHoverPop * scale;
            var side = 2f * (outer + glow + pop);
            _root.sizeDelta = new Vector2(side, side);

            var count = _slices.Count;
            var sprites = WheelSprites.Get(count, inner, outer, _theme.WheelGap * scale, _theme.BorderWidth * scale, glow);
            var step = RadialMath.Step(count);
            var mid = (inner + outer) * 0.5f;
            // Wide enough for "Watch my / screen" on two lines; auto-size shrinks a long word instead of breaking it.
            var chord = 2f * mid * Mathf.Sin(Mathf.Deg2Rad * Mathf.Min(89f, step * 0.5f));
            var labelW = Mathf.Max(chord * 1.05f, (outer - inner) * 0.95f);
            var labelH = (outer - inner) * 0.78f;
            for (var i = 0; i < count; i++)
            {
                var parts = _parts[i];
                parts.Group.sizeDelta = new Vector2(sprites.Size, sprites.Size);
                parts.Group.localRotation = Quaternion.Euler(0f, 0f, -RadialMath.CenterAngle(i, count));
                SetSprite(parts.Glow, sprites.Glow, sprites.Size);
                SetSprite(parts.Fill, sprites.Fill, sprites.Size);
                SetSprite(parts.Border, sprites.Border, sprites.Size);
                parts.Label.enableAutoSizing = true;
                parts.Label.fontSizeMax = _theme.FontSlice * scale;
                parts.Label.fontSizeMin = Mathf.Max(8f, _theme.FontSlice * scale * 0.7f);
                parts.Label.fontSize = _theme.FontSlice * scale;
                parts.Label.rectTransform.sizeDelta = new Vector2(Mathf.Max(40f * scale, labelW), labelH);
            }

            ((RectTransform)_centerDisk.transform).sizeDelta = new Vector2(2f * centerR, 2f * centerR);
            var ring = WheelSprites.Ring(centerR, _theme.BorderWidth * scale);
            _centerRing.sprite = ring;
            ((RectTransform)_centerRing.transform).sizeDelta = new Vector2(ring.rect.width, ring.rect.height);
            _centerLabel.fontSize = _theme.FontSliceCenter * scale;
            _centerLabel.rectTransform.sizeDelta = new Vector2(1.8f * centerR, centerR * 0.7f);
            _centerLabel.rectTransform.anchoredPosition = new Vector2(0f, centerR * 0.12f);
            _centerHint.fontSize = _theme.FontSliceHint * scale;
            _centerHint.rectTransform.sizeDelta = new Vector2(1.8f * centerR, centerR * 0.45f);
            _centerHint.rectTransform.anchoredPosition = new Vector2(0f, -centerR * 0.38f);
            _shownHover = int.MinValue;
        }

        static void SetSprite(Image image, Sprite sprite, int size)
        {
            image.sprite = sprite;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>Thin line from an anchor on her to the wheel edge (body-part wheels). Null hides it.</summary>
        public void SetConnector(Vector2? anchor, bool intimate)
        {
            if (anchor == null)
            {
                _connector.gameObject.SetActive(false);
                return;
            }

            var end = RadialMath.ConnectorEnd(anchor.Value, Center, OuterRadius);
            var delta = end - anchor.Value;
            _connector.gameObject.SetActive(true);
            _connector.anchorMin = _connector.anchorMax = Vector2.zero;
            _connector.pivot = new Vector2(0f, 0.5f);
            _connector.anchoredPosition = anchor.Value;
            _connector.sizeDelta = new Vector2(delta.magnitude, _theme.ConnectorWidth * Scale);
            _connector.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            _connectorImage.color = intimate ? _theme.Intimate : _theme.Border;
        }

        /// <summary>The slice at a point in Unity screen pixels.</summary>
        public int HitTest(Vector2 unityScreen)
        {
            return RadialMath.SliceAt(unityScreen - Center, _slices.Count, CenterRadius, InnerRadius, OuterRadius + _theme.WheelHoverPop * Scale);
        }

        /// <summary>True when a point is anywhere on the wheel (slices, gaps or centre).</summary>
        public bool Contains(Vector2 unityScreen)
        {
            return (unityScreen - Center).magnitude <= OuterRadius + (_theme.WheelHoverPop + 4f) * Scale;
        }

        /// <summary>Updates hover and the opening animation. Call every frame while open.</summary>
        public void Tick(Vector2? pointer, float deltaTime)
        {
            _openTime += deltaTime;
            var open = _theme.WheelOpenSeconds <= 0f ? 1f : Mathf.Clamp01(_openTime / _theme.WheelOpenSeconds);
            _group.alpha = open;
            var grow = Mathf.Lerp(0.88f, 1f, 1f - (1f - open) * (1f - open));
            _root.localScale = new Vector3(grow, grow, 1f);

            _hover = pointer.HasValue ? HitTest(pointer.Value) : RadialMath.Outside;
            if (_hover == _shownHover)
                return;
            _shownHover = _hover;
            Paint();
        }

        void Paint()
        {
            var count = _slices.Count;
            for (var i = 0; i < count; i++)
            {
                var slice = _slices[i];
                var parts = _parts[i];
                var hovered = i == _hover;
                var intimate = slice.Accent == WheelAccent.Intimate;
                Color fill;
                Color text;
                var border = intimate ? _theme.Intimate : _theme.Border;
                var pop = 0f;
                Color glow = Color.clear;
                if (!slice.Enabled)
                {
                    fill = HappiTheme.WithAlpha(HappiTheme.Darken(_theme.Disabled, 0.45f), _theme.WheelBackground.a);
                    text = _theme.Disabled;
                    if (hovered)
                        border = _theme.Disabled;
                }
                else if (hovered)
                {
                    fill = intimate ? _theme.Intimate : _theme.Hover;
                    text = _theme.HoverText;
                    pop = _theme.WheelHoverPop * Scale;
                    glow = intimate ? _theme.IntimateGlow : _theme.Glow;
                }
                else
                {
                    fill = _theme.WheelBackground;
                    text = _theme.Text;
                }

                parts.Fill.color = fill;
                parts.Border.color = border;
                parts.Glow.color = glow;
                parts.Glow.enabled = glow.a > 0.001f;
                var direction = RadialMath.Direction(RadialMath.CenterAngle(i, count));
                parts.Group.anchoredPosition = direction * pop;
                parts.Label.color = text;
                parts.Label.rectTransform.anchoredPosition = direction * ((InnerRadius + OuterRadius) * 0.5f + pop);
            }

            var centerHover = _hover == RadialMath.Center;
            _centerDisk.color = centerHover ? _theme.Hover : _theme.WheelBackground;
            _centerLabel.color = centerHover ? _theme.HoverText : _theme.Text;
            _centerHint.color = centerHover ? _theme.HoverText : _theme.TextMuted;
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
            if (_connector != null)
                Object.Destroy(_connector.gameObject);
        }
    }
}
