using System.Globalization;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Moves the overlay window like a small physical thing on the desktop: it falls to
    /// the taskbar after a drag, and walks left or right when <see cref="IWalkRequest"/>
    /// asks. The arithmetic lives in <see cref="DesktopPresenceModel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(220)]
    public sealed class DesktopPresence : MonoBehaviour, IOverlayKeepAwake, IWalkRequest
    {
        [SerializeField] float walkSpeed = DesktopPresenceModel.DefaultWalkSpeed;
        [SerializeField] float gravity = DesktopPresenceModel.DefaultGravity;
        [SerializeField] int seed = 12345;

        OverlayWindow _window;
        IAvatarLocomotion _locomotion;
        DesktopPresenceModel _model;
        bool _wasDragging;
        bool _walkingSent;
        bool _fallingSent;
        float _walkDirectionSent = float.NaN;
        bool _hasPendingWalk;
        float _pendingDirection;
        float _pendingSeconds;
        bool _pendingStop;
        bool _unsupportedLogged;

        public bool KeepFrameRateActive
        {
            get { return _model != null && _model.State != PresenceState.Idle; }
        }

        /// <summary>Walks for a bounded time, asked by the animation director.</summary>
        public void RequestWalk(float direction, float seconds)
        {
            var dir = direction < 0f ? -1 : 1;
            Debug.Log(
                "Walk: request received dir=" + dir.ToString(CultureInfo.InvariantCulture)
                + " secs=" + seconds.ToString("0.##", CultureInfo.InvariantCulture));

            // The model does not exist until the first motion step, and a fall in
            // progress ignores the request. Keep it until she is standing.
            if (_model == null || _model.State == PresenceState.Falling)
            {
                _pendingDirection = dir;
                _pendingSeconds = seconds;
                _hasPendingWalk = true;
                _pendingStop = false;
                return;
            }

            _hasPendingWalk = false;
            _pendingStop = false;
            _model.RequestWalk(dir, seconds);
        }

        public void RequestStop()
        {
            if (_model == null || _model.State == PresenceState.Falling)
            {
                _pendingStop = true;
                _hasPendingWalk = false;
                return;
            }

            _pendingStop = false;
            _model.RequestStop();
        }

        void TryApplyPending()
        {
            if (_model == null || _model.State == PresenceState.Falling)
                return;

            ApplyPendingWalk();
        }

        void ApplyPendingWalk()
        {
            if (_model == null)
                return;

            if (_pendingStop)
            {
                _pendingStop = false;
                _model.RequestStop();
                return;
            }

            if (_hasPendingWalk)
            {
                _hasPendingWalk = false;
                _model.RequestWalk(_pendingDirection, _pendingSeconds);
            }
        }

        public DesktopPresenceModel Model
        {
            get { return _model; }
        }

        /// <summary>Distance from the window bottom to her soles, from FootGrounding.</summary>
        public void SetFootOffset(float pixels)
        {
            if (_model != null)
                _model.SetFootOffset(pixels);
        }

        void Awake()
        {
            _window = GetComponent<OverlayWindow>();
            _locomotion = GetComponent<IAvatarLocomotion>();
        }

        void LateUpdate()
        {
            if (_window == null)
                _window = GetComponent<OverlayWindow>();

            var chrome = _window != null ? _window.Chrome : null;
            if (chrome == null || !chrome.IsSupported)
            {
                if (!_unsupportedLogged)
                {
                    _unsupportedLogged = true;
                    Debug.Log("Desktop presence is off: window chrome is not supported in this build.");
                }

                return;
            }

            if (!chrome.TryGetWindowRect(out var left, out var top, out var right, out var bottom))
                return;
            if (!chrome.TryGetWorkArea(out var areaLeft, out var areaTop, out var areaRight, out var areaBottom))
                return;

            var frame = Advance(
                Time.unscaledDeltaTime,
                left,
                top,
                right,
                bottom,
                areaLeft,
                areaTop,
                areaRight,
                areaBottom,
                _window.IsDragging);
            if (frame.Moved)
                chrome.MoveWindow(new Vector2Int(frame.X, frame.Y), _window.IsTopmost);
        }

        /// <summary>
        /// One motion step once a window rect and a work area are known. LateUpdate
        /// reads those from the Windows chrome. Tests pass the same numbers, because
        /// the editor chrome cannot move a window.
        /// </summary>
        public PresenceFrame Advance(
            float deltaSeconds,
            int left,
            int top,
            int right,
            int bottom,
            int areaLeft,
            int areaTop,
            int areaRight,
            int areaBottom,
            bool dragging)
        {
            var width = Mathf.Max(1, right - left);
            var height = Mathf.Max(1, bottom - top);

            if (_model == null)
                _model = new DesktopPresenceModel(width, height, seed, walkSpeed, gravity, wanderByIdle: false);
            else
                _model.SetWindowSize(width, height);

            _model.SetWorkArea(new DesktopWorkArea(areaLeft, areaTop, areaRight, areaBottom));

            if (dragging)
            {
                _model.PlaceAt(left, top);
                _wasDragging = true;
                ApplyLocomotion(false, false, true);
                return new PresenceFrame(
                    Mathf.RoundToInt(_model.X),
                    Mathf.RoundToInt(_model.Y),
                    _model.State,
                    _model.FacingRight,
                    false);
            }

            if (_wasDragging)
            {
                _wasDragging = false;
                _model.PlaceAt(left, top);
                _model.Drop();
            }

            // SetWorkArea turns an in-air window into a fall and would drop a walk
            // that was applied a line earlier. Apply only once she is not falling.
            TryApplyPending();

            var frame = _model.Tick(deltaSeconds);
            ApplyLocomotion(frame.Walking, frame.Falling, frame.FacingRight);
            return frame;
        }

        void ApplyLocomotion(bool walking, bool falling, bool facingRight)
        {
            if (_locomotion == null)
                _locomotion = GetComponent<IAvatarLocomotion>();
            if (_locomotion == null)
                return;

            if (walking != _walkingSent)
            {
                _walkingSent = walking;
                _locomotion.SetWalking(walking);
            }

            if (falling != _fallingSent)
            {
                _fallingSent = falling;
                _locomotion.SetFalling(falling);
            }

            var direction = walking ? (facingRight ? 1f : -1f) : 0f;
            if (!Mathf.Approximately(direction, _walkDirectionSent))
            {
                _walkDirectionSent = direction;
                _locomotion.SetWalkingDirection(direction);
            }
        }
    }

    /// <summary>
    /// Adds the presence to the overlay host in any scene, so the scene file does not
    /// need a second component. It disables itself when the chrome cannot move a window.
    /// </summary>
    static class DesktopPresenceInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var hosts = Object.FindObjectsByType<OverlayWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < hosts.Length; i++)
            {
                if (hosts[i] == null)
                    continue;
                if (hosts[i].GetComponent<DesktopPresence>() == null)
                    hosts[i].gameObject.AddComponent<DesktopPresence>();
                if (hosts[i].GetComponent<FootGrounding>() == null)
                    hosts[i].gameObject.AddComponent<FootGrounding>();
            }
        }
    }
}
