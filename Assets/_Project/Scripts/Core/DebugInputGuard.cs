using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// While a text field has focus, debug keys must not fire. Letters, including
    /// the push-to-talk key, and the function keys still report <c>GetKey</c>.
    /// </summary>
    public static class DebugInputGuard
    {
        static int _claims;
        static bool _legacy;

        public static bool TextFieldFocused => _legacy || _claims > 0;

        public static void SetTextFieldFocused(bool focused)
        {
            _legacy = focused;
        }

        public static void ClaimTextField()
        {
            _claims++;
        }

        public static void ReleaseTextField()
        {
            if (_claims > 0)
                _claims--;
        }

        public static bool IsBlocked(KeyCode key)
        {
            if (!TextFieldFocused)
                return false;

            if (key >= KeyCode.A && key <= KeyCode.Z)
                return true;

            switch (key)
            {
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                case KeyCode.Alpha5:
                case KeyCode.Keypad1:
                case KeyCode.Keypad2:
                case KeyCode.Keypad3:
                case KeyCode.Keypad4:
                case KeyCode.Keypad5:
                case KeyCode.F6:
                case KeyCode.F7:
                case KeyCode.F8:
                case KeyCode.F9:
                case KeyCode.F12:
                    return true;
                default:
                    return false;
            }
        }
    }
}
