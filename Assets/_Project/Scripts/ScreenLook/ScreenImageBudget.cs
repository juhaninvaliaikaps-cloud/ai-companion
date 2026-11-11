using System;

namespace AICompanion.ScreenLook
{
    /// <summary>
    /// Shrinks a captured frame before it can be encoded. The encoder is injected
    /// so tests can bound the byte size without a JPEG library.
    /// </summary>
    public static class ScreenImageBudget
    {
        public const int MaxEdge = 768;
        /// <summary>
        /// Local vision keeps the window at its native size up to this edge. The earlier 1280
        /// halved a 1595 px Notepad window, which turned 11-16 px text into 5-8 px and made the
        /// 3B vision model guess. Above this edge the scale still uses whole-number blocks.
        /// </summary>
        public const int LocalMaxEdge = 1600;
        public const int LocalMaxBytes = 1536 * 1024;
        public static readonly int[] Qualities = { 70, 55, 40 };
        public static readonly int[] LocalQualities = { 92, 85, 75 };

        public static void Downscale(byte[] bgra, int width, int height, int stride, out byte[] pixels, out int dstWidth, out int dstHeight)
        {
            Downscale(bgra, width, height, stride, out pixels, out dstWidth, out dstHeight, MaxEdge, false);
        }

        /// <summary>
        /// sharp picks one source pixel instead of averaging the block. Averaging
        /// smears small window text. It does not make the vision model able to read it.
        /// </summary>
        public static void Downscale(byte[] bgra, int width, int height, int stride, out byte[] pixels, out int dstWidth, out int dstHeight, int maxEdge, bool sharp)
        {
            pixels = null;
            dstWidth = 0;
            dstHeight = 0;
            if (bgra == null || width <= 0 || height <= 0)
                return;
            if (stride < width * 4)
                stride = width * 4;
            if (maxEdge < 1)
                maxEdge = MaxEdge;

            var scale = ScaleFor(width, height, maxEdge);
            dstWidth = Math.Max(1, width / scale);
            dstHeight = Math.Max(1, height / scale);
            pixels = new byte[dstWidth * dstHeight * 4];

            for (var y = 0; y < dstHeight; y++)
            {
                var srcY0 = y * scale;
                var srcY1 = srcY0 + scale;
                if (srcY1 > height)
                    srcY1 = height;
                for (var x = 0; x < dstWidth; x++)
                {
                    var srcX0 = x * scale;
                    var srcX1 = srcX0 + scale;
                    if (srcX1 > width)
                        srcX1 = width;
                    long b = 0;
                    long g = 0;
                    long r = 0;
                    long a = 0;
                    var count = 0;
                    for (var sy = srcY0; sy < srcY1; sy++)
                    {
                        var row = sy * stride;
                        for (var sx = srcX0; sx < srcX1; sx++)
                        {
                            var i = row + sx * 4;
                            if (i + 3 >= bgra.Length)
                                continue;
                            b += bgra[i];
                            g += bgra[i + 1];
                            r += bgra[i + 2];
                            a += bgra[i + 3];
                            count++;
                        }
                    }

                    var o = (y * dstWidth + x) * 4;
                    if (sharp || count == 0)
                    {
                        var sx = srcX0 + Math.Max(0, (srcX1 - srcX0) / 2);
                        var sy = srcY0 + Math.Max(0, (srcY1 - srcY0) / 2);
                        if (sx >= width)
                            sx = width - 1;
                        if (sy >= height)
                            sy = height - 1;
                        var i = sy * stride + sx * 4;
                        if (i + 3 < bgra.Length)
                        {
                            pixels[o] = bgra[i];
                            pixels[o + 1] = bgra[i + 1];
                            pixels[o + 2] = bgra[i + 2];
                            pixels[o + 3] = bgra[i + 3];
                        }

                        continue;
                    }

                    pixels[o] = (byte)(b / count);
                    pixels[o + 1] = (byte)(g / count);
                    pixels[o + 2] = (byte)(r / count);
                    pixels[o + 3] = (byte)(a / count);
                }
            }
        }

