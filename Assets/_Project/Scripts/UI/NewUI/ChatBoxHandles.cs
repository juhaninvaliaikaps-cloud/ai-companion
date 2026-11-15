using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-127: an invisible edge or corner of the chat box. Pressing it starts a resize;
    /// <see cref="HappiChatView"/> follows the mouse globally until the button is released,
    /// like the header drag. While the pointer is over it, the resize cursor shows.
    /// </summary>
    public sealed class ChatResizeHandle : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public ResizeEdges Edges;
        public RectTransform Rect;
        public Action<ResizeEdges> Pressed;

        public bool Hovered { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;
            if (Pressed != null)
                Pressed(Edges);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hovered = false;
        }

        void OnDisable()
        {
            Hovered = false;
        }
    }

    /// <summary>
    /// JUH-127: the Windows resize cursors (↔ ↕ ⤡ ⤢) while the pointer is over a chat box edge
    /// or corner, or while resizing. Unity sets its own cursor again on every mouse move, so
    /// <see cref="Show"/> is called each frame while it is needed. Only in the Windows player;
    /// elsewhere it does nothing. The cursor handles are loaded once; nothing is allocated per frame.
    /// </summary>
    public static class ResizeCursor
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int IdcSizeNwse = 32642;
        const int IdcSizeNesw = 32643;
        const int IdcSizeWe = 32644;
        const int IdcSizeNs = 32645;

        static IntPtr _we;
        static IntPtr _ns;
        static IntPtr _nwse;
        static IntPtr _nesw;
        static bool _loaded;

        [DllImport("user32.dll")]
        static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);

        [DllImport("user32.dll")]
        static extern IntPtr SetCursor(IntPtr cursor);

        static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            _we = LoadCursor(IntPtr.Zero, new IntPtr(IdcSizeWe));
            _ns = LoadCursor(IntPtr.Zero, new IntPtr(IdcSizeNs));
            _nwse = LoadCursor(IntPtr.Zero, new IntPtr(IdcSizeNwse));
            _nesw = LoadCursor(IntPtr.Zero, new IntPtr(IdcSizeNesw));
        }
#endif

        /// <summary>The cursor for these edges: 0 ↔, 1 ↕, 2 ⤡ (top-left and bottom-right), 3 ⤢; -1 for none.</summary>
        public static int ShapeFor(ResizeEdges edges)
        {
            var horizontal = (edges & (ResizeEdges.Left | ResizeEdges.Right)) != 0;
            var vertical = (edges & (ResizeEdges.Top | ResizeEdges.Bottom)) != 0;
            if (horizontal && vertical)
            {
                var falling = edges == ResizeEdges.TopLeft || edges == ResizeEdges.BottomRight;
                return falling ? 2 : 3;
            }

            if (horizontal)
                return 0;
            return vertical ? 1 : -1;
        }

        public static void Show(ResizeEdges edges)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var shape = ShapeFor(edges);
            if (shape < 0)
                return;
            Load();
            var cursor = shape == 0 ? _we : shape == 1 ? _ns : shape == 2 ? _nwse : _nesw;
            if (cursor != IntPtr.Zero)
                SetCursor(cursor);
#endif
        }
    }
}
