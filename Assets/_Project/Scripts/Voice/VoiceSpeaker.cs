using System;
using System.Collections.Generic;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Voice
{
    /// <summary>
    /// Speaks chat replies one sentence at a time and drives the talk animation
    /// from the audio, not from the text stream. F6 mutes. Esc, Stop, and a new
    /// message cancel through <see cref="ISpeechOutput.Cancel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceSpeaker : MonoBehaviour, ICompanionStartup, ISpeechOutput
    {
        public const string MutePref = "ai-companion.tts-muted";

        [SerializeField] ChatSettings chatSettings;
        [SerializeField] VoiceSettings voiceSettings;

        readonly SpeechTurn _turn = new SpeechTurn();
        readonly SpeechPlayback _playback = new SpeechPlayback();
        readonly Queue<AudioClip> _clips = new Queue<AudioClip>();
        readonly HashSet<string> _noted = new HashSet<string>();
        AudioSource _source;
        AudioClip _current;
        ITtsProvider _provider;
        CancellationTokenSource _ttsCancel;
        int _requestSerial;
        bool _requestInFlight;
        bool _muted;
        bool _started;
        bool _alive = true;
        bool _talkingApplied;
        double _playUntil;
        float _requestStartedAt;

        public bool IsMuted => _muted;

        public bool IsSpeaking => _playback.ShouldTalk || _requestInFlight || _playback.QueuedCount > 0 || _clips.Count > 0;

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            if (chatSettings == null)
                chatSettings = ScriptableObject.CreateInstance<ChatSettings>();
            if (voiceSettings == null)
                voiceSettings = ScriptableObject.CreateInstance<VoiceSettings>();
            _muted = PlayerPrefs.GetInt(MutePref, 0) != 0;
            EnsureSource();
            var timeout = voiceSettings.timeoutSeconds <= 0f ? 20f : voiceSettings.timeoutSeconds;
            _provider = new XaiTtsProvider(new EnvironmentApiKeySource(), new UnityTtsTransport(this), timeout);
        }

        void Start()
        {
            StartCompanion();
        }

        void OnDestroy()
        {
            _alive = false;
            _turn.Cancel();
            SilencePlayback();
            InvalidateRequest();
            ApplyTalking();
        }

        void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F6) && !DebugInputGuard.IsBlocked(KeyCode.F6))
                    SetMuted(!_muted);

                PumpPlayback();
                PumpSynthesis();
                ApplyTalking();
            }
            catch (Exception ex)
            {
                Note(TtsErrors.Unplayable, "Voice error: " + TtsErrors.Unplayable + ": " + ChatErrors.Scrub(ex.Message));
            }
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            PlayerPrefs.SetInt(MutePref, muted ? 1 : 0);
            if (!muted)
                return;

            _turn.Cancel();
            SilencePlayback();
            InvalidateRequest();
            ApplyTalking();
        }

        public void BeginUtterance()
        {
            SilencePlayback();
            InvalidateRequest();
            _noted.Clear();
            _turn.Begin(SpeechPolicy.ShouldSpeak(chatSettings, voiceSettings, _muted));
            ApplyTalking();
        }

        public void Append(string text)
        {
            _turn.Append(text);
        }

        public void CompleteUtterance()
        {
            _turn.Complete();
        }

        public void Cancel()
        {
            _turn.Cancel();
            SilencePlayback();
            InvalidateRequest();
            ApplyTalking();
        }

        void PumpSynthesis()
        {
            if (_requestInFlight)
            {
                var limit = voiceSettings != null && voiceSettings.timeoutSeconds > 0f ? voiceSettings.timeoutSeconds : 20f;
                if (Time.realtimeSinceStartup - _requestStartedAt > limit + 1f)
                {
                    var screen = ChatErrors.TimedOut("the voice");
                    Note(screen, "Voice error: " + screen);
                    InvalidateRequest();
                }

                return;
            }

            if (_provider == null || _muted)
                return;
            if (!_turn.TryTake(out var generation, out var sentence))
                return;

            _requestInFlight = true;
            _requestStartedAt = Time.realtimeSinceStartup;
            _ttsCancel = new CancellationTokenSource();
            var serial = ++_requestSerial;
            RunSynthesis(BuildRequest(sentence), generation, serial, _ttsCancel.Token);
        }

        async void RunSynthesis(TtsRequest request, int generation, int serial, CancellationToken token)
        {
            try
            {
                var chars = request != null && request.Text != null ? request.Text.Length : 0;
                var askedRate = TtsRequestJson.NormalizeSampleRate(request != null ? request.SampleRate : 24000);
                Debug.Log("Voice: TTS request starting (" + chars.ToString() + " chars, pcm, " + askedRate.ToString() + " Hz)");

                TtsResult result;
                try
                {
                    result = await _provider.Synthesize(request, token);
                }
                catch (Exception ex)
                {
                    var screen = ChatErrors.Unreachable("the voice");
                    result = TtsResult.Fail(screen, "Voice error: " + screen + ": " + ChatErrors.Scrub(ex.Message));
                }

                if (!_alive)
                    return;

                if (!result.Quiet || result.StatusCode != 0 || result.ByteCount > 0)
                    Debug.Log(ResponseLine(result));

                if (serial != _requestSerial)
                    return;

                _requestInFlight = false;
                if (_muted || !_turn.IsCurrent(generation) || result.Quiet)
                    return;

                if (!result.Ok)
                {
                    Note(result.UserMessage, result.LogMessage);
                    if (result.StopQueue)
                        _turn.Abandon();
                    return;
                }

                PlayDecoded(result);
            }
            catch (Exception ex)
            {
                if (!_alive || serial != _requestSerial)
                    return;

                _requestInFlight = false;
                Note(TtsErrors.Unplayable, "Voice error: " + TtsErrors.Unplayable + ": " + ChatErrors.Scrub(ex.Message));
            }
        }

        void PlayDecoded(TtsResult result)
        {
            var rate = TtsRequestJson.NormalizeSampleRate(voiceSettings != null ? voiceSettings.sampleRate : 24000);
            if (!SpeechAudio.TryDecode(result.Audio, result.ContentType, rate, true, out var samples, out var channels, out var decodedRate)
                || samples == null
                || channels <= 0
                || samples.Length == 0
                || samples.Length % channels != 0)
            {
                Note(TtsErrors.Unplayable, "Voice error: " + TtsErrors.Unplayable);
                return;
            }

            var frames = samples.Length / channels;
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("tts", frames, channels, decodedRate, false);
                if (clip == null || !clip.SetData(samples, 0))
                {
                    DestroyClip(clip);
                    Note(TtsErrors.Unplayable, "Voice error: " + TtsErrors.Unplayable);
                    return;
                }
            }
            catch (Exception ex)
            {
                DestroyClip(clip);
                Note(TtsErrors.Unplayable, "Voice error: " + TtsErrors.Unplayable + ": " + ChatErrors.Scrub(ex.Message));
                return;
            }

            Debug.Log("Voice: playing clip " + frames.ToString() + " frames " + decodedRate.ToString() + " Hz " + channels.ToString() + " ch");
            EnqueueAudio(clip);
            ApplyTalking();
        }

        static string ResponseLine(TtsResult result)
        {
            var type = result.ContentType ?? string.Empty;
            type = type.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (type.Length == 0)
                type = "none";
            else if (type.Length > 80)
                type = type.Substring(0, 80);

            var head = string.IsNullOrEmpty(result.Head) ? "empty" : result.Head;
            return "Voice: TTS response HTTP " + result.StatusCode.ToString()
                + " " + type
                + " " + result.ByteCount.ToString()
                + " bytes head " + head;
        }

        TtsRequest BuildRequest(string sentence)
        {
            return new TtsRequest
            {
                Text = sentence,
                VoiceId = voiceSettings != null ? voiceSettings.voiceId : "eve",
                Language = voiceSettings != null ? voiceSettings.language : "en",
                Speed = voiceSettings != null ? voiceSettings.speed : 1f,
                SampleRate = voiceSettings != null ? voiceSettings.sampleRate : 24000
            };
        }

        void EnqueueAudio(AudioClip clip)
        {
            _clips.Enqueue(clip);
            _playback.EnqueueClip();
            if (_playback.Playing)
                return;

            _playback.Pump();
            PlayHead();
        }

        void PumpPlayback()
        {
            if (_source == null || !_playback.Playing || SourceBusy())
                return;

            _playback.ClipFinished();
            if (_playback.Playing)
                PlayHead();
        }

        void PlayHead()
        {
            if (_source == null || _clips.Count == 0)
            {
                _playback.Stop();
                _playUntil = 0d;
                return;
            }

            DestroyClip(_current);
            _current = _clips.Dequeue();
            _source.clip = _current;
            _source.volume = Mathf.Clamp01(voiceSettings != null ? voiceSettings.volume : 1f);
            _source.Play();
            _playUntil = AudioSettings.dspTime + _current.length;
        }

        bool SourceBusy()
        {
            if (_source == null || _current == null)
                return false;
            if (_source.isPlaying)
                return true;
            return _playUntil > 0d && AudioSettings.dspTime + 0.03d < _playUntil;
        }

        void SilencePlayback()
        {
            _playback.Stop();
            _playUntil = 0d;
            if (_source != null)
            {
                _source.Stop();
                _source.clip = null;
            }

            while (_clips.Count > 0)
                DestroyClip(_clips.Dequeue());
            DestroyClip(_current);
            _current = null;
        }

        void InvalidateRequest()
        {
            _requestSerial++;
            _requestInFlight = false;
            if (_ttsCancel == null)
                return;

            _ttsCancel.Cancel();
            _ttsCancel = null;
        }

        void ApplyTalking()
        {
            var talk = _playback.ShouldTalk && SourceBusy();
            if (talk == _talkingApplied)
                return;

            _talkingApplied = talk;
            var animator = GetComponent<IAvatarAnimator>();
            if (animator != null)
                animator.SetTalking(talk);
        }

        void Note(string userMessage, string logMessage)
        {
            if (string.IsNullOrEmpty(userMessage) || !_noted.Add(userMessage))
                return;

            try
            {
                var notes = GetComponent<ISpeechNotes>();
                if (notes != null)
                    notes.ShowSpeechNote(userMessage);
            }
            catch (Exception)
            {
            }

            if (!string.IsNullOrEmpty(logMessage))
                Debug.LogWarning(logMessage);
            else
                Debug.Log("Voice error: " + userMessage);
        }

        void EnsureSource()
        {
            if (_source != null)
                return;

            _source = GetComponent<AudioSource>();
            if (_source == null)
                _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
        }

        static void DestroyClip(AudioClip clip)
        {
            if (clip == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(clip);
            else
                UnityEngine.Object.DestroyImmediate(clip);
        }
    }
}
