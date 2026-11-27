using System;
using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Which edges a resize handle moves. A corner is two edges.</summary>
    [Flags]
    public enum ResizeEdges
    {
        None = 0,
        Left = 1,
        Right = 2,
        Bottom = 4,
        Top = 8,
        BottomLeft = Bottom | Left,
        BottomRight = Bottom | Right,
        TopLeft = Top | Left,
        TopRight = Top | Right,
    }

    /// <summary>
    /// JUH-127: the chat box resized by its edges and corners, like a window. Plain math on
    /// Unity screen rects (origin bottom-left, y up), so the tests need no scene.
    /// Sizes are saved in pixels at 100% scaling (<see cref="ToUnits"/>), so they follow the
    /// DPI scale; 0 means the automatic size.
    /// </summary>
    public static class ChatBoxResize
    {
        /// <summary>The smallest box at 100% scaling: every row still fits (header, reply, controls, input).</summary>
        public const float MinWidth = 260f;
        public const float MinHeight = 160f;
        public const float RibbonWidth = 560f;
        const float RibbonAspect = 10f / 7f;

        public static float FitRibbonScale(float uiScale, Vector2 workSize, bool keyRow)
        {
            const float margins = 16f;
            var widthScale = workSize.x / (MinWidth + margins);
            var heightScale = workSize.y / (MinHeight + (keyRow ? 32f : 0f) + margins);
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.5f, uiScale);
        }

        public static float RibbonHeight(float width, float uiScale, float heightSetting)
        {
            return Mathf.Max(MinHeight * uiScale, width / RibbonAspect * heightSetting);
        }

        /// <summary>The handles: edges this thick, corners this big, at 100% scaling.</summary>
        public const float EdgeThickness = 6f;
        public const float CornerSize = 14f;

        /// <summary>Default height from the avatar window, before user resize and screen limits.</summary>
        public static float AutomaticHeight(float bodyHeight, float uiScale, float heightSetting, bool ribbon)
        {
            var minimum = (ribbon ? 256f : 196f) * uiScale * heightSetting;
            var maximum = (ribbon ? 392f : 320f) * uiScale * heightSetting;
            return Mathf.Clamp(bodyHeight * (ribbon ? 0.44f : 0.40f) * heightSetting, minimum, maximum);
        }

        /// <summary>
        /// The box rect after dragging <paramref name="edges"/> by <paramref name="delta"/> from
        /// <paramref name="start"/>. The edges not dragged stay where they were; the size stays
        /// at least <paramref name="min"/> and the rect inside <paramref name="bounds"/>.
        /// </summary>
        public static Rect Apply(Rect start, Vector2 delta, ResizeEdges edges, Vector2 min, Rect bounds)
        {
            var xMin = start.xMin;
            var xMax = start.xMax;
            var yMin = start.yMin;
            var yMax = start.yMax;
            var minW = Mathf.Max(1f, min.x);
            var minH = Mathf.Max(1f, min.y);

            if ((edges & ResizeEdges.Left) != 0)
                xMin = Mathf.Min(Mathf.Max(start.xMin + delta.x, bounds.xMin), xMax - minW);
            else if ((edges & ResizeEdges.Right) != 0)
                xMax = Mathf.Max(Mathf.Min(start.xMax + delta.x, bounds.xMax), xMin + minW);

            if ((edges & ResizeEdges.Bottom) != 0)
                yMin = Mathf.Min(Mathf.Max(start.yMin + delta.y, bounds.yMin), yMax - minH);
            else if ((edges & ResizeEdges.Top) != 0)
                yMax = Mathf.Max(Mathf.Min(start.yMax + delta.y, bounds.yMax), yMin + minH);

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>Pixels on screen to pixels at 100% scaling, for saving.</summary>
        public static float ToUnits(float pixels, float scale)
        {
            return pixels / Mathf.Max(0.01f, scale);
        }

        /// <summary>
        /// The size to lay out: the saved size (100% units, 0 for automatic) at this scale,
        /// kept between <paramref name="minPixels"/> and <paramref name="maxPixels"/>; the
        /// automatic size when nothing is saved.
        /// </summary>
        public static float Resolve(float savedUnits, float automaticPixels, float scale, float minPixels, float maxPixels)
        {
            if (float.IsNaN(savedUnits) || savedUnits <= 0f)
                return automaticPixels;
            var max = Mathf.Max(minPixels, maxPixels);
            return Mathf.Clamp(savedUnits * Mathf.Max(0.01f, scale), minPixels, max);
        }

        /// <summary>A saved size from the settings file: a number above 0, or 0 for automatic.</summary>
        public static float SanitizeSaved(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                return 0f;
            return Mathf.Min(value, 10000f);
        }
    }

}
