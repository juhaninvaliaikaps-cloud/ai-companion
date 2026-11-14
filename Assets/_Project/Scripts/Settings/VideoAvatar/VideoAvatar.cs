using System;
using System.IO;
using AICompanion.Avatar;
using AICompanion.Core;
using AICompanion.Overlay;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    /// <summary>
    /// Optional full-body 2D avatar: video clips drawn in the overlay window in place of
    /// the 3D model. The 3D model is only hidden, never unloaded, so every system that
    /// talks to it keeps working and switching back is instant.
    ///
    /// What plays is decided by <see cref="VideoAvatarDirector"/>: a default idle, optional
    /// idle variations at the idle's loop point, a walk loop while the desktop presence
    /// moves the window, and a walk-stop when the walk ends. This class owns the decoders
    /// and the picture. It changes clips only when the next one shows a picture of its own,
    /// so the previous clip stays on screen until then and nothing blank is drawn.
    ///
    /// The picture is a uGUI RawImage that is not a raycast target. Click-through, drag,
    /// and double-click therefore still follow the visible pixels in the framebuffer, the
    /// same as for the 3D model. No clip's sound is ever played.
    ///
    /// See docs/VIDEO_AVATAR.md.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(215)]
    public sealed class VideoAvatar : MonoBehaviour, IOverlayKeepAwake, IAvatarReactions
    {
        const string ShaderResource = "VideoAvatar";
        const float RetrySeconds = 2f;
        const float HideRescanSeconds = 0.5f;
        const float SyncReportSeconds = 15f;
        const float TestWalkSeconds = 30f;
        const float MaskLow = 0.06f;
        const float MaskHigh = 0.94f;

        static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        static readonly int ColorRectId = Shader.PropertyToID("_ColorRect");
        static readonly int MaskRectId = Shader.PropertyToID("_MaskRect");
        static readonly int MaskSourceId = Shader.PropertyToID("_MaskSource");
        static readonly int MaskDecodeId = Shader.PropertyToID("_MaskDecode");
        static readonly int MaskLowId = Shader.PropertyToID("_MaskLow");
        static readonly int MaskHighId = Shader.PropertyToID("_MaskHigh");

        SettingsPanel _settings;
        AvatarMount _mount;
        IAvatarAnimator _animator;
        Component _animatorObject;
        ISpeechOutput _speech;
        Component _speechObject;
        bool _clipSoundsOn;
        Emotion _lastEmotion = Emotion.Neutral;
        DesktopPresence _presence;

        GameObject _canvasObject;
        Canvas _canvas;
        RawImage _image;
        RectTransform _imageRect;
        Material _material;

        VideoAvatarClipSet _set;
        VideoClipPlayer[] _players;
        VideoAvatarDirector _director;
        int _shown = -1;
        int _shownEntry;
        bool _shownMirrored;
        int _incoming = -1;
        Texture _maskShown;
        float _referenceWidth;
        float _referenceHeight;

        VideoAvatarState _state = VideoAvatarState.Off;
        string _folder = string.Empty;
        string _requested;
        string _error = string.Empty;
        string _status = "3D avatar.";
        string _appData = string.Empty;
        string _persistent = string.Empty;
        Rect _placed;
        Vector2 _portraitHead;
        bool _portraitHeadKnown;
        float _retryIn;
        float _rescanIn;
        float _syncReportIn = SyncReportSeconds;
        bool _paths;
        bool _reload;
        bool _gaitSet;
        bool _shaderWarned;

        public VideoAvatarState State
        {
            get { return _state; }
        }

        /// <summary>One line for the settings panel.</summary>
        public string StatusText
        {
            get { return _status; }
        }

        /// <summary>What she is doing, for the settings panel. Idle when no clip is playing.</summary>
        public VideoAvatarAct Act
        {
            get { return _director != null ? _director.Act : VideoAvatarAct.Idle; }
        }

        /// <summary>
        /// Clip changes need the full frame rate. The default idle does not, so the overlay
        /// may drop to its idle rate while she only stands.
        /// </summary>
        public bool KeepFrameRateActive
        {
            get
            {
                if (_state != VideoAvatarState.Playing || _director == null)
                    return false;
                return _incoming >= 0
                    || _director.Queued >= 0
                    || _director.Act == VideoAvatarAct.Variation
                    || _director.Act == VideoAvatarAct.Reaction
                    || _director.Act == VideoAvatarAct.Pose
                    || _director.Act == VideoAvatarAct.Walk
                    || _director.Act == VideoAvatarAct.WalkStop;
            }
        }

        /// <summary>Reads the folder and video-avatar.json again on the next frame.</summary>
        public void Reload()
        {
            _reload = true;
        }

        /// <summary>Test control: walk left (-1) or right (+1) until stopped, through the desktop presence.</summary>
        public void TestWalk(float direction)
        {
            Bind();
            if (_presence != null && _state == VideoAvatarState.Playing)
                _presence.RequestWalk(direction, TestWalkSeconds);
        }

        /// <summary>Test control: end the walk. The walk-stop clip plays when the folder has one.</summary>
        public void TestStop()
        {
            Bind();
            if (_presence != null)
                _presence.RequestStop();
        }

        /// <summary>
        /// Plays a reaction clip by name ("happy", "concerned", "turn360") once, from the
        /// default idle. False when the folder has no such clip or she is not in the idle.
        /// </summary>
        public bool PlayReaction(string name)
        {
            return RequestReaction(name) != AvatarReactionResult.Refused;
        }

        /// <summary>
        /// A reaction the reply asked for by name (the cue's reaction attribute). It plays from
        /// the default idle, or waits a few seconds for it. Refused unless a clip is playing.
        /// </summary>
        public AvatarReactionResult RequestReaction(string name)
        {
            if (_director == null || _state != VideoAvatarState.Playing)
                return AvatarReactionResult.Refused;
            return _director.Request(name, true);
        }

        /// <summary>Test control: return to the default idle from a held pose, the side idle, or a variation.</summary>
        public void TestFront()
        {
            if (_director != null)
                _director.RequestFront();
        }

        void OnDestroy()
        {
            AvatarReactionCatalog.Clear();
            ReleaseAll();
            if (_presence != null && _gaitSet)
                _presence.ClearGait();
            if (_mount != null && _mount.ModelHidden)
                _mount.SetModelHidden(false);
            if (_material != null)
                Destroy(_material);
            if (_canvasObject != null)
                Destroy(_canvasObject);
        }

        void LateUpdate()
        {
            Bind();
            var settings = _settings != null ? _settings.CurrentSettings : null;
            if (settings == null || !AvatarModeNames.IsVideo(settings.AvatarMode))
            {
                if (_state != VideoAvatarState.Off)
                    SwitchOff();
                return;
            }

            var delta = Time.unscaledDeltaTime;
            var requested = settings.VideoAvatarFolder ?? string.Empty;
            var waiting = _state == VideoAvatarState.Off
                || _state == VideoAvatarState.NoFolder
                || _state == VideoAvatarState.NoClip;
            _retryIn -= delta;
            if (_reload
                || !string.Equals(requested, _requested, StringComparison.Ordinal)
                || (waiting && _retryIn <= 0f))
            {
                Resolve(requested);
            }

            // An emotion she already had before the video showed is not a change. Only a
            // change while the video plays starts a reaction.
            if (_state != VideoAvatarState.Playing && _animatorObject != null)
                _lastEmotion = _animator.Emotion;

            if (_state == VideoAvatarState.Loading)
                TickLoading(delta);

            if (_state == VideoAvatarState.Playing)
                TickPlaying(settings, delta);
        }

        void Bind()
        {
            if (_settings == null)
                _settings = GetComponent<SettingsPanel>();
            if (_mount == null)
                _mount = GetComponent<AvatarMount>();
            if (_presence == null)
                _presence = GetComponent<DesktopPresence>();
            if (_animatorObject == null)
            {
                _animator = GetComponent<IAvatarAnimator>();
                _animatorObject = _animator as Component;
            }
            if (_speechObject == null)
            {
                _speech = GetComponent<ISpeechOutput>();
                _speechObject = _speech as Component;
            }
        }

        /// <summary>Reads the folder once. This runs on a setting change, on Reload, and every two seconds while there is nothing to play.</summary>
        void Resolve(string requested)
        {
            var forced = _reload;
            _reload = false;
            _requested = requested;
            _retryIn = RetrySeconds;
            EnsurePaths();

            var folder = VideoAvatarFolders.Select(requested, _appData, _persistent, Directory.Exists);
            if (string.IsNullOrEmpty(folder))
            {
                ReleaseAll();
                _set = null;
                _folder = string.IsNullOrWhiteSpace(requested)
                    ? VideoAvatarFolders.Primary(_appData, _persistent) ?? string.Empty
                    : requested.Trim();
                SetState(VideoAvatarState.NoFolder);
                return;
            }

            string[] files;
            string manifest = null;
            try
            {
                files = Directory.GetFiles(folder);
                var manifestPath = Path.Combine(folder, VideoAvatarManifest.FileName);
                if (File.Exists(manifestPath))
                    manifest = File.ReadAllText(manifestPath);
            }
            catch (Exception)
            {
                files = Array.Empty<string>();
            }

            var set = VideoAvatarManifest.Resolve(manifest, files);
            _folder = folder;
            if (!set.Found)
            {
                ReleaseAll();
                _set = set;
                SetState(VideoAvatarState.NoClip);
                return;
            }

            var busy = _state == VideoAvatarState.Loading || _state == VideoAvatarState.Playing;
            if (!forced && busy && set.SameAs(_set))
                return;
            // A set that already failed is not retried on its own. Reload or a new folder tries again.
            if (!forced && _state == VideoAvatarState.Failed && set.SameAs(_set))
                return;

            Load(set);
        }

        void Load(VideoAvatarClipSet set)
        {
            ReleaseAll();
            EnsureUi();
            _set = set;
            _players = new VideoClipPlayer[set.Clips.Count];
            _error = string.Empty;
            for (var i = 0; i < set.Warnings.Count; i++)
                Debug.LogWarning("VideoAvatar: " + set.Warnings[i]);

            var idle = EnsurePlayer(set.Idle);
            if (idle == null || idle.Failed)
            {
                Fail(idle != null ? idle.Error : "The default idle could not be created");
                return;
            }

            SetState(VideoAvatarState.Loading);
        }

        /// <summary>The decoders for a clip, created on first use. Null when the clip failed earlier.</summary>
        VideoClipPlayer EnsurePlayer(int index)
        {
            if (_players == null || index < 0 || index >= _players.Length)
                return null;
            if (_players[index] != null)
                return _players[index];

            var player = new VideoClipPlayer(_set.Clips[index]);
            player.Create(transform);
            _players[index] = player;
            return player;
        }

        void TickLoading(float delta)
        {
            var idle = _players != null ? _players[_set.Idle] : null;
            if (idle == null)
            {
                Fail("The default idle was removed");
                return;
            }

            idle.Tick(delta);
            if (idle.Failed)
            {
                Fail(idle.Error);
                return;
            }

            if (idle.ReadyToBegin)
                idle.Begin();
            if (!idle.HasFreshFrame)
                return;

            _director = new VideoAvatarDirector(_set, Environment.TickCount);
            var texture = idle.Texture;
            _referenceWidth = VideoAvatarLayout.DisplayWidth(texture.width, idle.Spec.Mask);
            _referenceHeight = texture.height;
            Show(_set.Idle, false);
            _shownEntry = _director.Entry;
            _rescanIn = 0f;

            // The movement clips load now, in the background, so the first walk does not wait.
            EnsurePlayer(_set.Walk);
            EnsurePlayer(_set.WalkStop);
            EnsurePlayer(_set.SideIdle);

            SetState(VideoAvatarState.Playing);
            Debug.Log("VideoAvatar: playing " + VideoAvatarFiles.FileName(idle.Spec.ColorPath)
                + " " + texture.width + "x" + texture.height + " mask=" + idle.Spec.Mask
                + " clips=" + _set.Clips.Count
                + " folder=" + System.IO.Path.GetDirectoryName(idle.Spec.ColorPath)
                + (_set.FromManifest ? " manifest=" + VideoAvatarManifest.FileName : " manifest=none"));
        }

        void TickPlaying(CompanionSettings settings, float delta)
        {
            if (_players == null || _director == null || _image == null)
            {
                Fail("The video player was removed");
                return;
            }

            var shownEvent = VideoClipEvent.None;
            for (var i = 0; i < _players.Length; i++)
            {
                var player = _players[i];
                if (player == null)
                    continue;

                var clipEvent = player.Tick(delta);
                if (player.Failed)
                {
                    DropClip(i);
                    if (_state != VideoAvatarState.Playing)
                        return;
                    continue;
                }

                if (i == _shown)
                    shownEvent = clipEvent;
            }

            var model = _presence != null ? _presence.Model : null;
            var walking = model != null && model.State == PresenceState.Walking;
            var phase = model != null ? model.StopPhase : PresenceStopPhase.None;
            _director.SetMovement(
                walking,
                model != null && model.FacingRight,
                phase == PresenceStopPhase.Lead,
                phase == PresenceStopPhase.Glide);
            ReadTestKeys();
            ReactToEmotion();
            // JUH-103 phase 2: her mood may stand a silent reaction in for an idle variation.
            _director.MoodClip = CompanionMoodNow.Rest.IdleClip;
            var queuedBefore = _director.Queued;
            _director.Tick(delta);
            if (_director.QueuedIsMood && _director.Queued != queuedBefore)
                Debug.Log(CompanionMoodLooks.LogLine(CompanionMoodNow.Effective, _set.Clips[_director.Queued].Name, "2D idle clip"));

            if (_director.Queued >= 0)
                EnsurePlayer(_director.Queued);
            // Load the pose that follows a reaction while the reaction plays.
            if (_director.Next >= 0)
                EnsurePlayer(_director.Next);
            if (_shown == _director.Clip && _director.Entry == _shownEntry)
            {
                // A reaction does not wait for the idle's loop point. The delivered reactions
                // start in the idle pose, and the idle moves little, so the cut is small.
                if (_director.QueuedIsReaction && QueuedReady())
                    _director.OnQueuedReady();

                if (shownEvent == VideoClipEvent.LoopPoint)
                    _director.OnBoundary(QueuedReady());
                else if (shownEvent == VideoClipEvent.Ended)
                    _director.OnEnded();
            }

            UpdateSwitch();
            if (_state != VideoAvatarState.Playing)
                return;

            RefreshPicture();
            ApplyClipSound(settings);
            ReportSync(delta);
            EnsureCamera();
            Place(settings);
            ApplyGait(settings);
            KeepModelHidden(delta);

            // The clip stands on the bottom edge of the window, so that edge is the ground line.
            // This runs after FootGrounding and before DesktopPresence reads the offset.
            if (_presence != null)
                _presence.SetFootOffset(0f);
        }

        /// <summary>
        /// When the avatar's emotion changes to one the folder has a clip for, that clip
        /// plays once. The emotion comes from the same place the 3D face reads it.
        /// </summary>
        void ReactToEmotion()
        {
            if (_animatorObject == null)
                return;

            var emotion = _animator.Emotion;
            if (emotion == _lastEmotion)
                return;

            _lastEmotion = emotion;
            var name = VideoReactionNames.For(emotion);
            if (name != null)
                _director.Request(name, false);
        }

        bool QueuedReady()
        {
            var queued = _director.Queued;
            if (queued < 0 || _players[queued] == null)
                return false;
            return _players[queued].ReadyToBegin;
        }

        /// <summary>
        /// Moves the picture to the clip the director wants. The clip is started first and
        /// shown only when it has a picture of its own. Until then the previous clip stays.
        /// </summary>
        void UpdateSwitch()
        {
            if (_director.Entry == _shownEntry)
            {
                CancelIncoming();
                return;
            }

            var target = _director.Clip;
            if (target == _shown)
            {
                // The clip on screen was entered again, for example a stop asked twice.
                CancelIncoming();
                var current = _players[target];
                if (current == null)
                    return;
                if (current.Begun && !current.Ended)
                    _shownEntry = _director.Entry;
                else if (current.Begun)
                    current.Park();
                else if (current.ReadyToBegin)
                {
                    current.Begin();
                    _shownEntry = _director.Entry;
                }

                return;
            }

            var next = EnsurePlayer(target);
            if (next == null)
                return;

            if (_incoming != target)
            {
                CancelIncoming();
                if (!next.ReadyToBegin)
                    return;
                next.Begin();
                _incoming = target;
            }

            if (!next.HasFreshFrame)
                return;

            var previous = _shown;
            Show(target, _director.Mirrored);
            _shownEntry = _director.Entry;
            _incoming = -1;
            Retire(previous);
        }

        void CancelIncoming()
        {
            if (_incoming >= 0 && _incoming != _shown && _players[_incoming] != null)
                _players[_incoming].Park();
            _incoming = -1;
        }

        /// <summary>Puts away the clip that just left the screen. A variation is unloaded, because the next one may be a different file.</summary>
        void Retire(int index)
        {
            if (index < 0 || _players[index] == null)
                return;

            // The run is over: report it now, so each line belongs to one clip's run.
            LogSync(_players[index]);

            // Variations and reactions are loaded when they are wanted and unloaded after.
            if (_players[index].Spec.Role == VideoClipRole.IdleVariation || _players[index].Spec.Role == VideoClipRole.Reaction)
            {
                _players[index].Dispose();
                _players[index] = null;
                return;
            }

            _players[index].Park();
        }

        /// <summary>A clip other than the default idle failed. It is never used again, and she carries on without it.</summary>
        void DropClip(int index)
        {
            var player = _players[index];
            var error = player != null ? player.Error : string.Empty;
            if (index == _set.Idle)
            {
                Fail(error);
                return;
            }

            Debug.LogWarning("VideoAvatar: clip \"" + _set.Clips[index].Name + "\" is not used. " + error);
            _director.SetUnavailable(index);
            if (_incoming == index)
                _incoming = -1;

            if (_shown == index)
            {
                // The clip on screen died. The default idle is parked on its first frame, so
                // it can take over in this same frame.
                var idle = _players[_set.Idle];
                if (idle != null && !idle.Begun)
                    idle.Begin();
                Show(_set.Idle, false);
                _shownEntry = _director.Entry;
            }

            if (player != null)
                player.Dispose();
            _players[index] = null;
        }

        /// <summary>
        /// Every few seconds, one log line per clip that has a separate mask clip: how often
        /// the mask was on the picture's frame. This is what tells a slipping outline that
        /// comes from playback apart from one that is in the files.
        /// </summary>
        void ReportSync(float delta)
        {
            _syncReportIn -= delta;
            if (_syncReportIn > 0f)
                return;

            _syncReportIn = SyncReportSeconds;
            if (_players == null)
                return;
            for (var i = 0; i < _players.Length; i++)
                LogSync(_players[i]);
        }

        bool SpeechMuted
        {
            get { return _speech != null && _speechObject != null && _speech.IsMuted; }
        }

        bool Speaking
        {
            get { return _speech != null && _speechObject != null && _speech.IsSpeaking; }
        }

        /// <summary>
        /// Only the clip on screen may sound, at the setting's volume, and never while speech
        /// is muted or she speaks: the clip then runs on silently.
        /// </summary>
        void ApplyClipSound(CompanionSettings settings)
        {
            _clipSoundsOn = settings.PlayClipSounds;
            var muted = SpeechMuted;
            var speaking = Speaking;
            for (var i = 0; i < _players.Length; i++)
            {
                var player = _players[i];
                if (player == null || !player.HasSound)
                    continue;
                player.SetVolume(i == _shown
                    ? VideoClipSound.Volume(settings.PlayClipSounds, settings.ClipSoundVolume, true, muted, speaking)
                    : 0f);
            }
        }

        static void LogSync(VideoClipPlayer player)
        {
            var report = player != null ? player.TakeSyncReport() : null;
            if (report != null)
                Debug.Log("VideoAvatar: " + report);
        }

        void Show(int index, bool mirrored)
        {
            var player = _players[index];
            var files = VideoAvatarFiles.FileName(player.Spec.ColorPath);
            if (player.Spec.Mask == VideoMaskKind.MaskVideo || player.Spec.Mask == VideoMaskKind.MaskImage)
                files += "+" + VideoAvatarFiles.FileName(player.Spec.MaskPath);
            Debug.Log("VideoAvatar: showing " + player.Spec.Name + " act=" + (_director != null ? _director.Act.ToString() : "Idle")
                + " mask=" + player.Spec.Mask + " file=" + files + (mirrored ? " mirrored" : string.Empty));
            if (player.HasSound)
                Debug.Log("VideoAvatar: " + VideoClipSound.Describe(player.Spec.Name, _clipSoundsOn, SpeechMuted, Speaking));
            _shown = index;
            _shownMirrored = mirrored;
            _image.texture = player.Texture;
            ApplyMaterial(player, mirrored);
            _image.enabled = true;
            _placed = new Rect(0f, 0f, 0f, 0f);
        }

        /// <summary>Keeps the picture, the mask, and the mirror in step with the showing clip. All three change in the same frame.</summary>
        void RefreshPicture()
        {
            if (_shown < 0 || _players[_shown] == null)
                return;

            var player = _players[_shown];
            var texture = player.Texture;
            if (texture != null && _image.texture != texture)
                _image.texture = texture;

            var mask = player.MaskTexture;
            if (_material != null && mask != null && mask != _maskShown)
            {
                _maskShown = mask;
                _material.SetTexture(MaskTexId, mask);
            }

            if (_shown == _director.Clip && _director.Entry == _shownEntry && _shownMirrored != _director.Mirrored)
            {
                _shownMirrored = _director.Mirrored;
                ApplyMaterial(player, _shownMirrored);
                _placed = new Rect(0f, 0f, 0f, 0f);
            }
        }

        /// <summary>
        /// Her head in window pixels from the top-left, for the expression portrait. False
        /// unless a clip is playing. Taken from the default idle's rectangle, so it stays put
        /// while she walks, stops, or plays a reaction; it moves only with the size and
        /// position settings.
        /// </summary>
        Rect _bodyRect;
        AICompanion.Overlay.OverlayWindow _overlay;

        /// <summary>
        /// Where she is drawn: the whole window, or (new UI) the window without the room beside and
        /// below her that the chat box uses. Same as the whole window while the new UI is off.
        /// </summary>
        Rect BodyArea()
        {
            if (_overlay == null)
                _overlay = GetComponent<AICompanion.Overlay.OverlayWindow>();
            return _overlay != null ? _overlay.BodyRect : new Rect(0f, 0f, Screen.width, Screen.height);
        }

        /// <summary>
        /// JUH-104: the default idle's rectangle in window pixels from the bottom-left (her feet are
        /// near its bottom). False unless a clip is playing. Used to place the new chat box beside her.
        /// </summary>
        public bool TryGetBodyRect(out Rect body)
        {
            body = _bodyRect;
            return _portraitHeadKnown && _state == VideoAvatarState.Playing && _bodyRect.height > 0f;
        }

        public bool TryGetPortraitHead(out Vector2 head)
        {
            head = _portraitHead;
            return _portraitHeadKnown && _state == VideoAvatarState.Playing;
        }

        void Place(CompanionSettings settings)
        {
            if (_shown < 0 || _imageRect == null || _players[_shown] == null)
                return;

            // The idle clip's own rectangle on screen, with its scale and offsets, not mirrored.
            // It is the same whatever clip shows, so the portrait does not move with the clips.
            var idleRect = new Rect(0f, 0f, 0f, 0f);
            if (_set != null && _set.Idle >= 0)
            {
                var idleSpec = _set.Clips[_set.Idle];
                idleRect = VideoAvatarLayout.ComputeAligned(
                    BodyArea().width,
                    BodyArea().height,
                    _referenceWidth,
                    _referenceHeight,
                    _referenceWidth,
                    _referenceHeight,
                    settings.VideoAvatarSize,
                    settings.VideoAvatarOffsetX,
                    settings.VideoAvatarOffsetY,
                    idleSpec.Scale,
                    idleSpec.OffsetX,
                    idleSpec.OffsetY,
                    false);
                idleRect.position += BodyArea().position;
            }

            _portraitHeadKnown = idleRect.height > 0f;
            _bodyRect = idleRect;
            if (_portraitHeadKnown)
                _portraitHead = PortraitAnchor.VideoHead(idleRect, Screen.height, _set.HeadX, _set.HeadY);

            var texture = _image.texture;
            if (texture == null)
                return;

            var spec = _players[_shown].Spec;
            var body = BodyArea();
            var rect = VideoAvatarLayout.ComputeAligned(
                body.width,
                body.height,
                _referenceWidth,
                _referenceHeight,
                VideoAvatarLayout.DisplayWidth(texture.width, spec.Mask),
                texture.height,
                settings.VideoAvatarSize,
                settings.VideoAvatarOffsetX,
                settings.VideoAvatarOffsetY,
                spec.Scale,
                spec.OffsetX,
                spec.OffsetY,
                _shownMirrored);
            rect.position += body.position;
            if (rect == _placed)
                return;

            _placed = rect;
            _imageRect.anchoredPosition = new Vector2(rect.x, rect.y);
            _imageRect.sizeDelta = new Vector2(rect.width, rect.height);
        }

        /// <summary>
        /// Tells the desktop presence how fast the feet in the clip carry her right now. The
        /// speed follows the clip: zero while she stands at the start or the end of the walk,
        /// full in between, and zero again once the walk-stop has finished its last step.
        /// </summary>
        void ApplyGait(CompanionSettings settings)
        {
            if (_presence == null)
                return;

            var walk = _set.Walk >= 0 ? _players[_set.Walk] : null;
            if (walk == null)
            {
                // No walk clip: the window moves at the 3D speed and the idle slides with it.
                if (_gaitSet)
                {
                    _presence.ClearGait();
                    _gaitSet = false;
                }

                return;
            }

            var walkSpec = _set.Clips[_set.Walk];
            var height = VideoAvatarLayout.DisplayedHeight(
                BodyArea().width,
                BodyArea().height,
                _referenceWidth,
                _referenceHeight,
                settings.VideoAvatarSize,
                walkSpec.Scale);
            var heightsPerSecond = settings.VideoAvatarWalkSpeed > 0f ? settings.VideoAvatarWalkSpeed : walkSpec.Speed;

            var stop = _set.WalkStop >= 0 ? _players[_set.WalkStop] : null;
            var stopMoves = 0f;
            // The manifest's moveEnd is known before the walk-stop has loaded. Using it then
            // keeps a stop on the very first walk from halting the window while she still steps.
            if (stop != null)
                stopMoves = VideoGait.MoveEnd(_set.Clips[_set.WalkStop].MoveEnd, stop.Prepared ? stop.DurationSeconds : 0f);

            var factor = 0f;
            var lead = stopMoves;
            if (_shown == _set.Walk && walk.Begun)
            {
                factor = VideoGait.Factor(walk.Seconds, walkSpec.MoveStart, walkSpec.MoveEnd, walkSpec.MoveRamp, walk.DurationSeconds);
                // A stop waits for the walk to reach its loop point, then for the steps in the walk-stop.
                lead = stop != null ? walk.SecondsToLoopPoint + stopMoves : 0f;
            }
            else if (_shown == _set.WalkStop && stop != null && stop.Begun && !stop.Ended)
            {
                var stopSpec = _set.Clips[_set.WalkStop];
                factor = VideoGait.Factor(stop.Seconds, stopSpec.MoveStart, stopSpec.MoveEnd, stopSpec.MoveRamp, stop.DurationSeconds);
            }

            _presence.SetGait(heightsPerSecond * height * factor, lead, 0f);
            _gaitSet = true;
        }

        /// <summary>Arrow keys drive the test walk while the 2D avatar plays, the settings panel is closed, and no text field has focus.</summary>
        void ReadTestKeys()
        {
            if (DebugInputGuard.TextFieldFocused)
                return;
            // With the panel open the arrows belong to its sliders.
            if (_settings != null && _settings.KeepFrameRateActive)
                return;

            if (Input.GetKeyDown(KeyCode.LeftArrow))
                TestWalk(-1f);
            else if (Input.GetKeyDown(KeyCode.RightArrow))
                TestWalk(1f);
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                TestStop();
            else if (Input.GetKeyDown(KeyCode.UpArrow))
                TestFront();
        }

        void KeepModelHidden(float delta)
        {
            if (_mount == null)
                return;

            if (!_mount.ModelHidden)
            {
                _mount.SetModelHidden(true);
                _rescanIn = HideRescanSeconds;
                return;
            }

            // Parts such as the blush cheeks are added to the model after it loads.
            _rescanIn -= delta;
            if (_rescanIn > 0f)
                return;
            _rescanIn = HideRescanSeconds;
            _mount.ApplyModelHidden();
        }

        void ApplyMaterial(VideoClipPlayer player, bool mirrored)
        {
            if (_material == null)
            {
                var shader = Resources.Load<Shader>(ShaderResource);
                if (shader != null && shader.isSupported)
                {
                    _material = new Material(shader);
                    _material.hideFlags = HideFlags.DontSave;
                }
                else if (!_shaderWarned)
                {
                    _shaderWarned = true;
                    Debug.LogWarning("VideoAvatar: the VideoAvatar shader is missing or unsupported. The clip is shown without a mask.");
                }
            }

            _image.material = _material;
            if (_material == null)
                return;

            // x and y scale the UV, z and w shift it. A mirrored clip reads its picture right to left.
            var colorRect = mirrored ? new Vector4(-1f, 1f, 1f, 0f) : new Vector4(1f, 1f, 0f, 0f);
            var maskRect = colorRect;
            var source = 0f;
            var decode = 0f;
            Texture maskTexture = Texture2D.whiteTexture;
            // A video frame is decoded as sRGB. In a linear project that darkens the mask's
            // greys, so the shader puts them back. A mask picture is loaded linear already.
            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            switch (player.Spec.Mask)
            {
                case VideoMaskKind.MaskImage:
                    source = 1f;
                    if (player.MaskTexture != null)
                        maskTexture = player.MaskTexture;
                    break;
                case VideoMaskKind.MaskVideo:
                    source = 1f;
                    decode = linear ? 1f : 0f;
                    if (player.MaskTexture != null)
                        maskTexture = player.MaskTexture;
                    break;
                case VideoMaskKind.Packed:
                    source = 2f;
                    decode = linear ? 1f : 0f;
                    // Each half is read half a texel in from its edges. Otherwise the filter
                    // blends the picture's last column into the mask's first one, which shows
                    // as a faint line down the side of the clip.
                    var texture = player.Texture;
                    var texel = texture != null && texture.width > 0 ? 1f / texture.width : 0f;
                    var packed = VideoAvatarLayout.PackedRects(texel, mirrored);
                    colorRect = packed.Color;
                    maskRect = packed.Mask;
                    break;
            }

            _maskShown = maskTexture;
            _material.SetTexture(MaskTexId, maskTexture);
            _material.SetVector(ColorRectId, colorRect);
            _material.SetVector(MaskRectId, maskRect);
            _material.SetFloat(MaskSourceId, source);
            _material.SetFloat(MaskDecodeId, decode);
            _material.SetFloat(MaskLowId, MaskLow);
            _material.SetFloat(MaskHighId, MaskHigh);
        }

        void EnsureUi()
        {
            if (_canvasObject != null)
                return;

            _canvasObject = new GameObject("VideoAvatarCanvas");
            _canvasObject.transform.SetParent(transform, false);
            _canvasObject.hideFlags = HideFlags.DontSave;
            _canvas = _canvasObject.AddComponent<Canvas>();
            // Behind the portrait (40), the chat panel (50), settings (80), and the wizard (90).
            _canvas.sortingOrder = 5;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            // No GraphicRaycaster on purpose: the clip must not catch clicks as a rectangle.

            var picture = new GameObject("Clip");
            picture.transform.SetParent(_canvasObject.transform, false);
            _imageRect = picture.AddComponent<RectTransform>();
            _imageRect.anchorMin = new Vector2(0f, 0f);
            _imageRect.anchorMax = new Vector2(0f, 0f);
            _imageRect.pivot = new Vector2(0f, 0f);
            _image = picture.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _image.color = Color.white;
            _image.enabled = false;
            EnsureCamera();
        }

        void EnsureCamera()
        {
            if (_canvas == null)
                return;
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera && _canvas.worldCamera != null)
                return;

            var camera = Camera.main;
            if (camera == null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = camera;
            _canvas.planeDistance = 0.6f;
        }

        void EnsurePaths()
        {
            if (_paths)
                return;
            _paths = true;
            _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? string.Empty;
            _persistent = Application.persistentDataPath ?? string.Empty;
        }

        void Fail(string error)
        {
            _error = error ?? string.Empty;
            ReleaseAll();
            SetState(VideoAvatarState.Failed);
            Debug.LogWarning("VideoAvatar: " + _status);
        }

        void SwitchOff()
        {
            ReleaseAll();
            _requested = null;
            _set = null;
            SetState(VideoAvatarState.Off);
        }

        void SetState(VideoAvatarState state)
        {
            _state = state;
            // The prompt offers her reactions only while the 2D avatar really plays.
            if (state == VideoAvatarState.Playing && _set != null)
                AvatarReactionCatalog.Set(_set.OfferedReactions(), _set.OfferedAdultReactions());
            else
                AvatarReactionCatalog.Clear();
            if (state != VideoAvatarState.Playing)
                _portraitHeadKnown = false;
            _status = state == VideoAvatarState.Playing && _set != null
                ? VideoAvatarStatus.DescribeSet(_set)
                : VideoAvatarStatus.Describe(state, _folder, IdleClip(), _error);
            if (state == VideoAvatarState.NoClip && _set != null)
            {
                for (var i = 0; i < _set.Warnings.Count; i++)
                    _status += " " + _set.Warnings[i];
            }

            if (_mount != null && !VideoAvatarStatus.HidesModel(state) && _mount.ModelHidden)
                _mount.SetModelHidden(false);
        }

        VideoAvatarClip IdleClip()
        {
            if (_set == null || !_set.Found)
                return default(VideoAvatarClip);
            var idle = _set.Clips[_set.Idle];
            return new VideoAvatarClip(idle.Name, idle.ColorPath, idle.MaskPath, idle.Mask);
        }

        /// <summary>Stops every decoder, hides the picture, and gives movement back to the 3D speed. The canvas and the material are kept.</summary>
        void ReleaseAll()
        {
            if (_image != null)
            {
                _image.enabled = false;
                _image.texture = null;
            }

            if (_players != null)
            {
                for (var i = 0; i < _players.Length; i++)
                {
                    if (_players[i] != null)
                        _players[i].Dispose();
                }
            }

            _players = null;
            _director = null;
            _shown = -1;
            _incoming = -1;
            _maskShown = null;
            if (_presence != null && _gaitSet)
                _presence.ClearGait();
            _gaitSet = false;
        }
    }
}
