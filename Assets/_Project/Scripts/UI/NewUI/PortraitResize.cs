using System;
using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-132: the 2D portrait frame resized like the chat box, but never past the source
    /// image size. Rectangles use GUI coordinates (top-left origin, y down).
    /// </summary>
    public static class PortraitResize
    {
        public const float MinWidth = 64f;
        public const float MinHeight = 72f;
        public const float EdgeThickness = 8f;
        public const float CornerSize = 18f;

        public static Rect Apply(Rect start, Vector2 delta, ResizeEdges edges, Vector2 min, Vector2 max, Rect bounds, float aspect = 0f)
        {
            var xMin = start.xMin;
            var xMax = start.xMax;
            var yMin = start.yMin;
            var yMax = start.yMax;

            if ((edges & ResizeEdges.Left) != 0)
                xMin = Mathf.Clamp(start.xMin + delta.x, bounds.xMin, xMax - min.x);
            else if ((edges & ResizeEdges.Right) != 0)
                xMax = Mathf.Clamp(start.xMax + delta.x, xMin + min.x, bounds.xMax);

            if ((edges & ResizeEdges.Top) != 0)
                yMin = Mathf.Clamp(start.yMin + delta.y, bounds.yMin, yMax - min.y);
            else if ((edges & ResizeEdges.Bottom) != 0)
                yMax = Mathf.Clamp(start.yMax + delta.y, yMin + min.y, bounds.yMax);

            if (xMax - xMin > max.x)
            {
                if ((edges & ResizeEdges.Left) != 0)
                    xMin = xMax - max.x;
                else
                    xMax = xMin + max.x;
            }

            if (yMax - yMin > max.y)
            {
                if ((edges & ResizeEdges.Top) != 0)
                    yMin = yMax - max.y;
                else
                    yMax = yMin + max.y;
            }

            if (aspect > 0.01f)
            {
                var horizontal = (edges & (ResizeEdges.Left | ResizeEdges.Right)) != 0;
                var vertical = (edges & (ResizeEdges.Top | ResizeEdges.Bottom)) != 0;
                var width = xMax - xMin;
                var height = yMax - yMin;
                if (horizontal && !vertical)
                {
                    height = Mathf.Clamp(width / aspect, min.y, max.y);
                    width = height * aspect;
                    // Keep the top edge fixed unless the top edge was the one dragged.
                    if ((edges & ResizeEdges.Top) != 0)
                        yMin = yMax - height;
                    else
                        yMax = yMin + height;
                }
                else if (vertical && !horizontal)
                {
                    width = Mathf.Clamp(height * aspect, min.x, max.x);
                    height = width / aspect;
                    xMax = xMin + width;
                }
                else if (horizontal && vertical)
                {
                    height = Mathf.Clamp(width / aspect, min.y, max.y);
                    width = height * aspect;
                    if ((edges & ResizeEdges.Top) != 0)
                        yMin = yMax - height;
                    else
                        yMax = yMin + height;
                    if ((edges & ResizeEdges.Left) != 0)
                        xMin = xMax - width;
                    else
                        xMax = xMin + width;
                }

                if (width > max.x)
                {
                    width = max.x;
                    height = width / aspect;
                }
                if (height > max.y)
                {
                    height = max.y;
                    width = height * aspect;
                }

                xMax = xMin + width;
                yMax = yMin + height;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>The saved size at this scale, clamped to the source image (0 = automatic).</summary>
        public static Vector2 Resolve(float savedWidth, float savedHeight, Vector2 automatic, float scale, Vector2 maxPixels)
        {
            var s = Mathf.Max(0.01f, scale);
            var width = savedWidth > 0f ? savedWidth * s : automatic.x;
            var height = savedHeight > 0f ? savedHeight * s : automatic.y;
            var min = new Vector2(MinWidth * s, MinHeight * s);
            var cap = new Vector2(Mathf.Max(min.x, maxPixels.x), Mathf.Max(min.y, maxPixels.y));
            return new Vector2(Mathf.Clamp(width, min.x, cap.x), Mathf.Clamp(height, min.y, cap.y));
        }

        public static float ToUnits(float pixels, float scale)
        {
            return pixels / Mathf.Max(0.01f, scale);
        }

        public static ResizeEdges EdgeAt(Rect rect, Vector2 pointer, float scale)
        {
            var edge = EdgeThickness * Mathf.Max(0.5f, scale);
            var corner = CornerSize * Mathf.Max(0.5f, scale);
            var left = pointer.x >= rect.xMin - edge && pointer.x <= rect.xMin + edge;
            var right = pointer.x >= rect.xMax - edge && pointer.x <= rect.xMax + edge;
            var top = pointer.y >= rect.yMin - edge && pointer.y <= rect.yMin + edge;
            var bottom = pointer.y >= rect.yMax - edge && pointer.y <= rect.yMax + edge;
            var inX = pointer.x >= rect.xMin - edge && pointer.x <= rect.xMax + edge;
            var inY = pointer.y >= rect.yMin - edge && pointer.y <= rect.yMax + edge;
            if (!inX || !inY)
                return ResizeEdges.None;

            var nearCorner = (pointer.x <= rect.xMin + corner || pointer.x >= rect.xMax - corner)
                && (pointer.y <= rect.yMin + corner || pointer.y >= rect.yMax - corner);
            if (!nearCorner && !left && !right && !top && !bottom)
                return ResizeEdges.None;

            if (top && left) return ResizeEdges.TopLeft;
            if (top && right) return ResizeEdges.TopRight;
            if (bottom && left) return ResizeEdges.BottomLeft;
            if (bottom && right) return ResizeEdges.BottomRight;
            if (top) return ResizeEdges.Top;
            if (bottom) return ResizeEdges.Bottom;
            if (left) return ResizeEdges.Left;
            if (right) return ResizeEdges.Right;
            return ResizeEdges.None;
        }
    }
}
