using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace AICompanion.Settings
{
    /// <summary>
    /// The decoders for one clip: its picture and, when the mask is a second clip, its
    /// mask. Both are started, paused, and rewound together, and the mask is moved back
    /// onto the picture's frame when it drifts.
    ///
    /// A clip is never shown from a stale picture. After <see cref="Begin"/> it counts as
    /// fresh only once both decoders report a frame from this run. After it is put away
    /// with <see cref="Park"/> it is rewound in the background, ready for the next time.
    /// The clip's sound is never played.
    /// </summary>
    public sealed class VideoClipPlayer
    {
        const float PrepareTimeoutSeconds = 20f;
        // A rewind is waited for only this long. A leftover picture is kept off the screen
        // by the freshness test in any case, so a slow rewind must not delay a walk.
        const float SeekTimeoutSeconds = 0.2f;
        const float FreshTimeoutSeconds = 1.5f;
        const float ResyncCooldownSeconds = 0.5f;

        readonly VideoClipSpec _spec;
        VideoPlayer _color;
        VideoPlayer _mask;
        Texture2D _maskImage;
        bool _colorPrepared;
        bool _maskPrepared;
        bool _colorSeeking;
        bool _maskSeeking;
        bool _begun;
        bool _ended;
        bool _boundarySent;
        bool _loopPointHit;
        long _lastFrame = -1;
        float _preparingFor;
        float _seekingFor;
        float _sinceBegin;
        float _resyncIn;
        float _maskSpeed = 1f;
        readonly VideoSyncCounter _sync = new VideoSyncCounter();

        public VideoClipPlayer(VideoClipSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public VideoClipSpec Spec
        {
            get { return _spec; }
        }

        public bool Created
        {
            get { return _color != null; }
        }

        public bool Failed { get; private set; }

        public string Error { get; private set; } = string.Empty;

        /// <summary>Both decoders are loaded.</summary>
        public bool Prepared
        {
            get { return Created && !Failed && _colorPrepared && _maskPrepared; }
        }

        /// <summary>Loaded and at its first frame, so <see cref="Begin"/> can start it.</summary>
        public bool ReadyToBegin
        {
            get { return Prepared && !_begun && (!Seeking || _seekingFor > SeekTimeoutSeconds); }
        }

        /// <summary>True between <see cref="Begin"/> and <see cref="Park"/>.</summary>
        public bool Begun
        {
            get { return _begun; }
        }

        /// <summary>A play-once clip that has reached its end and is holding its last picture.</summary>
        public bool Ended
        {
            get { return _ended; }
        }

        bool Seeking
        {
            get { return _colorSeeking || _maskSeeking; }
        }

        public Texture Texture
        {
            get { return _color != null ? _color.texture : null; }
        }

        /// <summary>The mask to draw with: the mask clip's picture, the mask image, or null when the clip needs none.</summary>
        public Texture MaskTexture
        {
            get
            {
                if (_mask != null)
                    return _mask.texture;
                return _maskImage;
            }
        }

        public long Frame
        {
            get { return _color != null ? _color.frame : -1; }
        }

        public long FrameCount
        {
            get { return _color != null ? (long)_color.frameCount : 0; }
        }

        public float FrameRate
        {
            get { return _color != null ? _color.frameRate : 0f; }
        }

        /// <summary>Seconds into the clip.</summary>
        public float Seconds
        {
            get { return _color != null ? (float)_color.time : 0f; }
        }

        public float DurationSeconds
        {
            get { return _color != null ? (float)_color.length : 0f; }
        }

        public float SecondsToLoopPoint
        {
            get { return VideoClipProgress.SecondsToLoopPoint(Frame, FrameCount, FrameRate); }
        }

        /// <summary>
        /// True once the clip shows a picture from the current run, in the picture and in
        /// the mask. If a decoder never reports a frame, a short timeout lets it through
        /// so the avatar cannot get stuck on the previous clip.
        /// </summary>
        public bool HasFreshFrame
        {
            get
            {
                if (!_begun || _color == null || _color.texture == null)
                    return false;
                if (_mask != null && _mask.texture == null)
                    return false;
                if (_sinceBegin > FreshTimeoutSeconds)
                    return true;

                var rate = FrameRate;
                if (!VideoClipProgress.IsFresh(_color.frame, _sinceBegin, rate))
                    return false;
                return _mask == null || VideoClipProgress.IsFresh(_mask.frame, _sinceBegin, rate);
            }
        }

        /// <summary>Creates the decoders under <paramref name="parent"/> and starts loading.</summary>
        public void Create(Transform parent)
        {
            if (Created || Failed)
                return;

            if (_spec.Mask == VideoMaskKind.MaskImage && !LoadMaskImage(_spec.MaskPath))
            {
                Fail("The mask picture " + VideoAvatarFiles.FileName(_spec.MaskPath) + " could not be read");
                return;
            }

            _color = CreateDecoder(parent, "VideoAvatar " + _spec.Name, _spec.ColorPath);
            _maskPrepared = _spec.Mask != VideoMaskKind.MaskVideo;
            if (_spec.Mask == VideoMaskKind.MaskVideo)
                _mask = CreateDecoder(parent, "VideoAvatar " + _spec.Name + " mask", _spec.MaskPath);

            _preparingFor = 0f;
            _color.Prepare();
            if (_mask != null)
                _mask.Prepare();
        }

        /// <summary>Starts the picture and the mask together from the first frame.</summary>
        public void Begin()
        {
            if (!Prepared)
                return;

            _begun = true;
            _ended = false;
            _boundarySent = false;
            _loopPointHit = false;
            _lastFrame = -1;
            _sinceBegin = 0f;
            _resyncIn = ResyncCooldownSeconds;
            _sync.Restart();
            if (_mask != null && !Mathf.Approximately(_maskSpeed, 1f) && _mask.canSetPlaybackSpeed)
                _mask.playbackSpeed = 1f;
            _maskSpeed = 1f;
            _color.Play();
            if (_mask != null)
                _mask.Play();
        }

        /// <summary>Pauses both decoders on the picture they show now.</summary>
        public void Hold()
        {
            if (_color != null)
                _color.Pause();
            if (_mask != null)
                _mask.Pause();
        }

        /// <summary>Takes the clip off duty: pauses it and rewinds it, so the next <see cref="Begin"/> starts clean.</summary>
        public void Park()
        {
            if (!Created || Failed)
                return;

            // A clip that was never started is still on its first frame. Nothing to rewind.
            var wasBegun = _begun;
            _begun = false;
            _ended = false;
            Hold();
            if (!wasBegun || !_colorPrepared || !_maskPrepared)
                return;

            _seekingFor = 0f;
            _colorSeeking = true;
            _color.frame = 0;
            if (_mask != null)
            {
                _maskSeeking = true;
                _mask.frame = 0;
            }
        }

        /// <summary>
        /// Call once a frame. Returns what happened to a clip that is playing: it reached its
        /// loop point, or a play-once clip reached its end and is now held.
        /// </summary>
        public VideoClipEvent Tick(float deltaSeconds)
        {
            if (!Created || Failed)
                return VideoClipEvent.None;

            if (!Prepared)
            {
                _preparingFor += deltaSeconds;
                if (_preparingFor > PrepareTimeoutSeconds)
                    Fail(VideoAvatarFiles.FileName(_spec.ColorPath) + " did not load within " + Mathf.RoundToInt(PrepareTimeoutSeconds) + " seconds");
                return VideoClipEvent.None;
            }

            if (Seeking)
                _seekingFor += deltaSeconds;
            if (!_begun || _ended)
                return VideoClipEvent.None;

            _sinceBegin += deltaSeconds;
            var frame = _color.frame;
            var count = (long)_color.frameCount;

            // Until the decoder reports a frame from this run, its frame number can be a
            // leftover from before the rewind. Nothing is decided on a leftover.
            var started = VideoClipProgress.IsFresh(frame, _sinceBegin, FrameRate) || _sinceBegin > FreshTimeoutSeconds;
            SyncMask(frame, count, started, deltaSeconds);
            var result = VideoClipEvent.None;
            if (!_spec.Loop)
            {
                // Stop just short of the end. A decoder that runs out can drop its picture,
                // and the last picture has to stay on screen.
                if (_loopPointHit || (started && VideoClipProgress.AtEnd(frame, count)))
                {
                    Hold();
                    _ended = true;
                    result = VideoClipEvent.Ended;
                }

                return result;
            }

            if (!started)
                return result;

            if (VideoClipProgress.Wrapped(_lastFrame, frame))
            {
                // The loop has started again. If the last frames were skipped, say so now.
                if (!_boundarySent)
                    result = VideoClipEvent.LoopPoint;
                _boundarySent = false;
            }
            else if (!_boundarySent && VideoClipProgress.NearLoopPoint(frame, count))
            {
                _boundarySent = true;
                result = VideoClipEvent.LoopPoint;
            }

            if (frame >= 0)
                _lastFrame = frame;
            return result;
        }

        public void Dispose()
        {
            DestroyDecoder(_color);
            DestroyDecoder(_mask);
            _color = null;
            _mask = null;
            _colorPrepared = false;
            _maskPrepared = false;
            _begun = false;
            if (_maskImage != null)
                UnityEngine.Object.Destroy(_maskImage);
            _maskImage = null;
        }

        /// <summary>
        /// True when the picture and the mask come from two decoders, which can drift.
        /// A packed clip, a still mask, and a clip with its own alpha cannot.
        /// </summary>
        public bool HasSeparateMaskClip
        {
            get { return _mask != null; }
        }

        /// <summary>
        /// How well the mask kept to the picture since the last call, as one log line that
        /// names the file or files, or null when nothing played. Reading it starts a new count.
        /// </summary>
        public string TakeSyncReport()
        {
            var files = VideoAvatarFiles.FileName(_spec.ColorPath);
            if (_mask != null)
                files += "+" + VideoAvatarFiles.FileName(_spec.MaskPath);
            return _sync.Report(_spec.Name, files, _mask != null ? 2 : 1);
        }

        void SyncMask(long frame, long count, bool started, float deltaSeconds)
        {
            // Measure only settled frames: during a rewind or a resync seek, and before this
            // run's first frame, the decoders report numbers that do not match the picture.
            var settled = started && !Seeking;
            long offset;
            if (_mask == null)
            {
                _sync.Sample(frame, frame, count, settled, out offset);
                return;
            }

            var maskFrame = _mask.frame;
            _sync.Sample(frame, maskFrame, count, settled, out offset);
            // Nothing acts on an unsettled frame, and the cooldown waits too: a slow seek is
            // never followed by a second one on top of it. A seek that never completes shows
            // in the log as frames that keep being skipped.
            if (!settled)
                return;
            _resyncIn -= deltaSeconds;

            // One frame off: nudge the mask's speed until the numbers match again.
            offset = VideoMaskSync.SignedOffset(frame, maskFrame, count);
            var speed = VideoMaskSync.MaskSpeed(offset);
            if (!Mathf.Approximately(speed, _maskSpeed) && _mask.canSetPlaybackSpeed)
            {
                _maskSpeed = speed;
                _mask.playbackSpeed = speed;
            }

            if (_resyncIn > 0f)
                return;
            if (!VideoMaskSync.NeedsResync(frame, maskFrame, count))
                return;

            // More than one frame off: jump. The seek is in flight until seekCompleted.
            _maskSeeking = true;
            _seekingFor = 0f;
            _mask.frame = frame;
            _resyncIn = ResyncCooldownSeconds;
            _sync.CountSeek();
        }

        VideoPlayer CreateDecoder(Transform parent, string objectName, string path)
        {
            var host = new GameObject(objectName);
            host.hideFlags = HideFlags.DontSave;
            host.transform.SetParent(parent, false);
            var player = host.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = path;
            player.isLooping = _spec.Loop;
            // The clip's sound is never routed anywhere. The companion's voice is a separate path.
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.APIOnly;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.prepareCompleted += OnPrepared;
            player.errorReceived += OnError;
            player.seekCompleted += OnSeekCompleted;
            player.loopPointReached += OnLoopPoint;
            return player;
        }

        void DestroyDecoder(VideoPlayer player)
        {
            if (player == null)
                return;

            player.prepareCompleted -= OnPrepared;
            player.errorReceived -= OnError;
            player.seekCompleted -= OnSeekCompleted;
            player.loopPointReached -= OnLoopPoint;
            player.Stop();
            UnityEngine.Object.Destroy(player.gameObject);
        }

        bool LoadMaskImage(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                // Linear, so a mid-grey in the file stays mid-grey when the shader reads it.
                _maskImage = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                _maskImage.hideFlags = HideFlags.DontSave;
                _maskImage.wrapMode = TextureWrapMode.Clamp;
                if (ImageConversion.LoadImage(_maskImage, bytes, true))
                    return true;
            }
            catch (Exception)
            {
            }

            if (_maskImage != null)
                UnityEngine.Object.Destroy(_maskImage);
            _maskImage = null;
            return false;
        }

        void OnPrepared(VideoPlayer source)
        {
            if (source == null)
                return;
            if (source == _color)
                _colorPrepared = true;
            else if (source == _mask)
                _maskPrepared = true;
        }

        void OnSeekCompleted(VideoPlayer source)
        {
            if (source == null)
                return;
            if (source == _color)
                _colorSeeking = false;
            else if (source == _mask)
                _maskSeeking = false;
        }

        void OnLoopPoint(VideoPlayer source)
        {
            if (source != null && source == _color && _begun)
                _loopPointHit = true;
        }

        void OnError(VideoPlayer source, string message)
        {
            if (source == null || (source != _color && source != _mask))
                return;
            Fail(string.IsNullOrWhiteSpace(message) ? "The video decoder reported an error" : message);
        }

        void Fail(string error)
        {
            Failed = true;
            Error = error ?? string.Empty;
        }
    }

    public enum VideoClipEvent
    {
        None = 0,

        /// <summary>A looping clip is at, or just before, the frame where it starts again.</summary>
        LoopPoint = 1,

        /// <summary>A play-once clip has finished and is paused on its last picture.</summary>
        Ended = 2,
    }
}
