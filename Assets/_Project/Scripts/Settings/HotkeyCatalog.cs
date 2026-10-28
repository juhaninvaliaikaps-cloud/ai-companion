using UnityEngine;

namespace AICompanion.Settings
{
    public readonly struct HotkeyEntry
    {
        public readonly string Keys;
        public readonly string Action;

        public HotkeyEntry(string keys, string action)
        {
            Keys = keys ?? string.Empty;
            Action = action ?? string.Empty;
        }
    }

    public static class HotkeyCatalog
    {
        public const KeyCode SettingsKey = KeyCode.F12;
        public const string RebindNote = "Rebinding comes later. This list is the keys the app uses now.";

        public static HotkeyEntry[] Current(string pushToTalkKey)
        {
            var ptt = PushToTalkKeyName.Normalize(pushToTalkKey);
            return new[]
            {
                new HotkeyEntry("F6", "Mute speech"),
                new HotkeyEntry("F7", "Cycle avatar"),
                new HotkeyEntry("F8", "Toggle always on top"),
                new HotkeyEntry("F9", "Quick send"),
                new HotkeyEntry("F10", "Show or hide chat"),
                new HotkeyEntry("F12", "Show or hide settings"),
                new HotkeyEntry(ptt + " hold", "Push to talk"),
                new HotkeyEntry("1-5", "Emotion"),
                new HotkeyEntry("T", "Toggle talk"),
                new HotkeyEntry("Right-click", "Rotate mode, on a visible pixel"),
                new HotkeyEntry("R", "Reset rotation while rotate mode is on"),
                new HotkeyEntry("Esc", "Leave rotate mode and stop speech"),
                new HotkeyEntry("Scroll", "Zoom toward the cursor"),
                new HotkeyEntry("Ctrl + scroll", "Scale the avatar")
            };
        }
    }
}
