using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.Voice;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class PiperTtsProviderTests
    {
        sealed class FakeRunner : IPiperRunner
        {
            public int Calls;
            public PiperRun Result = new PiperRun(true, new byte[] { 1, 2, 3 }, string.Empty);

            public Task<PiperRun> Run(string exePath, string voicePath, string text, float timeoutSeconds, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(Result);
            }
        }

        static TtsRequest Request()
        {
            return new TtsRequest { Text = "Hei." };
        }

        static string TempFile()
        {
            var path = Path.GetTempFileName();
            return path;
        }

        [Test]
        public void MissingExecutableFailsWithoutRunning()
        {
            var runner = new FakeRunner();
            var provider = new PiperTtsProvider("C:/does/not/exist/piper.exe", "C:/does/not/exist/voice.onnx", runner, 10f);

            var result = provider.Synthesize(Request(), CancellationToken.None).Result;

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(PiperTtsProvider.MissingExeMessage, result.UserMessage);
            Assert.AreEqual(0, runner.Calls);
        }

        [Test]
        public void MissingVoiceModelFailsWithoutRunning()
        {
            var exe = TempFile();
            try
            {
                var runner = new FakeRunner();
                var provider = new PiperTtsProvider(exe, "C:/does/not/exist/voice.onnx", runner, 10f);

                var result = provider.Synthesize(Request(), CancellationToken.None).Result;

                Assert.IsFalse(result.Ok);
                Assert.AreEqual(PiperTtsProvider.MissingVoiceMessage, result.UserMessage);
                Assert.AreEqual(0, runner.Calls);
            }
            finally
            {
                File.Delete(exe);
            }
        }

        [Test]
        public void AWavFromTheRunnerBecomesAudio()
        {
            var exe = TempFile();
            var voice = TempFile();
            try
            {
                var runner = new FakeRunner();
                var provider = new PiperTtsProvider(exe, voice, runner, 10f);

                var result = provider.Synthesize(Request(), CancellationToken.None).Result;

                Assert.IsTrue(result.Ok);
                Assert.AreEqual(3, result.Audio.Length);
                Assert.AreEqual(1, runner.Calls);
            }
            finally
            {
                File.Delete(exe);
                File.Delete(voice);
            }
        }

        [Test]
        public void ARunnerFailureIsReported()
        {
            var exe = TempFile();
            var voice = TempFile();
            try
            {
                var runner = new FakeRunner { Result = PiperRun.Fail("piper exit 1") };
                var provider = new PiperTtsProvider(exe, voice, runner, 10f);

                var result = provider.Synthesize(Request(), CancellationToken.None).Result;

                Assert.IsFalse(result.Ok);
                Assert.IsTrue(result.LogMessage.Contains("piper exit 1"));
            }
            finally
            {
                File.Delete(exe);
                File.Delete(voice);
            }
        }

        [Test]
        public void ACancelledTokenReturnsCancelled()
        {
            var exe = TempFile();
            var voice = TempFile();
            try
            {
                var runner = new FakeRunner();
                var provider = new PiperTtsProvider(exe, voice, runner, 10f);
                var cancel = new CancellationTokenSource();
                cancel.Cancel();

                var result = provider.Synthesize(Request(), cancel.Token).Result;

                Assert.IsFalse(result.Ok);
                Assert.IsTrue(result.Quiet);
            }
            finally
            {
                File.Delete(exe);
                File.Delete(voice);
            }
        }

        [Test]
        public void TheProviderIdIsPiper()
        {
            var provider = new PiperTtsProvider(string.Empty, string.Empty, new FakeRunner(), 10f);
            Assert.AreEqual("piper", provider.ProviderId);
        }
    }
}
