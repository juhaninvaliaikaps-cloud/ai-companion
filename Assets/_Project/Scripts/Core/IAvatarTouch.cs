using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Implemented by anything that wants pointer presses on the avatar's silhouette. The
    /// overlay dispatches only when the pointer is on her body and not over UI, so ordinary
    /// window dragging, click-through and the chat panel keep working.
    /// </summary>
    public interface IAvatarTouch
    {
        /// <summary>
        /// Left button pressed on her silhouette, in Unity screen space. Return true to consume
        /// the press so the overlay does not start a window drag; a hand grab does exactly that.
        /// </summary>
        bool OnAvatarTouch(Vector2 screenPosition);

        /// <summary>The left button was released after touching her.</summary>
        void OnAvatarRelease();
    }
}
