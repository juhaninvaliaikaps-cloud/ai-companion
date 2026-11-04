using UnityEngine.UI;

namespace AICompanion.Settings
{
    /// <summary>
    /// ScrollRects default to one pixel per wheel tick, which makes the settings
    /// list crawl. This value is meant for a mouse wheel notch and a touchpad gesture.
    /// </summary>
    public static class SettingsScroll
    {
        public const float Sensitivity = 80f;

        public static void Apply(ScrollRect scroll)
        {
            if (scroll == null)
                return;

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = Sensitivity;
        }
    }
}
