using System;

namespace AICompanion.Settings
{
    /// <summary>What the 2D avatar is doing.</summary>
    public enum VideoAvatarAct
    {
        /// <summary>The default idle loop.</summary>
        Idle = 0,

        /// <summary>An idle variation, played once.</summary>
        Variation = 1,

        /// <summary>The walk loop, while the window moves.</summary>
        Walk = 2,

        /// <summary>The walk-stop clip, played once while the window slows down.</summary>
        WalkStop = 3,

        /// <summary>The last frame of the walk-stop clip, held.</summary>
        Hold = 4,

        /// <summary>The side-facing idle loop, when the folder has one.</summary>
        SideIdle = 5,

        /// <summary>A reaction clip such as "happy", played once from the default idle.</summary>
        Reaction = 6,
    }

    /// <summary>
    /// Decides which clip should be on screen. It knows nothing about video players:
    /// the player tells it when the window walks, when a loop reaches its end, and when
    /// a play-once clip is over, and reads <see cref="Clip"/>, <see cref="Mirrored"/> and
    /// <see cref="Entry"/> back.
    ///
    /// Rules:
    /// - An idle variation starts only when the default idle reaches its loop point.
    /// - A reaction starts from the default idle as soon as its clip is loaded, and returns
    ///   to the default idle. It is ignored while she walks, stops, holds a pose, or plays
    ///   another clip.
    /// - A walk starts at once and interrupts anything.
    /// - A stop waits for the walk loop to reach its loop point, then plays the walk-stop.
    /// - After the walk-stop she stays as the clip left her: the side idle if there is one,
    ///   otherwise the walk-stop's last frame, held. She returns to the default idle only
    ///   when asked, or when the manifest says the walk-stop ends in the idle pose.
    /// </summary>
    public sealed class VideoAvatarDirector
    {
        readonly VideoAvatarClipSet _set;
        readonly Random _random;
        readonly bool[] _unavailable;
        float _untilVariation;
        int _queued = -1;
        bool _queuedReaction;
        int _lastVariation = -1;
        bool _stopPending;
        bool _facingRight;

        public VideoAvatarDirector(VideoAvatarClipSet set, int seed)
        {
            _set = set ?? throw new ArgumentNullException(nameof(set));
            _random = new Random(seed);
            _unavailable = new bool[set.Clips.Count];
            Act = VideoAvatarAct.Idle;
            Clip = set.Idle;
            Entry = 1;
            ScheduleVariation();
        }

        public VideoAvatarAct Act { get; private set; }

        /// <summary>Index into the clip set of the clip that should be showing.</summary>
        public int Clip { get; private set; }

        /// <summary>True when the clip must be flipped left to right.</summary>
        public bool Mirrored { get; private set; }

        /// <summary>Goes up by one each time a clip is entered, also when the same clip is entered again.</summary>
        public int Entry { get; private set; }

        /// <summary>The idle variation waiting for the next idle loop point, or -1. The player loads it ahead of time.</summary>
        public int Queued
        {
            get { return _queued; }
        }

        /// <summary>True when <see cref="Queued"/> is a reaction. It does not wait for the idle's loop point.</summary>
        public bool QueuedIsReaction
        {
            get { return _queued >= 0 && _queuedReaction; }
        }

        /// <summary>
        /// Asks for a reaction clip by name. True when it was accepted: the clip exists and
        /// she is in the default idle. It takes the place of an idle variation that was waiting.
        /// </summary>
        public bool RequestReaction(string name)
        {
            if (Act != VideoAvatarAct.Idle)
                return false;

            var clip = _set.FindReaction(name);
            if (!Usable(clip))
                return false;

            _queued = clip;
            _queuedReaction = true;
            return true;
        }

        /// <summary>The queued clip is loaded. A reaction starts now. An idle variation keeps waiting for the loop point.</summary>
        public void OnQueuedReady()
        {
            if (Act == VideoAvatarAct.Idle && _queuedReaction && Usable(_queued))
                Enter(VideoAvatarAct.Reaction, _queued, false);
        }

        /// <summary>Seconds until the next variation is queued. For tests and the status line.</summary>
        public float UntilVariation
        {
            get { return _untilVariation; }
        }

        /// <summary>A clip that failed to load is never chosen again. If it is showing, she returns to the default idle.</summary>
        public void SetUnavailable(int clip)
        {
            if (clip < 0 || clip >= _unavailable.Length || clip == _set.Idle)
                return;

            _unavailable[clip] = true;
            if (_queued == clip)
            {
                _queued = -1;
                _queuedReaction = false;
                ScheduleVariation();
            }

            if (Clip == clip)
                EnterIdle();
        }

