using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class SpeechToTextTests
    {
        [Test]
        public void Resample_MixesStereoAndKeeps16kMono()
        {
            var stereo = new[] { 1f, 0f, 0f, 1f };
            var mono = PcmResample.ToMono16k(stereo, 2, PcmResample.WhisperRate);

            Assert.AreEqual(2, mono.Length);
            Assert.AreEqual(0.5f, mono[0], 0.0001f);
            Assert.AreEqual(0.5f, mono[1], 0.0001f);
        }

        [Test]
        public void Resample_Halves32kTo16k()
        {
            var source = new[] { 0f, 0f, 1f, 1f };
            var output = PcmResample.ToMono16k(source, 1, 32000);

            Assert.AreEqual(2, output.Length);
            Assert.AreEqual(0f, output[0], 0.0001f);
            Assert.AreEqual(1f, output[1], 0.0001f);
        }

        [Test]
        public void Resample_Averages48kStereoDownTo16k()
        {
            const int frames = 48;
            var source = new float[frames * 2];
            for (var i = 0; i < frames; i++)
            {
                source[i * 2] = 0.25f;
                source[i * 2 + 1] = 0.75f;
            }

            var output = PcmResample.ToMono16k(source, 2, 48000);

            Assert.AreEqual(16, output.Length);
            for (var i = 0; i < output.Length; i++)
                Assert.AreEqual(0.5f, output[i], 0.0001f);
        }

        [Test]
        public void Resample_RejectsEmptyAndBadShapes()
        {
            Assert.AreEqual(0, PcmResample.ToMono16k(null, 1, 16000).Length);
            Assert.AreEqual(0, PcmResample.ToMono16k(new float[0], 1, 16000).Length);
            Assert.AreEqual(0, PcmResample.ToMono16k(new[] { 1f }, 0, 16000).Length);
            Assert.AreEqual(0, PcmResample.ToMono16k(new[] { 1f }, 1, 0).Length);
        }

        [Test]
        public void PushToTalk_HoldStartsAndReleaseTranscribes()
        {
            var gate = new PushToTalkGate();

            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(true, false, false));
            Assert.AreEqual(PushToTalkPhase.Recording, gate.Phase);
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(true, false, false));
            Assert.AreEqual(PushToTalkSignal.Released, gate.Tick(false, false, false));
            Assert.AreEqual(PushToTalkPhase.Transcribing, gate.Phase);
        }

        [Test]
        public void PushToTalk_IgnoresAHoldWhileTranscribingUntilRelease()
        {
            var gate = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(true, false, false));
            Assert.AreEqual(PushToTalkSignal.Released, gate.Tick(false, false, false));

            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(true, true, false));
            gate.FinishTranscription(true);
            Assert.AreEqual(PushToTalkPhase.Idle, gate.Phase);
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(true, true, false));
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(false, false, false));
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(true, false, false));
        }

        [Test]
        public void PushToTalk_BlockedKeyDoesNotRecordButTheButtonDoes()
        {
            var blocked = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.None, blocked.Tick(true, false, true));
            Assert.AreEqual(PushToTalkPhase.Idle, blocked.Phase);

            var button = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.Started, button.Tick(false, true, true));
            Assert.AreEqual(PushToTalkPhase.Recording, button.Phase);
            Assert.AreEqual(PushToTalkSignal.Released, button.Tick(true, false, true));
            Assert.AreEqual(PushToTalkPhase.Transcribing, button.Phase);
        }

        [Test]
        public void PushToTalk_BlockingTheKeyStopsAKeyHold()
        {
            var gate = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(true, false, false));
            Assert.AreEqual(PushToTalkSignal.Released, gate.Tick(true, false, true));
            Assert.AreEqual(PushToTalkPhase.Transcribing, gate.Phase);
        }

        [Test]
        public void PushToTalk_AbortWaitsForReleaseBeforeTheNextHold()
        {
            var gate = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(true, false, false));
            gate.Abort();
            Assert.AreEqual(PushToTalkPhase.Idle, gate.Phase);
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(true, false, false));
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(false, false, false));
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(false, true, false));
        }

        [Test]
        public void PushToTalk_ForceReleaseEndsAFullBuffer()
        {
            var gate = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(true, false, false));
            Assert.AreEqual(PushToTalkSignal.Released, gate.ForceRelease());
            Assert.AreEqual(PushToTalkPhase.Transcribing, gate.Phase);
            Assert.AreEqual(PushToTalkSignal.None, gate.ForceRelease());
        }

        [Test]
        public void Transcript_DropsBlankTokensAndPunctuation()
        {
            string text;
            Assert.IsFalse(TranscriptText.TryAccept(null, out text));
            Assert.AreEqual(string.Empty, text);
            Assert.IsFalse(TranscriptText.TryAccept("  [BLANK_AUDIO] ", out text));
            Assert.IsFalse(TranscriptText.TryAccept("...", out text));
            Assert.IsFalse(TranscriptText.TryAccept("(music)", out text));

            Assert.IsTrue(TranscriptText.TryAccept("  Hei!  ", out text));
            Assert.AreEqual("Hei!", text);
            Assert.IsTrue(TranscriptText.TryAccept("Hello [BLANK_AUDIO]", out text));
            Assert.AreEqual("Hello", text);
            Assert.IsTrue(TranscriptText.TryAccept("(music) Moi", out text));
            Assert.AreEqual("Moi", text);
        }

        [Test]
        public void MicChoice_UsesTheDefaultAndFallsBack()
        {
            var none = MicChoice.Resolve("Any", null);
            Assert.IsFalse(none.Available);

            var emptyList = MicChoice.Resolve("", new string[0]);
            Assert.IsFalse(emptyList.Available);

            var preferred = MicChoice.Resolve("", new[] { "Built-in", "USB" });
            Assert.IsTrue(preferred.Available);
            Assert.IsFalse(preferred.FellBack);
            Assert.IsNull(preferred.Device);

            var named = MicChoice.Resolve("usb", new[] { "Built-in", "USB" });
            Assert.IsFalse(named.FellBack);
            Assert.AreEqual("USB", named.Device);

            var missing = MicChoice.Resolve("Headset", new[] { "Built-in" });
            Assert.IsTrue(missing.Available);
            Assert.IsTrue(missing.FellBack);
            Assert.IsNull(missing.Device);
        }

        [Test]
        public void MicRate_StaysInsideDeviceCaps()
        {
            Assert.AreEqual(16000, MicRate.Choose(0, 0));
            Assert.AreEqual(16000, MicRate.Choose(8000, 48000));
            Assert.AreEqual(44100, MicRate.Choose(44100, 48000));
            Assert.AreEqual(8000, MicRate.Choose(8000, 8000));
            Assert.AreEqual(48000, MicRate.Choose(48000, 48000));
        }

        [Test]
        public void Settings_DefaultToBaseModelAutoLanguageAndV()
        {
            var settings = ScriptableObject.CreateInstance<SttSettings>();
            try
            {
                Assert.IsTrue(settings.enabled);
                Assert.AreEqual(KeyCode.V, settings.PushToTalkKey);
                Assert.AreEqual("auto", settings.LanguageCode);
                Assert.AreEqual("ggml-base.bin", settings.ModelFileName);
                Assert.AreEqual(3200, SttSettings.MinSamples16k);

                settings.modelFile = "C:/models/ggml-small.bin";
                Assert.AreEqual("ggml-small.bin", settings.ModelFileName);
                settings.modelFile = "../secrets.txt";
                Assert.AreEqual("ggml-base.bin", settings.ModelFileName);
                settings.language = " FI ";
                Assert.AreEqual("fi", settings.LanguageCode);
                settings.pushToTalkKey = KeyCode.F7;
                Assert.AreEqual(KeyCode.V, settings.PushToTalkKey);
                settings.pushToTalkKey = KeyCode.G;
                Assert.AreEqual(KeyCode.G, settings.PushToTalkKey);
                DebugInputGuard.SetTextFieldFocused(false);
                Assert.IsFalse(DebugInputGuard.IsBlocked(KeyCode.G));
                DebugInputGuard.SetTextFieldFocused(true);
                Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.G));
                DebugInputGuard.SetTextFieldFocused(false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void NativeLoad_ListsEditorAndPlayerPluginFolders()
        {
            var assets = Path.Combine("/proj", "Assets");
            var editor = WhisperNativeLoad.CandidateDirectories(assets);
            CollectionAssert.Contains(editor, Path.Combine("/proj", "Packages", "com.whisper.unity", "Plugins", "Windows"));
            CollectionAssert.Contains(editor, Path.Combine(assets, "Plugins", "x86_64"));

            var data = Path.Combine("/proj", "AICompanion_Data");
            var player = WhisperNativeLoad.CandidateDirectories(data);
            CollectionAssert.Contains(player, Path.Combine(data, "Plugins", "x86_64"));
            Assert.AreEqual(0, WhisperNativeLoad.CandidateDirectories(null).Length);
        }

        [Test]
        public void NativeLoad_FindsLibwhisperInThePluginFolder()
        {
            var root = Path.Combine(Path.GetTempPath(), "whisper-load-" + Guid.NewGuid().ToString("N"));
            var assets = Path.Combine(root, "Assets");
            var plugins = Path.Combine(root, "Packages", "com.whisper.unity", "Plugins", "Windows");
            Directory.CreateDirectory(plugins);
            File.WriteAllText(Path.Combine(plugins, WhisperNativeLoad.LibraryFile), "not a real library");
            try
            {
                Assert.AreEqual(Path.GetFullPath(plugins), WhisperNativeLoad.FindDirectory(assets));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void NativeLoad_FindsLibwhisperNextToThePlayer()
        {
            var root = Path.Combine(Path.GetTempPath(), "whisper-player-" + Guid.NewGuid().ToString("N"));
            var data = Path.Combine(root, "AICompanion_Data");
            var plugins = Path.Combine(data, "Plugins", "x86_64");
            Directory.CreateDirectory(plugins);
            File.WriteAllText(Path.Combine(plugins, WhisperNativeLoad.LibraryFile), "not a real library");
            try
            {
                Assert.AreEqual(Path.GetFullPath(plugins), WhisperNativeLoad.FindDirectory(data));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void NativeLoad_FailureLineNamesTheTypeAndPluginPath()
        {
            var path = "C:/Game/Packages/com.whisper.unity/Plugins/Windows";
            var line = WhisperNativeLoad.FailureLine(
                "ggml-base.bin",
                "DllNotFoundException",
                "libwhisper assembly:<unknown assembly> type:<unknown type> member:(null)",
                path,
                null);

            StringAssert.Contains("STT: transcribe failed DllNotFoundException ggml-base.bin", line);
            StringAssert.Contains("libwhisper assembly:<unknown assembly> type:<unknown type> member:(null)", line);
            StringAssert.Contains("path=" + path, line);
            Assert.IsFalse(line.Contains("load="));

            var same = WhisperNativeLoad.FailureLine("ggml-base.bin", "DllNotFoundException", "missing", path, path);
            Assert.IsFalse(same.Contains("load="));

            var noisy = WhisperNativeLoad.FailureLine(
                "ggml-base.bin",
                "DllNotFoundException",
                "missing",
                path,
                new string('x', 600));
            Assert.LessOrEqual(noisy.Length, 500);
            StringAssert.Contains("DllNotFoundException", noisy);
            StringAssert.Contains("path=" + path, noisy);
            StringAssert.Contains("load=", noisy);
        }
    }
}
