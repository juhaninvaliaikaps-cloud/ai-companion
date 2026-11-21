using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-165: the Whisper model is found in the build or in her data folder, and can be downloaded there.</summary>
    public sealed class WhisperModelTests
    {
        string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "happi-whisper-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        string MakeModel(string folder, long bytes)
        {
            var dir = Path.Combine(_root, folder, WhisperModelFiles.FolderName);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "ggml-base.bin");
            using (var stream = File.Create(path))
                stream.SetLength(bytes);
            return path;
        }

        [Test]
        public void Find_PrefersTheBuild_ThenTheDataFolder()
        {
            var build = Path.Combine(_root, "build");
            var data = Path.Combine(_root, "data");
            Assert.IsNull(WhisperModelFiles.Find(build, data, "ggml-base.bin"), "nowhere yet");

            var dataModel = MakeModel("data", 2000000);
            Assert.AreEqual(dataModel, WhisperModelFiles.Find(build, data, "ggml-base.bin"), "a worktree build without the model finds the data-folder copy");

            var buildModel = MakeModel("build", 2000000);
            Assert.AreEqual(buildModel, WhisperModelFiles.Find(build, data, "ggml-base.bin"), "the build's own copy comes first");
        }

        [Test]
        public void Find_IgnoresAStubOrAnEmptyFile()
        {
            MakeModel("build", 1000);
            Assert.IsNull(WhisperModelFiles.Find(Path.Combine(_root, "build"), null, "ggml-base.bin"));
        }

        [Test]
        public void OnlyWhisperCppNames_CanBeDownloaded()
        {
            Assert.IsTrue(WhisperModelFiles.Downloadable("ggml-base.bin"));
            Assert.IsTrue(WhisperModelFiles.Downloadable("ggml-small-q5_1.bin"));
            Assert.IsTrue(WhisperModelFiles.Downloadable("ggml-large-v3-turbo-q5_0.bin"));
            Assert.IsFalse(WhisperModelFiles.Downloadable("evil.bin"));
            Assert.IsFalse(WhisperModelFiles.Downloadable("ggml-base.exe"));
            Assert.IsFalse(WhisperModelFiles.Downloadable("ggml-../x.bin"));
            Assert.IsFalse(WhisperModelFiles.Downloadable("ggml-Base.bin"), "whisper.cpp names are lower case");
            Assert.IsFalse(WhisperModelFiles.Downloadable(null));
            Assert.AreEqual("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin", WhisperModelFiles.DownloadUrl("ggml-base.bin"));
            Assert.IsNull(WhisperModelFiles.DownloadUrl("evil.bin"));
        }

        [Test]
        public void ADownload_MustLookLikeAModel()
        {
            Assert.IsTrue(WhisperModelFiles.LooksLikeModel(new byte[] { 0x6C, 0x6D, 0x67, 0x67, 1, 2 }), "ggml magic 0x67676d6c, little-endian");
            Assert.IsFalse(WhisperModelFiles.LooksLikeModel(new byte[] { (byte)'<', (byte)'h', (byte)'t', (byte)'m' }), "an HTML error page");
            Assert.IsFalse(WhisperModelFiles.LooksLikeModel(new byte[] { 0x6C, 0x6D }));
            Assert.IsFalse(WhisperModelFiles.LooksLikeModel(null));
        }

        [Test]
        public void TheModel_IsLeftOutOfDataExports()
        {
            Assert.IsTrue(LocalDataInventory.IsExcluded("Whisper/ggml-base.bin"));
            Assert.IsTrue(LocalDataInventory.IsExcluded("whisper\\ggml-small-q5_1.bin"));
            Assert.IsFalse(LocalDataInventory.IsExcluded("whisper-notes.json"));
        }

        [Test]
        public void TheMissingMessage_SaysWhereToGetIt()
        {
            StringAssert.Contains("Settings > Voice", WhisperSttProvider.MissingModelMessage("ggml-base.bin"));
        }

        [Test]
        public void TheListener_LooksInBothPlaces()
        {
            var root = Path.Combine(Application.dataPath, "_Project/Scripts/");
            var listener = File.ReadAllText(root + "Voice/SpeechListener.cs");
            StringAssert.DoesNotContain("Path.Combine(Application.streamingAssetsPath, \"Whisper\"", listener, "the build-only path is gone");
            Assert.AreEqual(3, CountOf(listener, "WhisperModelFiles.Find(Application.streamingAssetsPath, _dataFolder"), "recording, transcribing and the settings status");
            StringAssert.Contains("listener.SetDataFolder(", File.ReadAllText(root + "Settings/SettingsApplicator.cs"));
        }

        static int CountOf(string text, string part)
        {
            var count = 0;
            for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
                count++;
            return count;
        }
    }
}
