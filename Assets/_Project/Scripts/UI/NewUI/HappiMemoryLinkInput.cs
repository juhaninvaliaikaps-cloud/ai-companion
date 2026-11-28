using System.Collections.Generic;
using AICompanion.Core;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    sealed class HappiMemoryLinkInput : Selectable, IPointerMoveHandler, IPointerClickHandler, ISubmitHandler
    {
        HappiChatView _view;
        TextMeshProUGUI _text;
        IReadOnlyList<ChatMemoryLink> _links;
        int _hovered = -1;
        int _focused;

        internal void Bind(HappiChatView view, TextMeshProUGUI text, IReadOnlyList<ChatMemoryLink> links)
        {
            _view = view;
            _text = text;
            _links = links;
            _hovered = -1;
            _focused = 0;
            interactable = links != null;
            text.raycastTarget = links != null;
            transition = Transition.None;
            navigation = new Navigation { mode = links != null ? Navigation.Mode.Automatic : Navigation.Mode.None };
        }

        int LinkAt(PointerEventData pointer)
            => TMP_TextUtilities.FindIntersectingLink(_text, pointer.position, pointer.enterEventCamera);

        ChatMemoryLink Target(int index)
        {
            if (_links == null || index < 0 || index >= _text.textInfo.linkCount ||
                !int.TryParse(_text.textInfo.linkInfo[index].GetLinkID(), out var id) || id < 0 || id >= _links.Count)
                return null;
            return _links[id];
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            var index = LinkAt(eventData);
            if (index == _hovered)
                return;
            _hovered = index;
            var target = Target(index);
            if (target != null)
                _view.ShowMemoryPreview(this, target);
            else
                _view.HideMemoryPreview(this);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            OnPointerMove(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (Target(LinkAt(eventData)) != null)
                base.OnPointerDown(eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            _hovered = -1;
            _view?.HideMemoryPreview(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Open(LinkAt(eventData));
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            var target = Target(_focused);
            if (target != null)
                _view.ShowMemoryPreview(this, target);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            _view?.HideMemoryPreview(this);
        }

        public override void OnMove(AxisEventData eventData)
        {
            var next = _focused + (eventData.moveDir == MoveDirection.Left ? -1 :
                eventData.moveDir == MoveDirection.Right ? 1 : 0);
            if (next != _focused && Target(next) != null)
            {
                _focused = next;
                _view.ShowMemoryPreview(this, Target(next));
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }

        public void OnSubmit(BaseEventData eventData) => Open(_focused);

        protected override void OnDisable()
        {
            _view?.HideMemoryPreview(this);
            base.OnDisable();
        }

        void Open(int index)
        {
            var target = Target(index);
            if (target != null)
                _view.OpenMemoryLink(this, target);
        }
    }
}
