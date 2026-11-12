using System;
using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>
    /// The one place for the new UI's look (JUH-104). Every colour, font, size, corner
    /// radius, spacing and animation timing the new chat, settings, wizard and radial
    /// wheels use comes from here. Change a value here and every new view follows.
    ///
    /// Palette approved by Jussi on 2026-11-12 (JUH-104 comments and
    /// docs/ui-overhaul/radial_mockup.png). The old UI does not read this class.
    /// </summary>
    [Serializable]
    public sealed class HappiTheme
    {
        // ---- Approved palette -------------------------------------------------------

        /// <summary>Wheel and panel background, medium blue, about 90% opaque.</summary>
        public const string WheelBackgroundHex = "#3B6FB6";
        /// <summary>Slice borders, dividers and the ring outline.</summary>
        public const string BorderHex = "#7FA8E0";
        /// <summary>Hovered slice: pale sky blue, with dark text and a soft glow.</summary>
        public const string HoverHex = "#9FD4FF";
        /// <summary>Dark text on the hover colour.</summary>
        public const string HoverTextHex = "#0B1530";
        /// <summary>Text and icons.</summary>
        public const string TextHex = "#EAF2FF";
        /// <summary>Disabled or refused slices and controls.</summary>
        public const string DisabledHex = "#B4C3DC";
        /// <summary>Intimate or affectionate accent (kiss, hold hand, lips wheel).</summary>
        public const string IntimateHex = "#FF7EB6";

        /// <summary>Background alpha from the JUH-104 palette comment ("about 90% opacity").</summary>
        public const float BackgroundAlpha = 0.90f;

        public Color WheelBackground = WithAlpha(Hex(WheelBackgroundHex), BackgroundAlpha);
        public Color Border = Hex(BorderHex);
        public Color Hover = Hex(HoverHex);
        public Color HoverText = Hex(HoverTextHex);
        public Color Text = Hex(TextHex);
        public Color Disabled = Hex(DisabledHex);
        public Color Intimate = Hex(IntimateHex);

        // ---- Derived surfaces (all from the palette above) --------------------------

        /// <summary>Panel body (chat, settings, wizard): the wheel blue, a little more opaque.</summary>
        public Color PanelBackground = WithAlpha(Hex(WheelBackgroundHex), 0.94f);
        /// <summary>Recessed areas such as the transcript and input fields.</summary>
        public Color PanelInset = WithAlpha(Darken(Hex(WheelBackgroundHex), 0.45f), 0.92f);
        /// <summary>Buttons at rest.</summary>
        public Color Button = Darken(Hex(WheelBackgroundHex), 0.18f);
        /// <summary>Buttons under the pointer use the hover colour with HoverText.</summary>
        public Color ButtonHover = Hex(HoverHex);
        /// <summary>A selected tab or toggled-on button.</summary>
        public Color Selected = Lighten(Hex(WheelBackgroundHex), 0.22f);
        /// <summary>Muted secondary text (notes, hints, timestamps).</summary>
        public Color TextMuted = WithAlpha(Hex(DisabledHex), 0.95f);
        /// <summary>Narration lines in the visual-novel chat.</summary>
        public Color Narration = Hex(DisabledHex);
        /// <summary>The line being read or spoken now.</summary>
        public Color HighlightBackground = WithAlpha(Hex(HoverHex), 0.22f);
        /// <summary>Glow behind the hovered slice.</summary>
        public Color Glow = WithAlpha(Hex(HoverHex), 0.55f);
        /// <summary>Glow behind a hovered intimate slice.</summary>
        public Color IntimateGlow = WithAlpha(Hex(IntimateHex), 0.55f);
        /// <summary>Thin dark gap colour between slices (only used where a gap is drawn, not cut).</summary>
        public Color Gap = new Color(0.04f, 0.06f, 0.12f, 0.85f);
        /// <summary>Local (private) model badge.</summary>
        public Color BadgeLocal = new Color(0.72f, 0.90f, 0.78f, 1f);
        /// <summary>Cloud model badge.</summary>
        public Color BadgeCloud = new Color(0.98f, 0.86f, 0.62f, 1f);
        /// <summary>Recording microphone.</summary>
        public Color Recording = new Color(0.86f, 0.25f, 0.30f, 1f);

        // ---- Fonts -------------------------------------------------------------------

        /// <summary>
        /// Windows system font tried first (dynamic TMP font asset from the OS). When it is
        /// missing, the TMP default (LiberationSans SDF from TMP Essential Resources) is used.
        /// </summary>
        public string FontFamily = "Segoe UI";
        public string FontStyleRegular = "Regular";
        public string FontStyleBold = "Bold";

        // Sizes in pixels at 100% Windows scaling. Scaled by UiScale at runtime.
        public float FontTitle = 16f;
        public float FontBody = 14f;
        public float FontSmall = 12f;
        public float FontSlice = 13f;
        public float FontSliceCenter = 15f;
        public float FontSliceHint = 9f;

        // ---- Shape ---------------------------------------------------------------------

        public float CornerRadius = 10f;
        public float CornerRadiusSmall = 6f;
        public float BorderWidth = 1.5f;

        // Main wheel (mockup: 8 slices around a centre button).
        public float WheelOuterRadius = 118f;
        public float WheelInnerRadius = 46f;
        public float WheelCenterRadius = 40f;
        /// <summary>Gap between slices, in pixels along the arc.</summary>
        public float WheelGap = 4f;
        /// <summary>How far the hovered slice pops out, in pixels.</summary>
        public float WheelHoverPop = 6f;
        /// <summary>Soft glow width outside the hovered slice.</summary>
        public float WheelGlow = 10f;
        /// <summary>Space between the top of her head and the bottom of the wheel.</summary>
        public float WheelAboveHead = 12f;
        /// <summary>Distance kept from the window edge when the wheel is clamped on screen.</summary>
        public float WheelEdgeMargin = 6f;

        // Body-part wheel (mockup right side: smaller, 3 slices, beside her head).
        public float PartWheelOuterRadius = 76f;
        public float PartWheelInnerRadius = 28f;
        public float PartWheelCenterRadius = 24f;
        public float PartWheelOffset = 110f;
        public float ConnectorWidth = 2f;

        // ---- Spacing -------------------------------------------------------------------

        public float Padding = 10f;
        public float Spacing = 6f;
        public float RowHeight = 28f;
        public float ButtonHeight = 28f;
        public float ChatWidth = 360f;
        public float SettingsWidth = 460f;
        public float SettingsMaxHeight = 600f;

        // ---- Animation timings (seconds) ----------------------------------------------

        public float WheelOpenSeconds = 0.12f;
        public float WheelCloseSeconds = 0.08f;
        public float HoverSeconds = 0.08f;
        public float HighlightFadeSeconds = 0.15f;
        /// <summary>Auto-play: pause after a narration line, per character, before the next segment.</summary>
        public float NarrationSecondsPerChar = 0.035f;
        public float NarrationMinSeconds = 1.2f;
        public float NarrationMaxSeconds = 6f;
        /// <summary>Auto-play: pause after a speech line that was not voiced (muted or TTS off).</summary>
        public float SilentSpeechSecondsPerChar = 0.04f;

        // ---- Scaling for 100–200% Windows display scaling -----------------------------

        /// <summary>0 = follow the monitor DPI. Any other value forces that factor.</summary>
        public float UiScaleOverride;
        public const float MinUiScale = 1f;
        public const float MaxUiScale = 2.5f;
        public const float ReferenceDpi = 96f;

        static HappiTheme _current;

        /// <summary>The theme every new view reads. Replace it to restyle the new UI.</summary>
        public static HappiTheme Current
        {
            get
            {
                if (_current == null)
                    _current = new HappiTheme();
                return _current;
            }
            set { _current = value; }
        }

        /// <summary>
        /// Factor for 100–200% Windows scaling. The overlay forces every CanvasScaler to
        /// ConstantPixelSize 1 (OverlayWindow.LockUiPixels), so new views multiply their
        /// own sizes by this instead of relying on the scaler.
        /// </summary>
        public float UiScale(float dpi)
        {
            if (UiScaleOverride > 0f)
                return Mathf.Clamp(UiScaleOverride, MinUiScale, MaxUiScale);
            return ScaleForDpi(dpi);
        }

        public static float ScaleForDpi(float dpi)
        {
            if (float.IsNaN(dpi) || dpi <= 0f)
                return MinUiScale;
            return Mathf.Clamp(dpi / ReferenceDpi, MinUiScale, MaxUiScale);
        }

        /// <summary>Text colour that stays readable on a background (dark on hover, light elsewhere).</summary>
        public Color TextOn(Color background)
        {
            return Luminance(background) > 0.55f ? HoverText : Text;
        }

        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Color.magenta;
            var s = hex[0] == '#' ? hex.Substring(1) : hex;
            if (s.Length != 6 && s.Length != 8)
                return Color.magenta;
            uint value;
            if (!uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value))
                return Color.magenta;
            if (s.Length == 6)
                value = (value << 8) | 0xFF;
            return new Color32(
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }

        public static string ToHex(Color color)
        {
            Color32 c = color;
            return "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static Color Darken(Color color, float amount)
        {
            var a = color.a;
            var result = Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
            result.a = a;
            return result;
        }

        public static Color Lighten(Color color, float amount)
        {
            var a = color.a;
            var result = Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
            result.a = a;
            return result;
        }

        public static float Luminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        /// <summary>WCAG contrast ratio between two opaque colours.</summary>
        public static float Contrast(Color a, Color b)
        {
            var la = RelativeLuminance(a);
            var lb = RelativeLuminance(b);
            var hi = Mathf.Max(la, lb);
            var lo = Mathf.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        static float RelativeLuminance(Color c)
        {
            return 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
        }

        static float Linear(float channel)
        {
            return channel <= 0.03928f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