        /// <summary>Counts down to the next idle variation. Only the default idle counts.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Act != VideoAvatarAct.Idle || _queued >= 0 || deltaSeconds <= 0f)
                return;
            if (!HasVariation())
                return;

            _untilVariation -= deltaSeconds;
            if (_untilVariation <= 0f)
                _queued = PickVariation();
        }

        /// <summary>
        /// The showing loop is at its loop point. <paramref name="queuedReady"/> says whether
        /// the queued variation is loaded. If it is not, it waits for the next loop point.
        /// </summary>
        public void OnBoundary(bool queuedReady)
        {
            if (Act == VideoAvatarAct.Walk && _stopPending)
            {
                BeginStop();
                return;
            }

            if (Act == VideoAvatarAct.Idle && _queued >= 0 && queuedReady && Usable(_queued))
            {
                var clip = _queued;
                if (_queuedReaction)
                {
                    Enter(VideoAvatarAct.Reaction, clip, false);
                    return;
                }

                _lastVariation = clip;
                Enter(VideoAvatarAct.Variation, clip, false);
            }
        }

        /// <summary>The showing play-once clip has reached its end.</summary>
        public void OnEnded()
        {
            if (Act == VideoAvatarAct.Variation || Act == VideoAvatarAct.Reaction)
                EnterIdle();
            else if (Act == VideoAvatarAct.WalkStop)
                Rest();
        }

        /// <summary>
        /// The desktop presence's state this frame. <paramref name="stopLead"/> is true while
        /// a stop was asked and she still walks at full speed. <paramref name="stopGlide"/> is
        /// true while she slows down.
        /// </summary>
        public void SetMovement(bool walking, bool facingRight, bool stopLead, bool stopGlide)
        {
            if (walking && stopLead)
            {
                // A stop is under way and she still moves. While the walk loop shows, wait for
                // its loop point. Once the walk-stop shows, its own steps are carrying her:
                // this is not a new walk.
                if (Act == VideoAvatarAct.Walk)
                    _stopPending = true;
                return;
            }

            if (walking && !stopGlide)
            {
                _facingRight = facingRight;
                _stopPending = false;
                if (!Usable(_set.Walk))
                    return;

                var mirrored = facingRight != _set.Clips[_set.Walk].FacesRight;
                if (Act != VideoAvatarAct.Walk)
                    Enter(VideoAvatarAct.Walk, _set.Walk, mirrored);
                else
                    Mirrored = mirrored;
                return;
            }

            // She is slowing down, or the walk ended at once (a screen edge, a drag, a fall).
            _stopPending = false;
            if (Act == VideoAvatarAct.Walk)
                BeginStop();
        }

        /// <summary>Manual: face front again from the side pose, the side idle, or a variation. Ignored while walking.</summary>
        public void RequestFront()
        {
            if (Act == VideoAvatarAct.Walk || Act == VideoAvatarAct.Idle)
                return;
            EnterIdle();
        }

        void BeginStop()
        {
            _stopPending = false;
            if (Usable(_set.WalkStop))
            {
                Enter(VideoAvatarAct.WalkStop, _set.WalkStop, _facingRight != _set.Clips[_set.WalkStop].FacesRight);
                return;
            }

            // No walk-stop clip. A side idle is the next best thing. Without either there
            // is nothing side-facing to show, so she goes back to the default idle.
            if (Usable(_set.SideIdle))
                Enter(VideoAvatarAct.SideIdle, _set.SideIdle, _facingRight != _set.Clips[_set.SideIdle].FacesRight);
            else
                EnterIdle();
        }

        void Rest()
        {
            if (Usable(_set.SideIdle))
            {
                Enter(VideoAvatarAct.SideIdle, _set.SideIdle, _facingRight != _set.Clips[_set.SideIdle].FacesRight);
                return;
            }

            // The manifest says this walk-stop ends facing front in the idle pose, so the
            // default idle follows it without a jump in pose.
            if (Usable(_set.WalkStop) && _set.Clips[_set.WalkStop].ThenIdle)
            {
                EnterIdle();
                return;
            }

            // Same clip, no new entry: the player keeps the last frame on screen.
            Act = VideoAvatarAct.Hold;
        }

        void EnterIdle()
        {
            Enter(VideoAvatarAct.Idle, _set.Idle, false);
        }

        void Enter(VideoAvatarAct act, int clip, bool mirrored)
        {
            Act = act;
            Clip = clip;
            Mirrored = mirrored;
            Entry++;
            _queued = -1;
            _queuedReaction = false;
            if (act == VideoAvatarAct.Idle)
                ScheduleVariation();
        }

        void ScheduleVariation()
        {
            var min = Math.Max(0f, _set.VariationMinSeconds);
            var max = Math.Max(min, _set.VariationMaxSeconds);
            _untilVariation = min + (float)_random.NextDouble() * (max - min);
        }

