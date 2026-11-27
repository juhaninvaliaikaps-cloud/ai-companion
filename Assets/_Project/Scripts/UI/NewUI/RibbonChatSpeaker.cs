using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Small non-interactive author mark beside a Ribbon chat card.</summary>
    sealed class RibbonChatSpeaker
    {
        readonly Image _frame;
        readonly Image _fill;
        readonly RibbonGlyph _silhouette;
        readonly RawImage _portrait;

        public RibbonChatSpeaker(Transform parent)
        {
            _frame = HappiUiKit.Panel(parent, "Chat speaker", Color.white, false);
            _fill = HappiUiKit.Panel(_frame.transform, "Rounded speaker plate", new Color32(37, 42, 52, 255), false);
            _fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _silhouette = RibbonGlyph.Add(_fill.transform, "Speaker silhouette", RibbonGlyph.Mark.Profile, Color.white);
            _portrait = HappiUiKit.Rect(_fill.transform, "Portrait").gameObject.AddComponent<RawImage>();
            _portrait.raycastTarget = false;
            HappiUiKit.Stretch(_portrait.rectTransform, 0f, 0f, 0f, 0f);
            Hide();
        }

        public void Show(bool reply, Texture picture, Rect bounds)
        {
            _frame.gameObject.SetActive(true);
            var showPortrait = reply && picture != null;
            _silhouette.gameObject.SetActive(!showPortrait);
            _portrait.gameObject.SetActive(showPortrait);
            _frame.color = reply ? new Color32(114, 238, 207, 255) : new Color32(172, 179, 194, 255);
            _silhouette.color = reply ? new Color32(164, 247, 223, 255) : new Color32(243, 246, 255, 255);
            var rim = Mathf.Clamp(bounds.width * 0.035f, 1f, 2.5f);
            var iconInset = bounds.width * 0.17f;
            HappiUiKit.Stretch(_fill.rectTransform, rim, rim, rim, rim);
            HappiUiKit.Stretch(_silhouette.rectTransform, iconInset, iconInset, iconInset, iconInset);
            if (showPortrait && _portrait.texture != picture)
            {
                _portrait.texture = picture;
                var w = Mathf.Min(1f, (float)picture.height / picture.width);
                var h = Mathf.Min(1f, (float)picture.width / picture.height);
                _portrait.uvRect = new Rect((1f - w) * 0.5f, (1f - h) * 0.5f, w, h);
            }
            HappiUiKit.Place(_frame.rectTransform, bounds.x, bounds.y, bounds.width, bounds.height);
        }

        public void Hide()
        {
            _frame.gameObject.SetActive(false);
        }
    }
}
