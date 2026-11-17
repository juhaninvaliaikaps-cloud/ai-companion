using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.Voice
{
    /// <summary>One open Chatterbox process. Abstracted so the provider can be tested.</summary>
    public interface IChatterboxSession : IDisposable
    {
        Task<byte[]> Synthesize(string text, string voice, float speed, CancellationToken cancellationToken);

        void Prewarm();
    }

    /// <summary>
    /// JUH-128: offline Chatterbox Turbo speech. A single Python process loads the model once;
    /// each request names a preset folder under <c>chatterboxVoices</c> containing reference.wav.
    /// </summary>
    public sealed class ChatterboxTtsProvider : ITtsProvider, IDisposable
    {
        public const string Id = "chatterbox";
        public const string MissingPythonMessage = "Chatterbox is not installed. Set the Python and script paths in Settings.";
        public const string MissingVoicesMessage = "The Chatterbox voice presets folder is missing.";

        readonly string _pythonExe;
        readonly string _scriptPath;
        readonly string _voicesPath;
        readonly string _voice;
        readonly IChatterboxSession _session;
        readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        readonly float _timeoutSeconds;

        public ChatterboxTtsProvider(
            string pythonExe,
            string scriptPath,
            string voicesPath,
            string voice,
            IChatterboxSession session,
            float timeoutSeconds)
        {
            _pythonExe = Clean(pythonExe);
            _scriptPath = Clean(scriptPath);
            _voicesPath = Clean(voicesPath);
            _voice = VoiceUtterance.ChatterboxVoiceName(voice);
            _session = session;
            _timeoutSeconds = timeoutSeconds <= 0f ? 180f : timeoutSeconds;
        }

        public string ProviderId => Id;

        public string Voice => _voice;

        public void Prewarm()
        {
            if (_session == null)
                return;
            if (string.IsNullOrWhiteSpace(_pythonExe) || !File.Exists(_pythonExe))
                return;
            if (string.IsNullOrWhiteSpace(_scriptPath) || !File.Exists(_scriptPath))
                return;
            if (string.IsNullOrWhiteSpace(_voicesPath) || !Directory.Exists(_voicesPath))
                return;
            _session.Prewarm();
        }

        public async Task<TtsResult> Synthesize(TtsRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
                return TtsResult.Cancelled();
            if (string.IsNullOrEmpty(_pythonExe) || !File.Exists(_pythonExe))
                return TtsResult.Fail(MissingPythonMessage, "Chatterbox python missing: " + _pythonExe);
            if (string.IsNullOrEmpty(_scriptPath) || !File.Exists(_scriptPath))
                return TtsResult.Fail(MissingPythonMessage, "Chatterbox script missing: " + _scriptPath);
            if (string.IsNullOrEmpty(_voicesPath) || !Directory.Exists(_voicesPath))
                return TtsResult.Fail(MissingVoicesMessage, "Chatterbox voices folder missing: " + _voicesPath);
            if (_session == null)
                return TtsResult.Fail(MissingPythonMessage, "Chatterbox session was not created.");
            if (cancellationToken.IsCancellationRequested)
                return TtsResult.Cancelled();

            try
            {
                await _gate.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return TtsResult.Cancelled();
            }

            try
            {
                // The request's VoiceId is the cloud voice id (for example "eve"). A
                // Chatterbox preset has already been chosen in settings and is held by the
                // provider, so never pass the cloud id to the preset folder.
                var voice = _voice;
                var wav = await _session.Synthesize(request.Text, voice, request.Speed, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return TtsResult.Cancelled();
                if (wav == null || wav.Length == 0)
                    return TtsResult.Fail("Chatterbox returned no audio.", "Empty WAV from Chatterbox.");
                return TtsResult.Success(wav, 200, "audio/wav");
            }
            catch (OperationCanceledException)
            {
                return TtsResult.Cancelled();
            }
            catch (Exception e)
            {
                return TtsResult.Fail("Chatterbox failed.", "Chatterbox threw " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            if (_session != null)
                _session.Dispose();
            _gate.Dispose();
        }

        static string Clean(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }

    /// <summary>The real process bridge: one JSON request per stdin line, one JSON reply per stdout line.</summary>
    public sealed class ChatterboxProcessSession : IChatterboxSession
    {
        readonly object _sync = new object();
        readonly string _voicesPath;
        readonly string _voice;
        readonly float _timeoutSeconds;
        Process _process;
        StreamWriter _input;
        StreamReader _output;
        int _stop;

        public ChatterboxProcessSession(
            string pythonExe,
            string scriptPath,
            string voicesPath,
            string voice,
            string device,
            float timeoutSeconds)
        {
            PythonExe = pythonExe;
            ScriptPath = scriptPath;
            _voicesPath = voicesPath;
            _voice = voice;
            Device = string.IsNullOrWhiteSpace(device) ? "auto" : device.Trim();
            _timeoutSeconds = timeoutSeconds <= 0f ? 180f : timeoutSeconds;
        }

        public string PythonExe { get; private set; }
        public string ScriptPath { get; private set; }
        public string Device { get; private set; }

        void EnsureStarted()
        {
            if (Volatile.Read(ref _stop) != 0)
                throw new ObjectDisposedException(nameof(ChatterboxProcessSession));
            if (_process != null && !_process.HasExited)
                return;
            if (string.IsNullOrWhiteSpace(PythonExe) || !File.Exists(PythonExe))
                return;
            if (string.IsNullOrWhiteSpace(ScriptPath) || !File.Exists(ScriptPath))
                return;

            Close();

            var info = new ProcessStartInfo
            {
                FileName = PythonExe,
                Arguments = "-u \"" + ScriptPath + "\" --voices \"" + _voicesPath + "\" --device \"" + Device + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            _process = Process.Start(info);
            if (_process == null)
                throw new InvalidOperationException("Chatterbox process did not start.");
            if (Volatile.Read(ref _stop) != 0)
            {
                KillCurrent();
                throw new ObjectDisposedException(nameof(ChatterboxProcessSession));
            }

            _input = _process.StandardInput;
            _input.AutoFlush = true;
            _output = _process.StandardOutput;
            _process.ErrorDataReceived += (sender, args) => { };
            _process.BeginErrorReadLine();
        }

        public Task<byte[]> Synthesize(string text, string voice, float speed, CancellationToken cancellationToken)
        {
            return Task.Run(() => SynthesizeBlocking(text, voice, speed, cancellationToken), cancellationToken);
        }

        public void Prewarm()
        {
            lock (_sync)
                EnsureStarted();
        }

        byte[] SynthesizeBlocking(string text, string voice, float speed, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                EnsureStarted();

                _input.WriteLine(ChatterboxRequestJson.Build(text, voice, speed));
                _input.Flush();

                var deadline = DateTime.UtcNow.AddSeconds(_timeoutSeconds);
                ChatterboxReply reply = null;
                while (reply == null)
                {
                    var output = _output;
                    var read = Task.Run(() => ReadLineOrNull(output));
                    while (!read.Wait(50))
                    {
                        if (Volatile.Read(ref _stop) != 0)
                            throw new ObjectDisposedException(nameof(ChatterboxProcessSession));
                        // The reply may still be written after this returns. Kill the process
                        // so the next request cannot read it from the same stdout.
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Close();
                            throw new OperationCanceledException(cancellationToken);
                        }
                        if (DateTime.UtcNow > deadline)
                        {
                            Close();
                            throw new TimeoutException("Chatterbox timed out after " + _timeoutSeconds.ToString("0") + " s.");
                        }
                    }

                    if (Volatile.Read(ref _stop) != 0)
                        throw new ObjectDisposedException(nameof(ChatterboxProcessSession));

                    var line = read.Result;
                    if (line == null)
                        throw new InvalidOperationException("Chatterbox closed its output.");

                    // The model loader and Perth may print a line to stdout before the first
                    // JSON reply. Skip those lines instead of treating them as JSON.
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] != '{')
                        continue;
                    // Loader noise can start with '{'. A reply always names "ok".
                    // JsonUtility is a Unity API and is not safe on this worker thread.
                    if (trimmed.IndexOf("\"ok\"", StringComparison.Ordinal) < 0)
                        continue;

                    reply = ChatterboxReply.Parse(trimmed);
                }

                if (!reply.ok)
                    throw new InvalidOperationException(reply.error);
                return Convert.FromBase64String(reply.wav ?? string.Empty);
            }
        }

        public void Close()
        {
            try
            {
                if (_input != null)
                    _input.Close();
            }
            catch (IOException)
            {
            }

            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }

            _process = null;
            _input = null;
            _output = null;
        }

        /// <summary>
        /// Stops the process without taking <c>_sync</c>. Synthesis holds that lock for the
        /// whole read, and RefreshProvider calls Dispose on the Unity main thread.
        /// </summary>
        public void Dispose()
        {
            Volatile.Write(ref _stop, 1);
            KillCurrent();
        }

        void KillCurrent()
        {
            var process = _process;
            if (process == null)
                return;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        static string ReadLineOrNull(StreamReader output)
        {
            if (output == null)
                return null;
            try
            {
                return output.ReadLine();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    internal sealed class ChatterboxReply
    {
        public bool ok;
        public string wav;
        public string error;

        public static ChatterboxReply Parse(string line)
        {
            var reply = new ChatterboxReply();
            if (string.IsNullOrEmpty(line))
                return reply;

            reply.ok = ReadBool(line);
            reply.wav = Extract(line, "\"wav\"");
            reply.error = Extract(line, "\"error\"");
            return reply;
        }

        static bool ReadBool(string line)
        {
            var start = line.IndexOf("\"ok\"", StringComparison.Ordinal);
            if (start < 0)
                return false;

            var colon = line.IndexOf(':', start + 4);
            if (colon < 0)
                return false;

            var i = colon + 1;
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;

            return i + 4 <= line.Length
                && string.CompareOrdinal(line, i, "true", 0, 4) == 0
                && (i + 4 == line.Length || !char.IsLetterOrDigit(line[i + 4]));
        }

        static string Extract(string line, string key)
        {
            var start = line.IndexOf(key, StringComparison.Ordinal);
            if (start < 0)
                return null;

            var colon = line.IndexOf(':', start);
            if (colon < 0)
                return null;

            var quote = line.IndexOf('"', colon + 1);
            if (quote < 0)
                return null;

            var sb = new StringBuilder();
            for (var i = quote + 1; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '\\' && i + 1 < line.Length)
                {
                    var next = line[i + 1];
                    i++;
                    if (next == 'n')
                        sb.Append('\n');
                    else if (next == 'r')
                        sb.Append('\r');
                    else if (next == 't')
                        sb.Append('\t');
                    else
                        sb.Append(next);
                    continue;
                }

                if (c == '"')
                    break;
                sb.Append(c);
            }

            return sb.ToString();
        }
    }

    internal static class ChatterboxRequestJson
    {
        public static string Build(string text, string voice, float speed)
        {
            var safeSpeed = speed <= 0f ? 1f : speed;
            return "{\"text\":" + Quote(text) + ",\"voice\":" + Quote(voice) + ",\"speed\":"
                + safeSpeed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "}";
        }

        static string Quote(string value)
        {
            if (value == null)
                return "\"\"";

            var sb = new StringBuilder(value.Length + 8);
            sb.Append('"');
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '"' || c == '\\')
                    sb.Append('\\').Append(c);
                else if (c == '\n')
                    sb.Append("\\n");
                else if (c == '\r')
                    sb.Append("\\r");
                else if (c == '\t')
                    sb.Append("\\t");
                else if (c < 0x20)
                    sb.Append(' ');
                else
                    sb.Append(c);
            }

            sb.Append('"');
            return sb.ToString();
        }
    }
}
