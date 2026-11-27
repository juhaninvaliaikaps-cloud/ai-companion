using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Cast shadow and dry brush strokes clipped by the scroll's paper mask.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    sealed class RibbonScrollFinish : MaskableGraphic
    {
        static readonly Vector2[] BrushStrokes =
        {
            new(0.014f, 0.94f), new(0.031f, 0.63f), new(0.053f, 0.83f), new(0.088f, 0.65f),
            new(0.112f, 0.31f), new(0.183f, 0.35f), new(0.746f, 0.34f), new(0.81f, 0.42f),
            new(0.853f, 0.75f), new(0.887f, 0.61f), new(0.926f, 0.90f), new(0.958f, 0.65f),
            new(0.975f, 1f), new(0.986f, 0.76f)
        };
        bool _mirrored;
        Vector2 _join;
        Vector2 _flare;
        float _spread;
        float _curlWidth;

        public void SetPaper(bool mirrored, Vector2 join, Vector2 flare, float spread, float curlWidth)
        {
            if (_mirrored == mirrored && _join == join && _flare == flare && _spread == spread && _curlWidth == curlWidth)
                return;
            _mirrored = mirrored;
            _join = join;
            _flare = flare;
            _spread = spread;
            _curlWidth = curlWidth;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            var direction = _mirrored ? -1f : 1f;
            var rollTop = rect.yMax - _flare.x;
            var rollBottom = rect.yMin + _flare.y;
            var height = rollTop - rollBottom;
            if (_curlWidth > 0f)
            {
                var start = _mirrored ? rect.xMax : rect.xMin;
                var top = new Vector2(start + direction * _join.x, rollTop - height * 0.045f);
                var bottom = new Vector2(start + direction * _join.y, rollBottom + height * 0.045f);
                var feather = Vector2.right * (direction * _curlWidth * 0.64f);
                var middle = feather * 0.52f;
                var dark = new Color32(45, 24, 4, 173);
                var falloff = new Color32(69, 34, 4, 120);
                var clear = new Color32(69, 34, 4, 0);
                AddStrip(mesh, top, bottom, top + middle, bottom + middle, dark, falloff);
                AddStrip(mesh, top + middle, bottom + middle, top + feather, bottom + feather, falloff, clear);
            }

            var end = _mirrored ? rect.xMin : rect.xMax;
            var farTop = rollTop + _flare.x * _spread;
            var farBottom = rollBottom - _flare.y * _spread;
            var taper = Mathf.Min(height * 0.16f, rect.width * 0.08f);
            var maxStroke = Mathf.Min(height * 0.30f, rect.width * 0.09f);
            foreach (var stroke in BrushStrokes)
            {
                var y = Mathf.Lerp(farTop, farBottom, stroke.x);
                var tip = new Vector2(end - direction * taper * stroke.x, y);
                var tail = tip + new Vector2(-direction * maxStroke * stroke.y, -height * 0.045f);
                var thickness = Vector2.up * Mathf.Max(0.6f, height * stroke.y * 0.016f);
                var ink = new Color32(64, 38, 9, (byte)Mathf.Lerp(70f, 205f, stroke.y));
                AddStrip(mesh, tail, tail - thickness * 0.25f, tip + thickness, tip, new Color32(75, 43, 10, 0), ink);
            }
        }

        void AddStrip(VertexHelper mesh, Vector2 startTop, Vector2 startBottom, Vector2 endTop, Vector2 endBottom,
            Color32 startColor, Color32 endColor)
        {
            var i = mesh.currentVertCount;
            mesh.AddVert(startTop, startColor, Vector2.zero);
            mesh.AddVert(endTop, endColor, Vector2.zero);
            mesh.AddVert(startBottom, startColor, Vector2.zero);
            mesh.AddVert(endBottom, endColor, Vector2.zero);
            if (_mirrored)
            {
                mesh.AddTriangle(i, i + 2, i + 1);
                mesh.AddTriangle(i + 1, i + 2, i + 3);
            }
            else
            {
                mesh.AddTriangle(i, i + 1, i + 2);
                mesh.AddTriangle(i + 1, i + 3, i + 2);
            }
        }
    }
}
