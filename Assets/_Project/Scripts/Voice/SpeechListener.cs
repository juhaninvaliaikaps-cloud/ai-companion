using System;
using System.Globalization;
using System.IO;
using AICompanion.AI;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Voice
{
    /// <summary>
    /// Hold-to-talk microphone. Audio is transcribed on this PC and sent as a
    /// chat line. Nothing is written to a file, and the transcript is not logged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpeechListener : MonoBehaviour, ICompanionStartup, IPushToTalk
    {
        [SerializeField] SttSettings sttSettings;

        readonly PushToTalkGate _gate = new PushToTalkGate();
        readonly MicSession _mic = new MicSession();
        ISttProvider _provider;
        int _serial;
        bool _buttonHeld;
        bool _started;
        bool _alive = true;
        bool _pauseSearched;
        ICompanionPause _pause;

        public bool IsRecording => _gate.Phase == PushToTalkPhase.Recording;

        public bool IsTranscribing => _gate.Phase == PushToTalkPhase.Transcribing;

        public void SetButtonHeld(bool held)
        {
            _buttonHeld = held;
        }

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            if (sttSettings == null)
                sttSettings = ScriptableObject.CreateInstance<SttSettings>();
        }

        void Start()
        {
            StartCompanion();
        }

        void OnDisable()
        {
            _serial++;
            _buttonHeld = false;
            StopMic();
            _gate.Abort();
        }

        void OnDestroy()
        {
            _alive = false;
            _serial++;
            StopMic();
        }

        void Update()
        {
            try
            {
                if (!_started || sttSettings == null)
                    return;

                if (!sttSettings.enabled)
                {
                    if (_gate.Phase == PushToTalkPhase.Recording)
                    {
                        StopMic();
                        _gate.Abort();
                        Debug.Log("STT: recording cancelled");
                    }

                    return;
                }

                var key = sttSettings.PushToTalkKey;
                var keyHeld = Input.GetKey(key);
                var listeningBlocked = !CompanionPause.ListeningAllowed(IsPaused());
                var signal = _gate.Tick(keyHeld, _buttonHeld, DebugInputGuard.TextFieldFocused, listeningBlocked);
                if (signal == PushToTalkSignal.Cancelled)
                {
                    _serial++;
                    StopMic();
                    Debug.Log("STT: paused");
                    return;
                }
                if (_gate.Phase == PushToTalkPhase.Recording && _mic.Running)
                {
                    var capped = _mic.Elapsed >= sttSettings.MaxRecordSeconds;
                    if (capped || !_mic.IsStillRecording())
                        signal = _gate.ForceRelease();
                }

                if (signal == PushToTalkSignal.Started)
                    BeginRecording();
                else if (signal == PushToTalkSignal.Released)
                    EndRecording();
            }
            catch (Exception ex)
            {
                StopMic();
                _gate.Abort();
                Debug.Log("STT: " + Short(ChatErrors.Scrub(ex.Message)));
                Note("Speech could not be transcribed.");
            }
        }

        void BeginRecording()
        {
            CancelSpeech();
            var modelFile = sttSettings.ModelFileName;
            var modelPath = Path.Combine(Application.streamingAssetsPath, "Whisper", modelFile);
            if (!File.Exists(modelPath) || new FileInfo(modelPath).Length < 1000000)
            {
                _gate.Abort();
                Debug.Log("STT: model missing " + modelFile);
                Note(WhisperSttProvider.MissingModelMessage(modelFile));
                return;
            }

            var choice = MicChoice.Resolve(sttSettings.microphoneDevice, Microphone.devices);
            if (!choice.Available)
            {
                _gate.Abort();
                Debug.Log("STT: no microphone");
                Note("No microphone is available.");
                return;
            }

            if (choice.FellBack)
                Debug.Log("STT: microphone missing, using default");

            int min;
            int max;
            Microphone.GetDeviceCaps(choice.Device, out min, out max);
            var rate = MicRate.Choose(min, max);
            string note;
            if (!_mic.TryStart(choice.Device, sttSettings.MaxRecordSeconds, rate, out note))
            {
                _gate.Abort();
                Debug.Log("STT: microphone start failed");
                Note(string.IsNullOrEmpty(note) ? "The microphone could not be started." : note);
                return;
            }

            Debug.Log("STT: recording " + _mic.Label + " " + _mic.Rate.ToString(CultureInfo.InvariantCulture)
                + " Hz " + _mic.Channels.ToString(CultureInfo.InvariantCulture) + " ch");
        }

        void EndRecording()
        {
            int rate;
            int channels;
            float seconds;
            var captured = _mic.Stop(out rate, out channels, out seconds);
            var mono = PcmResample.ToMono16k(captured, channels, rate);
            if (mono.Length < SttSettings.MinSamples16k)
            {
                _gate.FinishTranscription(HeldNow());
                Debug.Log("STT: recording too short");
                return;
            }

            Debug.Log("STT: recorded " + seconds.ToString("0.00", CultureInfo.InvariantCulture)
                + " s " + mono.Length.ToString(CultureInfo.InvariantCulture) + " samples");
            Transcribe(mono);
        }

        async void Transcribe(float[] samples)
        {
            var serial = _serial;
            try
            {
                if (_provider == null)
                {
                    var modelFile = sttSettings.ModelFileName;
                    var modelPath = Path.Combine(Application.streamingAssetsPath, "Whisper", modelFile);
                    _provider = new WhisperSttProvider(modelPath, modelFile);
                }

                var result = await _provider.TranscribeAsync(samples, sttSettings.LanguageCode);
                if (!_alive || serial != _serial)
                    return;

                if (IsPaused())
                {
                    if (_gate.Phase != PushToTalkPhase.Idle)
                        _gate.Abort();
                    return;
                }

                if (!string.IsNullOrEmpty(result.LogLine))
                    Debug.Log(result.LogLine);

                _gate.FinishTranscription(HeldNow());
                if (!result.Ok)
                {
                    if (!string.IsNullOrEmpty(result.UserMessage))
                        Note(result.UserMessage);
                    return;
                }

                var submitter = GetComponent<IChatSubmitter>();
                if (submitter == null || !submitter.SubmitUserText(result.Text))
                    Note("A reply is still in progress.");
            }
            catch (Exception ex)
            {
                if (!_alive || serial != _serial)
                    return;

                _gate.FinishTranscription(HeldNow());
                var model = sttSettings != null ? sttSettings.ModelFileName : "model";
                Debug.Log(WhisperNativeLoad.FailureLine(
                    model,
                    ex.GetType().Name,
                    ex.Message,
                    WhisperNativeLoad.PluginDirectory,
                    WhisperNativeLoad.LoadDetail));
                Note("Speech could not be transcribed.");
            }
        }

        bool IsPaused()
        {
            if (!_pauseSearched)
            {
                _pauseSearched = true;
                _pause = GetComponent<ICompanionPause>();
            }

            return _pause != null && _pause.IsPaused;
        }

        bool HeldNow()
        {
            if (!_alive || sttSettings == null)
                return false;
            return _buttonHeld || (Input.GetKey(sttSettings.PushToTalkKey) && !DebugInputGuard.TextFieldFocused);
        }

        void CancelSpeech()
        {
            var speech = GetComponent<ISpeechOutput>();
            if (speech != null)
                speech.Cancel();
        }

        void StopMic()
        {
            _mic.Discard();
        }

        void Note(string message)
        {
            var notes = GetComponent<ISpeechNotes>();
            if (notes != null)
                notes.ShowSpeechNote(message);
        }

        static string Short(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "error";
            if (value.Length > 80)
                return value.Substring(0, 80);
            return value;
        }

        sealed class MicSession
        {
            AudioClip _clip;
            string _device;
            float _startedAt;
            int _rate;
            int _channels;

            public bool Running { get; private set; }
            public int Rate => _rate;
            public int Channels => _channels;
            public float Elapsed => Running ? Time.unscaledTime - _startedAt : 0f;
            public string Label => string.IsNullOrEmpty(_device) ? "default" : TrimDevice(_device);

            public bool TryStart(string device, int seconds, int requestedRate, out string note)
            {
                note = null;
                Discard();
                if (seconds < 1)
                    seconds = 1;
                var clip = Microphone.Start(device, false, seconds, requestedRate);
                if (clip == null)
                {
                    note = "The microphone could not be started. Check that this app is allowed to use the microphone.";
                    return false;
                }

                _clip = clip;
                _device = device;
                _rate = clip.frequency > 0 ? clip.frequency : requestedRate;
                _channels = clip.channels > 0 ? clip.channels : 1;
                _startedAt = Time.unscaledTime;
                Running = true;
                return true;
            }

            public bool IsStillRecording()
            {
                if (!Running)
                    return false;
                return Microphone.IsRecording(_device);
            }

            public float[] Stop(out int rate, out int channels, out float seconds)
            {
                rate = _rate > 0 ? _rate : PcmResample.WhisperRate;
                channels = _channels > 0 ? _channels : 1;
                seconds = Running ? Time.unscaledTime - _startedAt : 0f;
                var clip = _clip;
                var device = _device;
                Running = false;
                _clip = null;
                if (clip == null)
                    return Array.Empty<float>();

                var frames = 0;
                try
                {
                    frames = Microphone.GetPosition(device);
                }
                catch (Exception)
                {
                    frames = 0;
                }

                EndDevice(device);
                if (frames <= 0 || frames > clip.samples)
                    frames = frames > clip.samples ? clip.samples : frames;
                if (frames <= 0)
                {
                    Destroy(clip);
                    return Array.Empty<float>();
                }

                var data = new float[frames * channels];
                var read = clip.GetData(data, 0);
                Destroy(clip);
                return read ? data : Array.Empty<float>();
            }

            public void Discard()
            {
                var device = _device;
                var clip = _clip;
                var wasRunning = Running;
                Running = false;
                _clip = null;
                if (wasRunning)
                    EndDevice(device);
                if (clip != null)
                    Destroy(clip);
            }

            static void EndDevice(string device)
            {
                try
                {
                    if (Microphone.IsRecording(device))
                        Microphone.End(device);
                }
                catch (Exception)
                {
                    // The device can already be gone. Recording still has to stop.
                }
            }

            static string TrimDevice(string device)
            {
                var trimmed = device.Trim();
                if (trimmed.Length > 60)
                    trimmed = trimmed.Substring(0, 60);
                return trimmed;
            }
        }
    }
}
