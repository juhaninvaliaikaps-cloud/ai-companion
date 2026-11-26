using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Fades the painting into the yellow paper under the submenu buttons.</summary>
    sealed class RibbonScrollArt : RawImage
    {
        bool _fadeOnLeft = true;

        public void SetFadeSide(bool fadeOnLeft)
        {
            if (_fadeOnLeft == fadeOnLeft)
                return;
            _fadeOnLeft = fadeOnLeft;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            const float fade = 0.09f;
            var uv = uvRect;
            for (var i = 0; i < 3; i++)
            {
                var t = i == 0 ? 0f : i == 2 ? 1f : _fadeOnLeft ? fade : 1f - fade;
                var x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                var u = Mathf.Lerp(uv.xMin, uv.xMax, t);
                Color32 tint = color;
                if (i == (_fadeOnLeft ? 0 : 2))
                    tint.a = 0;
                vertices.AddVert(new Vector2(x, rect.yMin), tint, new Vector2(u, uv.yMin));
                vertices.AddVert(new Vector2(x, rect.yMax), tint, new Vector2(u, uv.yMax));
            }

            for (var i = 0; i < 4; i += 2)
            {
                vertices.AddTriangle(i, i + 1, i + 2);
                vertices.AddTriangle(i + 2, i + 1, i + 3);
            }
        }
    }
}
