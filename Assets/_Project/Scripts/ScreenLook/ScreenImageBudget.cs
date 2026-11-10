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
        public const int LocalMaxEdge = 1280;
        public const int LocalMaxBytes = 1536 * 1024;
        public static readonly int[] Qualities = { 70, 55, 40 };
        public static readonly int[] LocalQualities = { 85, 75, 60 };

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

            var longEdge = width > height ? width : height;
            var scale = longEdge <= maxEdge ? 1 : (longEdge + maxEdge - 1) / maxEdge;
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

        public static bool IsJpeg(byte[] bytes)
        {
            return bytes != null && bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        }
    }
}
