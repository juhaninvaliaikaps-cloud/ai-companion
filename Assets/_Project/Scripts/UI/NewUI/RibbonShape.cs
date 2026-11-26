using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Pixel-defined Ribbon geometry. Only the rendered mesh changes; controls retain their hit areas.</summary>
    sealed class RibbonShape : BaseMeshEffect
    {
        bool _tab;

        public static void Apply(Image image, bool tab)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            var shape = image.gameObject.AddComponent<RibbonShape>();
            shape._tab = tab;
            image.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive())
                return;

            var rect = graphic.GetPixelAdjustedRect();
            var cut = Mathf.Min(_tab ? rect.height * 0.28f : 14f, rect.width * 0.25f);
            Color32 color = graphic.color;
            vertices.Clear();
            if (_tab)
            {
                Add(vertices, rect.xMin, rect.yMin, color);
                Add(vertices, rect.xMin + cut, rect.yMax, color);
                Add(vertices, rect.xMax, rect.yMax, color);
                Add(vertices, rect.xMax - cut, rect.yMin, color);
            }
            else
            {
                Add(vertices, rect.xMin, rect.yMin + cut, color);
                Add(vertices, rect.xMin, rect.yMax, color);
                Add(vertices, rect.xMax - cut, rect.yMax, color);
                Add(vertices, rect.xMax, rect.yMax - cut, color);
                Add(vertices, rect.xMax, rect.yMin, color);
                Add(vertices, rect.xMin + cut, rect.yMin, color);
            }

            for (var i = 1; i + 1 < vertices.currentVertCount; i++)
                vertices.AddTriangle(0, i, i + 1);
        }

        static void Add(VertexHelper vertices, float x, float y, Color32 color)
        {
            vertices.AddVert(new Vector3(x, y, 0f), color, Vector2.zero);
        }
    }
}
