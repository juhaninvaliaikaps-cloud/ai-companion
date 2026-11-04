using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.Voice
{
    /// <summary>One open Kokoro process. Abstracted so the provider is testable.</summary>
    public interface IKokoroSession : IDisposable
    {
        Task<byte[]> Synthesize(string text, float speed, CancellationToken cancellationToken);

        /// <summary>Starts the process so the model loads before the first sentence.</summary>
        void Prewarm();
    }

    /// <summary>
    /// Offline speech through Kokoro. A single Python process loads the model once and answers
    /// one JSON request per line, so the 15 second model load is paid once per session instead
    /// of once per sentence.
    /// </summary>
    public sealed class KokoroTtsProvider : ITtsProvider
    {
        public const string Id = "kokoro";
        public const string MissingPythonMessage = "Kokoro is not installed. Set the Python and script paths in Settings.";
        public const string MissingModelMessage = "The Kokoro model files are missing. Check the model and voices paths in Settings.";

        readonly string _pythonExe;
        readonly string _scriptPath;
        readonly string _modelPath;
        readonly string _voicesPath;
        readonly string _voice;
        readonly IKokoroSession _session;
        readonly float _timeoutSeconds;
        readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public KokoroTtsProvider(string pythonExe, string scriptPath, string modelPath, string voicesPath, string voice, IKokoroSession session, float timeoutSeconds)
        {
            _pythonExe = Clean(pythonExe);
            _scriptPath = Clean(scriptPath);
            _modelPath = Clean(modelPath);
            _voicesPath = Clean(voicesPath);
            _voice = VoiceUtterance.KokoroVoiceName(voice);
            _session = session;
            _timeoutSeconds = timeoutSeconds <= 0f ? 60f : timeoutSeconds;
        }

        static string Clean(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        public string ProviderId
        {
            get { return Id; }
        }

        /// <summary>The voice name the process was started with.</summary>
        public string Voice
        {
            get { return _voice; }
        }

        /// <summary>Starts the Python process early; the model loads in the background.</summary>
        public void Prewarm()
        {
            if (_session != null)
                _session.Prewarm();
        }

        public async Task<TtsResult> Synthesize(TtsRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
                return TtsResult.Cancelled();

            if (string.IsNullOrEmpty(_pythonExe) || !File.Exists(_pythonExe))
                return TtsResult.Fail(MissingPythonMessage, "Kokoro python missing: " + _pythonExe);

            if (string.IsNullOrEmpty(_scriptPath) || !File.Exists(_scriptPath))
                return TtsResult.Fail(MissingPythonMessage, "Kokoro script missing: " + _scriptPath);

            if (string.IsNullOrEmpty(_modelPath) || !File.Exists(_modelPath) ||
                string.IsNullOrEmpty(_voicesPath) || !File.Exists(_voicesPath))
                return TtsResult.Fail(MissingModelMessage, "Kokoro model or voices missing.");

            if (_session == null)
                return TtsResult.Fail(MissingPythonMessage, "Kokoro session was not created.");

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
                var wav = await _session.Synthesize(request.Text, request.Speed, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return TtsResult.Cancelled();
                if (wav == null || wav.Length == 0)
                    return TtsResult.Fail("Kokoro returned no audio.", "Empty WAV from Kokoro.");
                return TtsResult.Success(wav, 200, "audio/wav");
            }
            catch (OperationCanceledException)
            {
                return TtsResult.Cancelled();
            }
            catch (Exception e)
            {
                return TtsResult.Fail("Kokoro failed.", "Kokoro threw " + e.GetType().Name + ": " + e.Message);
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

        internal float TimeoutSeconds
        {
            get { return _timeoutSeconds; }
        }
    }

    /// <summary>
    /// The real session: starts the Python server once, writes one JSON request per line and
    /// reads one JSON reply. The WAV comes back base64 encoded, so there is no temp file.
    /// </summary>
    public sealed class KokoroProcessSession : IKokoroSession
    {
        readonly object _sync = new object();
        readonly string _voice;
        readonly float _timeoutSeconds;
        Process _process;
        StreamWriter _input;
        StreamReader _output;

        public KokoroProcessSession(string pythonExe, string scriptPath, string modelPath, string voicesPath, string voice, float timeoutSeconds)
        {
            PythonExe = pythonExe;
            ScriptPath = scriptPath;
            ModelPath = modelPath;
            VoicesPath = voicesPath;
            _voice = VoiceUtterance.KokoroVoiceName(voice);
            _timeoutSeconds = timeoutSeconds <= 0f ? 60f : timeoutSeconds;
        }

        public string PythonExe { get; private set; }
        public string ScriptPath { get; private set; }
        public string ModelPath { get; private set; }
        public string VoicesPath { get; private set; }

        void EnsureStarted()
        {
            if (_process != null && !_process.HasExited)
                return;

            Close();

            var info = new ProcessStartInfo
            {
                FileName = PythonExe,
                Arguments = "-u \"" + ScriptPath + "\" --model \"" + ModelPath + "\" --voices \"" + VoicesPath + "\" --voice \"" + _voice + "\"",
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
                throw new InvalidOperationException("Kokoro process did not start.");

            _input = _process.StandardInput;
            _input.AutoFlush = true;
            _output = _process.StandardOutput;
            _process.ErrorDataReceived += (sender, args) => { };
            _process.BeginErrorReadLine();
        }

        public Task<byte[]> Synthesize(string text, float speed, CancellationToken cancellationToken)
        {
            return Task.Run(() => SynthesizeBlocking(text, speed, cancellationToken), cancellationToken);
        }

        public void Prewarm()
        {
            lock (_sync)
                EnsureStarted();
        }

        byte[] SynthesizeBlocking(string text, float speed, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                EnsureStarted();

                var request = new KokoroRequest { text = text, speed = speed, voice = _voice };
                _input.WriteLine(request.ToJson());
                _input.Flush();

                var deadline = DateTime.UtcNow.AddSeconds(_timeoutSeconds);
                string line = null;
                var read = Task.Run(() => line = _output.ReadLine());
                while (!read.Wait(50))
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);
                    if (DateTime.UtcNow > deadline)
                        throw new TimeoutException("Kokoro timed out after " + _timeoutSeconds.ToString("0") + " s.");
                }

                if (line == null)
                    throw new InvalidOperationException("Kokoro closed its output.");

                var reply = KokoroReply.Parse(line);
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

        public void Dispose()
        {
            lock (_sync)
                Close();
        }
    }

    [Serializable]
    internal sealed class KokoroRequest
    {
        public string text;
        public float speed;
        public string voice;

        public string ToJson()
        {
            return "{\"text\":" + Quote(text) + ",\"speed\":" + speed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ",\"voice\":" + Quote(voice) + "}";
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

    internal sealed class KokoroReply
    {
        public bool ok;
        public string wav;
        public string error;

        public static KokoroReply Parse(string line)
        {
            var reply = new KokoroReply();
            var ok = IndexOf(line, "\"ok\"");
            if (ok >= 0)
                reply.ok = line.IndexOf("true", ok, StringComparison.Ordinal) >= 0;

            reply.wav = Extract(line, "\"wav\"");
            reply.error = Extract(line, "\"error\"");
            return reply;
        }

        static int IndexOf(string text, string token)
        {
            return text.IndexOf(token, StringComparison.Ordinal);
        }

        static string Extract(string line, string key)
        {
            var start = IndexOf(line, key);
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
}
