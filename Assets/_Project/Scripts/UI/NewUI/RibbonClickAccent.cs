using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>A full-button ink stamp drawn behind the selected label.</summary>
    sealed class RibbonClickAccent : MonoBehaviour
    {
        internal const float ReadableSeconds = 0.20f;
        const float Duration = 0.28f;
        RibbonGlyph _ink;
        HappiHoverText _hoverText;
        Color _normalText;
        Color _hoveredText;
        float _startedAt;

        public static RibbonClickAccent Create(Transform layer)
        {
            var glyph = RibbonGlyph.Add(layer, "Ribbon click ink", RibbonGlyph.Mark.Ink,
                new Color32(255, 119, 192, 255));
            var accent = glyph.gameObject.AddComponent<RibbonClickAccent>();
            accent._ink = glyph;
            accent.gameObject.SetActive(false);
            return accent;
        }

        public void Play(HappiButton source)
        {
            Stop();
            _hoverText = source.Button.GetComponent<HappiHoverText>();
            _normalText = _hoverText.Normal;
            _hoveredText = _hoverText.Hover;
            _hoverText.Normal = RibbonMenu.Ink;
            _hoverText.Hover = RibbonMenu.Ink;
            source.Label.color = RibbonMenu.Ink;

            var rect = _ink.rectTransform;
            rect.SetParent(source.Rect, false);
            rect.SetAsFirstSibling();
            rect.pivot = new Vector2(0.5f, 0.5f);
            HappiUiKit.Stretch(rect, -3f, -2f, -3f, -2f);
            rect.localScale = Vector3.one;
            _startedAt = Time.unscaledTime;
            gameObject.SetActive(true);
            _ink.canvasRenderer.SetAlpha(1f);
        }

        public void Stop()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        void Update()
        {
            var elapsed = Time.unscaledTime - _startedAt;
            if (elapsed >= Duration)
            {
                Stop();
                return;
            }

            var fade = Mathf.InverseLerp(ReadableSeconds, Duration, elapsed);
            _ink.canvasRenderer.SetAlpha(Mathf.Lerp(1f, 0.8f, fade));
            _ink.rectTransform.localScale = new Vector3(1f + 0.035f * fade, 1f + 0.10f * fade, 1f);
        }

        void OnDisable()
        {
            if (_hoverText == null)
                return;

            _hoverText.Normal = _normalText;
            _hoverText.Hover = _hoveredText;
            _hoverText.Label.color = _normalText;
            _hoverText = null;
            _ink.canvasRenderer.SetAlpha(0f);
        }
    }
}