        bool HasVariation()
        {
            for (var i = 0; i < _set.Variations.Count; i++)
            {
                if (Usable(_set.Variations[i]))
                    return true;
            }

            return false;
        }

        /// <summary>A weighted pick that does not repeat the last variation when there is another one.</summary>
        int PickVariation()
        {
            var total = 0f;
            var usable = 0;
            for (var i = 0; i < _set.Variations.Count; i++)
            {
                if (Usable(_set.Variations[i]))
                    usable++;
            }

            for (var i = 0; i < _set.Variations.Count; i++)
            {
                var clip = _set.Variations[i];
                if (!Usable(clip) || (usable > 1 && clip == _lastVariation))
                    continue;
                total += Math.Max(0.0001f, _set.Clips[clip].Weight);
            }

            if (total <= 0f)
                return -1;

            var roll = (float)_random.NextDouble() * total;
            var last = -1;
            for (var i = 0; i < _set.Variations.Count; i++)
            {
                var clip = _set.Variations[i];
                if (!Usable(clip) || (usable > 1 && clip == _lastVariation))
                    continue;

                last = clip;
                roll -= Math.Max(0.0001f, _set.Clips[clip].Weight);
                if (roll <= 0f)
                    return clip;
            }

            return last;
        }

        bool Usable(int clip)
        {
            return clip >= 0 && clip < _unavailable.Length && !_unavailable[clip];
        }
    }

    /// <summary>
    /// How fast the window moves at a moment of a walking clip, as a share of full speed.
    /// A clip can start from a standing pose and end in one, so the window speeds up after
    /// <c>moveStart</c> and slows down before <c>moveEnd</c> instead of sliding under still feet.
    /// </summary>
    public static class VideoGait
    {
        public static float Factor(float seconds, float moveStart, float moveEnd, float ramp, float clipSeconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f)
                return 0f;

            var start = Math.Max(0f, moveStart);
            var end = moveEnd > 0f ? moveEnd : clipSeconds;
            if (clipSeconds > 0f)
                end = Math.Min(end, clipSeconds);
            if (!(end > start))
                return 0f;
            if (seconds <= start || seconds >= end)
                return 0f;
            if (!(ramp > 0f))
                return 1f;

            var rise = (seconds - start) / ramp;
            var fall = (end - seconds) / ramp;
            return Math.Max(0f, Math.Min(1f, Math.Min(rise, fall)));
        }

        /// <summary>The end of the moving part in seconds, with 0 meaning the whole clip.</summary>
        public static float MoveEnd(float moveEnd, float clipSeconds)
        {
            if (moveEnd > 0f)
                return clipSeconds > 0f ? Math.Min(moveEnd, clipSeconds) : moveEnd;
            return Math.Max(0f, clipSeconds);
        }
    }

    /// <summary>Frame arithmetic for deciding when a clip is at its loop point or its end.</summary>
    public static class VideoClipProgress
    {
        /// <summary>A loop counts as "at its loop point" this many frames before the last one, so the next clip can start in time.</summary>
        public const long BoundaryLeadFrames = 2;

        /// <summary>A play-once clip is paused this many frames before its end, so its last picture is certain to stay on screen.</summary>
        public const long EndMarginFrames = 1;

        public static bool NearLoopPoint(long frame, long frameCount)
        {
            return frameCount > 0 && frame >= 0 && frame >= frameCount - 1 - BoundaryLeadFrames;
        }

        public static bool Wrapped(long previousFrame, long frame)
        {
            return previousFrame >= 0 && frame >= 0 && frame < previousFrame;
        }

        public static bool AtEnd(long frame, long frameCount)
        {
            return frameCount > 0 && frame >= 0 && frame >= frameCount - 1 - EndMarginFrames;
        }

        /// <summary>Seconds until the loop point, or 0 when the length or the rate is unknown.</summary>
        public static float SecondsToLoopPoint(long frame, long frameCount, float framesPerSecond)
        {
            if (frameCount <= 0 || frame < 0 || !(framesPerSecond > 0f))
                return 0f;
            var left = frameCount - 1 - frame;
            return left <= 0 ? 0f : left / framesPerSecond;
        }

        /// <summary>
        /// True when a clip that was just started is showing a picture from this run. A frame
        /// number far ahead of the time played is a leftover from before the rewind.
        /// </summary>
        public static bool IsFresh(long frame, float secondsSinceBegin, float framesPerSecond)
        {
            if (frame < 1)
                return false;
            var rate = framesPerSecond > 0f ? framesPerSecond : 30f;
            return frame <= (long)(secondsSinceBegin * rate) + 8;
        }
    }
}
