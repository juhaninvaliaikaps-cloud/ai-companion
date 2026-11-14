using System;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-104: the new Happi UI is the app's UI. The classic settings panel is no longer
    /// reachable.
    /// </summary>
    public static class NewUiSwitch
    {
        static bool _active;

        /// <summary>True while the new UI is in charge of the chat, the wheel and settings.</summary>
        public static bool Active
        {
            get { return _active; }
        }

        /// <summary>Raised when <see cref="Active"/> changes.</summary>
        public static event Action Changed;

        public static void Set(bool active)
        {
            if (_active == active)
                return;
            _active = active;
            var changed = Changed;
            if (changed != null)
                changed();
        }

        /// <summary>For tests: off, without raising <see cref="Changed"/>.</summary>
        public static void Reset()
        {
            _active = false;
        }
    }
}
