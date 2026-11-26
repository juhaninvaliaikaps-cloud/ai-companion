using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Ribbon geometry changes the rendered mesh while controls keep rectangular hit areas.</summary>
    sealed class RibbonShape : BaseMeshEffect
    {
        internal enum Kind { Panel, Tab, ChatFrame, Composer, HeaderArt, ScrollPaper }
        Kind _kind;
        bool _mirrored;
        Vector2 _paperEdge;
        Vector2 _flare;
        float _flareProgress;
        static readonly Vector2[] ScrollBrushEdge =
        {
            new(0f, 0.06f), new(0.013f, 0.48f), new(0.024f, 0.11f), new(0.04f, 0.76f),
            new(0.061f, 0.03f), new(0.087f, 0.63f), new(0.104f, 0.21f), new(0.125f, 0.77f),
            new(0.16f, 0.25f), new(0.25f, 0.18f), new(0.37f, 0.41f), new(0.49f, 0.24f),
            new(0.63f, 0.36f), new(0.75f, 0.19f), new(0.84f, 0.14f), new(0.88f, 0.61f),
            new(0.907f, 0.23f), new(0.923f, 0.74f), new(0.938f, 0.33f), new(0.954f, 0.92f),
            new(0.971f, 0.44f), new(0.985f, 0.84f), new(0.994f, 0.36f), new(1f, 0.59f)
        };

        public static void Apply(Image image, bool tab) => Apply(image, tab ? Kind.Tab : Kind.Panel);

        public static RibbonShape Apply(Graphic graphic, Kind kind)
        {
            if (graphic is Image image)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
            }
            var shape = graphic.gameObject.AddComponent<RibbonShape>();
            shape._kind = kind;
            graphic.SetVerticesDirty();
            return shape;
        }

        public void SetScrollPaper(bool mirrored, Vector2 paperEdge, Vector2 flare, float flareProgress)
        {
            if (_mirrored == mirrored && _paperEdge == paperEdge && _flare == flare && _flareProgress == flareProgress)
                return;
            _mirrored = mirrored;
            _paperEdge = paperEdge;
            _flare = flare;
            _flareProgress = flareProgress;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive())
                return;

            var rect = graphic.GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            var uv = graphic is RawImage raw ? raw.uvRect : new Rect(0f, 0f, 0f, 0f);
            Color32 color = graphic.color;
            vertices.Clear();
            if (_kind == Kind.ScrollPaper)
            {
                AddScrollPaper(vertices, rect, color);
                return;
            }

            var count = VertexCount(_kind);
            for (var i = 0; i < count; i++)
            {
                var point = Point(_kind, rect, i);
                var u = uv.x + uv.width * (point.x - rect.xMin) / rect.width;
                var v = uv.y + uv.height * (point.y - rect.yMin) / rect.height;
                vertices.AddVert(point, color, new Vector2(u, v));
            }
            for (var i = 1; i + 1 < count; i++)
                vertices.AddTriangle(0, i, i + 1);
        }

        void AddScrollPaper(VertexHelper mesh, Rect rect, Color32 color)
        {
            var rollTop = rect.yMax - _flare.x;
            var rollBottom = rect.yMin + _flare.y;
            var rollHeight = rollTop - rollBottom;
            var pinch = rollHeight * 0.045f;
            var taper = Mathf.Min(rollHeight * 0.16f, rect.width * 0.08f);
            var brushDepth = Mathf.Min(rollHeight * 0.09f, rect.width * 0.035f);
            var farTop = rollTop + _flare.x * _flareProgress;
            var farBottom = rollBottom - _flare.y * _flareProgress;
            var start = _mirrored ? rect.xMax : rect.xMin;
            var end = _mirrored ? rect.xMin : rect.xMax;
            var direction = _mirrored ? -1f : 1f;
            foreach (var edge in ScrollBrushEdge)
            {
                var inset = taper * edge.x + brushDepth * edge.y;
                var join = start + direction * Mathf.Lerp(_paperEdge.x, _paperEdge.y, edge.x);
                var warm = Color32.Lerp(color, new Color32(240, 168, 15, 255), edge.x * 0.52f);
                mesh.AddVert(new Vector2(join, Mathf.Lerp(rollTop - pinch, rollBottom + pinch, edge.x)),
                    warm, Vector2.zero);
                mesh.AddVert(new Vector2(end - direction * inset, Mathf.Lerp(farTop, farBottom, edge.x)),
                    warm, Vector2.zero);
            }

            for (var i = 0; i < mesh.currentVertCount - 2; i += 2)
            {
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

        internal static int VertexCount(Kind kind)
        {
            if (kind == Kind.ChatFrame)
                return 8;
            return kind is Kind.Tab or Kind.HeaderArt ? 4 : 6;
        }

        internal static Vector2 Point(Kind kind, Rect rect, int index)
        {
            var left = rect.xMin;
            var right = rect.xMax;
            var top = rect.yMax;
            var bottom = rect.yMin;
            if (kind == Kind.ChatFrame)
            {
                var major = Mathf.Min(Mathf.Clamp(rect.height * 0.082f, 14f, 28f), rect.width * 0.14f);
                var minor = major * 0.66f;
                return index switch
                {
                    0 => new Vector2(left, bottom + minor),
                    1 => new Vector2(left, top - major),
                    2 => new Vector2(left + major, top),
                    3 => new Vector2(right - minor, top),
                    4 => new Vector2(right, top - minor),
                    5 => new Vector2(right, bottom + major),
                    6 => new Vector2(right - major, bottom),
                    _ => new Vector2(left + minor, bottom)
                };
            }

            if (kind == Kind.HeaderArt)
            {
                var cut = Mathf.Min(rect.height * 0.38f, rect.width * 0.25f);
                return index switch
                {
                    0 => new Vector2(left, bottom),
                    1 => new Vector2(left + cut, top),
                    2 => new Vector2(right, top),
                    _ => new Vector2(right, bottom)
                };
            }

            if (kind == Kind.Tab)
            {
                var cut = Mathf.Min(rect.height * 0.28f, rect.width * 0.25f);
                return index switch
                {
                    0 => new Vector2(left, bottom),
                    1 => new Vector2(left + cut, top),
                    2 => new Vector2(right, top),
                    _ => new Vector2(right - cut, bottom)
                };
            }

            if (kind == Kind.Composer)
            {
                var cut = Mathf.Min(rect.height * 0.30f, rect.width * 0.20f);
                return index switch
                {
                    0 => new Vector2(left, bottom),
                    1 => new Vector2(left, top - cut),
                    2 => new Vector2(left + cut, top),
                    3 => new Vector2(right, top),
                    4 => new Vector2(right - cut, bottom + 1f),
                    _ => new Vector2(right - cut - 1f, bottom)
                };
            }

            var panelCut = Mathf.Min(14f, Mathf.Min(rect.width * 0.25f, rect.height * 0.42f));
            return index switch
            {
                0 => new Vector2(left, bottom + panelCut),
                1 => new Vector2(left, top),
                2 => new Vector2(right - panelCut, top),
                3 => new Vector2(right, top - panelCut),
                4 => new Vector2(right, bottom),
                _ => new Vector2(left + panelCut, bottom)
            };
        }
    }
}
