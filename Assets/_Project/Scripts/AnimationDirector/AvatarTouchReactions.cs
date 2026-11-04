using System;
using System.Collections.Generic;
using AICompanion.Avatar;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// Turns clicks on her silhouette into small reactions: a face change, a gesture, a blush
    /// and a line the model can answer. The reaction fires on release, so a quick press is a
    /// touch while pressing and moving a hand starts an IK drag instead. Clicks over UI and the
    /// window drag keep working because the overlay only dispatches when the pointer is on her.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarTouchReactions : MonoBehaviour, IAvatarTouch
    {
        /// <summary>Pixels of pointer travel that turn a hand press into a drag.</summary>
        public const float DragThresholdPixels = 8f;

        [SerializeField] TouchZoneSet zoneSet;
        [SerializeField] Camera avatarCamera;

        readonly List<TouchZoneProbe> _probes = new List<TouchZoneProbe>(12);
        readonly TouchCooldown _cooldown = new TouchCooldown(1.5f);
        Animator _animator;
        AnimationDirector _director;
        AvatarAnimationController _controller;
        AvatarHandIk _ik;
        TouchZone _pressZone;
        TouchZone _dragging;
        Vector2 _pressScreen;
        AvatarIKGoal _dragGoal;
        float _dragDepth;
        bool _pressed;
        float _blushTickAt;
        IWalkRequest _walk;
        int _walkDirection;
        float _walkRefreshAt;
        Vector3 _ikTarget;

        /// <summary>Raised with the zone id when a touch reaction plays, so the chat can react in words.</summary>
        public event Action<string> Touched;

        /// <summary>Raised when a hand drag starts, so she knows the hand is being held.</summary>
        public event Action<string> HandHoldStarted;

        /// <summary>Raised when a hand drag ends, so holding the hand reads as a hold.</summary>
        public event Action<string> HandHeld;

        void Awake()
        {
            Resolve();
        }

        void OnEnable()
        {
            Resolve();
        }

        void Resolve()
        {
            // The component may live on the overlay host rather than next to the director, so
            // fall back to the scene when the same GameObject does not carry them.
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
                _animator = UnityEngine.Object.FindFirstObjectByType<Animator>();
            if (_director == null)
                _director = GetComponent<AnimationDirector>();
            if (_director == null)
                _director = UnityEngine.Object.FindFirstObjectByType<AnimationDirector>();
            if (_controller == null)
                _controller = GetComponent<AvatarAnimationController>();
            if (_controller == null)
                _controller = UnityEngine.Object.FindFirstObjectByType<AvatarAnimationController>();
            if (avatarCamera == null)
                avatarCamera = Camera.main;
            if (_walk == null)
                _walk = FindWalkRequest();
        }

        public void SetZones(TouchZoneSet set)
        {
            zoneSet = set;
        }

        /// <summary>The hand zone being dragged with IK, or null.</summary>
        public string DraggingZoneId
        {
            get { return _dragging != null ? _dragging.id : null; }
        }

        /// <summary>
        /// Only hand zones consume the press, so the window does not move while a hand is being
        /// grabbed. Every other zone lets the overlay behave exactly as before.
        /// </summary>
        public bool OnAvatarTouch(Vector2 screenPosition)
        {
            Resolve();
            _pressed = false;
            _pressZone = null;
            if (zoneSet == null || zoneSet.zones == null || _animator == null || avatarCamera == null)
                return false;

            BuildProbes();
            string id;
            if (!TouchZonePicker.TryPick(_probes, screenPosition, out id))
                return false;

            var zone = Find(id);
            if (zone == null)
                return false;

            // A passive zone is a handle for moving the window: no reaction, no consume, so the
            // overlay drags her exactly as if the pointer were on an empty part of the window.
            if (zone.passive)
                return false;

            _pressZone = zone;
            _pressScreen = screenPosition;
            _pressed = true;
            return zone.drag;
        }

        public void OnAvatarRelease()
        {
            if (_dragging != null)
            {
                var held = _dragging;
                EndHandDrag();
                _pressed = false;
                _pressZone = null;
                ReactHandHeld(held);
                return;
            }

            if (_pressed && _pressZone != null)
                ReactIfReady(_pressZone);

            _pressed = false;
            _pressZone = null;
        }

        void Update()
        {
            if (_dragging != null)
            {
                MoveHand();
                return;
            }

            if (!_pressed || _pressZone == null || !_pressZone.drag || avatarCamera == null)
                return;

            var travel = Vector2.Distance((Vector2)Input.mousePosition, _pressScreen);
            if (travel >= DragThresholdPixels)
                BeginHandDrag(_pressZone);
        }

        void MoveHand()
        {
            if (_ik == null || avatarCamera == null)
                return;

            var pointer = (Vector3)Input.mousePosition;
            pointer.z = _dragDepth;
            _ikTarget = avatarCamera.ScreenToWorldPoint(pointer);
            _ik.MoveTo(_ikTarget);

            if (_controller != null && Time.unscaledTime >= _blushTickAt)
            {
                _controller.AddTouchBlush(0.12f, 2.5f);
                _blushTickAt = Time.unscaledTime + 0.35f;
            }

            UpdatePullWalk();
        }

        void BeginHandDrag(TouchZone zone)
        {
            if (_animator == null)
                return;

            var bone = _animator.GetBoneTransform(zone.bone);
            if (bone == null)
                return;

            if (_ik == null)
                _ik = _animator.GetComponent<AvatarHandIk>();
            if (_ik == null)
                _ik = _animator.gameObject.AddComponent<AvatarHandIk>();

            _dragGoal = zone.bone == HumanBodyBones.LeftHand ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            _dragDepth = avatarCamera.WorldToScreenPoint(bone.position).z;
            if (_dragDepth < 0.1f)
                _dragDepth = 0.1f;

            _ik.Begin(_dragGoal, bone.position);
            _dragging = zone;
            _blushTickAt = Time.unscaledTime;

            // Holding a hand starts the same shy reaction a click would, then the blush deepens.
            if (_director != null)
                _director.Play(new AnimationCue(zone.emotion, zone.intensity, string.Empty, 0f, zone.expression));
            if (_controller != null)
                _controller.AddTouchBlush(0.15f, 2.5f);

            Debug.Log("Touch: dragging " + zone.id);

            // Tell the model while it is still happening, not only when the hand is released.
            if (HandHoldStarted != null)
                HandHoldStarted(zone.id);
        }

        /// <summary>
        /// Pulling a hand past her sideways reach leads her: she walks that way until you stop
        /// pulling or let go. Walking uses the same IWalkRequest the director uses.
        /// </summary>
        void UpdatePullWalk()
        {
            if (_dragging == null || _animator == null || _walk == null)
                return;

            var left = _dragGoal == AvatarIKGoal.LeftHand;
            var shoulder = _animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = _animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = _animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (shoulder == null || lower == null || hand == null)
                return;

            var root = _animator.transform;
            var shoulderLocal = root.InverseTransformPoint(shoulder.position);
            var requestedLocal = root.InverseTransformPoint(_ikTarget);

            var armLength = Vector3.Distance(shoulder.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            var maxReach = Mathf.Max(0.05f, armLength * 0.95f);
            // Start at the edge, but keep following with a lower threshold: walking towards the
            // pointer shortens the pull, and without hysteresis she would stop after one step.
            var keepFactor = _walkDirection != 0 ? 0.85f : HandPull.StretchFactor;
            var direction = HandPull.WalkDirection(requestedLocal, shoulderLocal, maxReach, keepFactor);

            var now = Time.unscaledTime;
            if (direction == 0)
            {
                if (_walkDirection != 0)
                {
                    _walkDirection = 0;
                    _walk.RequestStop();
                }

                return;
            }

            var changed = direction != _walkDirection;
            var expired = now >= _walkRefreshAt;
            if (!changed && !expired)
                return;

            _walkDirection = direction;
            _walkRefreshAt = now + 0.4f;
            _walk.RequestWalk(direction, 0.6f);

            if (changed)
                Debug.Log("Touch: leading her " + (direction < 0 ? "left" : "right"));
        }

        IWalkRequest FindWalkRequest()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var request = behaviours[i] as IWalkRequest;
                if (request != null)
                    return request;
            }

            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var request = all[i] as IWalkRequest;
                if (request != null)
                    return request;
            }

            return null;
        }

        void EndHandDrag()
        {
            if (_dragging == null)
                return;

            if (_ik != null)
                _ik.Release();

            if (_walkDirection != 0 && _walk != null)
            {
                _walkDirection = 0;
                _walk.RequestStop();
            }

            _dragging = null;
        }

        /// <summary>Letting go of a dragged hand still reacts: a deeper blush and a hold line.</summary>
        void ReactHandHeld(TouchZone zone)
        {
            if (zone == null)
                return;

            if (_director != null)
                _director.Play(new AnimationCue(zone.emotion, zone.intensity, string.Empty, 0f, zone.expression));
            if (_controller != null)
                _controller.AddTouchBlush(0.35f, 2f);

            Debug.Log("Touch: released " + zone.id);

            if (HandHeld != null)
                HandHeld(zone.id);
        }

        void ReactIfReady(TouchZone zone)
        {
            var now = Time.unscaledTime;
            if (!_cooldown.Ready(zone.id, now))
                return;

            _cooldown.Mark(zone.id, now, zone.cooldownSeconds);
            React(zone);
        }

        void React(TouchZone zone)
        {
            if (_director != null)
                _director.Play(new AnimationCue(zone.emotion, zone.intensity, zone.gesture, 0f, zone.expression));

            if (_controller != null)
                _controller.AddTouchBlush(zone.blush, 1.5f);

            Debug.Log("Touch: " + zone.id);

            if (Touched != null)
                Touched(zone.id);
        }

        void BuildProbes()
        {
            _probes.Clear();
            var zones = zoneSet.zones;
            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone == null || string.IsNullOrEmpty(zone.id))
                    continue;

                var bone = _animator.GetBoneTransform(zone.bone);
                if (bone == null)
                    continue;

                var world = zone.localOffset == Vector3.zero ? bone.position : bone.TransformPoint(zone.localOffset);
                var screen = avatarCamera.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                    continue;

                _probes.Add(new TouchZoneProbe(zone.id, new Vector2(screen.x, screen.y), zone.radiusPixels));
            }
        }

        TouchZone Find(string id)
        {
            var zones = zoneSet.zones;
            for (var i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].id == id)
                    return zones[i];
            }

            return null;
        }
    }
}
