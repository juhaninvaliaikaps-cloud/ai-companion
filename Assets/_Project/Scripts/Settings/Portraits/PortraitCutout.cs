using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    /// <summary>The lower edge of a cutout in texture-relative coordinates.</summary>
    public readonly struct PortraitCutout
    {
        const int Grid = 48;
        public readonly float Left;
        public readonly float Right;
        public readonly float Bottom;
        public bool Valid => Right > Left;

        PortraitCutout(float left, float right, float bottom)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
        }

        public static PortraitCutout Measure(Texture2D texture)
        {
            if (texture == null || !texture.isReadable || texture.format == TextureFormat.RGB24)
                return default;

            // Look for background transparency at the source canvas edge. Isolated transparent
            // pixels and opaque files saved with an alpha channel still use the framed style.
            var clear = 0;
            for (var i = 0; i < Grid; i++)
            {
                var at = (i + .5f) / Grid;
                if (texture.GetPixelBilinear(at, 0f).a < .08f) clear++;
                if (texture.GetPixelBilinear(at, 1f).a < .08f) clear++;
                if (texture.GetPixelBilinear(0f, at).a < .08f) clear++;
                if (texture.GetPixelBilinear(1f, at).a < .08f) clear++;
            }
            if (clear < Grid)
                return default;

            var bottom = -1;
            var left = Grid;
            var right = -1;
            for (var y = 0; y < Grid; y++)
            {
                if (bottom >= 0 && y >= bottom + Grid / 7)
                    break;
                for (var x = 0; x < Grid; x++)
                {
                    if (texture.GetPixelBilinear((x + .5f) / Grid, (y + .5f) / Grid).a < .5f)
                        continue;
                    if (bottom < 0)
                        bottom = y;
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
            }
            if (bottom < 0 || right - left < Grid / 10)
                return default;

            return new PortraitCutout(left / (float)Grid, (right + 1f) / Grid, bottom / (float)Grid);
        }
    }

    sealed class RibbonPortraitBase : MaskableGraphic
    {
        Vector3 _line;
        float _opacity;

        public void SetLine(Vector3 line, float opacity)
        {
            if (_line == line && Mathf.Approximately(_opacity, opacity))
                return;
            _line = line;
            _opacity = opacity;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var length = _line.y - _line.x;
            if (length <= 0f || _opacity <= .001f)
                return;

            var bottom = _line.z;
            var rise = Mathf.Min(22f, length * .09f);
            var depth = Mathf.Clamp(length * .027f, 2f, 8f);
            var notch = Mathf.Lerp(_line.x, _line.y, .28f);
            var mint = new Color(114f / 255f, 238f / 255f, 207f / 255f, _opacity);
            mesh.AddVert(new Vector3(_line.x, bottom + rise * .55f), mint, Vector2.zero);
            mesh.AddVert(new Vector3(notch, bottom + depth), mint, Vector2.zero);
            mesh.AddVert(new Vector3(_line.y, bottom + rise), mint, Vector2.zero);
            mesh.AddVert(new Vector3(notch, bottom), mint, Vector2.zero);
            mesh.AddTriangle(0, 1, 3);
            mesh.AddTriangle(1, 2, 3);
        }
    }
}
