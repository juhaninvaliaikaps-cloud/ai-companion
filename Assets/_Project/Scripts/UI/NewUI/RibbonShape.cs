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

        public void SetMirrored(bool mirrored)
        {
            if (_mirrored == mirrored)
                return;
            _mirrored = mirrored;
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
            var count = VertexCount(_kind);
            vertices.Clear();
            for (var i = 0; i < count; i++)
            {
                var point = Point(_kind, rect, _mirrored ? (count - i) % count : i);
                if (_mirrored)
                    point.x = rect.xMin + rect.xMax - point.x;
                var u = uv.x + uv.width * (point.x - rect.xMin) / rect.width;
                var v = uv.y + uv.height * (point.y - rect.yMin) / rect.height;
                vertices.AddVert(point, color, new Vector2(u, v));
            }
            for (var i = 1; i + 1 < count; i++)
                vertices.AddTriangle(0, i, i + 1);
        }

        internal static int VertexCount(Kind kind)
        {
            if (kind == Kind.ChatFrame)
                return 8;
            return kind is Kind.Tab or Kind.HeaderArt or Kind.ScrollPaper ? 4 : 6;
        }

        internal static Vector2 Point(Kind kind, Rect rect, int index)
        {
            var left = rect.xMin;
            var right = rect.xMax;
            var top = rect.yMax;
            var bottom = rect.yMin;
            if (kind == Kind.ScrollPaper)
            {
                var pinch = Mathf.Min(rect.height * 0.045f, rect.width * 0.10f);
                var taper = Mathf.Min(rect.height * 0.12f, rect.width * 0.08f);
                return index switch
                {
                    0 => new Vector2(left, bottom + pinch),
                    1 => new Vector2(left, top - pinch),
                    2 => new Vector2(right, top),
                    _ => new Vector2(right - taper, bottom)
                };
            }

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
