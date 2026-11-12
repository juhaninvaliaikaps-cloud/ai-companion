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
    public sealed class VoiceSpeaker : MonoBehaviour, ICompanionStartup, ISpeechOutput, ISpeechSuppression
    {
        public const string MutePref = "ai-companion.tts-muted";

        [SerializeField] ChatSettings chatSettings;
        [SerializeField] VoiceSettings voiceSettings;

        readonly SpeechTurn _turn = new SpeechTurn();
        readonly SpeechPlayback _playback = new SpeechPlayback();
        readonly Queue<AudioClip> _clips = new Queue<AudioClip>();
        readonly Queue<DecodedClip> _decoded = new Queue<DecodedClip>();
        readonly HashSet<string> _noted = new HashSet<string>();
        AudioSource _source;
        AudioClip _current;
        ITtsProvider _provider;
        SpeechOrigin _origin;
        bool _originSet;
        CancellationTokenSource _ttsCancel;
        int _requestSerial;
        bool _requestInFlight;
        bool _muted;
        bool _suppressed;
        bool _started;
        bool _alive = true;
        bool _talkingApplied;
        double _playUntil;
        float _requestStartedAt;

        public bool IsMuted => _muted;

        public ChatSettings RuntimeChat
        {
            get
            {
                if (!_started)
                    StartCompanion();
                return chatSettings;
            }
        }

        public VoiceSettings RuntimeVoice
        {
            get
            {
                if (!_started)
                    StartCompanion();
                return voiceSettings;
            }
        }

        public void ApplyPlaybackVolume()
        {
            if (_source != null && voiceSettings != null)
                _source.volume = Mathf.Clamp01(voiceSettings.volume);
        }

        public bool IsSpeaking => _playback.ShouldTalk || _requestInFlight || _playback.QueuedCount > 0 || _clips.Count > 0 || _decoded.Count > 0;

        /// <summary>The engine that will speak the current reply. Empty before the first route.</summary>
        public string ActiveEngineId
        {
            get { return _provider != null ? _provider.ProviderId : string.Empty; }
        }

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            chatSettings = chatSettings != null ? chatSettings.RuntimeCopy() : ScriptableObject.CreateInstance<ChatSettings>();
            voiceSettings = voiceSettings != null ? voiceSettings.RuntimeCopy() : ScriptableObject.CreateInstance<VoiceSettings>();
            _muted = PlayerPrefs.GetInt(MutePref, 0) != 0;
            EnsureSource();
            var timeout = voiceSettings.timeoutSeconds <= 0f ? 20f : voiceSettings.timeoutSeconds;
            _provider = BuildProvider(timeout);
            PrewarmProvider();
        }

        /// <summary>
        /// Rebuilds the provider from the current voice settings. Settings are applied after the
        /// speaker starts, so without this a provider chosen in Settings never took effect and
        /// speech silently used the cloud provider.
        /// </summary>
        public void RefreshProvider()
        {
            if (!_started)
            {
                StartCompanion();
                return;
            }

            var timeout = voiceSettings != null && voiceSettings.timeoutSeconds > 0f
                ? voiceSettings.timeoutSeconds
                : 20f;
            _provider = BuildProvider(timeout);
            PrewarmProvider();
        }

        void EnsureProvider()
        {
            if (!_originSet)
                _origin = OriginFromSettings();
            ApplyUtterance(_origin, false);
        }

        SpeechOrigin OriginFromSettings()
        {
            return new SpeechOrigin
            {
                ChatProviderId = chatSettings != null ? chatSettings.providerId : null,
                Model = chatSettings != null && VoiceRoute.UsesCloudTts(PrivateMode(), chatSettings.providerId)
                    ? chatSettings.model
                    : chatSettings != null ? chatSettings.localModel : null,
                Private = PrivateMode()
            };
        }

        void ApplyUtterance(SpeechOrigin origin, bool log)
        {
            if (!_started)
                StartCompanion();

            _origin = origin;
            _originSet = true;
            var plan = VoiceUtterance.Decide(origin, voiceSettings, System.IO.File.Exists);
            if (voiceSettings != null)
            {
                voiceSettings.provider = plan.EngineId;
                if (plan.Local)
                {
                    if (string.Equals(plan.EngineId, KokoroTtsProvider.Id, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(plan.Voice))
                            voiceSettings.kokoroVoice = plan.Voice;
                    }
                    else if (!string.IsNullOrEmpty(plan.PiperPath))
                    {
                        voiceSettings.piperVoice = plan.PiperPath;
                    }
                }
                else if (!string.IsNullOrEmpty(plan.Voice))
                {
                    voiceSettings.voiceId = plan.Voice;
                }
            }

            var desired = plan.EngineId;
            if (_provider == null || !string.Equals(_provider.ProviderId, desired, System.StringComparison.OrdinalIgnoreCase))
                RefreshProvider();

            if (log)
                Debug.Log(plan.LogLine());
        }

        void PrewarmProvider()
        {
            // Kokoro loads a 310 MB model on first use, so start it with the app when the
            // user has already chosen it. Other providers load nothing.
            var kokoro = _provider as KokoroTtsProvider;
            if (kokoro != null)
                kokoro.Prewarm();
        }

        ITtsProvider BuildProvider(float timeout)
        {
            var mode = voiceSettings != null ? voiceSettings.provider : null;
            if (string.Equals(mode, PiperTtsProvider.Id, System.StringComparison.OrdinalIgnoreCase))
            {
                return new PiperTtsProvider(
                    voiceSettings.piperExe,
                    voiceSettings.piperVoice,
                    new PiperProcessRunner(),
                    timeout);
            }

            if (string.Equals(mode, KokoroTtsProvider.Id, System.StringComparison.OrdinalIgnoreCase))
            {
                // The first request loads a 310 MB model, which takes about 17 s on this
                // machine. A 20 s voice timeout cut it off, so give the process a floor.
                var kokoroTimeout = Mathf.Max(timeout, 120f);
                var session = new KokoroProcessSession(
                    voiceSettings.kokoroPython,
                    voiceSettings.kokoroScript,
                    voiceSettings.kokoroModel,
                    voiceSettings.kokoroVoices,
                    voiceSettings.kokoroVoice,
                    kokoroTimeout);
                return new KokoroTtsProvider(
                    voiceSettings.kokoroPython,
                    voiceSettings.kokoroScript,
                    voiceSettings.kokoroModel,
                    voiceSettings.kokoroVoices,
                    voiceSettings.kokoroVoice,
                    session,
                    timeout);
            }

            return new XaiTtsProvider(new EnvironmentApiKeySource(), new UnityTtsTransport(this), timeout);
        }

        void Start()
        {
            StartCompanion();
        }

        void OnDestroy()
        {
            var disposable = _provider as System.IDisposable;
            if (disposable != null)
                disposable.Dispose();

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

                PumpDecoded();
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
            var changed = _muted != muted;
            _muted = muted;
            PlayerPrefs.SetInt(MutePref, muted ? 1 : 0);
            if (!muted || !changed)
                return;

            _turn.Cancel();
            SilencePlayback();
            InvalidateRequest();
            ApplyTalking();
        }

        public void BeginUtterance()
        {
            BeginUtterance(OriginFromSettings());
        }

        public void BeginUtterance(SpeechOrigin origin)
        {
            SilencePlayback();
            InvalidateRequest();
            _noted.Clear();
            ApplyUtterance(origin, true);
            var blocked = !CompanionPause.SpeechAllowed(_muted, _suppressed);
            var chatId = origin.Private ? ChatDefaults.LocalId : origin.ChatProviderId;
            if (chatSettings != null)
                chatSettings.providerId = string.IsNullOrWhiteSpace(chatId) ? chatSettings.providerId : chatId;
            _turn.Begin(SpeechPolicy.ShouldSpeak(chatSettings, voiceSettings, blocked, origin.Private));
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

        public void SetSuppressed(bool suppressed)
        {
            if (_suppressed == suppressed)
                return;

            _suppressed = suppressed;
            if (CompanionPause.SpeechAllowed(_muted, _suppressed))
                return;

            _turn.Cancel();
            SilencePlayback();
            InvalidateRequest();
            ApplyTalking();
        }

        bool PrivateMode()
        {
            var privacy = GetComponent<IPrivacyMode>();
            return IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
        }

        void PumpSynthesis()
        {
            if (!SpeechPolicy.ShouldSpeak(chatSettings, voiceSettings, false, PrivateMode()))
            {
                if (_turn.Accepting || _turn.PendingCount > 0 || _requestInFlight)
                {
                    _turn.Cancel();
                    SilencePlayback();
                    InvalidateRequest();
                    ApplyTalking();
                }

                return;
            }

            if (_requestInFlight)
            {
                var limit = voiceSettings != null && voiceSettings.timeoutSeconds > 0f ? voiceSettings.timeoutSeconds : 20f;
                // Kokoro loads its model on the first request, which takes about 17 s here.
                if (voiceSettings != null && string.Equals(voiceSettings.provider, KokoroTtsProvider.Id, System.StringComparison.OrdinalIgnoreCase))
                    limit = Mathf.Max(limit, 150f);
                if (Time.realtimeSinceStartup - _requestStartedAt > limit + 1f)
                {
                    var screen = ChatErrors.TimedOut("the voice");
                    Note(screen, "Voice error: " + screen);
                    InvalidateRequest();
                }

                return;
            }

            EnsureProvider();
            if (_provider == null || !CompanionPause.SpeechAllowed(_muted, _suppressed))
                return;
            if (BlocksCloud(_origin) && !VoiceRoute.IsLocalEngine(_provider.ProviderId))
            {
                Debug.Log(VoiceUtterance.Decide(_origin, voiceSettings, System.IO.File.Exists).LogLine() + " blocked=cloud");
                return;
            }

            if (!_turn.TryTake(out var generation, out var sentence))
                return;

            _requestInFlight = true;
            _requestStartedAt = Time.realtimeSinceStartup;
            _ttsCancel = new CancellationTokenSource();
            var serial = ++_requestSerial;
            RunSynthesis(BuildRequest(sentence), generation, serial, _ttsCancel.Token);
        }

        static bool BlocksCloud(SpeechOrigin origin)
        {
            return !VoiceRoute.UsesCloudTts(origin.Private, origin.ChatProviderId);
        }

        async void RunSynthesis(TtsRequest request, int generation, int serial, CancellationToken token)
        {
            try
            {
                if (BlocksCloud(_origin) && (_provider == null || !VoiceRoute.IsLocalEngine(_provider.ProviderId)))
                {
                    Debug.Log(VoiceUtterance.Decide(_origin, voiceSettings, System.IO.File.Exists).LogLine() + " blocked=cloud");
                    _requestInFlight = false;
                    return;
                }

                var chars = request != null && request.Text != null ? request.Text.Length : 0;
                Debug.Log(RequestLine(chars));

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
                if (!CompanionPause.SpeechAllowed(_muted, _suppressed) || !_turn.IsCurrent(generation) || result.Quiet)
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
            if (!SpeechAudio.TryDecode(result.Audio, result.ContentType, rate, true, out var samples, out var channels, out var decodedRate, out var reason)
                || samples == null
                || channels <= 0
                || samples.Length == 0
                || samples.Length % channels != 0)
            {
                if (string.IsNullOrEmpty(reason))
                    reason = "decoded samples were empty";
                Reject(result.ContentType, result.ByteCount, reason);
                return;
            }

            _decoded.Enqueue(new DecodedClip
            {
                Samples = samples,
                Channels = channels,
                Rate = decodedRate,
                ContentType = result.ContentType,
                ByteCount = result.ByteCount
            });
        }

        void PumpDecoded()
        {
            while (_decoded.Count > 0)
            {
                var decoded = _decoded.Dequeue();
                if (!TryBuildClip(decoded, out var clip, out var reason))
                {
                    Reject(decoded.ContentType, decoded.ByteCount, reason);
                    continue;
                }

                var frames = decoded.Samples.Length / decoded.Channels;
                Debug.Log("Voice: playing clip " + frames.ToString() + " frames " + decoded.Rate.ToString() + " Hz " + decoded.Channels.ToString() + " ch");
                EnqueueAudio(clip);
            }
        }

        static bool TryBuildClip(DecodedClip decoded, out AudioClip clip, out string reason)
        {
            clip = null;
            reason = "AudioClip.Create returned no clip";
            if (decoded.Samples == null || decoded.Channels <= 0 || decoded.Rate <= 0)
            {
                reason = "decoded samples were empty";
                return false;
            }

            var frames = decoded.Samples.Length / decoded.Channels;
            if (frames <= 0)
            {
                reason = "decoded samples were empty";
                return false;
            }

            try
            {
                clip = AudioClip.Create("tts", frames, decoded.Channels, decoded.Rate, false);
                if (clip == null || clip.channels <= 0 || clip.samples <= 0)
                {
                    DestroyClip(clip);
                    clip = null;
                    reason = "AudioClip.Create returned an empty clip. Unity audio is off in Project Settings > Audio";
                    return false;
                }

                if (!clip.SetData(decoded.Samples, 0))
                {
                    DestroyClip(clip);
                    clip = null;
                    reason = "AudioClip.SetData returned false (" + frames.ToString() + " frames, " + decoded.Rate.ToString() + " Hz, " + decoded.Channels.ToString() + " ch)";
                    return false;
                }
            }
            catch (Exception ex)
            {
                DestroyClip(clip);
                clip = null;
                reason = "AudioClip.Create failed: " + ChatErrors.Scrub(ex.Message);
                return false;
            }

            reason = string.Empty;
            return true;
        }

        void Reject(string contentType, int byteCount, string reason)
        {
            Debug.Log("Voice: rejected audio (" + ContentTypeLabel(contentType) + ", " + byteCount.ToString() + " bytes): " + reason);
            Note(TtsErrors.Unplayable, "Voice error: " + TtsErrors.Unplayable + ": " + reason);
        }

        static string ResponseLine(TtsResult result)
        {
            var type = ContentTypeLabel(result.ContentType);
            var head = string.IsNullOrEmpty(result.Head) ? "empty" : result.Head;
            return "Voice: TTS response HTTP " + result.StatusCode.ToString()
                + " " + type
                + " " + result.ByteCount.ToString()
                + " bytes head " + head;
        }

        static string ContentTypeLabel(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return "none";
            var type = contentType.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (type.Length == 0)
                return "none";
            if (type.Length > 80)
                type = type.Substring(0, 80);
            return type;
        }

        string RequestLine(int chars)
        {
            // JUH-112: the label used to say xAI for every engine that was not Piper, so Kokoro
            // lines looked like cloud requests in Player.log. It now names the real engine.
            var engine = _provider != null ? _provider.ProviderId : string.Empty;
            return VoiceUtterance.RequestLine(engine, voiceSettings, chars);
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
            _decoded.Clear();
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

        struct DecodedClip
        {
            public float[] Samples;
            public int Channels;
            public int Rate;
            public string ContentType;
            public int ByteCount;
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
