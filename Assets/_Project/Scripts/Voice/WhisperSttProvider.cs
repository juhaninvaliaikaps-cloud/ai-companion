using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AICompanion.AI;
using UnityEngine;
using Whisper;
using Whisper.Utils;

namespace AICompanion.Voice
{
    /// <summary>
    /// In-process whisper.cpp transcription. Audio and the transcript stay on
    /// this machine. Console lines are short timings and the model name.
    /// </summary>
    public sealed class WhisperSttProvider : ISttProvider
    {
        readonly string _path;
        readonly string _modelFile;
        WhisperWrapper _wrapper;

        public WhisperSttProvider(string absoluteModelPath, string modelFileName)
        {
            _path = absoluteModelPath ?? string.Empty;
            _modelFile = string.IsNullOrWhiteSpace(modelFileName) ? SttSettings.DefaultModelFile : modelFileName;
        }

        public string ModelFileName => _modelFile;

        public async Task<SttResult> TranscribeAsync(float[] samples16kMono, string language)
        {
            if (samples16kMono == null || samples16kMono.Length == 0)
                return SttResult.Empty("STT: no speech");

            LogUtils.Level = LogLevel.Warning;
            var quiet = SttLogQuiet.Begin();
            var loadedThisCall = false;
            var loadSeconds = 0f;
            try
            {
                if (_wrapper == null)
                {
                    if (!File.Exists(_path))
                        return SttResult.Fail(MissingModelMessage(_modelFile), "STT: model missing " + _modelFile);

                    var info = new FileInfo(_path);
                    if (info.Length < 1000000)
                        return SttResult.Fail(MissingModelMessage(_modelFile), "STT: model missing " + _modelFile);

                    var loadStarted = Time.realtimeSinceStartup;
                    var context = WhisperContextParams.GetDefaultParams();
                    context.UseGpu = false;
                    context.FlashAttn = false;
                    _wrapper = await WhisperWrapper.InitFromFileAsync(_path, context);
                    loadSeconds = Time.realtimeSinceStartup - loadStarted;
                    loadedThisCall = _wrapper != null;
                    if (_wrapper == null)
                        return SttResult.Fail("The speech model could not be loaded.", "STT: model load failed " + _modelFile);
                }

                var languageCode = string.IsNullOrWhiteSpace(language) ? "auto" : language;
                var started = Time.realtimeSinceStartup;
                var param = WhisperParams.GetDefaultParams();
                param.Language = languageCode;
                param.Translate = false;
                param.NoContext = true;
                param.SingleSegment = true;
                param.PrintSpecial = false;
                param.PrintProgress = false;
                param.PrintRealtime = false;
                param.PrintTimestamps = false;
                var result = await _wrapper.GetTextAsync(samples16kMono, PcmResample.WhisperRate, 1, param);
                var elapsed = Time.realtimeSinceStartup - started;
                if (result == null)
                    return SttResult.Fail("Speech could not be transcribed.", FailLine(_modelFile, elapsed, loadedThisCall, loadSeconds));

                string text;
                if (!TranscriptText.TryAccept(result.Result, out text))
                    return SttResult.Empty(DoneLine("no speech", _modelFile, languageCode, result.Language, elapsed, loadedThisCall, loadSeconds));

                return SttResult.Ready(text, DoneLine("transcribe", _modelFile, languageCode, result.Language, elapsed, loadedThisCall, loadSeconds));
            }
            catch (Exception ex)
            {
                var detail = ChatErrors.Scrub(ex.Message);
                if (detail.Length > 80)
                    detail = detail.Substring(0, 80);
                return SttResult.Fail("Speech could not be transcribed.", "STT: transcribe failed " + _modelFile + " " + detail);
            }
            finally
            {
                quiet.Dispose();
            }
        }

        public static string MissingModelMessage(string modelFile)
        {
            var name = string.IsNullOrWhiteSpace(modelFile) ? SttSettings.DefaultModelFile : modelFile;
            return "The speech model is missing (" + name + "). Use AI Companion > Download Whisper Model (base).";
        }

        static string FailLine(string modelFile, float elapsed, bool loaded, float loadSeconds)
        {
            var line = "STT: transcribe failed " + modelFile + " " + Seconds(elapsed) + " s";
            if (loaded)
                line += " load " + Seconds(loadSeconds) + " s";
            return line;
        }

        static string DoneLine(string kind, string modelFile, string requested, string detected, float elapsed, bool loaded, float loadSeconds)
        {
            var line = "STT: " + kind + " " + modelFile
                + " lang " + SafeCode(requested)
                + " detected " + SafeCode(detected)
                + " " + Seconds(elapsed) + " s";
            if (loaded)
                line += " load " + Seconds(loadSeconds) + " s";
            return line;
        }

        static string Seconds(float value)
        {
            if (value < 0f)
                value = 0f;
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        static string SafeCode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "-";

            var sb = new StringBuilder();
            for (var i = 0; i < value.Length && sb.Length < 16; i++)
            {
                var c = value[i];
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (ok)
                    sb.Append(c);
            }

            return sb.Length == 0 ? "-" : sb.ToString();
        }
    }

    /// <summary>
    /// whisper.cpp logs errors with <c>Debug.LogError</c>, which pauses Play mode.
    /// Downgrade those to warnings for the duration of one transcription.
    /// The transcript itself is logged by the package at Log level, and that
    /// level is raised so the text is not written.
    /// </summary>
    sealed class SttLogQuiet : IDisposable
    {
        readonly ILogHandler _previous;

        SttLogQuiet(ILogHandler previous)
        {
            _previous = previous;
        }

        public static SttLogQuiet Begin()
        {
            LogUtils.Level = LogLevel.Warning;
            var previous = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new Downgrade(previous);
            return new SttLogQuiet(previous);
        }

        public void Dispose()
        {
            if (Debug.unityLogger.logHandler is Downgrade)
                Debug.unityLogger.logHandler = _previous;
        }

        sealed class Downgrade : ILogHandler
        {
            readonly ILogHandler _inner;

            public Downgrade(ILogHandler inner)
            {
                _inner = inner;
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                if (_inner == null)
                    return;
                if (logType == LogType.Error || logType == LogType.Assert || logType == LogType.Exception)
                    logType = LogType.Warning;
                _inner.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                if (_inner == null)
                    return;
                var message = exception != null ? exception.Message : "exception";
                _inner.LogFormat(LogType.Warning, context, "{0}", message);
            }
        }
    }
}
