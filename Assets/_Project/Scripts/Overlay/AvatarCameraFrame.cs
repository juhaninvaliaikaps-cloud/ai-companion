using System;
using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Fits the overlay camera to the avatar after the animator has posed it.
    /// Humanoid bones win over renderer bounds. Zoom 0 is that full-body fit.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2000)]
    public sealed class AvatarCameraFrame : MonoBehaviour
    {
        const float SmoothRate = 12f;
        const float PortraitAspect = 480f / 720f;

        static readonly HumanBodyBones[] SampleBones =
        {
            HumanBodyBones.Head,
            HumanBodyBones.Chest,
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes,
        };

        Camera _camera;
        float _targetZoom;
        float _shownZoom;
        bool _hasPivot;
        Vector3 _pivot;
        bool _hasPose;
        CameraPose _shown = new CameraPose(0f, 1f, -3.2f);
        int _sourceId;
        int _findCooldown;
        float _bodyCenterZ;
        bool _hasBody;
        Animator _humanoid;
        Animator[] _animators;
        Renderer[] _renderers;
        readonly Vector3[] _points = new Vector3[64];
        readonly Vector3[] _scratch = new Vector3[64];

        public float TargetZoom => _targetZoom;
        public float ShownZoom => _shownZoom;

        public void Attach(Camera camera)
        {
            if (camera != null)
                _camera = camera;
        }

        public void SetZoom(float zoom)
        {
            _targetZoom = Mathf.Clamp01(zoom);
        }

        public void SetZoomToward(float zoom, Vector3 pivot)
        {
            _targetZoom = Mathf.Clamp01(zoom);
            _pivot = pivot;
            _hasPivot = true;
        }

        public void ResetZoom()
        {
            _targetZoom = 0f;
            _hasPivot = false;
        }

        public bool TryPivotFromCursor(Vector2 unityScreen, out Vector3 pivot)
        {
            pivot = default;
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return false;

            var ray = _camera.ScreenPointToRay(unityScreen);
            if (Mathf.Abs(ray.direction.z) < 0.0001f)
                return false;

            float planeZ = _hasBody ? _bodyCenterZ : 0f;
            float t = (planeZ - ray.origin.z) / ray.direction.z;
            if (t <= 0.01f)
                return false;

            pivot = ray.origin + ray.direction * t;
            return true;
        }

        void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            if (!TrySample(out var count, out var sourceId))
                return;

            float aspect = _camera.aspect;
            if (aspect < 0.05f || float.IsNaN(aspect))
                aspect = PortraitAspect;

            float fov = _camera.fieldOfView;
            if (fov < 1f)
                fov = AvatarFrameMath.DefaultFov;

            var full = AvatarFrameMath.Fit(_points, count, fov, aspect, _camera.nearClipPlane);
            bool snap = !_hasPose || sourceId != _sourceId;
            if (snap)
                _hasPivot = false;

            var pivot = _hasPivot ? _pivot : new Vector3(full.X, full.Y, _bodyCenterZ);
            var desired = AvatarFrameMath.ZoomToward(full, pivot, _targetZoom);
            float dt = Time.unscaledDeltaTime;
            if (snap || dt <= 0f)
            {
                _shown = desired;
                _shownZoom = _targetZoom;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-SmoothRate * dt);
                _shown = LerpPose(_shown, desired, blend);
                _shownZoom = Mathf.Lerp(_shownZoom, _targetZoom, blend);
                if (_targetZoom <= 0.001f && !AvatarFrameMath.Contains(_shown, _points, count, fov, aspect, _camera.nearClipPlane))
                {
                    _shown = desired;
                    _shownZoom = _targetZoom;
                }
            }

            _hasPose = true;
            _sourceId = sourceId;
            _camera.transform.SetPositionAndRotation(_shown.Position, Quaternion.identity);
        }

        bool TrySample(out int count, out int sourceId)
        {
            count = 0;
            sourceId = 0;
            _findCooldown--;
            bool refresh = _animators == null || _findCooldown <= 0;
            if (!refresh && _humanoid != null && !_humanoid.isActiveAndEnabled)
                refresh = true;
            if (refresh)
            {
                _findCooldown = 30;
                _animators = FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                _renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }

            var bestCount = 0;
            var bestId = 0;
            if (_animators != null)
            {
                for (var i = 0; i < _animators.Length; i++)
                {
                    var animator = _animators[i];
                    if (animator == null || !animator.isActiveAndEnabled || !animator.isHuman)
                        continue;

                    int n = SampleHumanoid(animator, _scratch);
                    if (n <= bestCount)
                        continue;

                    bestCount = n;
                    bestId = animator.GetInstanceID();
                    _humanoid = animator;
                    Array.Copy(_scratch, _points, n);
                }
            }

            if (bestCount >= 2)
            {
                count = bestCount;
                sourceId = bestId;
                _bodyCenterZ = AverageZ(_points, count);
                _hasBody = true;
                return true;
            }

            _humanoid = null;
            count = SampleRenderers(_renderers, _points, out sourceId);
            if (count < 2)
                return false;

            _bodyCenterZ = AverageZ(_points, count);
            _hasBody = true;
            return true;
        }

        int SampleHumanoid(Animator animator, Vector3[] points)
        {
            var cameraPosition = _camera != null ? _camera.transform.position : new Vector3(0f, 1f, -3.2f);
            Transform head = null;
            Transform leftFoot = null;
            Transform rightFoot = null;
            Transform leftToes = null;
            Transform rightToes = null;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            var count = 0;
            for (var i = 0; i < SampleBones.Length; i++)
            {
                var bone = animator.GetBoneTransform(SampleBones[i]);
                if (bone == null || count >= points.Length)
                    continue;

                var position = bone.position;
                points[count++] = position;
                if (position.y < minY)
                    minY = position.y;
                if (position.y > maxY)
                    maxY = position.y;

                switch (SampleBones[i])
                {
                    case HumanBodyBones.Head:
                        head = bone;
                        break;
                    case HumanBodyBones.LeftFoot:
                        leftFoot = bone;
                        break;
                    case HumanBodyBones.RightFoot:
                        rightFoot = bone;
                        break;
                    case HumanBodyBones.LeftToes:
                        leftToes = bone;
                        break;
                    case HumanBodyBones.RightToes:
                        rightToes = bone;
                        break;
                }
            }

            if (count < 2)
                return 0;

            float span = maxY - minY;
            if (span < 0.05f)
                span = 0.05f;

            if (head != null && count < points.Length)
                points[count++] = head.position + Vector3.up * Mathf.Max(0.12f, span * 0.10f);

            count = AddToeEstimate(points, count, leftToes != null ? leftToes : leftFoot, leftToes != null, span, cameraPosition);
            count = AddToeEstimate(points, count, rightToes != null ? rightToes : rightFoot, rightToes != null, span, cameraPosition);
            return count;
        }

        static int AddToeEstimate(Vector3[] points, int count, Transform bone, bool isToe, float span, Vector3 cameraPosition)
        {
            if (bone == null || count >= points.Length)
                return count;

            var position = bone.position;
            var toward = cameraPosition - position;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.000001f)
                toward = Vector3.back;
            else
                toward.Normalize();

            float forward = isToe ? Mathf.Max(0.05f, span * 0.04f) : Mathf.Max(0.12f, span * 0.09f);
            float down = isToe ? 0f : Mathf.Max(0.06f, span * 0.05f);
            points[count] = position + toward * forward + Vector3.down * down;
            return count + 1;
        }

        static int SampleRenderers(Renderer[] renderers, Vector3[] points, out int sourceId)
        {
            sourceId = 0;
            var count = 0;
            if (renderers == null)
                return 0;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!UsableRenderer(renderer))
                    continue;

                sourceId ^= renderer.GetInstanceID();
                var center = renderer.bounds.center;
                var extents = renderer.bounds.extents;
                for (var corner = 0; corner < 8; corner++)
                {
                    if (count >= points.Length)
                        return count;

                    points[count++] = center + new Vector3(
                        (corner & 1) == 0 ? -extents.x : extents.x,
                        (corner & 2) == 0 ? -extents.y : extents.y,
                        (corner & 4) == 0 ? -extents.z : extents.z);
                }
            }

            return count;
        }

        static bool UsableRenderer(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return false;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                return false;

            var size = renderer.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return longest >= 0.05f && longest <= 8f;
        }

        static float AverageZ(Vector3[] points, int count)
        {
            float sum = 0f;
            var n = 0;
            for (var i = 0; i < count; i++)
            {
                sum += points[i].z;
                n++;
            }

            return n > 0 ? sum / n : 0f;
        }

        static CameraPose LerpPose(CameraPose from, CameraPose to, float t)
        {
            return new CameraPose(
                Mathf.Lerp(from.X, to.X, t),
                Mathf.Lerp(from.Y, to.Y, t),
                Mathf.Lerp(from.Z, to.Z, t));
        }
    }
}
