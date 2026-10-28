using UnityEngine;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Original cheek texture: a soft pink disk with faint diagonal hatch lines.
    /// Generated in code. Nothing here is borrowed from a pack.
    /// </summary>
    public static class BlushTexture
    {
        public const int Size = 64;
        public const bool Hatch = true;

        static Texture2D _shared;

        public static Texture2D Shared
        {
            get
            {
                if (_shared == null)
                    _shared = Create();
                return _shared;
            }
        }

        public static Texture2D Create()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "CheekBlush",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                    pixels[y * Size + x] = Sample(x, y);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static Color32 Sample(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size)
                return new Color32(0, 0, 0, 0);

            var u = (x + 0.5f) / Size - 0.5f;
            var v = (y + 0.5f) / Size - 0.5f;
            var radius = Mathf.Sqrt(u * u + v * v) / 0.48f;
            if (radius >= 1f)
                return new Color32(0, 0, 0, 0);

            var falloff = 1f - radius;
            falloff *= falloff;
            var alpha = falloff * 0.82f;
            byte red = 255;
            byte green = 158;
            byte blue = 170;
            if (Hatch && ((x + y) % 6) == 0 && radius > 0.18f && radius < 0.82f)
            {
                alpha *= 0.78f;
                green = 118;
                blue = 132;
            }

            if (alpha < 0f)
                alpha = 0f;
            else if (alpha > 1f)
                alpha = 1f;

            return new Color32(red, green, blue, (byte)(alpha * 255f));
        }
    }
}
