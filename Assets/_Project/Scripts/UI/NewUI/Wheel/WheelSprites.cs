using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>The three white sprites of one slice shape (tinted by Image.color).</summary>
    public sealed class SliceSprites
    {
        public Sprite Fill;
        public Sprite Border;
        public Sprite Glow;
        /// <summary>Texture side in pixels; the wheel centre is the texture centre.</summary>
        public int Size;
    }

    /// <summary>
    /// Builds slice sprites in code: slice 0 of a wheel (centred on the top) as an anti-aliased
    /// ring segment with straight gap edges, a thin border along its outline, and a soft glow
    /// around it. Every slice of a wheel uses the same sprites, rotated into place, so a wheel
    /// costs three small textures. They are cached by shape and made again only when the
    /// wheel's scale changes. Images render through the normal uGUI path, like the panels.
    /// </summary>
    public static class WheelSprites
    {
        static readonly Dictionary<string, SliceSprites> Cache = new Dictionary<string, SliceSprites>();
        static readonly Dictionary<string, Sprite> Rings = new Dictionary<string, Sprite>();
        const int MaxCached = 12;

        /// <summary>
        /// Signed distance in pixels from a point to the edge of slice 0 (positive inside):
        /// the nearer of the two radii and the two gap edges. Gap edges are parallel to the
        /// slice's centre line, so the gap has the same width at every radius.
        /// </summary>
        public static float InsideDistance(Vector2 p, int count, float inner, float outer, float gap)
        {
            var r = p.magnitude;
            var radial = Mathf.Min(r - inner, outer - r);
            if (count <= 1)
                return radial;

            var half = 180f / count;
            var angle = RadialMath.AngleOf(p);
            if (angle > 180f)
                angle -= 360f;
            if (Mathf.Abs(angle) > 90f + half)
                return Mathf.Min(radial, -r);

            // Distance to each straight edge line through the centre at ±half, shifted inward by gap/2.
            var left = DistanceToEdge(p, -half) - gap * 0.5f;
            var right = DistanceToEdge(p, half) - gap * 0.5f;
            return Mathf.Min(radial, Mathf.Min(left, right));
        }

        /// <summary>Signed distance from a point to the line through the centre at an angle (clockwise from top); positive toward the slice centre.</summary>
        static float DistanceToEdge(Vector2 p, float edgeAngle)
        {
            var direction = RadialMath.Direction(edgeAngle);
            // Normal pointing toward angle 0 (the slice centre).
            var normal = edgeAngle < 0f ? new Vector2(direction.y, -direction.x) : new Vector2(-direction.y, direction.x);
            return Vector2.Dot(p, normal);
        }

        public static float FillAlpha(float inside)
        {
            return Mathf.Clamp01(inside + 0.5f);
        }

        public static float BorderAlpha(float inside, float border)
        {
            return Mathf.Clamp01(inside + 0.5f) * Mathf.Clamp01(border - inside + 0.5f);
        }

        public static float GlowAlpha(float inside, float glow)
        {
            if (glow <= 0f)
                return 0f;
            if (inside >= 0f)
                return 1f;
            var t = Mathf.Clamp01(1f + inside / glow);
            return t * t;
        }

        public static SliceSprites Get(int count, float inner, float outer, float gap, float border, float glow)
        {
            var key = count + "|" + Mathf.RoundToInt(inner) + "|" + Mathf.RoundToInt(outer) + "|" + Mathf.RoundToInt(gap * 10f) + "|" + Mathf.RoundToInt(border * 10f) + "|" + Mathf.RoundToInt(glow);
            SliceSprites sprites;
            if (Cache.TryGetValue(key, out sprites) && sprites.Fill != null)
                return sprites;
            if (Cache.Count >= MaxCached)
                Clear();

            var size = Mathf.CeilToInt(2f * (outer + glow + 2f));
            if (size % 2 == 1)
                size++;
            var fill = new Color32[size * size];
            var line = new Color32[size * size];
            var halo = new Color32[size * size];
            var c = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                    var inside = InsideDistance(p, count, inner, outer, gap);
                    var i = y * size + x;
                    fill[i] = White(FillAlpha(inside));
                    line[i] = White(BorderAlpha(inside, border));
                    halo[i] = White(GlowAlpha(inside, glow));
                }
            }

            sprites = new SliceSprites
            {
                Size = size,
                Fill = MakeSprite(fill, size, "SliceFill"),
                Border = MakeSprite(line, size, "SliceBorder"),
                Glow = MakeSprite(halo, size, "SliceGlow"),
            };
            Cache[key] = sprites;
            return sprites;
        }

        /// <summary>A thin full ring (the outline around the centre button).</summary>
        public static Sprite Ring(float radius, float border)
        {
            var key = Mathf.RoundToInt(radius) + "|" + Mathf.RoundToInt(border * 10f);
            Sprite ring;
            if (Rings.TryGetValue(key, out ring) && ring != null)
                return ring;
            var size = Mathf.CeilToInt(2f * (radius + 2f));
            if (size % 2 == 1)
                size++;
            var pixels = new Color32[size * size];
            var c = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                    var inside = InsideDistance(p, 1, radius - border, radius, 0f);
                    pixels[y * size + x] = White(FillAlpha(inside));
                }
            }

            ring = MakeSprite(pixels, size, "Ring");
            Rings[key] = ring;
            return ring;
        }

        static Color32 White(float alpha)
        {
            return new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
        }

        static Sprite MakeSprite(Color32[] pixels, int size, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void Clear()
        {
            foreach (var pair in Cache)
            {
                Destroy(pair.Value.Fill);
                Destroy(pair.Value.Border);
                Destroy(pair.Value.Glow);
            }

            Cache.Clear();
        }

        static void Destroy(Sprite sprite)
        {
            if (sprite == null)
                return;
            var texture = sprite.texture;
            Object.Destroy(sprite);
            if (texture != null)
                Object.Destroy(texture);
        }
    }
}
