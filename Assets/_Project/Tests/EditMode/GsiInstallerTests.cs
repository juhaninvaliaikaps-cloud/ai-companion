using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.GameCompanion;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-146: Happi finds CS2 and Dota 2 in the Steam libraries and writes its GSI file there.</summary>
    public sealed class GsiInstallerTests
    {
        const string Steam = @"C:\Program Files (x86)\Steam";
        const string Library = @"D:\SteamLibrary";
        const string Token = "abc123";

        sealed class FakeFiles : IGameFiles
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            public readonly HashSet<string> Dirs = new HashSet<string>();
            public int Writes;

            static string K(string path) { return SteamLibraries.Normal(path); }

            public void Dir(params string[] parts) { Dirs.Add(K(Join(parts))); }
            public void File(string text, params string[] parts) { Files[K(Join(parts))] = text; }
            public string Get(string path) { string t; return Files.TryGetValue(K(path), out t) ? t : null; }

            public bool FileExists(string path) { return Files.ContainsKey(K(path)); }
            public bool DirectoryExists(string path) { return Dirs.Contains(K(path)); }
            public string ReadAllText(string path) { return Files[K(path)]; }
            public void WriteAllText(string path, string text) { Writes++; Files[K(path)] = text; }
            public void CreateDirectory(string path) { Dirs.Add(K(path)); }
            public void DeleteFile(string path) { Files.Remove(K(path)); }
        }

        static string Join(params string[] parts)
        {
            var path = parts[0];
            for (var i = 1; i < parts.Length; i++)
                path = Path.Combine(path, parts[i]);
            return path;
        }

        const string LibraryVdf =
            "\"libraryfolders\"\n{\n" +
            "\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"228980\"\t\t\"1\"\n\t\t}\n\t}\n" +
            "\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"730\"\t\t\"1\"\n\t\t\t\"570\"\t\t\"1\"\n\t\t}\n\t}\n}\n";

        static string Manifest(int appId, string dir)
        {
            return "\"AppState\"\n{\n\t\"appid\"\t\t\"" + appId + "\"\n\t\"installdir\"\t\t\"" + dir + "\"\n}\n";
        }

        static FakeFiles SteamWithBothGames()
        {
            var fs = new FakeFiles();
            fs.Dir(Steam);
            fs.File(LibraryVdf, Steam, "steamapps", "libraryfolders.vdf");
            fs.File(Manifest(730, "Counter-Strike Global Offensive"), Library, "steamapps", "appmanifest_730.acf");
            fs.File(Manifest(570, "dota 2 beta"), Library, "steamapps", "appmanifest_570.acf");
            fs.Dir(Library, "steamapps", "common", "Counter-Strike Global Offensive");
            fs.Dir(Library, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
            fs.Dir(Library, "steamapps", "common", "dota 2 beta");
            fs.Dir(Library, "steamapps", "common", "dota 2 beta", "game", "dota", "cfg");
            return fs;
        }

        static List<string> Roots()
        {
            return SteamLibraries.RootCandidates(Steam);
        }

        [Test]
        public void TheLibraryList_ReadsTheCurrentAndTheOldFormat()
        {
            CollectionAssert.AreEqual(new[] { Steam, Library }, SteamLibraries.LibraryFolders(LibraryVdf));

            var old = "\"LibraryFolders\"\n{\n\t\"TimeNextStatsReport\"\t\t\"123\"\n\t\"1\"\t\t\"E:\\\\Games\\\\Steam\"\n}\n";
            CollectionAssert.AreEqual(new[] { @"E:\Games\Steam" }, SteamLibraries.LibraryFolders(old));

            CollectionAssert.IsEmpty(SteamLibraries.LibraryFolders(null));
            CollectionAssert.IsEmpty(SteamLibraries.LibraryFolders("garbage { \"unclosed"));
        }

        [Test]
        public void TheManifest_GivesTheFolder_AndRefusesOneThatLeavesTheLibrary()
        {
            Assert.AreEqual("dota 2 beta", SteamLibraries.InstallDir(Manifest(570, "dota 2 beta")));
            Assert.IsNull(SteamLibraries.InstallDir(Manifest(570, "..\\..\\Windows")));
            Assert.IsNull(SteamLibraries.InstallDir(Manifest(570, "C:\\Windows")));
            Assert.IsNull(SteamLibraries.InstallDir("// a comment only"));
        }

        [Test]
        public void TheRegistryPath_ComesFirst_ThenTheDefaults()
        {
            var roots = SteamLibraries.RootCandidates(@"E:\Steam");
            Assert.AreEqual(@"E:\Steam", roots[0]);
            CollectionAssert.Contains(roots, SteamLibraries.DefaultRoots[0]);
            Assert.AreEqual(SteamLibraries.DefaultRoots.Length, SteamLibraries.RootCandidates(null).Count);
            Assert.AreEqual(SteamLibraries.DefaultRoots.Length, SteamLibraries.RootCandidates("c:/program files (x86)/steam/").Count, "the same folder once");
        }

        [Test]
        public void SetUp_WritesTheCs2File_InTheGamesCfgFolder()
        {
            var fs = SteamWithBothGames();
            var result = GsiInstaller.Install(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, Token, fs);

            Assert.AreEqual(GsiInstallStatus.Written, result.Status, result.Message);
            var expected = Join(Library, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg", GsiConfig.FileName);
            Assert.AreEqual(SteamLibraries.Normal(expected), SteamLibraries.Normal(result.Path));
            Assert.AreEqual(GsiConfig.Cs2(GsiConfig.DefaultPort, Token), fs.Get(expected));
            StringAssert.StartsWith(GsiInstaller.Marker, fs.Get(expected));
            Assert.IsTrue(GsiInstaller.IsInstalled(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, Token, fs));
            Assert.IsFalse(GsiInstaller.IsInstalled(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, "other", fs), "a new token needs a new file");
        }

        [Test]
        public void SetUp_ForDota_CreatesTheGsiFolder_AndExplainsTheLaunchOption()
        {
            var fs = SteamWithBothGames();
            var result = GsiInstaller.Install(GameTitle.Dota2, Roots(), GsiConfig.DefaultPort, Token, fs);

            Assert.AreEqual(GsiInstallStatus.Written, result.Status, result.Message);
            Assert.IsTrue(fs.DirectoryExists(Join(Library, "steamapps", "common", "dota 2 beta", "game", "dota", "cfg", "gamestate_integration")));
            StringAssert.Contains(GsiInstaller.Dota2LaunchOption, result.Message);
        }

        [Test]
        public void SetUp_Twice_DoesNotRewrite()
        {
            var fs = SteamWithBothGames();
            GsiInstaller.Install(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, Token, fs);
            var again = GsiInstaller.Install(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, Token, fs);
            Assert.AreEqual(GsiInstallStatus.AlreadyThere, again.Status);
            Assert.AreEqual(1, fs.Writes);
        }

        [Test]
        public void AMissingGame_OrCfgFolder_WritesNothing()
        {
            var fs = new FakeFiles();
            fs.Dir(Steam);
            var missing = GsiInstaller.Install(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, Token, fs);
            Assert.AreEqual(GsiInstallStatus.GameNotFound, missing.Status);

            fs = SteamWithBothGames();
            fs.Dirs.Remove(SteamLibraries.Normal(Join(Library, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg")));
            var noCfg = GsiInstaller.Install(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, Token, fs);
            Assert.AreEqual(GsiInstallStatus.Failed, noCfg.Status);
            Assert.AreEqual(0, fs.Writes);

            Assert.AreEqual(GsiInstallStatus.NotSupported, GsiInstaller.Install(GameTitle.LeagueOfLegends, Roots(), GsiConfig.DefaultPort, Token, fs).Status, "League needs no file");
            Assert.AreEqual(GsiInstallStatus.Failed, GsiInstaller.Install(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, "", fs).Status);
        }

        [Test]
        public void Remove_DeletesOnlyOurOwnFile()
        {
            var fs = SteamWithBothGames();
            GsiInstaller.Install(GameTitle.CounterStrike2, Roots(), GsiConfig.DefaultPort, Token, fs);
            Assert.AreEqual(GsiInstallStatus.Removed, GsiInstaller.Remove(GameTitle.CounterStrike2, Roots(), fs).Status);
            Assert.AreEqual(GsiInstallStatus.NotInstalled, GsiInstaller.Remove(GameTitle.CounterStrike2, Roots(), fs).Status);

            var path = GsiInstaller.TargetPath(GameTitle.CounterStrike2, Roots(), fs);
            fs.Files[SteamLibraries.Normal(path)] = "\"Someone else\"\n{\n}\n";
            Assert.AreEqual(GsiInstallStatus.Failed, GsiInstaller.Remove(GameTitle.CounterStrike2, Roots(), fs).Status);
            Assert.IsTrue(fs.FileExists(path), "a file we didn't write stays");
        }

        [Test]
        public void TheGame_IsFoundInTheSteamFolderItself_Too()
        {
            var fs = new FakeFiles();
            fs.Dir(Steam);
            fs.File(Manifest(730, "Counter-Strike Global Offensive"), Steam, "steamapps", "appmanifest_730.acf");
            fs.Dir(Steam, "steamapps", "common", "Counter-Strike Global Offensive");
            var dir = SteamLibraries.FindGame(GameTitle.CounterStrike2, Roots(), fs);
            Assert.AreEqual(SteamLibraries.Normal(Join(Steam, "steamapps", "common", "Counter-Strike Global Offensive")), SteamLibraries.Normal(dir));
            Assert.IsNull(SteamLibraries.FindGame(GameTitle.Dota2, Roots(), fs));
        }
    }
}
