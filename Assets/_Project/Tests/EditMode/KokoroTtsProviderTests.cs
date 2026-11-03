using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.Voice;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class KokoroTtsProviderTests
    {
        sealed class FakeSession : IKokoroSession
        {
            public int Calls;
            public byte[] Reply = new byte[] { 1, 2, 3, 4 };
            public Exception Throws;

            public Task<byte[]> Synthesize(string text, float speed, CancellationToken cancellationToken)
            {
                Calls++;
                if (Throws != null)
                    return Task.FromException<byte[]>(Throws);
                return Task.FromResult(Reply);
            }

            public int Prewarms;

            public void Prewarm()
            {
                Prewarms++;
            }

            public void Dispose()
            {
            }
        }

        static TtsRequest Request()
        {
            return new TtsRequest { Text = "Hei." };
        }

        static string TempFile()
        {
            return Path.GetTempFileName();
        }

        static KokoroTtsProvider Provider(FakeSession session, string python, string script, string model, string voices)
        {
            return new KokoroTtsProvider(python, script, model, voices, "af_heart", session, 10f);
        }

        [Test]
        public void AMissingPythonFailsWithoutCallingTheSession()
        {
            var session = new FakeSession();
            var provider = Provider(session, "C:/nope/python.exe", "C:/nope/kokoro_server.py", "C:/nope/m.onnx", "C:/nope/v.bin");

            var result = provider.Synthesize(Request(), CancellationToken.None).Result;

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(KokoroTtsProvider.MissingPythonMessage, result.UserMessage);
            Assert.AreEqual(0, session.Calls);
        }

        [Test]
        public void AMissingModelFailsWithoutCallingTheSession()
        {
            var python = TempFile();
            var script = TempFile();
            try
            {
                var session = new FakeSession();
                var provider = Provider(session, python, script, "C:/nope/m.onnx", "C:/nope/v.bin");

                var result = provider.Synthesize(Request(), CancellationToken.None).Result;

                Assert.IsFalse(result.Ok);
                Assert.AreEqual(KokoroTtsProvider.MissingModelMessage, result.UserMessage);
                Assert.AreEqual(0, session.Calls);
            }
            finally
            {
                File.Delete(python);
                File.Delete(script);
            }
        }

        [Test]
        public void AWavFromTheSessionBecomesAudio()
        {
            var python = TempFile();
            var script = TempFile();
            var model = TempFile();
            var voices = TempFile();
            try
            {
                var session = new FakeSession();
                var provider = Provider(session, python, script, model, voices);

                var result = provider.Synthesize(Request(), CancellationToken.None).Result;

                Assert.IsTrue(result.Ok);
                Assert.AreEqual(4, result.Audio.Length);
                Assert.AreEqual(1, session.Calls);
            }
            finally
            {
                File.Delete(python);
                File.Delete(script);
                File.Delete(model);
                File.Delete(voices);
            }
        }

        [Test]
        public void ASessionFailureIsReported()
        {
            var python = TempFile();
            var script = TempFile();
            var model = TempFile();
            var voices = TempFile();
            try
            {
                var session = new FakeSession { Throws = new InvalidOperationException("boom") };
                var provider = Provider(session, python, script, model, voices);

                var result = provider.Synthesize(Request(), CancellationToken.None).Result;

                Assert.IsFalse(result.Ok);
                StringAssert.Contains("boom", result.LogMessage);
            }
            finally
            {
                File.Delete(python);
                File.Delete(script);
                File.Delete(model);
                File.Delete(voices);
            }
        }

        [Test]
        public void ACancelledTokenReturnsCancelled()
        {
            var python = TempFile();
            var script = TempFile();
            var model = TempFile();
            var voices = TempFile();
            try
            {
                var session = new FakeSession();
                var provider = Provider(session, python, script, model, voices);
                var cancel = new CancellationTokenSource();
                cancel.Cancel();

                var result = provider.Synthesize(Request(), cancel.Token).Result;

                Assert.IsFalse(result.Ok);
                Assert.IsTrue(result.Quiet);
            }
            finally
            {
                File.Delete(python);
                File.Delete(script);
                File.Delete(model);
                File.Delete(voices);
            }
        }

        [Test]
        public void PrewarmStartsTheSessionWithoutAPath()
        {
            var session = new FakeSession();
            var provider = Provider(session, "C:/nope/python.exe", "C:/nope/script.py", "C:/nope/m.onnx", "C:/nope/v.bin");

            provider.Prewarm();

            Assert.AreEqual(1, session.Prewarms);
            Assert.AreEqual(0, session.Calls);
        }

        [Test]
        public void TheProviderIdIsKokoro()
        {
            var provider = Provider(new FakeSession(), string.Empty, string.Empty, string.Empty, string.Empty);
            Assert.AreEqual("kokoro", provider.ProviderId);
        }
    }
}
