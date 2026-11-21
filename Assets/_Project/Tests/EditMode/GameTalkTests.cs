using System.IO;
using AICompanion.Settings;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-162: talking to Happi while a game has the focus.</summary>
    public sealed class GameTalkTests
    {
        static string Source(string relative)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/" + relative));
        }

        [Test]
        public void MouseButtons_MapToTheSideButtonKeys()
        {
            Assert.AreEqual(TalkMouseButton.Mouse4, GameTalkButtons.Normalize(4));
            Assert.AreEqual(TalkMouseButton.Mouse5, GameTalkButtons.Normalize(5));
            Assert.AreEqual(TalkMouseButton.Off, GameTalkButtons.Normalize(7), "anything else is off");
            Assert.AreEqual(0x05, GameTalkButtons.VirtualKey(TalkMouseButton.Mouse4), "VK_XBUTTON1");
            Assert.AreEqual(0x06, GameTalkButtons.VirtualKey(TalkMouseButton.Mouse5), "VK_XBUTTON2");
            Assert.AreEqual(0, GameTalkButtons.VirtualKey(TalkMouseButton.Off));
        }

        [Test]
        public void TheXboxButton_IsTheGuideBit()
        {
            Assert.IsTrue(GameTalkButtons.XboxGuide(0x0400));
            Assert.IsTrue(GameTalkButtons.XboxGuide(0x1400), "with A held too");
            Assert.IsFalse(GameTalkButtons.XboxGuide(0x1000), "A alone");
        }

        [Test]
        public void TheDualSense_IsFoundByItsIds()
        {
            Assert.IsTrue(GameTalkButtons.IsDualSense(0x054C, 0x0CE6));
            Assert.IsTrue(GameTalkButtons.IsDualSense(0x054C, 0x0DF2), "DualSense Edge");
            Assert.IsFalse(GameTalkButtons.IsDualSense(0x054C, 0x09CC), "a DualShock 4 isn't asked for yet");
            Assert.IsFalse(GameTalkButtons.IsDualSense(0x045E, 0x0CE6));
            Assert.IsTrue(GameTalkButtons.IsBluetoothPath(@"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0ce6#9&1&0000#{4d1e55b2}"));
            Assert.IsFalse(GameTalkButtons.IsBluetoothPath(@"\\?\hid#vid_054c&pid_0ce6&mi_03#8&1&0000#{4d1e55b2}"));
        }

        [Test]
        public void ThePsButton_IsReadFromEachReportShape()
        {
            var usb = new byte[64];
            usb[0] = 0x01;
            Assert.IsFalse(GameTalkButtons.DualSensePs(usb, 64, false));
            usb[10] = 0x01;
            Assert.IsTrue(GameTalkButtons.DualSensePs(usb, 64, false), "USB report 0x01, byte 10 bit 0");
            usb[10] = 0x02;
            Assert.IsFalse(GameTalkButtons.DualSensePs(usb, 64, false), "the touchpad bit isn't PS");

            var full = new byte[78];
            full[0] = 0x31;
            full[11] = 0x01;
            Assert.IsTrue(GameTalkButtons.DualSensePs(full, 78, true), "Bluetooth report 0x31, byte 11");

            var simple = new byte[10];
            simple[0] = 0x01;
            simple[7] = 0x01;
            Assert.IsTrue(GameTalkButtons.DualSensePs(simple, 10, true), "Bluetooth short report 0x01, byte 7");
            Assert.IsFalse(GameTalkButtons.DualSensePs(simple, 10, false), "too short for the USB layout");

            Assert.IsFalse(GameTalkButtons.DualSensePs(new byte[] { 0x05, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, 12, false), "other reports");
            Assert.IsFalse(GameTalkButtons.DualSensePs(null, 0, false));
            Assert.IsFalse(GameTalkButtons.DualSensePs(usb, 100, false), "a length past the buffer");
        }

        [Test]
        public void AGamingButton_TalksEvenWhileATextFieldHasTheFocus()
        {
            var gate = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(false, false, true), "arms");
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(false, true, true), "the gaming button counts like the on-screen button");
            Assert.AreEqual(PushToTalkSignal.Released, gate.Tick(false, false, true));
        }

        [Test]
        public void TheSettings_DefaultToMouse4AndBothControllers_AndRoundTrip()
        {
            var settings = new CompanionSettings();
            Assert.AreEqual(4, settings.TalkMouseButton);
            Assert.IsTrue(settings.TalkXboxButton);
            Assert.IsTrue(settings.TalkPsButton);

            settings.TalkMouseButton = 5;
            settings.TalkXboxButton = false;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);
            Assert.AreEqual(5, back.TalkMouseButton);
            Assert.IsFalse(back.TalkXboxButton);
            Assert.IsTrue(back.TalkPsButton);

            back.TalkMouseButton = 9;
            back.Normalize();
            Assert.AreEqual(0, back.TalkMouseButton);
        }

        [Test]
        public void TheWiring_ReadsSharedAndOnlyWhilePushToTalkIsOn()
        {
            var listener = Source("Voice/SpeechListener.cs");
            StringAssert.Contains("_gate.Tick(keyHeld, _buttonHeld || _gameTalkHeld, DebugInputGuard.TextFieldFocused, listeningBlocked)", listener);
            StringAssert.Contains("_gameTalk.Dispose();", listener);

            var applicator = Source("Settings/SettingsApplicator.cs");
            StringAssert.Contains("listener.ConfigureGameTalk(", applicator);
            StringAssert.Contains("talk && settings.TalkPsButton", applicator);

            var input = Source("Voice/GameTalkInput.cs");
            StringAssert.Contains("CreateFile(path, GenericRead, FileShareRead | FileShareWrite,", input, "opened shared and read-only, so the game keeps its input");
            StringAssert.Contains("EntryPoint = \"#100\"", input, "XInputGetStateEx, which reports the Guide button");
            StringAssert.DoesNotContain("SetWindowsHookEx", input, "no keyboard hooks");
            StringAssert.DoesNotContain("RegisterHotKey", input, "the button isn't taken from the game");
        }
    }
}
