using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-150: the settings view's form pieces (sections, notes, buttons, choices, drop-downs,
    /// fields, text areas, switches) as a reusable builder, so a new window such as the Persona
    /// Studio looks and behaves like the rest of Happi. The owner clears the body, calls
    /// <see cref="Begin"/>, adds rows top to bottom, then <see cref="End"/>.
    /// </summary>
    public sealed class HappiForm
    {
        readonly HappiTheme _theme;
        readonly List<TMP_InputField> _fields = new List<TMP_InputField>();
        RectTransform _body;
        float _y;
        float _width;
        float _s = 1f;

        public HappiForm(HappiTheme theme)
        {
            _theme = theme;
        }

        /// <summary>True while rows are being built; button and field callbacks ignore it.</summary>
        public bool Building { get; private set; }

        /// <summary>The drop-down that is open now, by caption, or null.</summary>
        public string OpenDropdown;

        /// <summary>Set by any change that needs the body drawn again.</summary>
        public bool Rebuild;

        public float Scale
        {
            get { return _s; }
        }

        public float Width
        {
            get { return _width; }
        }

        public IReadOnlyList<TMP_InputField> Fields
        {
            get { return _fields; }
        }

        public void Begin(RectTransform body, float width, float scale)
        {
            _body = body;
            _width = width;
            _s = scale;
            _y = 4f;
            _fields.Clear();
            Building = true;
            for (var i = body.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(body.GetChild(i).gameObject);
        }

        public void End()
        {
            if (_body != null)
                _body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _y + 8f);
            Building = false;
        }

        public void Place(RectTransform rect, float height)
        {
            HappiUiKit.Place(rect, 0f, _y, _width, height);
            _y += height + _theme.Spacing * _s;
        }

        public void Gap(float height)
        {
            _y += height * _s;
        }

        public void Section(string title, bool intimate = false)
        {
            _y += 4f * _s;
            var text = HappiUiKit.Text(_body, "Section", HappiUiKit.Literal(title), _theme.FontBody * _s, intimate ? _theme.Intimate : _theme.Hover, TextAlignmentOptions.MidlineLeft, true);
            Place(text.rectTransform, 22f * _s);
        }

        public TextMeshProUGUI Label(string value)
        {
            var text = HappiUiKit.Text(_body, "Label", HappiUiKit.Literal(value), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            Place(text.rectTransform, Mathf.Max(20f * _s, text.GetPreferredValues(text.text, _width, 0f).y));
            return text;
        }

        public TextMeshProUGUI Note(string value)
        {
            var text = HappiUiKit.Text(_body, "Note", HappiUiKit.Literal(value), _theme.FontSmall * _s, _theme.TextMuted, TextAlignmentOptions.TopLeft, false);
            text.fontStyle = FontStyles.Italic;
            text.overflowMode = TextOverflowModes.Overflow;
            Place(text.rectTransform, Mathf.Max(16f * _s, text.GetPreferredValues(text.text, _width, 0f).y + 2f));
            return text;
        }

        public HappiButton Button(string label, Action action, bool danger = false)
        {
            var button = HappiUiKit.Button(_body, "Button", HappiUiKit.Literal(label), _theme.FontSmall * _s, () =>
            {
                if (!Building && action != null)
                    action();
            });
            button.Label.overflowMode = TextOverflowModes.Ellipsis;
            if (danger)
                Tint(button, HappiTheme.Darken(_theme.Intimate, 0.35f));
            Place(button.Rect, _theme.ButtonHeight * _s);
            return button;
        }

        /// <summary>Several buttons side by side on one row.</summary>
        public void Buttons(params KeyValuePair<string, Action>[] buttons)
        {
            if (buttons == null || buttons.Length == 0)
                return;
            var row = HappiUiKit.Rect(_body, "Buttons");
            Place(row, _theme.ButtonHeight * _s);
            var gap = _theme.Spacing * _s;
            var w = (_width - gap * (buttons.Length - 1)) / buttons.Length;
            for (var i = 0; i < buttons.Length; i++)
            {
                var action = buttons[i].Value;
                var button = HappiUiKit.Button(row, "Button", HappiUiKit.Literal(buttons[i].Key), _theme.FontSmall * _s, () =>
                {
                    if (!Building && action != null)
                        action();
                });
                button.Label.overflowMode = TextOverflowModes.Ellipsis;
                HappiUiKit.Place(button.Rect, i * (w + gap), 0f, w, _theme.ButtonHeight * _s);
            }
        }

        public void Choice(string label, bool selected, Action action)
        {
            var button = Button(selected ? HappiTheme.SelectedMark + label : label, action);
            if (selected)
                Tint(button, _theme.Selected);
        }

        /// <summary>A drop-down: the current value on a button, and the options under it while open.</summary>
        public void Dropdown(string caption, string shown, IReadOnlyList<string> options, Func<string, string> label, Func<string, bool> selected, Action<string> chosen)
        {
            Button(caption + ": " + shown + "  ▾", () =>
            {
                OpenDropdown = string.Equals(OpenDropdown, caption, StringComparison.Ordinal) ? null : caption;
                Rebuild = true;
            });
            if (!string.Equals(OpenDropdown, caption, StringComparison.Ordinal) || options == null)
                return;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                Choice(label != null ? label(option) : option, selected != null && selected(option), () =>
                {
                    OpenDropdown = null;
                    if (chosen != null)
                        chosen(option);
                    Rebuild = true;
                });
            }
        }

        public void Toggle(string label, bool value, Action<bool> changed)
        {
            var row = HappiUiKit.Rect(_body, "Toggle");
            Place(row, _theme.ButtonHeight * _s);
            var text = HappiUiKit.Text(row, "Label", HappiUiKit.Literal(label), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            HappiUiKit.Stretch(text.rectTransform, 0f, 0f, 70f * _s, 0f);
            var pill = HappiUiKit.Button(row, "Switch", value ? "On" : "Off", _theme.FontSmall * _s, () =>
            {
                if (!Building && changed != null)
                    changed(!value);
            });
            pill.Rect.anchorMin = new Vector2(1f, 0f);
            pill.Rect.anchorMax = new Vector2(1f, 1f);
            pill.Rect.pivot = new Vector2(1f, 0.5f);
            pill.Rect.anchoredPosition = Vector2.zero;
            pill.Rect.sizeDelta = new Vector2(62f * _s, 0f);
            if (value)
                Tint(pill, _theme.Selected);
        }

        /// <summary>A one-line field. <paramref name="changed"/> runs when editing ends.</summary>
        public TMP_InputField Field(string caption, string value, int limit, Action<string> changed)
        {
            var label = HappiUiKit.Text(_body, "Caption", HappiUiKit.Literal(caption), _theme.FontSmall * _s, _theme.TextMuted, TextAlignmentOptions.BottomLeft, false);
            Place(label.rectTransform, 15f * _s);
            _y -= _theme.Spacing * _s * 0.6f;
            var field = HappiUiKit.Input(_body, "Field", caption, _theme.FontBody * _s, false);
            Place((RectTransform)field.transform, 28f * _s);
            if (limit > 0)
                field.characterLimit = limit;
            field.text = value ?? string.Empty;
            field.onEndEdit.AddListener(text =>
            {
                if (!Building && changed != null)
                    changed(text ?? string.Empty);
            });
            _fields.Add(field);
            return field;
        }

        /// <summary>A multi-line text area. <paramref name="typed"/> runs on every change (for a live meter).</summary>
        public TMP_InputField Area(string caption, string value, float height, int limit, Action<string> changed, Action<string> typed = null)
        {
            var field = Field(caption, value, limit, changed);
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            if (field.textComponent != null)
            {
                field.textComponent.textWrappingMode = TextWrappingModes.Normal;
                field.textComponent.alignment = TextAlignmentOptions.TopLeft;
            }

            if (typed != null)
                field.onValueChanged.AddListener(text =>
                {
                    if (!Building)
                        typed(text ?? string.Empty);
                });

            var rect = (RectTransform)field.transform;
            _y -= rect.sizeDelta.y + _theme.Spacing * _s;
            Place(rect, height * _s);
            return field;
        }

        /// <summary>A labelled slider; the label follows the value while dragging.</summary>
        public void Slider(Func<float, string> label, float value, float min, float max, bool whole, Action<float> changed)
        {
            var caption = HappiUiKit.Text(_body, "SliderLabel", HappiUiKit.Literal(label(value)), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            Place(caption.rectTransform, 18f * _s);
            var rect = HappiUiKit.Rect(_body, "Slider");
            Place(rect, 18f * _s);
            var background = rect.gameObject.AddComponent<Image>();
            background.sprite = HappiUiKit.Rounded();
            background.type = Image.Type.Sliced;
            background.color = _theme.PanelInset;
            var fillArea = HappiUiKit.Rect(rect, "Fill Area");
            HappiUiKit.Stretch(fillArea, 8f * _s, 6f * _s, 8f * _s, 6f * _s);
            var fill = HappiUiKit.Rect(fillArea, "Fill");
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = HappiUiKit.Rounded();
            fillImage.type = Image.Type.Sliced;
            fillImage.color = _theme.Border;
            fill.sizeDelta = Vector2.zero;
            var handleArea = HappiUiKit.Rect(rect, "Handle Slide Area");
            HappiUiKit.Stretch(handleArea, 8f * _s, 0f, 8f * _s, 0f);
            var handle = HappiUiKit.Rect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(18f * _s, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = HappiUiKit.Circle();
            handleImage.color = _theme.Text;
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            var colors = slider.colors;
            colors.highlightedColor = _theme.Hover;
            slider.colors = colors;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.value = Mathf.Clamp(value, min, max);
            slider.onValueChanged.AddListener(next =>
            {
                if (Building)
                    return;
                caption.text = HappiUiKit.Literal(label(next));
                if (changed != null)
                    changed(next);
            });
        }

        /// <summary>A thin progress bar; returns the fill so the owner can move it.</summary>
        public RectTransform Progress(float share)
        {
            var track = HappiUiKit.Panel(_body, "Progress", _theme.PanelInset, false);
            track.sprite = HappiUiKit.Rounded();
            track.type = Image.Type.Sliced;
            Place(track.rectTransform, 8f * _s);
            var fill = HappiUiKit.Panel(track.rectTransform, "Fill", _theme.Hover, false);
            fill.sprite = HappiUiKit.Rounded();
            fill.type = Image.Type.Sliced;
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            return fill.rectTransform;
        }

        /// <summary>A picture, square or round, centred on its own row.</summary>
        public RawImage Picture(Texture texture, float size, bool round)
        {
            var row = HappiUiKit.Rect(_body, "PictureRow");
            Place(row, size * _s);
            var frame = HappiUiKit.Panel(row, "Frame", _theme.PanelInset, false);
            frame.sprite = round ? HappiUiKit.Circle() : HappiUiKit.Rounded();
            frame.type = round ? Image.Type.Simple : Image.Type.Sliced;
            var px = size * _s;
            HappiUiKit.Place(frame.rectTransform, (_width - px) * 0.5f, 0f, px, px);
            var mask = frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var image = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            image.transform.SetParent(frame.transform, false);
            HappiUiKit.Stretch(image.rectTransform, 2f, 2f, 2f, 2f);
            image.texture = texture;
            image.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            return image;
        }

        public bool AnyFieldFocused()
        {
            for (var i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != null && _fields[i].isFocused)
                    return true;
            }

            return false;
        }

        public void BlurFields()
        {
            for (var i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != null && _fields[i].isFocused)
                    _fields[i].DeactivateInputField();
            }
        }

        static void Tint(HappiButton button, Color color)
        {
            var colors = button.Button.colors;
            colors.normalColor = color;
            button.Button.colors = colors;
        }
    }

    /// <summary>JUH-150: drag a window by its title bar.</summary>
    public sealed class HappiWindowDrag : MonoBehaviour, IDragHandler
    {
        public RectTransform Window;

        public void OnDrag(PointerEventData eventData)
        {
            if (Window == null)
                return;
            var canvas = Window.GetComponentInParent<Canvas>();
            var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Window.anchoredPosition += eventData.delta / scale;
        }
    }
}
