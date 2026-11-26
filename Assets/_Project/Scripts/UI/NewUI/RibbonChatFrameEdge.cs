using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Thin neutral outline with mint at the two large cuts in the approved chat frame.</summary>
    sealed class RibbonChatFrameEdge : MaskableGraphic
    {
        public static void Add(Transform parent)
        {
            var edge = HappiUiKit.Rect(parent, "Ribbon chat frame edge").gameObject.AddComponent<RibbonChatFrameEdge>();
            edge.raycastTarget = false;
            HappiUiKit.Stretch(edge.rectTransform, 0f, 0f, 0f, 0f);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            const RibbonShape.Kind kind = RibbonShape.Kind.ChatFrame;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            var neutral = new Color32(165, 180, 193, 225);
            var mint = new Color32(114, 238, 207, 255);
            var count = RibbonShape.VertexCount(kind);
            for (var i = 0; i < count; i++)
            {
                var from = RibbonShape.Point(kind, rect, i);
                var to = RibbonShape.Point(kind, rect, (i + 1) % count);
                Stroke(mesh, from, to, neutral, 0.75f);
                if (i == 1 || i == 5)
                    Stroke(mesh, from, to, mint, 1.65f);
                else if (i == 2 || i == 6)
                    Stroke(mesh, from, Vector2.Lerp(from, to, 0.15f), mint, 1.45f);
                else if (i == 0 || i == 4)
                    Stroke(mesh, Vector2.Lerp(from, to, 0.85f), to, mint, 1.45f);
            }
        }

        static void Stroke(VertexHelper mesh, Vector2 from, Vector2 to, Color32 color, float halfWidth)
        {
            var delta = to - from;
            var offset = new Vector2(-delta.y, delta.x).normalized * halfWidth;
            var index = mesh.currentVertCount;
            mesh.AddVert(from - offset, color, Vector2.zero);
            mesh.AddVert(from + offset, color, Vector2.zero);
            mesh.AddVert(to + offset, color, Vector2.zero);
            mesh.AddVert(to - offset, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
