using System.IO;
using AICompanion.GameCompanion;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-161: Endfield is found, and she looks while her overlay sits on top of it.</summary>
    public sealed class EndfieldLookTests
    {
        [Test]
        public void Endfield_IsFoundByItsShippedTitle_OrItsProgram()
        {
            Assert.AreEqual(GameTitle.ArknightsEndfield, GachaGames.Match(string.Empty, "Endfield"), "the window is titled just Endfield");
            Assert.AreEqual(GameTitle.ArknightsEndfield, GachaGames.Match(string.Empty, "ENDFIELD"));
            Assert.AreEqual(GameTitle.ArknightsEndfield, GachaGames.Match(GachaGames.ExeName(@"D:\Games\GRYPHLINE\Endfield\Endfield.exe"), "anything"));
            Assert.AreEqual(GameTitle.Unknown, GachaGames.Match(string.Empty, "Endfield wiki - tier list"), "titles stay exact");
        }

        [Test]
        public void AFolderOrABrowserNamedEndfield_IsNotTheGame()
        {
            Assert.AreEqual(GameTitle.Unknown, GachaGames.Match("explorer", "Endfield"), "the install folder open in Explorer");
            Assert.AreEqual(GameTitle.Unknown, GachaGames.Match("chrome", "Genshin Impact"));
            Assert.AreEqual(GameTitle.Unknown, GachaGames.Match("msedge.exe", "Endfield"));
            Assert.AreEqual(GameTitle.GenshinImpact, GachaGames.Match("GenshinImpact", "Genshin Impact"), "the game itself still matches");
        }

        [Test]
        public void ExeName_TakesTheFileNameWithoutItsExtension()
        {
            Assert.AreEqual("Endfield", GachaGames.ExeName(@"C:\Program Files\Endfield\Endfield.exe"));
            Assert.AreEqual("StarRail", GachaGames.ExeName("D:/Games/StarRail.exe"));
            Assert.AreEqual("game", GachaGames.ExeName("game"));
            Assert.AreEqual(string.Empty, GachaGames.ExeName(null));
            Assert.AreEqual(string.Empty, GachaGames.ExeName("   "));
        }

        [Test]
        public void InFront_TheGame_OrHappiOnTopOfAFullScreenGame()
        {
            Assert.IsTrue(GachaGames.InFront(false, true, false, false), "the game has the focus");
            Assert.IsTrue(GachaGames.InFront(false, false, true, true), "her overlay has the focus over a full-screen game");
            Assert.IsFalse(GachaGames.InFront(false, false, true, false), "her overlay over a small game window: not a look");
            Assert.IsFalse(GachaGames.InFront(false, false, false, true), "a browser in front of a full-screen game: not a look");
            Assert.IsFalse(GachaGames.InFront(true, true, true, true), "minimized");
        }

        [Test]
        public void TheController_UsesTheLightQuery_AndTheNewRule()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/Settings/GameCompanionController.Gacha.cs"));
            StringAssert.Contains("GachaGames.InFront(_gachaWindow.Minimized, gameInFront, happiInFront, _gachaWindow.CoversMonitor)", source);
            StringAssert.Contains("const uint ProcessQueryLimitedInformation = 0x1000;", source);
            StringAssert.Contains("name = GachaGames.ExeName(ImagePath(pid));", source);
            StringAssert.DoesNotContain("PROCESS_VM_READ", source);
            StringAssert.DoesNotContain("ReadProcessMemory", source, "nothing reads the game's memory");
        }
    }
}
