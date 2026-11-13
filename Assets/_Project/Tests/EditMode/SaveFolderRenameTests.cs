using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Overlay;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// The Happi rename: Unity's save folder follows the product name, so the newest earlier
    /// folder (LocalLow\AI Companion\Backseat Catgirl) is carried over once, without
    /// overwriting anything and without touching the old folder.
    /// </summary>
    public class SaveFolderRenameTests
    {
        string _root;
        string _company;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "happi-rename-" + Guid.NewGuid().ToString("N"));
            _company = Path.Combine(_root, "LocalLow", "AI Companion");
            Directory.CreateDirectory(_company);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        string Folder(string product)
        {
            return Path.Combine(_company, product);
        }

        static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        [Test]
        public void AppName_IsHappi_WithEveryEarlierNameNewestFirst()
        {
            Assert.AreEqual("Happi", AppName.Display);
            Assert.AreEqual("Happi", AppName.ExeName);
            Assert.AreEqual("AI Companion", AppName.Company);
            CollectionAssert.AreEqual(new[] { "Backseat Catgirl", "AI Companion" }, AppName.PreviousDisplays);
        }

        [Test]
        public void Sibling_AndPickSource_FindTheNewestEarlierFolder()
        {
            var happi = Folder("Happi");
            Assert.AreEqual(Folder("Backseat Catgirl"), SaveFolderRenamePlan.Sibling(happi, "Backseat Catgirl"));
            Assert.AreEqual(Folder("Backseat Catgirl"), SaveFolderRenamePlan.Sibling(happi + Path.DirectorySeparatorChar, "Backseat Catgirl"));
            Assert.IsNull(SaveFolderRenamePlan.Sibling(string.Empty, "Backseat Catgirl"));
            Assert.IsNull(SaveFolderRenamePlan.Sibling(happi, string.Empty));

            Assert.IsNull(SaveFolderRenamePlan.PickSource(happi, AppName.PreviousDisplays, Directory.Exists));
            Directory.CreateDirectory(Folder("AI Companion"));
            Assert.AreEqual(Folder("AI Companion"), SaveFolderRenamePlan.PickSource(happi, AppName.PreviousDisplays, Directory.Exists));
            Directory.CreateDirectory(Folder("Backseat Catgirl"));
            Assert.AreEqual(Folder("Backseat Catgirl"), SaveFolderRenamePlan.PickSource(happi, AppName.PreviousDisplays, Directory.Exists));

            // The current folder is never its own source.
            Assert.IsNull(SaveFolderRenamePlan.PickSource(happi, new[] { "Happi" }, _ => true));
        }

        [Test]
        public void Run_CopiesMissingFilesOnce_KeepsTheOldFolder_AndSkipsLogsAndCache()
        {
            var old = Folder("Backseat Catgirl");
            Write(Path.Combine(old, "ProjectLeia", "secrets.json"), "old-secret");
            Write(Path.Combine(old, "ProjectLeia", "portraits", "a.png"), "png");
            Write(Path.Combine(old, "notes.txt"), "old-notes");
            Write(Path.Combine(old, "Player.log"), "old-log");
            Write(Path.Combine(old, "Player-prev.log"), "older-log");
            Write(Path.Combine(old, "Unity", "cache.bin"), "cache");

            var happi = Folder("Happi");
            // Unity has already started the new log and the person has a newer file.
            Write(Path.Combine(happi, "Player.log"), "new-log");
            Write(Path.Combine(happi, "notes.txt"), "new-notes");

            var result = SaveFolderRenamePlan.Run(happi, AppName.PreviousDisplays, Directory.Exists);
            Assert.IsTrue(result.Ran);
            Assert.AreEqual(old, result.Source);
            Assert.AreEqual(2, result.Copied);
            Assert.AreEqual(0, result.Failed);
            StringAssert.Contains("carried 2 files from Backseat Catgirl to Happi", result.Describe(happi));

            Assert.AreEqual("old-secret", File.ReadAllText(Path.Combine(happi, "ProjectLeia", "secrets.json")));
            Assert.AreEqual("png", File.ReadAllText(Path.Combine(happi, "ProjectLeia", "portraits", "a.png")));
            Assert.AreEqual("new-notes", File.ReadAllText(Path.Combine(happi, "notes.txt")), "never overwrites");
            Assert.AreEqual("new-log", File.ReadAllText(Path.Combine(happi, "Player.log")));
            Assert.IsFalse(File.Exists(Path.Combine(happi, "Player-prev.log")));
            Assert.IsFalse(Directory.Exists(Path.Combine(happi, "Unity")));
            Assert.IsTrue(File.Exists(Path.Combine(happi, SaveFolderRenamePlan.MarkerFileName)));

            // The old folder is untouched.
            Assert.AreEqual("old-secret", File.ReadAllText(Path.Combine(old, "ProjectLeia", "secrets.json")));
            Assert.AreEqual("old-log", File.ReadAllText(Path.Combine(old, "Player.log")));
            Assert.AreEqual("old-notes", File.ReadAllText(Path.Combine(old, "notes.txt")));

            // Once only: a later start, or a file deleted on purpose, is not copied again.
            File.Delete(Path.Combine(happi, "ProjectLeia", "secrets.json"));
            var again = SaveFolderRenamePlan.Run(happi, AppName.PreviousDisplays, Directory.Exists);
            Assert.IsFalse(again.Ran);
            Assert.AreEqual(string.Empty, again.Describe(happi));
            Assert.IsFalse(File.Exists(Path.Combine(happi, "ProjectLeia", "secrets.json")));
        }

        [Test]
        public void Run_OnAFreshInstall_WritesTheMarkerAndCopiesNothing()
        {
            var happi = Folder("Happi");
            var result = SaveFolderRenamePlan.Run(happi, AppName.PreviousDisplays, Directory.Exists);
            Assert.IsTrue(result.Ran);
            Assert.IsNull(result.Source);
            Assert.AreEqual(0, result.Copied);
            StringAssert.Contains("nothing to carry over", result.Describe(happi));
            Assert.IsTrue(File.Exists(Path.Combine(happi, SaveFolderRenamePlan.MarkerFileName)));
            Assert.IsFalse(SaveFolderRenamePlan.Run(happi, AppName.PreviousDisplays, Directory.Exists).Ran);
            Assert.IsFalse(SaveFolderRenamePlan.Run(null, AppName.PreviousDisplays, Directory.Exists).Ran);
        }

        [Test]
        public void Run_PrefersBackseatCatgirlOverTheOldestName()
        {
            Write(Path.Combine(Folder("AI Companion"), "ProjectLeia", "secrets.json"), "oldest");
            Write(Path.Combine(Folder("Backseat Catgirl"), "ProjectLeia", "secrets.json"), "newest");
            var happi = Folder("Happi");
            var result = SaveFolderRenamePlan.Run(happi, AppName.PreviousDisplays, Directory.Exists);
            Assert.AreEqual(Folder("Backseat Catgirl"), result.Source);
            Assert.AreEqual("newest", File.ReadAllText(Path.Combine(happi, "ProjectLeia", "secrets.json")));
        }

        [Test]
        public void Run_SkipsAnEarlierFolderWithOnlyLogs_AndUsesTheOlderOne()
        {
            // Bugbot on #76: launching Backseat Catgirl leaves a folder with just its log and cache.
            Write(Path.Combine(Folder("Backseat Catgirl"), "Player.log"), "log");
            Write(Path.Combine(Folder("Backseat Catgirl"), "Unity", "cache.bin"), "cache");
            Write(Path.Combine(Folder("AI Companion"), "ProjectLeia", "secrets.json"), "oldest");

            Assert.IsFalse(SaveFolderRenamePlan.HasFilesToCopy(Folder("Backseat Catgirl"), Directory.Exists));
            Assert.IsTrue(SaveFolderRenamePlan.HasFilesToCopy(Folder("AI Companion"), Directory.Exists));
            Assert.IsFalse(SaveFolderRenamePlan.HasFilesToCopy(Folder("Missing"), Directory.Exists));

            var happi = Folder("Happi");
            var result = SaveFolderRenamePlan.Run(happi, AppName.PreviousDisplays, Directory.Exists);
            Assert.AreEqual(Folder("AI Companion"), result.Source);
            Assert.AreEqual(1, result.Copied);
            Assert.AreEqual("oldest", File.ReadAllText(Path.Combine(happi, "ProjectLeia", "secrets.json")));
        }

        [Test]
        public void Run_WithOnlyLogsAnywhere_CopiesNothing()
        {
            Write(Path.Combine(Folder("Backseat Catgirl"), "Player.log"), "log");
            Write(Path.Combine(Folder("AI Companion"), "Player-prev.log"), "log");
            var happi = Folder("Happi");
            var result = SaveFolderRenamePlan.Run(happi, AppName.PreviousDisplays, Directory.Exists);
            Assert.IsNull(result.Source);
            Assert.AreEqual(0, result.Copied);
            Assert.IsFalse(File.Exists(Path.Combine(happi, "Player.log")));
        }

        [TestCase("Player.log", true)]
        [TestCase("player-prev.log", true)]
        [TestCase("renamed-from.txt", true)]
        [TestCase("Unity/x", true)]
        [TestCase("Crashes/dump.dmp", true)]
        [TestCase("ProjectLeia/Player.log", false)]
        [TestCase("ProjectLeia/secrets.json", false)]
        [TestCase("notes.txt", false)]
        [TestCase("", true)]
        public void ShouldSkip_OnlyTheLogsMarkerAndUnityCache(string relative, bool skip)
        {
            Assert.AreEqual(skip, SaveFolderRenamePlan.ShouldSkip(relative));
        }
    }
}
