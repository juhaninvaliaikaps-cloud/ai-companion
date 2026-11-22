using AICompanion.Voice;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-181: the Persona Studio voice drop-downs, one per engine.</summary>
    public sealed class VoiceChoicesTests
    {
        [TestCase("chatterbox", "Chatterbox")]
        [TestCase("Kokoro", "Kokoro")]
        [TestCase("piper", "Piper")]
        [TestCase("xai", "Piper")]
        [TestCase(null, "Piper")]
        public void LocalEngine_FollowsTheVoiceTab_AndFallsBackToPiper(string provider, string expected)
        {
            Assert.AreEqual(expected, VoiceChoices.LocalEngine(provider));
        }

        [Test]
        public void LocalOrder_PutsTheEngineInUseFirst_AndListsAllThree()
        {
            CollectionAssert.AreEqual(new[] { "Chatterbox", "Piper", "Kokoro" }, VoiceChoices.LocalOrder("chatterbox"));
            CollectionAssert.AreEqual(new[] { "Kokoro", "Piper", "Chatterbox" }, VoiceChoices.LocalOrder("kokoro"));
            CollectionAssert.AreEqual(new[] { "Piper", "Kokoro", "Chatterbox" }, VoiceChoices.LocalOrder("xai"));
        }

        [Test]
        public void PiperIds_AreFileNamesWithoutTheExtension_SortedAndUnique()
        {
            var ids = VoiceChoices.PiperIds(new[] { System.IO.Path.Combine("piper", "en_US-lessac-medium.onnx"), "en_GB-alba-medium.onnx", System.IO.Path.Combine("piper", "EN_US-lessac-medium.onnx"), "", null }, true, null);
            CollectionAssert.AreEqual(new[] { "", "en_GB-alba-medium", "en_US-lessac-medium" }, ids);
        }

        [Test]
        public void ASavedIdNotInTheList_IsKept()
        {
            var piper = VoiceChoices.PiperIds(new string[0], true, "fi_FI-harri-medium");
            CollectionAssert.AreEqual(new[] { "", "fi_FI-harri-medium" }, piper);

            var cloud = VoiceChoices.CloudIds(true, "my_custom_voice");
            Assert.AreEqual("", cloud[0], "the first entry keeps the Voice tab's voice");
            CollectionAssert.Contains(cloud, "my_custom_voice");
            Assert.AreEqual(VoiceChoices.Cloud.Length + 2, cloud.Count);

            var kokoro = VoiceChoices.KokoroIds(false, "AF_HEART");
            Assert.AreEqual(VoiceChoices.Kokoro.Length, kokoro.Count, "a saved id already in the list is not added twice");
        }

        [Test]
        public void Cloud_HasTheFiveXaiVoices_WithEveFirst()
        {
            CollectionAssert.AreEqual(new[] { "eve", "ara", "rex", "sal", "leo" }, VoiceChoices.Cloud);
        }

        [Test]
        public void PiperFolder_IsNextToTheVoice_ElseNextToPiperExe()
        {
            StringAssert.EndsWith("voices", VoiceChoices.PiperFolder(System.IO.Path.Combine("C", "voices", "a.onnx"), System.IO.Path.Combine("C", "bin", "piper.exe")));
            StringAssert.EndsWith("bin", VoiceChoices.PiperFolder("", System.IO.Path.Combine("C", "bin", "piper.exe")));
            Assert.AreEqual(string.Empty, VoiceChoices.PiperFolder(null, " "));
        }
    }
}
