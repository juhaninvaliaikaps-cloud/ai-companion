using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// TextMeshPro fonts for the new UI: the theme's Windows font (dynamic, from the OS) when
    /// present, otherwise the TMP default (LiberationSans SDF, TMP Essential Resources).
    /// </summary>
    public static class HappiFonts
    {
        static TMP_FontAsset _regular;
        static TMP_FontAsset _bold;
        static bool _tried;

        public static TMP_FontAsset Regular
        {
            get
            {
                Ensure();
                return _regular;
            }
        }

        /// <summary>A bold face when the OS has one; otherwise null and callers use FontStyles.Bold.</summary>
        public static TMP_FontAsset Bold
        {
            get
            {
                Ensure();
                return _bold;
            }
        }

        static void Ensure()
        {
            if (_tried)
                return;
            _tried = true;
            var theme = HappiTheme.Current;
            var fallback = TMP_Settings.defaultFontAsset;
            _regular = TryOs(theme.FontFamily, theme.FontStyleRegular, fallback);
            _bold = TryOs(theme.FontFamily, theme.FontStyleBold, fallback);
            if (_regular == null)
                _regular = fallback;
        }

        static TMP_FontAsset TryOs(string family, string style, TMP_FontAsset fallback)
        {
            try
            {
                var asset = TMP_FontAsset.CreateFontAsset(family, style, 90);
                if (asset == null)
                    return null;
                asset.hideFlags = HideFlags.DontSave;
                asset.name = family + " " + style + " (OS)";
                if (fallback != null)
                {
                    if (asset.fallbackFontAssetTable == null)
                        asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    asset.fallbackFontAssetTable.Add(fallback);
                }

                return asset;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>A button built by <see cref="HappiUiKit"/>.</summary>
    public sealed class HappiButton
    {
        public Button Button;
        public Image Image;
        public TextMeshProUGUI Label;
        public RectTransform Rect;

        public string Text
        {
            get { return Label != null ? Label.text : string.Empty; }
            set
            {
                if (Label != null && Label.text != value)
                    Label.text = value ?? string.Empty;
            }
        }

        public bool Interactable
        {
            get { return Button != null && Button.interactable; }
            set
            {
                if (Button != null && Button.interactable != value)
                    Button.interactable = value;
            }
        }

        public void SetVisible(bool visible)
        {
            if (Rect != null && Rect.gameObject.activeSelf != visible)
                Rect.gameObject.SetActive(visible);
        }
    }

    /// <summary>Dark text while the pointer is over a hovered (pale sky blue) control, light otherwise.</summary>
    public sealed class HappiHoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TextMeshProUGUI Label;
        public Selectable Target;
        public Color Normal = Color.white;
        public Color Hover = Color.black;
        public Color Disabled = Color.gray;
        bool _over;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _over = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            Apply();
        }

        void OnDisable()
        {
            _over = false;
        }

        void LateUpdate()
        {
            Apply();
        }

        void Apply()
        {
            if (Label == null)
                return;
            var interactable = Target == null || Target.IsInteractable();
            var color = !interactable ? Disabled : _over ? Hover : Normal;
            if (Label.color != color)
                Label.color = color;
        }
    }

    /// <summary>
    /// Code-built uGUI + TextMeshPro controls in the Happi theme. Like the old panels, nothing is
    /// a prefab, so the overlay's uGUI raycast click-through works unchanged.
    /// </summary>
    public static class HappiUiKit
    {
        static Sprite _rounded;
        static Texture2D _roundedTexture;
        static Sprite _circle;
        static Texture2D _circleTexture;

        public const int RoundedSize = 32;
        public const int RoundedRadius = 10;

        /// <summary>A white 9-sliced rounded rectangle, tinted by Image.color.</summary>
        public static Sprite Rounded()
        {
            if (_rounded != null)
                return _rounded;

            _roundedTexture = new Texture2D(RoundedSize, RoundedSize, TextureFormat.RGBA32, false);
            _roundedTexture.hideFlags = HideFlags.HideAndDontSave;
            _roundedTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[RoundedSize * RoundedSize];
            for (var y = 0; y < RoundedSize; y++)
            {
                for (var x = 0; x < RoundedSize; x++)
                {
                    var a = RoundedAlpha(x, y, RoundedSize, RoundedRadius);
                    pixels[y * RoundedSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            _roundedTexture.SetPixels32(pixels);
            _roundedTexture.Apply(false, true);
            var border = RoundedRadius + 1;
            _rounded = Sprite.Create(_roundedTexture, new Rect(0, 0, RoundedSize, RoundedSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _rounded.hideFlags = HideFlags.HideAndDontSave;
            return _rounded;
        }

        /// <summary>A white anti-aliased circle.</summary>
        public static Sprite Circle()
        {
            if (_circle != null)
                return _circle;

            const int size = 64;
            _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _circleTexture.hideFlags = HideFlags.HideAndDontSave;
            _circleTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            var c = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    var a = Mathf.Clamp01(c - d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            _circleTexture.SetPixels32(pixels);
            _circleTexture.Apply(false, true);
            _circle = Sprite.Create(_circleTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _circle.hideFlags = HideFlags.HideAndDontSave;
            return _circle;
        }

        /// <summary>Alpha of a rounded-rectangle pixel, anti-aliased over one pixel.</summary>
        public static float RoundedAlpha(int x, int y, int size, int radius)
        {
            var px = x + 0.5f;
            var py = y + 0.5f;
            var cx = Mathf.Clamp(px, radius, size - radius);
            var cy = Mathf.Clamp(py, radius, size - radius);
            var d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01(radius - d + 0.5f);
        }

        public static GameObject CreateCanvas(Transform parent, string name, int sortingOrder, out Canvas canvas)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.hideFlags = HideFlags.DontSave;
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem(parent);
            return go;
        }

        /// <summary>Same camera setup as the old panels so the canvas draws into the transparent overlay.</summary>
        public static void FollowCamera(Canvas canvas, float planeDistance)
        {
            if (canvas == null)
                return;
            var camera = Camera.main;
            if (camera == null)
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            if (canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != camera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
            }

            if (Mathf.Abs(canvas.planeDistance - planeDistance) > 0.0001f)
                canvas.planeDistance = planeDistance;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null)
                return;
            var system = new GameObject("EventSystem");
            system.transform.SetParent(parent, false);
            system.hideFlags = HideFlags.DontSave;
            system.AddComponent<EventSystem>();
            system.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            return rect;
        }

        public static Image Panel(Transform parent, string name, Color color, bool raycast)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions align, bool bold)
        {
            var rect = Rect(parent, name);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = bold && HappiFonts.Bold != null ? HappiFonts.Bold : HappiFonts.Regular;
            if (font != null)
                text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (bold && HappiFonts.Bold == null)
                text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            text.text = value ?? string.Empty;
            return text;
        }

        public static HappiButton Button(Transform parent, string name, string label, float fontSize, Action onClick)
        {
            var theme = HappiTheme.Current;
            var image = Panel(parent, name, Color.white, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = theme.Button;
            colors.highlightedColor = theme.ButtonHover;
            colors.pressedColor = HappiTheme.Lighten(theme.ButtonHover, 0.2f);
            colors.selectedColor = theme.Button;
            colors.disabledColor = HappiTheme.WithAlpha(theme.Disabled, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = theme.HoverSeconds;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            var text = Text(image.transform, "Label", label, fontSize, theme.Text, TextAlignmentOptions.Center, true);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(text.rectTransform, 4f, 1f, 4f, 1f);
            var hover = image.gameObject.AddComponent<HappiHoverText>();
            hover.Label = text;
            hover.Target = button;
            hover.Normal = theme.Text;
            hover.Hover = theme.HoverText;
            hover.Disabled = theme.Disabled;

            return new HappiButton { Button = button, Image = image, Label = text, Rect = image.rectTransform };
        }

        public static TMP_InputField Input(Transform parent, string name, string placeholder, float fontSize, bool password)
        {
            var theme = HappiTheme.Current;
            var resources = new TMP_DefaultControls.Resources();
            var go = TMP_DefaultControls.CreateInputField(resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            var field = go.GetComponent<TMP_InputField>();
            var image = go.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = Rounded();
                image.type = Image.Type.Sliced;
                image.color = theme.PanelInset;
            }

            var colors = field.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = HappiTheme.Lighten(Color.white, 0f);
            colors.selectedColor = Color.white;
            colors.pressedColor = Color.white;
            field.colors = colors;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            field.richText = false;
            field.caretColor = theme.Text;
            field.selectionColor = HappiTheme.WithAlpha(theme.Hover, 0.45f);
            var navigation = field.navigation;
            navigation.mode = Navigation.Mode.None;
            field.navigation = navigation;

            var font = HappiFonts.Regular;
            if (field.textComponent != null)
            {
                if (font != null)
                    field.textComponent.font = font;
                field.textComponent.fontSize = fontSize;
                field.textComponent.color = theme.Text;
                field.textComponent.richText = false;
            }

            var hint = field.placeholder as TMP_Text;
            if (hint != null)
            {
                if (font != null)
                    hint.font = font;
                hint.text = placeholder ?? string.Empty;
                hint.fontSize = fontSize;
                hint.fontStyle = FontStyles.Italic;
                hint.color = theme.TextMuted;
            }

            if (field.textViewport != null)
                Stretch(field.textViewport, 8f, 2f, 8f, 2f);
            return field;
        }

        /// <summary>Anchors to the parent's top-left corner, with a size; y grows downward.</summary>
        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Text shown literally in a rich-text TMP field (model or user text can contain &lt;).</summary>
        public static string Literal(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return "<noparse>" + text.Replace("</noparse>", "</ noparse>") + "</noparse>";
        }

        public static string ColorTag(Color color)
        {
            Color32 c = color;
            return "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2") + c.a.ToString("X2");
        }
    }
}
