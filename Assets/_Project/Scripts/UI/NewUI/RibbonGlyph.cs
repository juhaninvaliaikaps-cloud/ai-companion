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
                    Shard(mesh, 0.04f, 0.09f, 0.90f, 0.83f, 0.46f, 0.65f);
                    Shard(mesh, 0.39f, 0.09f, 0.99f, 0.47f, 0.75f, 0.49f);
                    Shard(mesh, 0.16f, 0.48f, 0.43f, 0.99f, 0.29f, 0.94f);
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

        void Shard(VertexHelper mesh, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            var index = mesh.currentVertCount;
            Vertex(mesh, x0, y0);
            Vertex(mesh, x1, y1);
            Vertex(mesh, x2, y2);
            mesh.AddTriangle(index, index + 1, index + 2);
        }

        void Vertex(VertexHelper mesh, float x, float y)
        {
            var rect = rectTransform.rect;
            var position = new Vector3(rect.xMin + x * rect.width, rect.yMin + y * rect.height, 0f);
            mesh.AddVert(position, color, Vector2.zero);
        }
    }
}
