using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    /// <summary>Draws the two transparent portrait frames in one premultiplied blend.</summary>
    sealed class RibbonPortraitBlend : RawImage
    {
        static readonly int NextTextureId = Shader.PropertyToID("_NextTex");
        static readonly int FirstRectId = Shader.PropertyToID("_FirstRect");
        static readonly int NextRectId = Shader.PropertyToID("_NextRect");
        static readonly int WeightsId = Shader.PropertyToID("_Weights");
        Material _blendMaterial;

        public static RibbonPortraitBlend Create(Transform parent, int siblingIndex)
        {
            var shader = Resources.Load<Shader>("RibbonPortraitBlend");
            if (shader == null || !shader.isSupported)
                return null;

            var node = new GameObject("Ribbon portrait blend", typeof(RectTransform));
            node.transform.SetParent(parent, false);
            node.transform.SetSiblingIndex(siblingIndex);
            var blend = node.AddComponent<RibbonPortraitBlend>();
            var rect = blend.rectTransform;
            var inset = Vector2.one * PortraitPlacement.Border;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = inset;
            rect.offsetMax = -inset;
            blend.raycastTarget = false;
            blend.maskable = false;
            blend._blendMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            blend.material = blend._blendMaterial;
            return blend;
        }

        public void SetFrames(Image first, Image next)
        {
            texture = first.sprite.texture;
            _blendMaterial.SetTexture(NextTextureId, next.sprite.texture);
            var canvasSize = Vector2.Max(Vector2.one, rectTransform.rect.size);
            _blendMaterial.SetVector(FirstRectId, PhotoRect(first.rectTransform, canvasSize));
            _blendMaterial.SetVector(NextRectId, PhotoRect(next.rectTransform, canvasSize));
            _blendMaterial.SetVector(WeightsId, new Vector4(first.color.a, next.color.a, 0f, 0f));
        }

        static Vector4 PhotoRect(RectTransform photo, Vector2 canvasSize)
        {
            var size = photo.sizeDelta;
            var corner = photo.anchoredPosition - size * 0.5f;
            return new Vector4(0.5f + corner.x / canvasSize.x, 0.5f + corner.y / canvasSize.y,
                size.x / canvasSize.x, size.y / canvasSize.y);
        }

        protected override void OnDestroy()
        {
            if (_blendMaterial != null)
                Destroy(_blendMaterial);
            base.OnDestroy();
        }
    }
}
