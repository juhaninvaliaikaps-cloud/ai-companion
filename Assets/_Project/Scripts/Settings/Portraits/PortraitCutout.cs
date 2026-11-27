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
                var x = Sample(i, texture.width);
                var y = Sample(i, texture.height);
                if (texture.GetPixel(x, 0).a < .08f) clear++;
                if (texture.GetPixel(x, texture.height - 1).a < .08f) clear++;
                if (texture.GetPixel(0, y).a < .08f) clear++;
                if (texture.GetPixel(texture.width - 1, y).a < .08f) clear++;
            }
            if (clear < Grid / 2)
                return default;

            var bottom = -1;
            var left = Grid;
            var right = -1;
            var clearPixels = 0;
            for (var y = 0; y < Grid; y++)
            {
                var py = Sample(y, texture.height);
                for (var x = 0; x < Grid; x++)
                {
                    var alpha = texture.GetPixel(Sample(x, texture.width), py).a;
                    if (alpha < .08f)
                        clearPixels++;
                    if (alpha < .5f || (bottom >= 0 && y >= bottom + Grid / 7))
                        continue;
                    if (bottom < 0)
                        bottom = y;
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
            }
            // A thin alpha border around an otherwise opaque image is not a cutout.
            if (bottom < 0 || right - left < Grid / 10 || clearPixels < Grid * Grid / 12)
                return default;

            return new PortraitCutout(left / (float)Grid, (right + 1f) / Grid, bottom / (float)Grid);
        }

        static int Sample(int cell, int size) => (2 * cell + 1) * size / (2 * Grid);
    }

    [RequireComponent(typeof(CanvasRenderer))]
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
            // The top edge meets the source crop. The angled edge hangs below it.
            var depth = Mathf.Clamp(length * .065f, 3f, 17f);
            var notch = Mathf.Lerp(_line.x, _line.y, .28f);
            var mint = new Color(114f / 255f, 238f / 255f, 207f / 255f, _opacity);
            mesh.AddVert(new Vector3(_line.x, bottom), mint, Vector2.zero);
            mesh.AddVert(new Vector3(notch, bottom - depth), mint, Vector2.zero);
            mesh.AddVert(new Vector3(_line.y, bottom), mint, Vector2.zero);
            mesh.AddTriangle(0, 1, 2);
        }
    }
}
