using System.Collections.Generic;
using AICompanion.Avatar;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// Turns clicks on her silhouette into small reactions: a face change, a gesture and a
    /// blush, driven by a <see cref="TouchZoneSet"/>. Clicks over UI and the window drag keep
    /// working because the overlay only dispatches when the pointer is on her body.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarTouchReactions : MonoBehaviour, IAvatarTouch
    {
        [SerializeField] TouchZoneSet zoneSet;
        [SerializeField] Camera avatarCamera;

        readonly List<TouchZoneProbe> _probes = new List<TouchZoneProbe>(8);
        TouchCooldown _cooldown = new TouchCooldown(1.5f);
        Animator _animator;
        AnimationDirector _director;
        AvatarAnimationController _controller;
        TouchZone _held;
        bool _touching;

        /// <summary>Raised with the zone id after a reaction, so the chat can react in words.</summary>
        public event System.Action<string> Touched;

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

        /// <summary>The zone that is currently held down, or null.</summary>
        public string HeldZoneId
        {
            get { return _held != null ? _held.id : null; }
        }

        public void OnAvatarTouch(Vector2 screenPosition)
        {
            Resolve();
            if (zoneSet == null || zoneSet.zones == null || _animator == null || avatarCamera == null)
                return;

            BuildProbes();
            string id;
            if (!TouchZonePicker.TryPick(_probes, screenPosition, out id))
                return;

            var zone = Find(id);
            if (zone == null)
                return;

            var now = Time.unscaledTime;
            if (!_cooldown.Ready(zone.id, now))
                return;

            _cooldown.Mark(zone.id, now, zone.cooldownSeconds);
            React(zone);
            _touching = true;

            if (zone.hold)
                _held = zone;
        }

        public void OnAvatarRelease()
        {
            if (!_touching)
                return;

            _touching = false;
            _held = null;
        }

        void React(TouchZone zone)
        {
            if (_director != null)
                _director.Play(new AnimationCue(zone.emotion, zone.intensity, zone.gesture, 0f, zone.expression));

            if (_controller != null)
            {
                var seconds = zone.hold ? 2.5f : 1.5f;
                _controller.AddTouchBlush(zone.blush, seconds);
            }

            Debug.Log("Touch: " + zone.id + (zone.hold ? " (hold)" : string.Empty));

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

                var screen = avatarCamera.WorldToScreenPoint(bone.position);
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
