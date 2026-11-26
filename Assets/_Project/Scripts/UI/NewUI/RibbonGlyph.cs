using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Decorative marks for the Ribbon UI. The surrounding control owns the hit area.</summary>
    sealed class RibbonGlyph : MaskableGraphic
    {
        internal enum Mark { Menu, Door, Ink, Voice, Muted, Screen, Profile, Model, Settings, Close, Chevron, Lock, OpenLock, PaperEdge }
        internal TMP_Text Label { get; set; }
        internal Mark Shape
        {
            get => _mark;
            set
            {
                if (_mark == value)
                    return;
                _mark = value;
                SetVerticesDirty();
            }
        }
        Mark _mark;

        public static RibbonGlyph Add(Transform parent, string name, Mark mark, Color color)
        {
            var glyph = HappiUiKit.Rect(parent, name).gameObject.AddComponent<RibbonGlyph>();
            glyph.Shape = mark;
            glyph.color = color;
            glyph.raycastTarget = false;
            glyph.SetVerticesDirty();
            return glyph;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (_mark)
            {
                case Mark.Menu:
                    Line(mesh, 0.17f, 0.25f, 0.71f, 0.25f);
                    Line(mesh, 0.17f, 0.50f, 0.83f, 0.50f);
                    Line(mesh, 0.17f, 0.75f, 0.83f, 0.75f);
                    break;
                case Mark.Door:
                    Line(mesh, 0.18f, 0.12f, 0.18f, 0.89f);
                    Line(mesh, 0.18f, 0.89f, 0.63f, 0.89f);
                    Line(mesh, 0.18f, 0.12f, 0.78f, 0.12f);
                    Line(mesh, 0.63f, 0.89f, 0.83f, 0.73f);
                    Line(mesh, 0.83f, 0.73f, 0.83f, 0.21f);
                    Line(mesh, 0.83f, 0.21f, 0.63f, 0.12f);
                    Line(mesh, 0.63f, 0.12f, 0.63f, 0.89f);
                    Line(mesh, 0.72f, 0.49f, 0.76f, 0.49f);
                    break;
                case Mark.Voice:
                case Mark.Muted:
                    Speaker(mesh, _mark == Mark.Muted);
                    break;
                case Mark.Screen:
                    Box(mesh, 0.12f, 0.29f, 0.88f, 0.85f);
                    Line(mesh, 0.50f, 0.29f, 0.50f, 0.14f);
                    Line(mesh, 0.32f, 0.14f, 0.68f, 0.14f);
                    break;
                case Mark.Profile:
                    Ring(mesh, new Vector2(0.50f, 0.70f), 0.16f);
                    Line(mesh, 0.15f, 0.16f, 0.21f, 0.33f);
                    Line(mesh, 0.21f, 0.33f, 0.37f, 0.41f);
                    Line(mesh, 0.37f, 0.41f, 0.63f, 0.41f);
                    Line(mesh, 0.63f, 0.41f, 0.79f, 0.33f);
                    Line(mesh, 0.79f, 0.33f, 0.85f, 0.16f);
                    Line(mesh, 0.15f, 0.16f, 0.85f, 0.16f);
                    break;
                case Mark.Model:
                    Box(mesh, 0.24f, 0.24f, 0.76f, 0.76f);
                    Box(mesh, 0.40f, 0.40f, 0.60f, 0.60f);
                    for (var i = 0; i < 3; i++)
                    {
                        var pin = 0.34f + i * 0.16f;
                        Line(mesh, pin, 0.76f, pin, 0.91f);
                        Line(mesh, pin, 0.09f, pin, 0.24f);
                        Line(mesh, 0.09f, pin, 0.24f, pin);
                        Line(mesh, 0.76f, pin, 0.91f, pin);
                    }
                    break;
                case Mark.Settings:
                    SettingsCog(mesh);
                    break;
                case Mark.Close:
                    Line(mesh, 0.25f, 0.25f, 0.75f, 0.75f);
                    Line(mesh, 0.25f, 0.75f, 0.75f, 0.25f);
                    break;
                case Mark.Chevron:
                    Line(mesh, 0.33f, 0.17f, 0.67f, 0.50f);
                    Line(mesh, 0.67f, 0.50f, 0.33f, 0.83f);
                    break;
                case Mark.Lock:
                case Mark.OpenLock:
                    Box(mesh, 0.21f, 0.15f, 0.79f, 0.57f);
                    Line(mesh, 0.50f, 0.30f, 0.50f, 0.41f);
                    Line(mesh, 0.33f, 0.57f, 0.33f, 0.75f);
                    Line(mesh, 0.33f, 0.75f, 0.43f, 0.87f);
                    Line(mesh, 0.43f, 0.87f, 0.61f, 0.87f);
                    Line(mesh, 0.61f, 0.87f, 0.69f, 0.75f);
                    Line(mesh, 0.69f, 0.75f, 0.69f, _mark == Mark.Lock ? 0.57f : 0.68f);
                    break;
                case Mark.PaperEdge:
                    PaperEdge(mesh);
                    break;
                case Mark.Ink:
                    Stamp(mesh);
                    break;
            }
        }

        void LateUpdate()
        {
            if (Label != null && color != Label.color)
                color = Label.color;
        }

        void Speaker(VertexHelper mesh, bool muted)
        {
            Line(mesh, 0.10f, 0.36f, 0.28f, 0.36f);
            Line(mesh, 0.28f, 0.36f, 0.53f, 0.16f);
            Line(mesh, 0.53f, 0.16f, 0.53f, 0.84f);
            Line(mesh, 0.53f, 0.84f, 0.28f, 0.64f);
            Line(mesh, 0.28f, 0.64f, 0.10f, 0.64f);
            Line(mesh, 0.10f, 0.64f, 0.10f, 0.36f);
            if (muted)
            {
                Line(mesh, 0.68f, 0.34f, 0.94f, 0.66f);
                Line(mesh, 0.68f, 0.66f, 0.94f, 0.34f);
                return;
            }

            Line(mesh, 0.68f, 0.31f, 0.78f, 0.50f);
            Line(mesh, 0.78f, 0.50f, 0.68f, 0.69f);
            Line(mesh, 0.81f, 0.20f, 0.95f, 0.50f);
            Line(mesh, 0.95f, 0.50f, 0.81f, 0.80f);
        }

        void Box(VertexHelper mesh, float left, float bottom, float right, float top)
        {
            Line(mesh, left, bottom, right, bottom);
            Line(mesh, right, bottom, right, top);
            Line(mesh, right, top, left, top);
            Line(mesh, left, top, left, bottom);
        }

        void Ring(VertexHelper mesh, Vector2 center, float radius)
        {
            var from = center + Vector2.right * radius;
            for (var i = 1; i <= 12; i++)
            {
                var angle = i * (2f * Mathf.PI / 12f);
                var to = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Line(mesh, from.x, from.y, to.x, to.y);
                from = to;
            }
        }

        void SettingsCog(VertexHelper mesh)
        {
            var center = new Vector2(0.5f, 0.5f);
            Ring(mesh, center, 0.13f);
            var from = center + Vector2.right * 0.33f;
            for (var i = 1; i <= 24; i++)
            {
                var angle = i * (2f * Mathf.PI / 24f);
                var radius = i % 4 == 0 || i % 4 == 3 ? 0.33f : 0.42f;
                var to = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Line(mesh, from.x, from.y, to.x, to.y);
                from = to;
            }
        }

        void Line(VertexHelper mesh, float x0, float y0, float x1, float y1)
        {
            var offset = new Vector2(y0 - y1, x1 - x0).normalized * 0.035f;
            var index = mesh.currentVertCount;
            Vertex(mesh, x0 + offset.x, y0 + offset.y);
            Vertex(mesh, x1 + offset.x, y1 + offset.y);
            Vertex(mesh, x1 - offset.x, y1 - offset.y);
            Vertex(mesh, x0 - offset.x, y0 - offset.y);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }

        void PaperEdge(VertexHelper mesh)
        {
            // A darker fold under two highlights and short ink fibers at the reveal edge.
            Color32 fold = new Color32(214, 147, 21, 255);
            Color32 ink = new Color32(74, 52, 18, 255);
            Vertex(mesh, 0.05f, 0.02f, fold);
            Vertex(mesh, 0.18f, 0.98f, fold);
            Vertex(mesh, 0.94f, 0.92f, fold);
            Vertex(mesh, 0.78f, 0.10f, fold);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 3);
            Line(mesh, 0.25f, 0.06f, 0.41f, 0.94f);
            Line(mesh, 0.46f, 0.14f, 0.58f, 0.89f);
            Shard(mesh, ink, 0.77f, 0.14f, 0.98f, 0.12f, 0.81f, 0.24f);
            Shard(mesh, ink, 0.81f, 0.76f, 0.99f, 0.79f, 0.83f, 0.84f);
            Shard(mesh, ink, 0.86f, 0.90f, 0.98f, 0.94f, 0.70f, 0.95f);
        }

        void Stamp(VertexHelper mesh)
        {
            // Broad pink paint holds the label, with irregular yellow marks at the edges.
            Vertex(mesh, 0.00f, 0.08f);
            Vertex(mesh, 0.05f, 0.55f);
            Vertex(mesh, 0.02f, 0.77f);
            Vertex(mesh, 0.15f, 0.98f);
            Vertex(mesh, 0.87f, 0.89f);
            Vertex(mesh, 1.00f, 0.96f);
            Vertex(mesh, 0.97f, 0.62f);
            Vertex(mesh, 1.00f, 0.42f);
            Vertex(mesh, 0.93f, 0.08f);
            Vertex(mesh, 0.12f, 0.14f);
            for (var i = 1; i <= 8; i++)
                mesh.AddTriangle(0, i, i + 1);

            Color32 yellow = new Color32(255, 215, 59, 255);
            Shard(mesh, yellow, 0.01f, 0.14f, 0.34f, 0.22f, 0.08f, 0.43f);
            Shard(mesh, yellow, 0.72f, 0.77f, 1.02f, 1.03f, 0.91f, 0.75f);
            Shard(mesh, yellow, 0.80f, 0.12f, 0.95f, 0.04f, 1.02f, 0.18f);
        }

        void Shard(VertexHelper mesh, Color32 ink, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            var index = mesh.currentVertCount;
            Vertex(mesh, x0, y0, ink);
            Vertex(mesh, x1, y1, ink);
            Vertex(mesh, x2, y2, ink);
            mesh.AddTriangle(index, index + 1, index + 2);
        }

        void Vertex(VertexHelper mesh, float x, float y) => Vertex(mesh, x, y, color);

        void Vertex(VertexHelper mesh, float x, float y, Color32 ink)
        {
            var rect = rectTransform.rect;
            var position = new Vector3(rect.xMin + x * rect.width, rect.yMin + y * rect.height, 0f);
            mesh.AddVert(position, ink, Vector2.zero);
        }
    }
}
