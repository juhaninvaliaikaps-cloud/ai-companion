using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Moves the overlay window like a small physical thing on the desktop: it falls to
    /// the taskbar after a drag, walks left and right along it, turns at the edges, and
    /// wanders while idle. The arithmetic lives in <see cref="DesktopPresenceModel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(220)]
    public sealed class DesktopPresence : MonoBehaviour, IOverlayKeepAwake
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
        bool _unsupportedLogged;

        public bool KeepFrameRateActive
        {
            get { return _model != null && _model.State != PresenceState.Idle; }
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

            var width = Mathf.Max(1, right - left);
            var height = Mathf.Max(1, bottom - top);

            if (_model == null)
                _model = new DesktopPresenceModel(width, height, seed, walkSpeed, gravity);
            else
                _model.SetWindowSize(width, height);

            _model.SetWorkArea(new DesktopWorkArea(areaLeft, areaTop, areaRight, areaBottom));

            if (_window.IsDragging)
            {
                _model.PlaceAt(left, top);
                _wasDragging = true;
                ApplyLocomotion(false, false, true);
                return;
            }

            if (_wasDragging)
            {
                _wasDragging = false;
                _model.PlaceAt(left, top);
                _model.Drop();
            }

            var frame = _model.Tick(Time.unscaledDeltaTime);
            if (frame.Moved)
                chrome.MoveWindow(new Vector2Int(frame.X, frame.Y), _window.IsTopmost);

            ApplyLocomotion(frame.Walking, frame.Falling, frame.FacingRight);
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
