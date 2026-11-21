using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-166: the sums behind the large picture that opens while the mouse rests on her small
    /// picture in the chat header. The picture is shown at its own size, smaller only when it
    /// wouldn't fit in Happi's window, and kept fully inside the window.
    /// </summary>
    public static class PicturePreviewMath
    {
        /// <summary>The share of the window the preview may fill at most.</summary>
        public const float MaxShare = 0.92f;

        /// <summary>The picture's own size, scaled down (never up) to fit <paramref name="max"/>, keeping its shape.</summary>
        public static Vector2 Fit(float width, float height, Vector2 max)
        {
            if (width <= 0f || height <= 0f || max.x <= 0f || max.y <= 0f)
                return Vector2.zero;
            var scale = Mathf.Min(1f, Mathf.Min(max.x / width, max.y / height));
            return new Vector2(width * scale, height * scale);
        }

        /// <summary>
        /// The preview's centre: next to the small picture, above it when there is room there and
        /// below it otherwise, then moved so the whole preview stays inside <paramref name="window"/>.
        /// </summary>
        public static Vector2 Place(Rect window, Rect anchor, Vector2 size, float margin)
        {
            var x = anchor.center.x;
            var above = window.yMax - anchor.yMax;
            var below = anchor.yMin - window.yMin;
            var y = above >= size.y + margin || above >= below
                ? anchor.yMax + margin + size.y * 0.5f
                : anchor.yMin - margin - size.y * 0.5f;
            x = Clamp(x, window.xMin + margin + size.x * 0.5f, window.xMax - margin - size.x * 0.5f, window.center.x);
            y = Clamp(y, window.yMin + margin + size.y * 0.5f, window.yMax - margin - size.y * 0.5f, window.center.y);
            return new Vector2(x, y);
        }

        static float Clamp(float value, float min, float max, float middle)
        {
            // A preview wider than the room left between the margins sits in the middle.
            return min > max ? middle : Mathf.Clamp(value, min, max);
        }
    }

    /// <summary>
    /// JUH-166: the large picture itself. Made once on first use; it never takes clicks or hover
    /// (raycastTarget off), so it can't flicker by covering the small picture or change click-through.
    /// <see cref="Tick"/> doesn't allocate.
    /// </summary>
    public sealed class PictureHoverPreview
    {
        /// <summary>How long the mouse rests on the small picture before the large one opens.</summary>
        public const float Delay = 0.25f;
        const float Margin = 8f;
        const float Border = 4f;

        readonly RectTransform _window;
        readonly HappiTheme _theme;
        RectTransform _frame;
        RawImage _image;
        float _hoverSince = -1f;
        Texture _shown;
        readonly Vector3[] _corners = new Vector3[4];

        public bool Open => _frame != null && _frame.gameObject.activeSelf;

        public PictureHoverPreview(RectTransform window, HappiTheme theme)
        {
            _window = window;
            _theme = theme;
        }

        public void Tick(bool hovering, Texture texture, RectTransform anchor, float now)
        {
            if (!hovering || texture == null || anchor == null || _window == null)
            {
                _hoverSince = -1f;
                Hide();
                return;
            }

            if (_hoverSince < 0f)
                _hoverSince = now;
            if (now - _hoverSince < Delay)
                return;
            Show(texture, anchor);
        }

        public void Hide()
        {
            if (_frame != null && _frame.gameObject.activeSelf)
                _frame.gameObject.SetActive(false);
            _shown = null;
        }

        void Show(Texture texture, RectTransform anchor)
        {
            if (_frame == null)
                Build();
            var room = _window.rect.size * PicturePreviewMath.MaxShare - new Vector2(Border, Border) * 2f;
            var canvas = _window.GetComponentInParent<Canvas>();
            var pixels = canvas != null && canvas.scaleFactor > 0f ? 1f / canvas.scaleFactor : 1f;
            var size = PicturePreviewMath.Fit(texture.width * pixels, texture.height * pixels, room);
            if (size.x <= 0f)
            {
                Hide();
                return;
            }

            if (!ReferenceEquals(texture, _shown))
            {
                _image.texture = texture;
                _shown = texture;
            }

            var frameSize = size + new Vector2(Border, Border) * 2f;
            _frame.sizeDelta = frameSize;
            _frame.localPosition = PicturePreviewMath.Place(_window.rect, AnchorRect(anchor), frameSize, Margin);
            if (!_frame.gameObject.activeSelf)
                _frame.gameObject.SetActive(true);
            _frame.SetAsLastSibling();
        }

        /// <summary>The small picture's rectangle in the window's own space.</summary>
        Rect AnchorRect(RectTransform anchor)
        {
            anchor.GetWorldCorners(_corners);
            var min = _window.InverseTransformPoint(_corners[0]);
            var max = _window.InverseTransformPoint(_corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        void Build()
        {
            var back = HappiUiKit.Panel(_window, "PicturePreview", _theme.PanelBackground, false);
            _frame = back.rectTransform;
            _frame.anchorMin = new Vector2(0.5f, 0.5f);
            _frame.anchorMax = new Vector2(0.5f, 0.5f);
            _frame.pivot = new Vector2(0.5f, 0.5f);
            _image = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            _image.transform.SetParent(_frame, false);
            HappiUiKit.Stretch(_image.rectTransform, Border, Border, Border, Border);
            _image.raycastTarget = false;
            _image.color = Color.white;
            _frame.gameObject.SetActive(false);
        }
    }
}
