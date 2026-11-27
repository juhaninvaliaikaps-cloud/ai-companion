using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AICompanion.NewUI
{
    /// <summary>JUH-228: a press on her room's floor, passed to the room view.</summary>
    sealed class HerRoomInput : MonoBehaviour, IPointerDownHandler
    {
        public Action<PointerEventData> Pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            Pressed?.Invoke(eventData);
        }
    }

    /// <summary>JUH-228: the resize corner of her room window.</summary>
    sealed class HerRoomResize : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        public Action<Vector2> Dragged;
        public Action Released;

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(eventData.delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Released?.Invoke();
        }
    }
}
