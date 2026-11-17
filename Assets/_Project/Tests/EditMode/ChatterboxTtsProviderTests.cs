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
        [Timeout(20000)]
        public void Cancel_DropsThePendingReplySoTheNextRequestIsANewProcess()
        {
            var bash = BashOrIgnore();
            var script = Path.Combine(_dir, "pid.sh");
            File.WriteAllText(script, PidScript);
            var seen = Path.Combine(_voices, "seen");
            var session = new ChatterboxProcessSession(bash, script, _voices, "af_nicole", "cpu", 5f);
            try
            {
                var first = PidOf(BytesWithin(session.Synthesize("ping", "af_nicole", 1f, CancellationToken.None), 8000));
                if (File.Exists(seen))
                    File.Delete(seen);

                var cancel = new CancellationTokenSource();
                var slow = session.Synthesize("SLOW", "af_nicole", 1f, cancel.Token);
                WaitForFile(seen);
                cancel.Cancel();
                WaitFor(slow, 3000);

                var second = PidOf(BytesWithin(session.Synthesize("ping", "af_nicole", 1f, CancellationToken.None), 8000));
                Assert.AreNotEqual(first, second);
                Assert.IsNotEmpty(second);
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        [Timeout(20000)]
        public void Dispose_DuringSynthesis_IsAQuietCancel()
        {
            // Synthesize is async. Blocking the Unity main thread on it deadlocks,
            // because the continuation has to return to that same thread.
            var session = new BlockingSession();
            var provider = new ChatterboxTtsProvider(_python, _script, _voices, "af_nicole", session, 5f);
            TtsResult result = default(TtsResult);
            Exception error = null;
            var done = new ManualResetEventSlim(false);
            var worker = new Thread(() =>
            {
                try
                {
                    var running = provider.Synthesize(new TtsRequest { Text = "hello", Speed = 1f }, CancellationToken.None);
                    if (!session.Entered.Wait(3000))
                        throw new TimeoutException("Chatterbox session did not start.");
                    provider.Dispose();
                    if (!running.Wait(3000))
                        throw new TimeoutException("Disposed synthesis did not finish.");
                    result = running.Result;
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    done.Set();
                }
            });
            worker.IsBackground = true;
            worker.Start();
            Assert.IsTrue(done.Wait(10000), "Quiet-cancel test timed out.");
            if (error != null)
                throw error;
            Assert.IsFalse(result.Ok);
            Assert.IsTrue(result.Quiet);
        }

        [Test]
        [Timeout(20000)]
        public void Dispose_ReturnsWhileSynthesisHoldsTheRead()
        {
            // Same deadlock as the quiet-cancel test: Synthesize is async, so waiting for it
            // on the Unity main thread never lets the continuation finish.
            var bash = BashOrIgnore();
            var script = Path.Combine(_dir, "slow.sh");
            File.WriteAllText(script, SlowScript);
            var session = new ChatterboxProcessSession(bash, script, _voices, "af_nicole", "cpu", 5f);
            var provider = new ChatterboxTtsProvider(bash, script, _voices, "af_nicole", session, 5f);
            Exception error = null;
            var done = new ManualResetEventSlim(false);
            var worker = new Thread(() =>
            {
                try
                {
                    var running = provider.Synthesize(new TtsRequest { Text = "hello", Speed = 1f }, CancellationToken.None);
                    WaitForFile(Path.Combine(_voices, "seen"));

                    var watch = Stopwatch.StartNew();
                    provider.Dispose();
                    watch.Stop();
                    if (watch.ElapsedMilliseconds >= 1000)
                        throw new TimeoutException("Dispose blocked for " + watch.ElapsedMilliseconds + " ms.");
                    if (!running.Wait(3000))
                        throw new TimeoutException("Disposed synthesis did not finish.");
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    done.Set();
                }
            });
            worker.IsBackground = true;
            worker.Start();
            try
            {
                Assert.IsTrue(done.Wait(10000), "Dispose-during-read test timed out.");
                if (error != null)
                    throw error;
            }
            finally
            {
                session.Dispose();
            }
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
                Assert.IsTrue(task.Wait(milliseconds), "Timed out after " + milliseconds + " ms.");
            }
            catch (AggregateException)
            {
                Assert.IsTrue(task.IsCompleted);
            }
        }

        static byte[] BytesWithin(Task<byte[]> task, int milliseconds)
        {
            Assert.IsTrue(task.Wait(milliseconds), "Timed out after " + milliseconds + " ms.");
            return task.Result;
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
            "sleep 3\n" +
            "printf '%s\\n' '{\"ok\": true, \"wav\": \"YQ==\"}'\n";

        [Test]
        public void PersonaMenu_KeepsThatPersonasVoiceAndTheInheritCaption()
        {
            var options = ChatterboxVoiceMenu.Options(
                new[] { "af_nicole", "af_bella" },
                true,
                "OnlyOnThisPersona");

            Assert.AreEqual(string.Empty, options[0]);
            CollectionAssert.Contains(options, "af_nicole");
            CollectionAssert.Contains(options, "OnlyOnThisPersona");
            Assert.AreEqual(4, options.Count);

            Assert.AreEqual(
                "(same as above)",
                ChatterboxVoiceMenu.ClosedLabel(string.Empty, v => string.IsNullOrEmpty(v) ? "(same as above)" : v));
            Assert.AreEqual("(not set)", ChatterboxVoiceMenu.ClosedLabel("  ", v => v));
            Assert.AreEqual("OnlyOnThisPersona", ChatterboxVoiceMenu.ClosedLabel("OnlyOnThisPersona", v => v));
        }

        [Test]
        public void Arguments_KeepATrailingBackslashInsideTheQuotes()
        {
            Assert.AreEqual("\"C:\\voices\\\\\"", ChatterboxProcessArguments.Quote("C:\\voices\\"));
            Assert.AreEqual("C:\\voices\\", WindowsArgument(ChatterboxProcessArguments.Quote("C:\\voices\\")));
            Assert.AreEqual("C:\\", WindowsArgument(ChatterboxProcessArguments.Quote("C:\\")));
            Assert.AreEqual("C:\\voi\"ces\\", WindowsArgument(ChatterboxProcessArguments.Quote("C:\\voi\"ces\\")));
            Assert.AreEqual("/tmp/voices", WindowsArgument(ChatterboxProcessArguments.Quote("/tmp/voices")));
        }

        static string WindowsArgument(string quoted)
        {
            Assert.IsTrue(quoted.Length >= 2 && quoted[0] == '"' && quoted[quoted.Length - 1] == '"');
            var sb = new StringBuilder();
            var slashes = 0;
            for (var i = 1; i < quoted.Length; i++)
            {
                var c = quoted[i];
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }

                if (c == '"')
                {
                    for (var n = 0; n < slashes / 2; n++)
                        sb.Append('\\');
                    if ((slashes % 2) == 1)
                    {
                        sb.Append('"');
                        slashes = 0;
                        continue;
                    }

                    slashes = 0;
                    break;
                }

                for (var n = 0; n < slashes; n++)
                    sb.Append('\\');
                slashes = 0;
                sb.Append(c);
            }

            return sb.ToString();
        }

        sealed class BlockingSession : IChatterboxSession
        {
            public readonly ManualResetEventSlim Entered = new ManualResetEventSlim(false);
            readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);

            public Task<byte[]> Synthesize(string text, string voice, float speed, CancellationToken cancellationToken)
            {
                Entered.Set();
                Func<byte[]> body = () =>
                {
                    if (!_release.Wait(5000))
                        throw new TimeoutException("Dispose did not release the Chatterbox session.");
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
