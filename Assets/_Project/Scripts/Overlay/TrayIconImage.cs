namespace AICompanion.Overlay
{
    /// <summary>
    /// 32×32 placeholder tray icon, generated in code. No imported texture.
    /// Pixels are BGRA, top row first. Corners outside the rounded tile are transparent.
    /// </summary>
    public static class TrayIconImage
    {
        public const int Size = 32;

        public static byte[] CreateBgra()
        {
            var pixels = new byte[Size * Size * 4];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    byte b;
                    byte g;
                    byte r;
                    byte a;
                    if (!Inside(x, y, 0, 8))
                    {
                        b = 0;
                        g = 0;
                        r = 0;
                        a = 0;
                    }
                    else if (!Inside(x, y, 2, 6))
                    {
                        b = 96;
                        g = 48;
                        r = 168;
                        a = 255;
                    }
                    else
                    {
                        b = 162;
                        g = 118;
                        r = 236;
                        a = 255;
                    }

                    if (InEye(x, y) || InMouth(x, y))
                    {
                        b = 40;
                        g = 22;
                        r = 48;
                        a = 255;
                    }

                    var i = (y * Size + x) * 4;
                    pixels[i] = b;
                    pixels[i + 1] = g;
                    pixels[i + 2] = r;
                    pixels[i + 3] = a;
                }
            }

            return pixels;
        }

        public static byte Alpha(byte[] bgra, int x, int y)
        {
            return bgra[(y * Size + x) * 4 + 3];
        }

        public static byte Red(byte[] bgra, int x, int y)
        {
            return bgra[(y * Size + x) * 4 + 2];
        }

        static bool InEye(int x, int y)
        {
            return Block(x, y, 10, 12) || Block(x, y, 19, 12);
        }

        static bool Block(int x, int y, int left, int top)
        {
            return x >= left && x <= left + 1 && y >= top && y <= top + 1;
        }

        static bool InMouth(int x, int y)
        {
            if (y == 21 && x >= 13 && x <= 18)
                return true;
            return y == 22 && (x == 12 || x == 19);
        }

        static bool Inside(int x, int y, int inset, int radius)
        {
            var left = inset;
            var right = Size - 1 - inset;
            var top = inset;
            var bottom = Size - 1 - inset;
            if (x < left || y < top || x > right || y > bottom)
                return false;

            var cx = x < left + radius ? left + radius : (x > right - radius ? right - radius : x);
            var cy = y < top + radius ? top + radius : (y > bottom - radius ? bottom - radius : y);
            var dx = x - cx;
            var dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
