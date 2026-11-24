using UnityEngine;
using UnityEngine.EventSystems;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-188: the pointer handlers must live on the uGUI object that the raycaster hits.
    /// The window component itself lives on the overlay root, so this small component forwards
    /// drag/scroll/click events to it.
    /// </summary>
    public sealed class AdultClipWindowInput : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        public AdultClipWindow Owner;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Owner != null)
                Owner.OnPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Owner != null)
                Owner.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Owner != null)
                Owner.OnPointerUp(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Owner != null)
                Owner.OnScroll(eventData);
        }
    }
}
