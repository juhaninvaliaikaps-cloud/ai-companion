using AICompanion.Core;
using AICompanion.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-27: the first-use consent card for a desktop tool. The permission service owns
    /// the decision and persistence; this view only asks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesktopToolConsentView : MonoBehaviour
    {
        const int SortingOrder = 72;
        const float PlaneDistance = 0.44f;

        SettingsPanel _settings;
        GameObject _canvasObject;
        Canvas _canvas;
        RectTransform _card;
        TextMeshProUGUI _title;
        TextMeshProUGUI _body;
        HappiButton _allow;
        HappiButton _deny;
        DesktopToolKind _pending;
        System.Action<bool> _answer;
        bool _attached;
        bool _open;

        public void Attach(SettingsPanel settings)
        {
            _settings = settings;
            _attached = true;
            if (_canvasObject == null)
                Build();
            DesktopToolPermissions.ConsentHandler = Show;
            _open = false;
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
        }

        public void Detach()
        {
            _attached = false;
            if (DesktopToolPermissions.ConsentHandler == Show)
                DesktopToolPermissions.ConsentHandler = null;
            _answer = null;
            _open = false;
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
        }

        void Show(DesktopToolKind kind, System.Action<bool> answer)
        {
            if (!_attached || _canvasObject == null)
            {
                answer?.Invoke(false);
                return;
            }

            _pending = kind;
            _answer = answer;
            _open = true;
            _title.text = "Allow " + DesktopToolCatalog.Label(kind) + "?";
            _body.text = DesktopToolCatalog.Description(kind)
                + "\n\nHappi will not use this until you allow it. You can revoke it at any time in Settings > Senses > Tools.";
            _canvasObject.SetActive(true);
            _canvasObject.transform.SetAsLastSibling();
            HappiUiKit.FollowCamera(_canvas, PlaneDistance);
        }

        void Resolve(bool allow)
        {
            if (!_open)
                return;
            _open = false;
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
            var answer = _answer;
            _answer = null;
            answer?.Invoke(allow);
        }

        void Update()
        {
            if (_open && _canvasObject != null && _canvasObject.activeSelf)
                HappiUiKit.FollowCamera(_canvas, PlaneDistance);
        }

        void Build()
        {
            _canvasObject = HappiUiKit.CreateCanvas(transform, "HappiToolConsent", SortingOrder, out _canvas);
            var backdrop = HappiUiKit.Panel(_canvasObject.transform, "Backdrop", new Color(0.02f, 0.04f, 0.08f, 0.72f), true);
            HappiUiKit.Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);
            var backButton = backdrop.gameObject.AddComponent<Button>();
            backButton.transition = Selectable.Transition.None;
            backButton.onClick.AddListener(() => Resolve(false));

            var card = HappiUiKit.Panel(_canvasObject.transform, "ConsentCard", HappiTheme.Current.PanelBackground, true);
            _card = card.rectTransform;
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(440f, 250f);
            _card.anchoredPosition = Vector2.zero;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = HappiTheme.Current.Border;
            outline.effectDistance = new Vector2(1f, 1f);

            _title = HappiUiKit.Text(_card, "Title", string.Empty, 20f, HappiTheme.Current.Text, TextAlignmentOptions.TopLeft, true);
            HappiUiKit.Place(_title.rectTransform, 20f, 16f, 400f, 30f);
            _body = HappiUiKit.Text(_card, "Body", string.Empty, 14f, HappiTheme.Current.Text, TextAlignmentOptions.TopLeft, false);
            _body.textWrappingMode = TextWrappingModes.Normal;
            HappiUiKit.Place(_body.rectTransform, 20f, 54f, 400f, 110f);
            _deny = HappiUiKit.Button(_card, "Deny", "Not now", 14f, () => Resolve(false));
            HappiUiKit.Place(_deny.Rect, 20f, 194f, 185f, 34f);
            _allow = HappiUiKit.Button(_card, "Allow", "Allow", 14f, () => Resolve(true));
            HappiUiKit.Place(_allow.Rect, 225f, 194f, 195f, 34f);
            _canvasObject.SetActive(false);
        }
    }
}
