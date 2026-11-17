using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
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

        [Test]
        public void Dispose_IsVisibleSoRefreshCanStopTheProcess()
        {
            var session = new FakeSession(new byte[] { 1 });
            var provider = new ChatterboxTtsProvider(_python, _script, _voices, "af_nicole", session, 180f);

            var disposable = provider as IDisposable;

            Assert.IsNotNull(disposable);
            disposable.Dispose();
            Assert.AreEqual(1, session.DisposeCalls);
        }

        [Test]
        public void MixedCasePreset_IsSpokenAsItself()
        {
            Assert.AreEqual("Af_Bella", VoiceUtterance.ChatterboxVoiceName("Af_Bella"));
            Assert.AreEqual("af_nicole", VoiceUtterance.ChatterboxVoiceName("  af_nicole  "));
            Assert.AreEqual(new string('A', 64), VoiceUtterance.ChatterboxVoiceName(new string('A', 64)));
            Assert.AreEqual(VoiceUtterance.DefaultChatterboxVoiceId, VoiceUtterance.ChatterboxVoiceName(new string('A', 65)));
            Assert.AreEqual(VoiceUtterance.DefaultChatterboxVoiceId, VoiceUtterance.ChatterboxVoiceName("Af Bella"));
            Assert.AreEqual(VoiceUtterance.DefaultChatterboxVoiceId, VoiceUtterance.ChatterboxVoiceName(@"E:\voices\Af_Bella"));
            Assert.AreEqual(VoiceUtterance.DefaultChatterboxVoiceId, VoiceUtterance.ChatterboxVoiceName(null));

            var session = new FakeSession(new byte[] { 9 });
            var provider = new ChatterboxTtsProvider(_python, _script, _voices, "Af_Bella", session, 180f);
            var result = provider.Synthesize(
                new TtsRequest { Text = "hello", VoiceId = "eve", Speed = 1f },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            Assert.AreEqual("Af_Bella", provider.Voice);
            Assert.AreEqual("Af_Bella", session.Voice);
        }

        [Test]
        public void RequestJson_EscapesOnAWorkerWithoutUnityJson()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                comma.NumberFormat.NumberDecimalSeparator = ",";
                CultureInfo.CurrentCulture = comma;

                var json = ChatterboxRequestJson.Build("say \"hi\"\\\nnext", "Af_Bella", 1.25f);

                Assert.AreEqual("{\"text\":\"say \\\"hi\\\"\\\\\\nnext\",\"voice\":\"Af_Bella\",\"speed\":1.25}", json);
                Assert.AreEqual(-1, json.IndexOf('\n'));
                Assert.IsFalse(json.Contains("1,25"));
                Assert.AreEqual(
                    "{\"text\":\"\",\"voice\":\"af_nicole\",\"speed\":1}",
                    ChatterboxRequestJson.Build(null, "af_nicole", 0f));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ReplyJson_ReadsOkWithoutTreatingTheErrorTextAsSuccess()
        {
            var ok = ChatterboxReply.Parse("{\"ok\": true, \"wav\": \"YQ==\"}");
            Assert.IsTrue(ok.ok);
            Assert.AreEqual("YQ==", ok.wav);
            Assert.IsNull(ok.error);

            var failed = ChatterboxReply.Parse("{\"ok\": false, \"error\": \"true failure\"}");
            Assert.IsFalse(failed.ok);
            Assert.AreEqual("true failure", failed.error);

            var escaped = ChatterboxReply.Parse("{\"ok\": false, \"error\": \"say \\\"no\\\"\"}");
            Assert.AreEqual("say \"no\"", escaped.error);
        }

        [Test]
        public void Cancel_DropsThePendingReplySoTheNextRequestIsANewProcess()
        {
            var bash = BashOrIgnore();
            var script = Path.Combine(_dir, "pid.sh");
            File.WriteAllText(script, PidScript);
            var seen = Path.Combine(_voices, "seen");
            var session = new ChatterboxProcessSession(bash, script, _voices, "af_nicole", "cpu", 30f);
            try
            {
                var first = PidOf(session.Synthesize("ping", "af_nicole", 1f, CancellationToken.None).GetAwaiter().GetResult());
                if (File.Exists(seen))
                    File.Delete(seen);

                var cancel = new CancellationTokenSource();
                var slow = session.Synthesize("SLOW", "af_nicole", 1f, cancel.Token);
                WaitForFile(seen);
                cancel.Cancel();
                WaitFor(slow, 3000);

                var second = PidOf(session.Synthesize("ping", "af_nicole", 1f, CancellationToken.None).GetAwaiter().GetResult());
                Assert.AreNotEqual(first, second);
                Assert.IsNotEmpty(second);
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        public void Dispose_DuringSynthesis_IsAQuietCancel()
        {
            var session = new BlockingSession();
            var provider = new ChatterboxTtsProvider(_python, _script, _voices, "af_nicole", session, 30f);
            var running = provider.Synthesize(new TtsRequest { Text = "hello", Speed = 1f }, CancellationToken.None);
            Assert.IsTrue(session.Entered.Wait(3000));

            provider.Dispose();

            var result = running.GetAwaiter().GetResult();
            Assert.IsFalse(result.Ok);
            Assert.IsTrue(result.Quiet);
        }

        [Test]
        public void Dispose_ReturnsWhileSynthesisHoldsTheRead()
        {
            var bash = BashOrIgnore();
            var script = Path.Combine(_dir, "slow.sh");
            File.WriteAllText(script, SlowScript);
            var session = new ChatterboxProcessSession(bash, script, _voices, "af_nicole", "cpu", 30f);
            var provider = new ChatterboxTtsProvider(bash, script, _voices, "af_nicole", session, 30f);
            var running = provider.Synthesize(new TtsRequest { Text = "hello", Speed = 1f }, CancellationToken.None);
            WaitForFile(Path.Combine(_voices, "seen"));

            var watch = Stopwatch.StartNew();
            provider.Dispose();
            watch.Stop();

            Assert.Less(watch.ElapsedMilliseconds, 1000);
            WaitFor(running, 3000);
        }

        static string BashOrIgnore()
        {
            if (File.Exists("/bin/bash"))
                return "/bin/bash";
            if (File.Exists("/usr/bin/bash"))
                return "/usr/bin/bash";
            Assert.Ignore("Chatterbox process tests need bash.");
            return string.Empty;
        }

        static void WaitFor(Task task, int milliseconds)
        {
            try
            {
                Assert.IsTrue(task.Wait(milliseconds));
            }
            catch (AggregateException)
            {
                Assert.IsTrue(task.IsCompleted);
            }
        }

        static void WaitForFile(string path)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!File.Exists(path) && DateTime.UtcNow < deadline)
                Thread.Sleep(20);
            Assert.IsTrue(File.Exists(path));
        }

        static string PidOf(byte[] wav)
        {
            return Encoding.UTF8.GetString(wav).Trim();
        }

        const string VoicesHeader =
            "#!/bin/bash\n" +
            "voices=\"\"\n" +
            "prev=\"\"\n" +
            "for arg in \"$@\"; do\n" +
            "  if [ \"$prev\" = \"--voices\" ]; then voices=\"$arg\"; fi\n" +
            "  prev=\"$arg\"\n" +
            "done\n";

        const string PidScript =
            VoicesHeader +
            "pid=$$\n" +
            "while IFS= read -r line; do\n" +
            "  printf '%s' \"$pid\" > \"$voices/seen\"\n" +
            "  case \"$line\" in\n" +
            "    *SLOW*) sleep 3 ;;\n" +
            "  esac\n" +
            "  wav=$(printf '%s' \"$pid\" | base64)\n" +
            "  printf '%s\\n' \"{\\\"ok\\\": true, \\\"wav\\\": \\\"$wav\\\"}\"\n" +
            "done\n";

        const string SlowScript =
            VoicesHeader +
            "IFS= read -r line\n" +
            "printf '%s' seen > \"$voices/seen\"\n" +
            "printf '%s\\n' READY\n" +
            "sleep 8\n" +
            "printf '%s\\n' '{\"ok\": true, \"wav\": \"YQ==\"}'\n";

        sealed class BlockingSession : IChatterboxSession
        {
            public readonly ManualResetEventSlim Entered = new ManualResetEventSlim(false);
            readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);

            public Task<byte[]> Synthesize(string text, string voice, float speed, CancellationToken cancellationToken)
            {
                Entered.Set();
                Func<byte[]> body = () =>
                {
                    _release.Wait();
                    throw new ObjectDisposedException(nameof(BlockingSession));
                };
                return Task.Run(body);
            }

            public void Prewarm()
            {
            }

            public void Dispose()
            {
                _release.Set();
            }
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
            public int DisposeCalls;

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
                DisposeCalls++;
            }
        }
    }
}
