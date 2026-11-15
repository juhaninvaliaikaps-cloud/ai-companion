using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.Voice;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-128: the Chatterbox provider and its per-request preset voice.</summary>
    public sealed class ChatterboxTtsProviderTests
    {
        string _dir;
        string _python;
        string _script;
        string _voices;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-chatterbox-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _python = Path.Combine(_dir, "python.exe");
            _script = Path.Combine(_dir, "chatterbox_server.py");
            _voices = Path.Combine(_dir, "voices");
            File.WriteAllText(_python, string.Empty);
            File.WriteAllText(_script, string.Empty);
            Directory.CreateDirectory(_voices);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void Synthesize_UsesTheConfiguredPresetNotTheCloudVoiceId()
        {
            var session = new FakeSession(new byte[] { 1, 2, 3 });
            var provider = new ChatterboxTtsProvider(_python, _script, _voices, "af_bella", session, 180f);

            var result = provider.Synthesize(
                new TtsRequest { Text = "hello", VoiceId = "eve", Speed = 1f },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result.Audio);
            Assert.AreEqual("af_bella", session.Voice);
        }

        [Test]
        public void Prewarm_DoesNothingUntilThePathsAreConfigured()
        {
            var session = new FakeSession(new byte[] { 1 });
            var provider = new ChatterboxTtsProvider(string.Empty, string.Empty, string.Empty, "af_heart", session, 180f);

            provider.Prewarm();

            Assert.AreEqual(0, session.PrewarmCalls);
        }

        [Test]
        public void Synthesize_EmptyVoicesFolderFailsWithAClearMessage()
        {
            var provider = new ChatterboxTtsProvider(_python, _script, Path.Combine(_dir, "missing"), "af_heart", new FakeSession(new byte[1]), 180f);

            var result = provider.Synthesize(
                new TtsRequest { Text = "hello" },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(ChatterboxTtsProvider.MissingVoicesMessage, result.UserMessage);
        }

        sealed class FakeSession : IChatterboxSession
        {
            readonly byte[] _wav;

            public FakeSession(byte[] wav)
            {
                _wav = wav;
            }

            public string Voice;
            public int PrewarmCalls;

            public Task<byte[]> Synthesize(string text, string voice, float speed, CancellationToken cancellationToken)
            {
                Voice = voice;
                return Task.FromResult(_wav);
            }

            public void Prewarm()
            {
                PrewarmCalls++;
            }

            public void Dispose()
            {
            }
        }
    }
}
