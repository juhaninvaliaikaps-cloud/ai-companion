using System;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-104: right-click on a visible pixel of her. The overlay asks this before it toggles
    /// rotate mode. The new UI installs a handler that opens the radial wheel and consumes
    /// both plain and Shift+right-click; rotate mode is no longer used with the 2D portrait.
    /// With no handler, which is always the case while the new UI is off, it returns false.
    /// </summary>
    public static class AvatarContextMenu
    {
        /// <summary>Gets the click in Unity screen pixels; returns true when it opened a menu.</summary>
        public static Func<Vector2, bool> Handler;

        public static bool TryOpen(Vector2 unityScreen)
        {
            var handler = Handler;
            if (handler == null)
                return false;
            try
            {
                return handler(unityScreen);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Wheel: could not open (" + ex.GetType().Name + ")");
                return false;
            }
        }
    }
}
