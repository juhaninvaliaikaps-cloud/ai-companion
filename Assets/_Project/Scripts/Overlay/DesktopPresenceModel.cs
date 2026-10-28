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

        readonly Random _random;
        readonly float _walkSpeed;
        readonly float _gravity;
        readonly float _maxFallSpeed;
        readonly float _minPause;
        readonly float _maxPause;
        readonly float _minWalk;
        readonly float _maxWalk;

        DesktopWorkArea _area;
        int _windowWidth;
        int _windowHeight;
        float _x;
        float _y;
        float _verticalVelocity;
        float _timer;
        int _direction = 1;

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
            float maxWalk = DefaultMaxWalk)
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

        /// <summary>Top-left Y of a window that sits on the work area bottom.</summary>
        public int GroundY
        {
            get { return _area.Bottom - _windowHeight; }
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
                    _x += _direction * _walkSpeed * step;
                    if (_x <= _area.Left)
                    {
                        _x = _area.Left;
                        StopWalking();
                    }
                    else if (_x >= _area.Right - _windowWidth)
                    {
                        _x = _area.Right - _windowWidth;
                        StopWalking();
                    }
                    else
                    {
                        _timer -= step;
                        if (_timer <= 0f)
                            StopWalking();
                    }
                    break;

                default:
                    _y = GroundY;
                    _timer -= step;
                    if (_timer <= 0f)
                        StartWalking();
                    break;
            }

            _x = ClampX(_x);
            var moved = Math.Abs(_x - wasX) > 0.001f || Math.Abs(_y - wasY) > 0.001f || State != wasState;
            return Frame(moved);
        }

        void StartWalking()
        {
            State = PresenceState.Walking;
            _timer = NextWalk();

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
