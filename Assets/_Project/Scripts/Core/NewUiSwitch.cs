using System;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-104: whether the new Happi UI is on. The setting "New UI (preview)" turns it on
    /// and keeps it on; the <c>-newui</c> launch argument turns it on for that launch only.
    /// Off by default. While it is off, nothing reads the new views and the app behaves as
    /// on main.
    /// </summary>
    public static class NewUiSwitch
    {
        public const string Argument = "-newui";

        static bool _active;

        /// <summary>True while the new UI is in charge of the chat, the wheel and settings.</summary>
        public static bool Active
        {
            get { return _active; }
        }

        /// <summary>Raised when <see cref="Active"/> changes.</summary>
        public static event Action Changed;

        public static bool ArgumentPresent(string[] args)
        {
            if (args == null)
                return false;
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.IsNullOrEmpty(arg))
                    continue;
                var trimmed = arg.Trim();
                if (string.Equals(trimmed, Argument, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "-" + Argument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>On when the setting is on, or when this launch has <c>-newui</c>.</summary>
        public static bool Resolve(bool setting, string[] args)
        {
            return setting || ArgumentPresent(args);
        }

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
