using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Outlines for the chat frame and composer, drawn against their clipped edges.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    sealed class RibbonChatFrameEdge : MaskableGraphic
    {
        RibbonShape.Kind _kind;

        public static void Add(Transform parent, RibbonShape.Kind kind = RibbonShape.Kind.ChatFrame)
        {
            var edge = HappiUiKit.Rect(parent, "Ribbon outline").gameObject.AddComponent<RibbonChatFrameEdge>();
            edge._kind = kind;
            edge.raycastTarget = false;
            HappiUiKit.Stretch(edge.rectTransform, 0f, 0f, 0f, 0f);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            var frame = _kind == RibbonShape.Kind.ChatFrame;
            var neutral = frame ? new Color32(165, 180, 193, 225) : new Color32(152, 165, 181, 240);
            var mint = new Color32(114, 238, 207, 255);
            var count = RibbonShape.VertexCount(_kind);
            for (var i = 0; i < count; i++)
            {
                var from = RibbonShape.Point(_kind, rect, i);
                var to = RibbonShape.Point(_kind, rect, (i + 1) % count);
                Stroke(mesh, from, to, neutral, frame ? 0.75f : 0.55f);
                if (!frame)
                    continue;
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
