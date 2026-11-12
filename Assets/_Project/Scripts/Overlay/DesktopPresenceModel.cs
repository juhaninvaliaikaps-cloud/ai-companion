using System;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Monitor work area in Windows screen pixels: origin top-left, Y grows down.
    /// </summary>
    public readonly struct DesktopWorkArea
    {
        public DesktopWorkArea(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }

        public int Width
        {
            get { return Right - Left; }
        }

        public int Height
        {
            get { return Bottom - Top; }
        }

        public bool IsValid
        {
            get { return Right > Left && Bottom > Top; }
        }
    }

    public enum PresenceState
    {
        Idle = 0,
        Walking = 1,
        Falling = 2,
    }

    /// <summary>
    /// How a walk is ending when a gait is set. Without a gait a walk ends at once and
    /// this is always <see cref="None"/>.
    /// </summary>
    public enum PresenceStopPhase
    {
        None = 0,

        /// <summary>A stop was asked. She keeps full speed until the walk animation reaches its loop point.</summary>
        Lead = 1,

        /// <summary>She slows to a halt while the stop animation plays.</summary>
        Glide = 2,
    }

    public readonly struct PresenceFrame
    {
        public PresenceFrame(int x, int y, PresenceState state, bool facingRight, bool moved)
        {
            X = x;
            Y = y;
            State = state;
            FacingRight = facingRight;
            Moved = moved;
        }

        public int X { get; }
        public int Y { get; }
        public PresenceState State { get; }
        public bool FacingRight { get; }
        public bool Moved { get; }

        public bool Walking
        {
            get { return State == PresenceState.Walking; }
        }

        public bool Falling
        {
            get { return State == PresenceState.Falling; }
        }
    }

    /// <summary>
    /// Desktop-space motion for the overlay window: falls to the taskbar, walks along
    /// it, turns at the edges, and wanders on its own. Deliberately not Rigidbody
    /// physics — the window moves in screen pixels, so the model is plain arithmetic
    /// and is testable without a scene.
    /// </summary>
    public sealed class DesktopPresenceModel
    {
        public const float DefaultWalkSpeed = 90f;
        public const float DefaultGravity = 2600f;
        public const float DefaultMaxFallSpeed = 2200f;
        public const float DefaultMinPause = 2.5f;
        public const float DefaultMaxPause = 7f;
        public const float DefaultMinWalk = 1.5f;
        public const float DefaultMaxWalk = 4.5f;
        public const float MaxStepSeconds = 0.1f;
        public const float MaxStopLeadSeconds = 15f;
        public const float MaxStopGlideSeconds = 5f;

        readonly Random _random;
        readonly float _walkSpeed;
        readonly float _gravity;
        readonly float _maxFallSpeed;
        readonly float _minPause;
        readonly float _maxPause;
        readonly float _minWalk;
        readonly float _maxWalk;
        readonly bool _wanderByIdle;
        bool _placed;

        DesktopWorkArea _area;
        int _windowWidth;
        int _windowHeight;
        float _x;
        float _y;
        float _verticalVelocity;
        float _timer;
        int _direction = 1;
        float _footOffsetPx;

        // Gait of a walking animation that is not the 3D one. All zero means "no gait":
        // the walk uses the constructor speed and ends at once, as it always did.
        bool _hasGait;
        float _gaitSpeed;
        float _gaitLeadSeconds;
        float _gaitGlideSeconds;
        PresenceStopPhase _stopPhase;
        float _stopTimer;
        float _glideTotal;

        public DesktopPresenceModel(
            int windowWidth,
            int windowHeight,
            int seed = 12345,
            float walkSpeed = DefaultWalkSpeed,
            float gravity = DefaultGravity,
            float maxFallSpeed = DefaultMaxFallSpeed,
            float minPause = DefaultMinPause,
            float maxPause = DefaultMaxPause,
            float minWalk = DefaultMinWalk,
            float maxWalk = DefaultMaxWalk,
            bool wanderByIdle = true)
        {
            _windowWidth = Math.Max(1, windowWidth);
            _windowHeight = Math.Max(1, windowHeight);
            _random = new Random(seed);
            _walkSpeed = walkSpeed;
            _gravity = gravity;
            _maxFallSpeed = maxFallSpeed;
            _minPause = minPause;
            _maxPause = maxPause;
            _minWalk = minWalk;
            _maxWalk = maxWalk;
            _wanderByIdle = wanderByIdle;
            State = PresenceState.Idle;
            _timer = NextPause();
        }

        public PresenceState State { get; private set; }

        public float X
        {
            get { return _x; }
        }

        public float Y
        {
            get { return _y; }
        }

        public bool FacingRight
        {
            get { return _direction > 0; }
        }

        public DesktopWorkArea WorkArea
        {
            get { return _area; }
        }

        /// <summary>True when <see cref="SetGait"/> has set a walking speed. That speed may be zero.</summary>
        public bool HasGait
        {
            get { return _hasGait; }
        }

        /// <summary>None unless she is walking and a stop is in progress.</summary>
        public PresenceStopPhase StopPhase
        {
            get { return State == PresenceState.Walking ? _stopPhase : PresenceStopPhase.None; }
        }

        /// <summary>Pixels per second she moves right now. Zero when she is not walking.</summary>
        public float CurrentSpeed
        {
            get
            {
                if (State != PresenceState.Walking)
                    return 0f;

                var speed = _hasGait ? _gaitSpeed : _walkSpeed;
                if (_stopPhase == PresenceStopPhase.Glide)
                    return _glideTotal > 0f ? speed * Math.Max(0f, _stopTimer / _glideTotal) : 0f;
                return speed;
            }
        }

        /// <summary>
        /// Matches the window's movement to a walking animation. <paramref name="speed"/>
        /// is pixels per second and may change every frame, also during a stop, so the
        /// caller can follow the feet in the clip. When a walk ends she keeps walking for
        /// <paramref name="stopLeadSeconds"/> (the time the animation still needs to finish
        /// its steps), then slows to zero over <paramref name="stopGlideSeconds"/>. The two
        /// durations in force when the stop begins are the ones used.
        /// </summary>
        public void SetGait(float speed, float stopLeadSeconds, float stopGlideSeconds)
        {
            _hasGait = true;
            _gaitSpeed = IsUsable(speed) ? Math.Max(0f, speed) : 0f;
            _gaitLeadSeconds = IsUsable(stopLeadSeconds) ? Math.Min(Math.Max(stopLeadSeconds, 0f), MaxStopLeadSeconds) : 0f;
            _gaitGlideSeconds = IsUsable(stopGlideSeconds) ? Math.Min(Math.Max(stopGlideSeconds, 0f), MaxStopGlideSeconds) : 0f;
        }

        /// <summary>Back to the constructor speed and an immediate stop.</summary>
        public void ClearGait()
        {
            _hasGait = false;
            _gaitSpeed = 0f;
            _gaitLeadSeconds = 0f;
            _gaitGlideSeconds = 0f;
        }

        static bool IsUsable(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// Top-left Y of a window whose soles sit on the work area bottom. The foot
        /// offset moves the window down by that many pixels, so the transparent part
        /// under the taskbar line is empty and click-through.
        /// </summary>
        public int GroundY
        {
            get { return _area.Bottom - _windowHeight + (int)Math.Round(Math.Min(_footOffsetPx, Math.Max(0, _windowHeight - 1))); }
        }

        public int WindowHeight
        {
            get { return _windowHeight; }
        }

        public float FootOffsetPx
        {
            get { return _footOffsetPx; }
        }

        /// <summary>
        /// Distance from the window bottom to her soles. Changing it while she stands or
        /// walks moves the window so the feet stay on the line instead of jumping.
        /// </summary>
        public void SetFootOffset(float pixels)
        {
            var max = _windowHeight <= 1 ? 0f : _windowHeight - 1f;
            var clamped = Math.Min(Math.Max(pixels, 0f), max);
            if (Math.Abs(clamped - _footOffsetPx) < 0.5f)
                return;

            // Move her by the same amount the ground moved, so the soles stay planted
            // instead of snapping up in one frame when the walk pose changes the offset.
            var before = GroundY;
            _footOffsetPx = clamped;
            var after = GroundY;
            if (State != PresenceState.Falling)
                _y += after - before;
        }

        public void SetWindowSize(int width, int height)
        {
            _windowWidth = Math.Max(1, width);
            _windowHeight = Math.Max(1, height);
        }

        /// <summary>
        /// New work area (taskbar moved, monitor changed). A window left below the new
        /// ground is lifted, and one left above it starts falling on the next tick.
        /// </summary>
        public void SetWorkArea(DesktopWorkArea area)
        {
            if (!area.IsValid)
                return;

            _area = area;
            _x = ClampX(_x);

            // The first work area is where she is placed, not dropped: without this she
            // spawned above the ground and fell onto the taskbar on every launch.
            if (!_placed)
            {
                _placed = true;
                _y = GroundY;
                _verticalVelocity = 0f;
                State = PresenceState.Idle;
                _timer = NextPause();
                return;
            }

            if (_y > GroundY)
                _y = GroundY;

            if (_y < GroundY)
            {
                State = PresenceState.Falling;
                _verticalVelocity = Math.Max(0f, _verticalVelocity);
            }
        }

        /// <summary>Window position is authoritative, for example during a drag.</summary>
        public void PlaceAt(int x, int y)
        {
            _x = ClampX(x);
            _y = Math.Max(_area.Top, y);
            _verticalVelocity = 0f;
            if (_y < GroundY)
            {
                State = PresenceState.Falling;
                return;
            }

            State = PresenceState.Idle;
            _timer = NextPause();
        }

        /// <summary>Starts a fall from wherever the window is now.</summary>
        public void Drop()
        {
            if (_y < GroundY)
            {
                State = PresenceState.Falling;
                _verticalVelocity = 0f;
            }
        }

        public PresenceFrame Tick(float deltaSeconds)
        {
            if (!_area.IsValid)
                return Frame(false);

            var step = Math.Min(Math.Max(deltaSeconds, 0f), MaxStepSeconds);
            if (step <= 0f)
                return Frame(false);

            var wasX = _x;
            var wasY = _y;
            var wasState = State;

            switch (State)
            {
                case PresenceState.Falling:
                    _verticalVelocity = Math.Min(_maxFallSpeed, _verticalVelocity + _gravity * step);
                    _y += _verticalVelocity * step;
                    if (_y >= GroundY)
                    {
                        _y = GroundY;
                        _verticalVelocity = 0f;
                        State = PresenceState.Idle;
                        _timer = NextPause();
                    }
                    break;

                case PresenceState.Walking:
                    _x += _direction * CurrentSpeed * step;
                    // An edge ends a walk toward it at once. There is no room to slow down in.
                    // A walk away from the edge carries on: with a gait she can stand still
                    // for a moment at the start, and must not be stopped by the edge behind her.
                    if (_direction < 0 && _x <= _area.Left)
                    {
                        _x = _area.Left;
                        StopWalking();
                        EndedAtEdge = -1;
                    }
                    else if (_direction > 0 && _x >= _area.Right - _windowWidth)
                    {
                        _x = _area.Right - _windowWidth;
                        StopWalking();
                        EndedAtEdge = 1;
                    }
                    else if (_stopPhase != PresenceStopPhase.None)
                    {
                        TickStop(step);
                    }
                    else
                    {
                        _timer -= step;
                        if (_timer <= 0f)
                            BeginStop();
                    }
                    break;

                default:
                    _y = GroundY;
                    _timer -= step;
                    if (_timer <= 0f)
                    {
                        // Wandering used to start on a timer, which fought every scripted
                        // gesture. With it off she only walks when the director asks.
                        if (_wanderByIdle)
                            StartWalking();
                        else
                            _timer = NextPause();
                    }

                    break;
            }

            _x = ClampX(_x);
            var moved = Math.Abs(_x - wasX) > 0.001f || Math.Abs(_y - wasY) > 0.001f || State != wasState;
            return Frame(moved);
        }

        /// <summary>
        /// Walks for a bounded time in the requested direction. A drop in progress is left
        /// alone so the request cannot teleport her off the ground.
        /// </summary>
        /// <summary>
        /// -1 when the last walk ended at the left edge, 1 at the right edge, 0 otherwise.
        /// Cleared by the next walk.
        /// </summary>
        public int EndedAtEdge { get; private set; }

        public void RequestWalk(float direction, float seconds)
        {
            if (State == PresenceState.Falling)
                return;

            EndedAtEdge = 0;

            State = PresenceState.Walking;
            _direction = direction < 0f ? -1 : 1;
            _timer = Math.Min(30f, Math.Max(0.2f, seconds));
            // A new walk during a stop cancels the stop, so she never moves two ways at once.
            _stopPhase = PresenceStopPhase.None;
            _stopTimer = 0f;
        }

        public void RequestStop()
        {
            if (State == PresenceState.Walking && _stopPhase == PresenceStopPhase.None)
                BeginStop();
        }

        /// <summary>
        /// Ends a walk. With no gait she stands still at once. With a gait she keeps
        /// walking through the lead and then slows down through the glide.
        /// </summary>
        void BeginStop()
        {
            if (!_hasGait || (_gaitLeadSeconds <= 0f && _gaitGlideSeconds <= 0f))
            {
                StopWalking();
                return;
            }

            _glideTotal = _gaitGlideSeconds;
            if (_gaitLeadSeconds > 0f)
            {
                _stopPhase = PresenceStopPhase.Lead;
                _stopTimer = _gaitLeadSeconds;
                return;
            }

            _stopPhase = PresenceStopPhase.Glide;
            _stopTimer = _glideTotal;
        }

        void TickStop(float step)
        {
            _stopTimer -= step;
            if (_stopTimer > 0f)
                return;

            if (_stopPhase == PresenceStopPhase.Lead && _glideTotal > 0f)
            {
                _stopPhase = PresenceStopPhase.Glide;
                _stopTimer = _glideTotal;
                return;
            }

            StopWalking();
        }

        void StartWalking()
        {
            State = PresenceState.Walking;
            _timer = NextWalk();
            _stopPhase = PresenceStopPhase.None;
            _stopTimer = 0f;

            var atLeft = _x <= _area.Left + 0.5f;
            var atRight = _x >= _area.Right - _windowWidth - 0.5f;
            if (atLeft)
                _direction = 1;
            else if (atRight)
                _direction = -1;
            else
                _direction = _random.Next(0, 2) == 0 ? -1 : 1;
        }

        void StopWalking()
        {
            State = PresenceState.Idle;
            _timer = NextPause();
            _stopPhase = PresenceStopPhase.None;
            _stopTimer = 0f;
        }

        float ClampX(float x)
        {
            if (!_area.IsValid)
                return x;

            var maxX = _area.Right - _windowWidth;
            if (maxX < _area.Left)
                maxX = _area.Left;
            return Math.Min(Math.Max(x, _area.Left), maxX);
        }

        float NextPause()
        {
            return _minPause + (float)_random.NextDouble() * (_maxPause - _minPause);
        }

        float NextWalk()
        {
            return _minWalk + (float)_random.NextDouble() * (_maxWalk - _minWalk);
        }

        PresenceFrame Frame(bool moved)
        {
            return new PresenceFrame(
                (int)Math.Round(_x),
                (int)Math.Round(_y),
                State,
                _direction > 0,
                moved);
        }
    }
}
