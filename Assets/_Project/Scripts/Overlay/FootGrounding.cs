using AICompanion.Avatar;
using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Measures where her soles are, in pixels above the bottom of the window, and hands
    /// that offset to <see cref="DesktopPresence"/> so the feet, not the window edge,
    /// land on the taskbar line. The lowest of the four foot bones wins, so a walk cycle
    /// cannot lift her, and a low-pass filter keeps breathing from bobbing her.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class FootGrounding : MonoBehaviour
    {
        static readonly HumanBodyBones[] FootBones =
        {
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes,
        };

        [SerializeField] float soleOffsetMeters = 0.02f;
        [SerializeField] float halfLifeSeconds = 0.35f;
        [SerializeField] float measureIntervalSeconds = 0.15f;

        DesktopPresence _presence;
        AvatarMount _mount;
        Camera _camera;
        float _targetPixels;
        float _smoothedPixels;
        float _timer;
        bool _hasTarget;

        public float OffsetPixels
        {
            get { return _smoothedPixels; }
        }

        void LateUpdate()
        {
            if (_presence == null)
                _presence = GetComponent<DesktopPresence>();
            if (_mount == null)
                _mount = GetComponent<AvatarMount>();
            if (_presence == null || _mount == null)
                return;

            if (_camera == null)
                _camera = Camera.main;

            var instance = _mount.Instance;
            if (instance == null || _camera == null)
                return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = Mathf.Max(0.02f, measureIntervalSeconds);
                if (TryMeasure(instance, out var pixels))
                {
                    _targetPixels = pixels;
                    _hasTarget = true;
                }
            }

            if (!_hasTarget)
                return;

            _smoothedPixels = FootOffsetMath.Smooth(_smoothedPixels, _targetPixels, Time.unscaledDeltaTime, halfLifeSeconds);
            _presence.SetFootOffset(_smoothedPixels + ExtraOffset());
        }

        float ExtraOffset()
        {
            var selection = _mount.Selection;
            return selection != null ? selection.groundOffsetPx : 0f;
        }

        bool TryMeasure(GameObject instance, out float pixels)
        {
            pixels = 0f;

            var animator = instance.GetComponentInChildren<Animator>();
            Vector3 solePoint;
            if (animator != null && animator.isHuman)
            {
                var found = false;
                var lowest = float.MaxValue;
                var lowestPoint = Vector3.zero;
                for (var i = 0; i < FootBones.Length; i++)
                {
                    var bone = animator.GetBoneTransform(FootBones[i]);
                    if (bone == null)
                        continue;

                    if (bone.position.y < lowest)
                    {
                        lowest = bone.position.y;
                        lowestPoint = bone.position;
                        found = true;
                    }
                }

                if (!found)
                    return false;

                solePoint = lowestPoint + Vector3.down * soleOffsetMeters;
            }
            else
            {
                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (renderers.Length == 0)
                    return false;

                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                solePoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            var screen = _camera.WorldToScreenPoint(solePoint);
            if (screen.z <= 0f)
                return false;

            pixels = screen.y;
            return pixels >= 0f;
        }
    }
}
