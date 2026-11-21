using System.IO;
using System.Text;
using AICompanion.Overlay;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-164: Happi stays above the game in front, even with "Always on top" off.</summary>
    public sealed class GameAboveTests
    {
        static string Source(string relative)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/" + relative));
        }

        [Test]
        public void SheIsOnTop_WithAlwaysOnTop_OrAGameInFront()
        {
            Assert.IsTrue(GameAbove.Want(true, false, false), "Always on top");
            Assert.IsTrue(GameAbove.Want(false, true, true), "Always on top off, a game in front");
            Assert.IsFalse(GameAbove.Want(false, true, false), "no game in front");
            Assert.IsFalse(GameAbove.Want(false, false, true), "Stay above games off");
        }

        [Test]
        public void AGameIs_AnotherProgramFillingItsMonitor_OrADetectedGame()
        {
            Assert.IsTrue(GameAbove.IsGameWindow(false, false, true, false), "borderless or windowed fullscreen");
            Assert.IsTrue(GameAbove.IsGameWindow(false, false, false, true), "a windowed game she's watching");
            Assert.IsFalse(GameAbove.IsGameWindow(false, false, false, false), "a normal window");
            Assert.IsFalse(GameAbove.IsGameWindow(true, false, true, false), "Happi herself");
            Assert.IsFalse(GameAbove.IsGameWindow(false, true, true, false), "the desktop fills the monitor too");
        }

        [Test]
        public void TheShell_IsNotAGame_InBothChecks()
        {
            foreach (var name in new[] { "Progman", "WorkerW", "Shell_TrayWnd", "XamlExplorerHostIslandWindow" })
            {
                Assert.IsTrue(GameAbove.IsShellClass(name), name);
                Assert.IsTrue(GameAbove.IsShellClass(new StringBuilder(name)), name);
            }

            Assert.IsFalse(GameAbove.IsShellClass("UnityWndClass"), "a Unity game");
            Assert.IsFalse(GameAbove.IsShellClass(new StringBuilder("UnrealWindow")), "an Unreal game");
            Assert.IsFalse(GameAbove.IsShellClass(new StringBuilder("progman")), "class names are exact");
            Assert.IsFalse(GameAbove.IsShellClass((string)null));
        }

        [Test]
        public void Covers_ChecksTheWholeMonitor()
        {
            Assert.IsTrue(GameAbove.Covers(0, 0, 1920, 1080, 0, 0, 1920, 1080), "exactly the monitor");
            Assert.IsTrue(GameAbove.Covers(-8, -8, 1928, 1088, 0, 0, 1920, 1080), "a little past its edges");
            Assert.IsTrue(GameAbove.Covers(1920, 0, 4480, 1440, 1920, 0, 4480, 1440), "the second monitor");
            Assert.IsFalse(GameAbove.Covers(100, 100, 1700, 1000, 0, 0, 1920, 1080), "a windowed game");
            Assert.IsFalse(GameAbove.Covers(0, 0, 1920, 1040, 0, 0, 1920, 1080), "a maximized window above the taskbar");
            Assert.IsFalse(GameAbove.Covers(0, 0, 10, 10, 0, 0, 0, 0), "no monitor");
        }

        [Test]
        public void TheSetting_DefaultsOn_AndRoundTrips()
        {
            var settings = new CompanionSettings();
            Assert.IsTrue(settings.StayAboveGames);
            settings.StayAboveGames = false;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);
            Assert.IsFalse(back.StayAboveGames);
            Assert.IsTrue(CompanionSettingsJson.Parse("{\n  \"version\": 1\n}\n", SettingsLegacyPrefs.None).StayAboveGames, "older files get it on");
        }

        [Test]
        public void EveryMove_UsesTheSameOnTopAnswer()
        {
            var window = Source("Overlay/OverlayWindow.cs");
            StringAssert.DoesNotContain("_chrome.SetTopmost(_topmost)", window, "a plain Always-on-top would drop her off a game");
            StringAssert.DoesNotContain(", _topmost);", window);
            StringAssert.Contains("var wantTopmost = EffectiveTopmost;", window);
            StringAssert.Contains("(frontChanged || Time.unscaledTime >= _nextTopmostAssert)", window, "back on top at once when a game takes the front");
            StringAssert.Contains("_window.EffectiveTopmost", Source("Overlay/DesktopPresence.cs"), "walking keeps her on top");
            StringAssert.Contains("window.SetStayAboveGames(settings.StayAboveGames);", Source("Settings/SettingsApplicator.cs"));
            StringAssert.Contains("GameWindowNow.Set(found.Hwnd);", Source("Settings/GameCompanionController.Gacha.cs"));
        }
    }
}
