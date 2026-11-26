using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Decorative marks for the Ribbon UI. The surrounding control owns the hit area.</summary>
    sealed class RibbonGlyph : MaskableGraphic
    {
        internal enum Mark { Menu, Door, Ink }
        Mark _mark;

        public static RibbonGlyph Add(Transform parent, string name, Mark mark, Color color)
        {
            var glyph = HappiUiKit.Rect(parent, name).gameObject.AddComponent<RibbonGlyph>();
            glyph._mark = mark;
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
                case Mark.Ink:
                    Stamp(mesh);
                    break;
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
