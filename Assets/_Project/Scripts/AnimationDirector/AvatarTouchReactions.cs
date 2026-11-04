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

        /// <summary>Raised with the zone id when a touch reaction plays, so the chat can react in words.</summary>
        public event Action<string> Touched;

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
                // A drag is not a touch: the hand was moved, not patted.
                EndHandDrag();
                _pressed = false;
                _pressZone = null;
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
            _ik.MoveTo(avatarCamera.ScreenToWorldPoint(pointer));
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
            Debug.Log("Touch: dragging " + zone.id);
        }

        void EndHandDrag()
        {
            if (_dragging == null)
                return;

            if (_ik != null)
                _ik.Release();

            _dragging = null;
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
