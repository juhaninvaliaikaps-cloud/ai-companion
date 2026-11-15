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

        /// <summary>The handles: edges this thick, corners this big, at 100% scaling.</summary>
        public const float EdgeThickness = 6f;
        public const float CornerSize = 14f;

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

    /// <summary>What the mouse wheel does over the chat box (JUH-127).</summary>
    public enum ChatWheelAction
    {
        /// <summary>Nothing: let the event go on (or no history to scroll).</summary>
        None = 0,

        /// <summary>Open the history, at the newest message, and scroll back by this wheel step.</summary>
        Open = 1,

        /// <summary>The history is open: scroll it by this wheel step.</summary>
        Scroll = 2,

        /// <summary>Scrolled down past the newest message: close the history, back to the live reply.</summary>
        Close = 3,
    }

    /// <summary>
    /// JUH-127, Jussi: "Just like a normal chat box works. Scrolling up and down lets the user
    /// see the chat history." Wheel up over the box opens the history and scrolls back; wheel
    /// down scrolls forward, and past the newest message it closes the history again, but only
    /// when the wheel opened it (History opened with the button stays open).
    /// </summary>
    public static class ChatWheel
    {
        /// <param name="historyOpen">The history list is showing.</param>
        /// <param name="openedByWheel">The wheel opened it, not the History button.</param>
        /// <param name="wasAtBottom">The list was at the newest message before this wheel step.</param>
        /// <param name="wheelY">The wheel step: above 0 is up (back in time), below 0 is down.</param>
        public static ChatWheelAction Decide(bool historyOpen, bool openedByWheel, bool wasAtBottom, float wheelY)
        {
            if (float.IsNaN(wheelY) || Mathf.Abs(wheelY) < 0.01f)
                return ChatWheelAction.None;
            if (!historyOpen)
                return wheelY > 0f ? ChatWheelAction.Open : ChatWheelAction.None;
            if (wheelY < 0f && wasAtBottom && openedByWheel)
                return ChatWheelAction.Close;
            return ChatWheelAction.Scroll;
        }

        /// <summary>The list counts as at the newest message within this much of the bottom (normalized).</summary>
        public const float BottomSlack = 0.01f;

        public static bool AtBottom(float verticalNormalizedPosition)
        {
            return float.IsNaN(verticalNormalizedPosition) || verticalNormalizedPosition <= BottomSlack;
        }
    }
}
