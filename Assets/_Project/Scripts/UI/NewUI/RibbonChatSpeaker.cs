using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Small non-interactive author mark beside a Ribbon chat card.</summary>
    sealed class RibbonChatSpeaker
    {
        readonly Image _frame;
        readonly Outline _border;
        readonly RibbonGlyph _user;
        readonly RawImage _portrait;

        public RibbonChatSpeaker(Transform parent)
        {
            _frame = HappiUiKit.Panel(parent, "Chat speaker", new Color32(37, 44, 56, 255), false);
            _border = _frame.gameObject.AddComponent<Outline>();
            _border.effectDistance = new Vector2(1f, -1f);
            _user = RibbonGlyph.Add(_frame.transform, "User", RibbonGlyph.Mark.Profile, Color.white);
            HappiUiKit.Stretch(_user.rectTransform, 5f, 5f, 5f, 5f);
            _portrait = HappiUiKit.Rect(_frame.transform, "Portrait").gameObject.AddComponent<RawImage>();
            _portrait.raycastTarget = false;
            HappiUiKit.Stretch(_portrait.rectTransform, 2f, 2f, 2f, 2f);
            Hide();
        }

        public void Show(bool reply, Texture picture, float x, float y, float size)
        {
            _frame.gameObject.SetActive(true);
            var showPortrait = reply && picture != null;
            _user.gameObject.SetActive(!showPortrait);
            _portrait.gameObject.SetActive(showPortrait);
            _border.effectColor = reply ? new Color32(114, 238, 207, 225) : new Color32(175, 184, 198, 200);
            if (showPortrait && _portrait.texture != picture)
            {
                _portrait.texture = picture;
                var w = Mathf.Min(1f, (float)picture.height / picture.width);
                var h = Mathf.Min(1f, (float)picture.width / picture.height);
                _portrait.uvRect = new Rect((1f - w) * 0.5f, (1f - h) * 0.5f, w, h);
            }
            HappiUiKit.Place(_frame.rectTransform, x, y, size, size);
        }

        public void Hide()
        {
            _frame.gameObject.SetActive(false);
        }
    }
}
