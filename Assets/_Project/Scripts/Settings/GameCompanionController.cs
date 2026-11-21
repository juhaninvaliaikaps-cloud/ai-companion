using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using AICompanion.Core;
using AICompanion.GameCompanion;
using AICompanion.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-47: the game companion at runtime. It owns the feeds (the GSI listener for CS2 and
    /// Dota 2, the League poller), turns posts into game events, and passes each one through
    /// <see cref="GameCommentarySession"/>. Then it asks the cloud AI API for a line in her
    /// personality, or says a pre-written one (JUH-49). Thin on purpose; the decisions are in
    /// GameCompanion and tested there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameCompanionController : MonoBehaviour
    {
        public const float LeaguePollSeconds = 1f;
        public const float LeagueIdlePollSeconds = 5f;
        public const float AnimationIntensity = 0.7f;

        readonly GameCommentarySession _session = new GameCommentarySession();
        readonly Cs2GsiReader _cs2 = new Cs2GsiReader();
        readonly Dota2GsiReader _dota = new Dota2GsiReader();
        readonly LeagueLiveReader _league = new LeagueLiveReader();
        readonly List<GameEvent> _events = new List<GameEvent>();

        GsiListener _listener;
        int _listenerPort;
        string _listenerToken = string.Empty;
        Coroutine _leaguePoll;
        bool _leagueConnected;
        CancellationTokenSource _cancel;
        bool _alive;
        string _status = string.Empty;
        CommentaryMode _mode = CommentaryMode.Off;
        string _modeReason = string.Empty;
        string _frequencyName;

        /// <summary>Bumped when the status changes, so the Games section can redraw.</summary>
        public int Revision { get; private set; }

        public CommentaryMode Mode
        {
            get { return _mode; }
        }

        /// <summary>Why the mode is not "model first", or empty.</summary>
        public string ModeReason
        {
            get { return _modeReason; }
        }

        public string StatusLine
        {
            get { return _status; }
        }

        public bool Muted
        {
            get { return _session.Pacer.IsMuted(DateTime.UtcNow); }
        }

        public bool ListenerRunning
        {
            get { return _listener != null && _listener.Running; }
        }

        public bool LeagueConnected
        {
            get { return _leagueConnected; }
        }

        void OnEnable()
        {
            _alive = true;
        }

        void OnDisable()
        {
            _alive = false;
            StopAll();
        }

        void OnDestroy()
        {
            _alive = false;
            StopAll();
        }

        void Update()
        {
            var panel = GetComponent<SettingsPanel>();
            var settings = panel != null ? panel.CurrentSettings : null;
            if (settings == null)
                return;

            var chat = GetComponent<ChatPanel>();
            var privacy = GetComponent<IPrivacyMode>();
            string reason;
            var mode = CommentaryGate.Decide(settings.GameCompanion, settings.GameCloudConsent, chat != null && chat.CloudReady,
                privacy != null && privacy.PrivateMode, out reason);
            if (mode != _mode || reason != _modeReason)
            {
                if (mode == CommentaryMode.Off)
                    _session.Cancel();
                _mode = mode;
                _modeReason = reason;
                Touch(mode == CommentaryMode.Off ? reason : _status);
            }

            if (!ReferenceEquals(settings.GameFrequency, _frequencyName))
            {
                _frequencyName = settings.GameFrequency;
                _session.Pacer.Frequency = GameCommentarySession.FrequencyFor(_frequencyName);
            }

            KeepListener(settings, mode != CommentaryMode.Off && (settings.GameCs2 || settings.GameDota2));
            KeepLeague(mode != CommentaryMode.Off && settings.GameLeague);
            if (mode == CommentaryMode.Off)
                return;

            DrainListener(settings);
        }

        // ---- The feeds ---------------------------------------------------------------------

        void KeepListener(CompanionSettings settings, bool wanted)
        {
            var token = settings.GsiToken ?? string.Empty;
            wanted = wanted && token.Length > 0;
            if (_listener != null && (!wanted || _listenerPort != settings.GsiPort || _listenerToken != token))
            {
                _listener.Dispose();
                _listener = null;
                _cs2.Reset();
                _dota.Reset();
            }

            if (!wanted || _listener != null)
                return;

            _listener = new GsiListener(settings.GsiPort, token);
            _listenerPort = settings.GsiPort;
            _listenerToken = token;
            if (_listener.Start())
            {
                Touch("Listening for CS2 and Dota 2 on port " + settings.GsiPort + ".");
            }
            else
            {
                Touch("Couldn't listen on port " + settings.GsiPort + ". Another app may be using it.");
                Debug.LogWarning("Game companion: GSI listener failed on port " + settings.GsiPort);
            }
        }

        void DrainListener(CompanionSettings settings)
        {
            if (_listener == null || !_listener.Running)
                return;
            string body;
            var guard = 0;
            while (guard++ < GsiListener.MaxPending && _listener.Pending.TryDequeue(out body))
            {
                var now = DateTime.UtcNow;
                var game = GsiRouting.GameOf(body);
                if (game == GameTitle.CounterStrike2 && settings.GameCs2)
                    Handle(_cs2.Read(body, now));
                else if (game == GameTitle.Dota2 && settings.GameDota2)
                    Handle(_dota.Read(body, now));
            }
        }

        void KeepLeague(bool wanted)
        {
            if (wanted && _leaguePoll == null && _alive)
                _leaguePoll = StartCoroutine(PollLeague());
            else if (!wanted && _leaguePoll != null)
            {
                StopCoroutine(_leaguePoll);
                _leaguePoll = null;
                SetLeagueConnected(false);
            }
        }

        IEnumerator PollLeague()
        {
            while (_alive)
            {
                if (string.IsNullOrEmpty(_league.Me))
                {
                    yield return Get(LeagueLiveReader.ActivePlayerUrl);
                    if (_lastBody == null)
                    {
                        SetLeagueConnected(false);
                        yield return new WaitForSecondsRealtime(LeagueIdlePollSeconds);
                        continue;
                    }

                    _league.SetActivePlayer(_lastBody);
                    yield return Get(LeagueLiveReader.PlayerListUrl);
                    if (_lastBody != null)
                        _league.SetPlayers(_lastBody);
                }

                yield return Get(LeagueLiveReader.EventsUrl);
                if (_lastBody == null)
                {
                    SetLeagueConnected(false);
                    yield return new WaitForSecondsRealtime(LeagueIdlePollSeconds);
                    continue;
                }

                SetLeagueConnected(true);
                Handle(_league.Read(_lastBody, DateTime.UtcNow));
                yield return new WaitForSecondsRealtime(LeaguePollSeconds);
            }

            _leaguePoll = null;
        }

        string _lastBody;

        /// <summary>One GET to the League client; the body lands in <see cref="_lastBody"/>, or null.</summary>
        IEnumerator Get(string url)
        {
            _lastBody = null;
            using (var request = UnityWebRequest.Get(url))
            {
                request.certificateHandler = new LeagueLocalCertificate();
                request.disposeCertificateHandlerOnDispose = true;
                request.timeout = 2;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                    _lastBody = request.downloadHandler != null ? request.downloadHandler.text : null;
            }
        }

        void SetLeagueConnected(bool connected)
        {
            if (connected == _leagueConnected)
                return;
            _leagueConnected = connected;
            if (!connected)
                _league.Reset();
            Touch(connected ? "League of Legends match found." : "Waiting for a League of Legends match.");
        }

        /// <summary>
        /// Riot's Live Client Data API on 127.0.0.1:2999 uses a certificate from Riot's own root,
        /// which Windows doesn't trust. This handler is set only on requests to that loopback
        /// address (the <see cref="LeagueLiveReader"/> URLs), never on any other request.
        /// Open item: pin Riot's root certificate (riotgames.pem) instead of trusting loopback.
        /// </summary>
        sealed class LeagueLocalCertificate : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                return certificateData != null && certificateData.Length > 0;
            }
        }

        // ---- Events to lines ---------------------------------------------------------------

        void Handle(List<GameEvent> events)
        {
            if (events == null || events.Count == 0)
                return;
            for (var i = 0; i < events.Count; i++)
                HandleOne(events[i]);
        }

        void HandleOne(GameEvent e)
        {
            var now = DateTime.UtcNow;
            var voice = CurrentVoice();
            var step = _session.Next(e, _mode, voice, now);
            if (step.Nothing)
                return;

            var chat = GetComponent<ChatPanel>();
            if (chat == null || !chat.CanSayGameLine)
            {
                // She is answering the user: the game waits.
                _session.Cancel();
                return;
            }

            if (step.Source == CommentarySource.PreWritten)
            {
                Say(chat, step.Reaction, false);
                return;
            }

            RequestModelLine(chat, step, voice);
        }

        async void RequestModelLine(ChatPanel chat, GameCommentaryStep step, GameVoice voice)
        {
            if (_cancel != null)
                _cancel.Dispose();
            _cancel = new CancellationTokenSource();
            var started = Time.realtimeSinceStartup;
            var timeout = (float)CommentaryRoute.ModelTimeoutSeconds;
            AICompanion.AI.ChatResult result;
            try
            {
                result = await chat.RequestGameLine(CommentaryPrompt.GameText(voice), step.Turn, CommentaryPrompt.MaxTokens,
                    CommentaryPrompt.Temperature, timeout, _cancel.Token);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Game companion: model line failed (" + ex.GetType().Name + ")");
                result = new AICompanion.AI.ChatResult(false, string.Empty, AICompanion.AI.ChatErrorKind.Unreachable, string.Empty, string.Empty);
            }

            if (!_alive || _mode == CommentaryMode.Off)
            {
                _session.Cancel();
                return;
            }

            bool fromModel;
            var elapsed = Time.realtimeSinceStartup - started;
            var line = _session.Resolve(step, result.Ok, result.Text, elapsed, DateTime.UtcNow, out fromModel);
            if (!fromModel)
                Debug.Log("Game companion: pre-written line instead of the model (" + (result.Ok ? "late or empty" : result.LogMessage) + ")");
            if (!line.IsEmpty && chat != null && chat.CanSayGameLine)
                Say(chat, line, fromModel);
        }

        void Say(ChatPanel chat, GameReaction reaction, bool fromModel)
        {
            var cue = new AnimationCue(reaction.Face, AnimationIntensity, reaction.Gesture ?? string.Empty);
            if (chat.SayGameLine(reaction.Text, cue, fromModel))
                Touch((fromModel ? "She said: " : "She said (pre-written): ") + reaction.Text);
        }

        /// <summary>Her game voice now, for the Games section.</summary>
        public GameVoice Voice
        {
            get { return CurrentVoice(); }
        }

        /// <summary>Her game voice from the active persona's Pisara sliders and relationship role.</summary>
        GameVoice CurrentVoice()
        {
            var panel = GetComponent<SettingsPanel>();
            if (panel == null)
                return GameVoice.Cool;
            try
            {
                var view = panel.PisaraView();
                var sheet = panel.ActiveSheet();
                var role = sheet != null && sheet.relationship != null ? sheet.relationship.role : null;
                return GameVoices.For(view.Axes, role);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Game companion: couldn't read her temperament (" + ex.GetType().Name + ")");
                return GameVoice.Cool;
            }
        }

        // ---- Settings actions --------------------------------------------------------------

        public void MuteFor(TimeSpan duration)
        {
            _session.Pacer.MuteFor(duration, DateTime.UtcNow);
            Touch("Quiet for " + Math.Round(duration.TotalMinutes) + " minutes.");
        }

        public void Unmute()
        {
            _session.Pacer.Unmute();
            Touch("She can talk about your game again.");
        }

        public static IList<string> SteamRoots()
        {
            return SteamLibraries.RootCandidates(WindowsSteamPath.Read());
        }

        public GsiInstallResult Install(GameTitle game, CompanionSettings settings)
        {
            var result = GsiInstaller.Install(game, SteamRoots(), settings.GsiPort, settings.GsiToken, new DiskGameFiles());
            Debug.Log("Game companion: setup " + game + " = " + result.Status);
            Touch(result.Message);
            return result;
        }

        public GsiInstallResult Remove(GameTitle game)
        {
            var result = GsiInstaller.Remove(game, SteamRoots(), new DiskGameFiles());
            Debug.Log("Game companion: remove " + game + " = " + result.Status);
            Touch(result.Message);
            return result;
        }

        public bool IsInstalled(GameTitle game, CompanionSettings settings)
        {
            return settings != null && GsiInstaller.IsInstalled(game, SteamRoots(), settings.GsiPort, settings.GsiToken, new DiskGameFiles());
        }

        void StopAll()
        {
            if (_listener != null)
            {
                _listener.Dispose();
                _listener = null;
            }

            if (_leaguePoll != null)
            {
                StopCoroutine(_leaguePoll);
                _leaguePoll = null;
            }

            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
                _cancel = null;
            }

            _session.Cancel();
            _leagueConnected = false;
        }

        void Touch(string status)
        {
            _status = status ?? string.Empty;
            Revision++;
        }
    }
}
