using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-228: which part of the wallpaper picture sits behind a piece of the desktop, so the
    /// door into her room shows the wallpaper that was there. Windows' default "Fill" is assumed:
    /// the picture covers the monitor and is cropped at the centre.
    /// </summary>
    public static class WallpaperCrop
    {
        /// <summary>
        /// The texture rectangle (0–1, y up as Unity's uvRect wants) for a desktop rectangle on a
        /// monitor. Both rectangles are in desktop pixels with y down. A full-monitor rectangle
        /// gives the centre crop of the picture.
        /// </summary>
        public static Rect Uv(int textureWidth, int textureHeight, Rect monitor, Rect piece)
        {
            if (textureWidth <= 0 || textureHeight <= 0 || monitor.width <= 0f || monitor.height <= 0f)
                return new Rect(0f, 0f, 1f, 1f);

            // Fill: scale the picture to cover the monitor, then crop the overflow evenly.
            var scale = Mathf.Max(monitor.width / textureWidth, monitor.height / textureHeight);
            var shownWidth = monitor.width / scale / textureWidth;
            var shownHeight = monitor.height / scale / textureHeight;
            var offsetX = (1f - shownWidth) * 0.5f;
            var offsetY = (1f - shownHeight) * 0.5f;

            var u = (piece.x - monitor.x) / monitor.width;
            var vTop = (piece.y - monitor.y) / monitor.height;
            var w = piece.width / monitor.width;
            var h = piece.height / monitor.height;
            return new Rect(
                offsetX + u * shownWidth,
                offsetY + (1f - vTop - h) * shownHeight,
                w * shownWidth,
                h * shownHeight);
        }
    }
}
