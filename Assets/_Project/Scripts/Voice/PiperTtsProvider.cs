using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.Voice
{
    /// <summary>Result of one Piper run. The WAV bytes are the synthesized speech.</summary>
    public readonly struct PiperRun
    {
        public bool Ok { get; }
        public byte[] Wav { get; }
        public string Message { get; }

        public PiperRun(bool ok, byte[] wav, string message)
        {
            Ok = ok;
            Wav = wav;
            Message = message ?? string.Empty;
        }

        public static PiperRun Fail(string message)
        {
            return new PiperRun(false, null, message);
        }
    }

    /// <summary>
    /// Runs the local Piper binary for one line of text. Abstracted so the provider can be
    /// tested without the binary present.
    /// </summary>
    public interface IPiperRunner
    {
        Task<PiperRun> Run(string exePath, string voicePath, string text, float timeoutSeconds, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Offline text to speech through Piper (https://github.com/rhasspy/piper). No API key and
    /// no network: the binary reads the text on stdin and writes a WAV file.
    /// </summary>
    public sealed class PiperTtsProvider : ITtsProvider
    {
        public const string Id = "piper";
        public const string MissingExeMessage = "Local voice is not installed. Set the Piper exe path in Settings.";
        public const string MissingVoiceMessage = "Local voice model is missing. Set the Piper voice path in Settings.";

        readonly string _exePath;
        readonly string _voicePath;
        readonly IPiperRunner _runner;
        readonly float _timeoutSeconds;

        public PiperTtsProvider(string exePath, string voicePath, IPiperRunner runner, float timeoutSeconds)
        {
            _exePath = exePath == null ? string.Empty : exePath.Trim();
            _voicePath = voicePath == null ? string.Empty : voicePath.Trim();
            _runner = runner;
            _timeoutSeconds = timeoutSeconds <= 0f ? 30f : timeoutSeconds;
        }

        public string ProviderId
        {
            get { return Id; }
        }

        public async Task<TtsResult> Synthesize(TtsRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
                return TtsResult.Cancelled();

            if (_runner == null)
                return TtsResult.Fail("No local voice runner.", "Piper runner was not configured.");

            if (string.IsNullOrEmpty(_exePath) || !File.Exists(_exePath))
                return TtsResult.Fail(MissingExeMessage, "Piper exe missing: " + _exePath);

            if (string.IsNullOrEmpty(_voicePath) || !File.Exists(_voicePath))
                return TtsResult.Fail(MissingVoiceMessage, "Piper voice missing: " + _voicePath);

            PiperRun run;
            try
            {
                run = await _runner.Run(_exePath, _voicePath, request.Text, _timeoutSeconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return TtsResult.Cancelled();
            }
            catch (Exception e)
            {
                return TtsResult.Fail("Local voice failed.", "Piper threw " + e.GetType().Name + ": " + e.Message);
            }

            if (cancellationToken.IsCancellationRequested)
                return TtsResult.Cancelled();

            if (!run.Ok || run.Wav == null || run.Wav.Length == 0)
                return TtsResult.Fail("Local voice failed.", run.Message);

            return TtsResult.Success(run.Wav, 200, "audio/wav");
        }
    }

    /// <summary>
    /// Starts piper.exe per line: the text goes to stdin, a temporary WAV comes back. The file
    /// is deleted after it is read.
    /// </summary>
    public sealed class PiperProcessRunner : IPiperRunner
    {
        public Task<PiperRun> Run(string exePath, string voicePath, string text, float timeoutSeconds, CancellationToken cancellationToken)
        {
            return Task.Run(() => RunBlocking(exePath, voicePath, text, timeoutSeconds, cancellationToken), cancellationToken);
        }

        static PiperRun RunBlocking(string exePath, string voicePath, string text, float timeoutSeconds, CancellationToken cancellationToken)
        {
            var wavPath = Path.Combine(Path.GetTempPath(), "ai-companion-piper-" + Guid.NewGuid().ToString("N") + ".wav");
            Process process = null;
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-m \"" + voicePath + "\" -f \"" + wavPath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                process = Process.Start(info);
                if (process == null)
                    return PiperRun.Fail("Piper did not start.");

                process.StandardInput.Write(text);
                process.StandardInput.Close();

                var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
                while (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested || DateTime.UtcNow > deadline)
                    {
                        TryKill(process);
                        return PiperRun.Fail(cancellationToken.IsCancellationRequested ? "cancelled" : "timeout");
                    }

                    Thread.Sleep(20);
                }

                var error = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                    return PiperRun.Fail("Piper exit " + process.ExitCode + ": " + error);

                if (!File.Exists(wavPath))
                    return PiperRun.Fail("Piper wrote no audio.");

                var bytes = File.ReadAllBytes(wavPath);
                return new PiperRun(bytes.Length > 0, bytes, string.Empty);
            }
            catch (Exception e)
            {
                return PiperRun.Fail(e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                TryKill(process);
                try
                {
                    if (File.Exists(wavPath))
                        File.Delete(wavPath);
                }
                catch (IOException)
                {
                }
            }
        }

        static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}
