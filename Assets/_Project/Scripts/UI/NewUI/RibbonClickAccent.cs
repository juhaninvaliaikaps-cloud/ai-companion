using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>A single ink stamp beside a selected control. It can finish after the menu closes.</summary>
    sealed class RibbonClickAccent : MonoBehaviour
    {
        const float Duration = 0.22f;
        RibbonGlyph _ink;
        float _startedAt;
        Vector2 _start;

        public static RibbonClickAccent Create(Transform layer)
        {
            var glyph = RibbonGlyph.Add(layer, "Ribbon click ink", RibbonGlyph.Mark.Ink, RibbonMenu.Cyan);
            var accent = glyph.gameObject.AddComponent<RibbonClickAccent>();
            accent._ink = glyph;
            accent.gameObject.SetActive(false);
            return accent;
        }

        public void Play(RectTransform source)
        {
            if (!transform.parent.gameObject.activeInHierarchy)
                return;

            var rect = _ink.rectTransform;
            var button = source.rect;
            var layer = (RectTransform)rect.parent;
            var corner = layer.InverseTransformPoint(source.TransformPoint(new Vector3(button.xMax, button.yMax, 0f)));
            var scale = Mathf.Clamp(button.height / 29f, 0.7f, 2f);
            rect.anchorMin = rect.anchorMax = layer.pivot;
            rect.pivot = new Vector2(0.5f, 0.3f);
            rect.sizeDelta = new Vector2(27f, 18f) * scale;
            _start = new Vector2(
                Mathf.Clamp(corner.x - 7f * scale, layer.rect.xMin + 15f * scale, layer.rect.xMax - 15f * scale),
                Mathf.Min(corner.y, layer.rect.yMax - 16f * scale));
            rect.anchoredPosition = _start;
            rect.localScale = Vector3.one * 0.8f;
            _startedAt = Time.unscaledTime;
            rect.SetAsLastSibling();
            gameObject.SetActive(true);
            _ink.canvasRenderer.SetAlpha(1f);
        }

        void Update()
        {
            var progress = (Time.unscaledTime - _startedAt) / Duration;
            if (progress >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }

            _ink.rectTransform.anchoredPosition = _start + new Vector2(2f, 6f) * progress;
            _ink.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.15f, progress);
            _ink.canvasRenderer.SetAlpha(1f - progress * progress);
        }

        void OnDisable()
        {
            // Hiding chat mid-stamp must not replay the effect when chat opens again.
            _startedAt = float.NegativeInfinity;
        }
    }
}