        /// <summary>
        /// Resize to a long edge of at most maxEdge with bilinear sampling, so a 2560-wide
        /// display becomes 1600 wide instead of the 1280 an integer 2x step would give.
        /// Text survives better than point sampling and the aspect ratio is kept.
        /// </summary>
        public static void DownscaleFit(byte[] bgra, int width, int height, int stride, out byte[] pixels, out int dstWidth, out int dstHeight, int maxEdge)
        {
            pixels = null;
            dstWidth = 0;
            dstHeight = 0;
            if (bgra == null || width <= 0 || height <= 0)
                return;
            if (stride < width * 4)
                stride = width * 4;
            if (maxEdge < 1)
                maxEdge = MaxEdge;

            var longEdge = width > height ? width : height;
            var scale = longEdge > maxEdge ? (float)maxEdge / longEdge : 1f;
            dstWidth = Math.Max(1, (int)Math.Round(width * scale));
            dstHeight = Math.Max(1, (int)Math.Round(height * scale));
            pixels = new byte[dstWidth * dstHeight * 4];

            for (var y = 0; y < dstHeight; y++)
            {
                var sy = (y + 0.5f) / scale - 0.5f;
                var y0 = (int)Math.Floor(sy);
                var wy = sy - y0;
                var y1 = y0 + 1;
                if (y0 < 0) { y0 = 0; wy = 0f; }
                if (y1 >= height) { y1 = height - 1; wy = 0f; }

                for (var x = 0; x < dstWidth; x++)
                {
                    var sx = (x + 0.5f) / scale - 0.5f;
                    var x0 = (int)Math.Floor(sx);
                    var wx = sx - x0;
                    var x1 = x0 + 1;
                    if (x0 < 0) { x0 = 0; wx = 0f; }
                    if (x1 >= width) { x1 = width - 1; wx = 0f; }

                    var i00 = y0 * stride + x0 * 4;
                    var i10 = y0 * stride + x1 * 4;
                    var i01 = y1 * stride + x0 * 4;
                    var i11 = y1 * stride + x1 * 4;
                    var o = (y * dstWidth + x) * 4;
                    for (var c = 0; c < 4; c++)
                    {
                        var top = bgra[i00 + c] * (1f - wx) + bgra[i10 + c] * wx;
                        var bottom = bgra[i01 + c] * (1f - wx) + bgra[i11 + c] * wx;
                        pixels[o + c] = (byte)(top * (1f - wy) + bottom * wy);
                    }
                }
            }
        }
        /// <summary>
        /// Unity textures are bottom-up. The first pixel of the returned buffer is the
        /// bottom-left of a top-down BGRA image, and BGRA is swapped to RGBA.
        /// </summary>
        public static byte[] BgraTopDownToRgbaBottomUp(byte[] bgra, int width, int height)
        {
            if (bgra == null || width <= 0 || height <= 0 || bgra.Length < width * height * 4)
                return null;

            var rgba = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                var srcRow = y * width * 4;
                var dstRow = (height - 1 - y) * width * 4;
                for (var x = 0; x < width; x++)
                {
                    var s = srcRow + x * 4;
                    var d = dstRow + x * 4;
                    rgba[d] = bgra[s + 2];
                    rgba[d + 1] = bgra[s + 1];
                    rgba[d + 2] = bgra[s];
                    rgba[d + 3] = bgra[s + 3];
                }
            }

            return rgba;
        }

        public static bool LooksBlank(byte[] bgra, int width, int height)
        {
            if (bgra == null || width <= 0 || height <= 0 || bgra.Length < width * height * 4)
                return true;

            long sum = 0;
            var count = 0;
            var step = width * height > 4096 ? 16 : 1;
            for (var i = 0; i + 2 < bgra.Length; i += 4 * step)
            {
                sum += bgra[i];
                sum += bgra[i + 1];
                sum += bgra[i + 2];
                count += 3;
            }

            if (count == 0)
                return true;
            return sum / count < 3;
        }

        public static byte[] FitJpeg(Func<int, byte[]> encodeAtQuality, out int qualityUsed)
        {
            return FitJpeg(encodeAtQuality, out qualityUsed, AICompanion.AI.VisionCapability.MaxImageBytes, Qualities);
        }

        public static byte[] FitJpeg(Func<int, byte[]> encodeAtQuality, out int qualityUsed, int maxBytes, int[] qualities)
        {
            qualityUsed = 0;
            if (encodeAtQuality == null)
                return null;
            if (qualities == null || qualities.Length == 0)
                qualities = Qualities;
            if (maxBytes < 1)
                maxBytes = AICompanion.AI.VisionCapability.MaxImageBytes;

            for (var i = 0; i < qualities.Length; i++)
            {
                byte[] bytes;
                try
                {
                    bytes = encodeAtQuality(qualities[i]);
                }
                catch (Exception)
                {
                    return null;
                }

                if (!IsJpeg(bytes))
                    continue;
                qualityUsed = qualities[i];
                if (bytes.Length <= maxBytes)
                    return bytes;
            }

            return null;
        }

        public static int ScaleFor(int width, int height, int maxEdge)
        {
            if (width <= 0 || height <= 0)
                return 1;
            if (maxEdge < 1)
                maxEdge = MaxEdge;
            var longEdge = width > height ? width : height;
            if (longEdge <= maxEdge)
                return 1;
            return (longEdge + maxEdge - 1) / maxEdge;
        }

        public static bool IsJpeg(byte[] bytes)
        {
            return bytes != null && bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        }
    }
}
