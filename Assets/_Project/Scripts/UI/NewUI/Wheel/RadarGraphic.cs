using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>
    /// JUH-121: the temperament radar in Profile → Personality. Draws rings, spokes and the
    /// filled value shape as one mesh. Values are 0 to 1, clockwise from the top. The mesh is
    /// rebuilt only when the values change.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadarGraphic : MaskableGraphic
    {
        /// <summary>The share of half the rect the outer ring uses; the rest is room for labels.</summary>
        public const float RadiusShare = 0.72f;

        float[] _values = new float[0];
        Color _fill = new Color(0.62f, 0.83f, 1f, 0.35f);
        Color _line = new Color(0.62f, 0.83f, 1f, 1f);
        Color _grid = new Color(0.5f, 0.66f, 0.88f, 0.35f);
        float _lineWidth = 2f;

        public void Set(float[] values, Color fill, Color line, Color grid, float lineWidth)
        {
            _values = values ?? new float[0];
            _fill = fill;
            _line = line;
            _grid = grid;
            _lineWidth = Mathf.Max(1f, lineWidth);
            raycastTarget = false;
            SetVerticesDirty();
        }

        /// <summary>Where axis <paramref name="index"/> points at <paramref name="share"/> of the radius, in local space.</summary>
        public Vector2 Point(int index, float share)
        {
            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f * RadiusShare;
            var count = Mathf.Max(3, _values.Length);
            var angle = Mathf.PI * 2f * index / count;
            return rect.center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius * share;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var count = _values.Length;
            if (count < 3)
                return;

            for (var ring = 1; ring <= 4; ring++)
            {
                var share = ring / 4f;
                for (var i = 0; i < count; i++)
                    Line(vh, Point(i, share), Point((i + 1) % count, share), 1f, _grid);
            }

            for (var i = 0; i < count; i++)
                Line(vh, Point(i, 0f), Point(i, 1f), 1f, _grid);

            var center = Point(0, 0f);
            var start = vh.currentVertCount;
            vh.AddVert(center, _fill, Vector2.zero);
            for (var i = 0; i < count; i++)
                vh.AddVert(Point(i, Clamp(_values[i])), _fill, Vector2.zero);
            for (var i = 0; i < count; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);

            for (var i = 0; i < count; i++)
                Line(vh, Point(i, Clamp(_values[i])), Point((i + 1) % count, Clamp(_values[(i + 1) % count])), _lineWidth, _line);
        }

        static float Clamp(float value)
        {
            return float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var direction = b - a;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            var normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            var start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
