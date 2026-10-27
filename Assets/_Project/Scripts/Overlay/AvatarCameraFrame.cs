using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Fits the overlay camera to the avatar after the animator has posed it.
    /// Humanoid bones win over renderer bounds. Zoom 0 is that full-body fit.
    /// Ctrl+scroll scales the avatar root. The window factor only grows above 1.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2000)]
    public sealed class AvatarCameraFrame : MonoBehaviour
    {
        const float SmoothRate = 12f;
        const float PortraitAspect = 480f / 720f;
        const float CloseNearClip = 0.05f;
        const float BoundsMargin = 0.5f;
        const float RendererExtentMin = 0.05f;
        const float RendererExtentMax = 4f;

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
        float _modelScale = 1f;
        float _windowFactor = 1f;
        bool _hasPivot;
        Vector3 _pivot;
        bool _hasPose;
        CameraPose _shown = new CameraPose(0f, 1f, -3.2f);
        ulong _sourceId;
        int _findCooldown;
        float _bodyCenterZ;
        bool _hasBody;
        Animator _humanoid;
        Animator[] _animators;
        Renderer[] _renderers;
        Renderer[] _avatarRenderers;
        Transform _scaleRoot;
        Vector3 _baseScale = Vector3.one;
        bool _hasGround;
        float _groundFootY;
        float _plantedScale = -1f;
        float _sampleFootY;
        bool _hasSampleFoot;
        float _pendingFootY;
        bool _pendingHasFoot;
        int _rendererCooldown;
        int _logSignature = int.MinValue;

        [SerializeField]
        [Tooltip("Logs camera framing when the zoom target, scale, or renderer set changes. Off by default.")]
        bool logCamera;

        readonly Vector3[] _points = new Vector3[256];
        readonly Vector3[] _scratch = new Vector3[256];

        public float TargetZoom => _targetZoom;
        public float ShownZoom => _shownZoom;
        public float ModelScale => _modelScale;

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

        public void SetModelScale(float scale, float windowFactor)
        {
            if (float.IsNaN(scale) || scale < 0.05f)
                scale = 0.05f;
            if (scale > 4f)
                scale = 4f;
            _modelScale = scale;
            if (float.IsNaN(windowFactor) || windowFactor < 1f)
                windowFactor = 1f;
            _windowFactor = windowFactor;
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

            if (_camera.nearClipPlane > CloseNearClip)
                _camera.nearClipPlane = CloseNearClip;
            _camera.useOcclusionCulling = false;

            ApplyRootScale();
            if (!TrySample(out var count, out var sourceId))
                return;

            float planted = PlantFeet();
            if (Mathf.Abs(planted) > 0.00001f)
            {
                for (var i = 0; i < count; i++)
                {
                    var p = _points[i];
                    p.y += planted;
                    _points[i] = p;
                }
            }

            HoldRenderersInView(count);
            count = AppendRendererCorners(_avatarRenderers != null ? _avatarRenderers : _renderers, _points, count);

            float aspect = _camera.aspect;
            if (aspect < 0.05f || float.IsNaN(aspect))
                aspect = PortraitAspect;

            float fov = _camera.fieldOfView;
            if (fov < 1f)
                fov = AvatarFrameMath.DefaultFov;

            float near = _camera.nearClipPlane;
            var pivot = _scaleRoot != null ? _scaleRoot.position : Vector3.zero;
            var full = AvatarFrameMath.FrameForAvatarScale(
                _points, count, pivot, _modelScale, _windowFactor, fov, aspect, near, _scratch);
            bool snap = !_hasPose || sourceId != _sourceId;
            if (snap)
                _hasPivot = false;

            var zoomPivot = _hasPivot ? _pivot : new Vector3(full.X, full.Y, _bodyCenterZ);
            float clearance = AvatarFrameMath.SurfaceClearance(BodyHeight(count), near);
            var desired = AvatarFrameMath.ZoomToward(full, zoomPivot, _targetZoom);
            desired = AvatarFrameMath.KeepInFront(desired, _points, count, clearance);
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
                if (_targetZoom <= 0.001f && !AvatarFrameMath.Contains(_shown, _points, count, fov, aspect, near))
                {
                    _shown = desired;
                    _shownZoom = _targetZoom;
                }
            }

            _shown = AvatarFrameMath.KeepInFront(_shown, _points, count, clearance);
            _hasPose = true;
            _sourceId = sourceId;
            _camera.transform.SetPositionAndRotation(_shown.Position, Quaternion.identity);
            LogCamera(count);
        }

        void ApplyRootScale()
        {
            if (_scaleRoot == null)
                return;

            var scale = _baseScale * _modelScale;
            if (_scaleRoot.localScale != scale)
                _scaleRoot.localScale = scale;
        }

        float PlantFeet()
        {
            if (_scaleRoot == null || !_hasSampleFoot)
                return 0f;

            float scale = _modelScale < 0.0001f ? 0.0001f : _modelScale;
            float footY = _sampleFootY;
            if (!_hasGround)
            {
                _groundFootY = AvatarFrameMath.UnitFootY(_scaleRoot.position.y, footY, scale);
                _hasGround = true;
                _plantedScale = -1f;
            }

            if (Mathf.Abs(_plantedScale - scale) <= 0.0001f)
                return 0f;

            float shift = _groundFootY - footY;
            _scaleRoot.position += new Vector3(0f, shift, 0f);
            _sampleFootY += shift;
            _plantedScale = scale;
            return shift;
        }

        bool TrySample(out int count, out ulong sourceId)
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
            ulong bestId = 0;
            if (_animators != null)
            {
                for (var i = 0; i < _animators.Length; i++)
                {
                    var animator = _animators[i];
                    if (animator == null || !animator.isActiveAndEnabled || !animator.isHuman)
                        continue;

                    if (_scaleRoot == null || animator.transform == _scaleRoot)
                        NoteScaleRoot(animator.transform);
                    int n = SampleHumanoid(animator, _scratch);
                    if (n <= bestCount)
                        continue;

                    if (animator.transform != _scaleRoot)
                    {
                        NoteScaleRoot(animator.transform);
                        n = SampleHumanoid(animator, _scratch);
                    }

                    bestCount = n;
                    bestId = EntityId.ToULong(animator.GetEntityId());
                    _humanoid = animator;
                    _sampleFootY = _pendingFootY;
                    _hasSampleFoot = _pendingHasFoot;
                    Array.Copy(_scratch, _points, n);
                }
            }

            if (bestCount >= 2)
            {
                count = bestCount;
                sourceId = bestId;
                RefreshAvatarRenderers();
                if (_avatarRenderers != null)
                {
                    for (var i = 0; i < _avatarRenderers.Length; i++)
                    {
                        var renderer = _avatarRenderers[i];
                        if (renderer != null)
                            sourceId ^= EntityId.ToULong(renderer.GetEntityId());
                    }
                }

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

        void NoteScaleRoot(Transform root)
        {
            if (root == null || root == _scaleRoot)
                return;

            _scaleRoot = root;
            _baseScale = root.localScale;
            _hasGround = false;
            _plantedScale = -1f;
            _avatarRenderers = null;
            root.localScale = _baseScale * _modelScale;
        }

        void RefreshAvatarRenderers()
        {
            _rendererCooldown--;
            if (_scaleRoot == null)
                return;
            if (_avatarRenderers != null && _rendererCooldown > 0)
                return;

            _rendererCooldown = 30;
            _avatarRenderers = _scaleRoot.GetComponentsInChildren<Renderer>(true);
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
            {
                _pendingHasFoot = false;
                return 0;
            }

            _pendingFootY = minY;
            _pendingHasFoot = true;
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

        static int SampleRenderers(Renderer[] renderers, Vector3[] points, out ulong sourceId)
        {
            sourceId = 0;
            var count = 0;
            if (renderers == null)
                return 0;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!SaneRenderer(renderer))
                    continue;

                sourceId ^= EntityId.ToULong(renderer.GetEntityId());
                count = AddBoundsCorners(points, count, renderer.bounds);
            }

            return count;
        }

        int AppendRendererCorners(Renderer[] renderers, Vector3[] points, int count)
        {
            if (renderers == null || count <= 0)
                return count;

            var center = new Vector3(0f, 0f, _bodyCenterZ);
            float y = 0f;
            for (var i = 0; i < count; i++)
                y += points[i].y;
            center.y = y / count;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!SaneRenderer(renderer))
                    continue;

                var bounds = renderer.bounds;
                if ((bounds.center - center).sqrMagnitude > 9f)
                    continue;

                count = AddBoundsCorners(points, count, bounds);
            }

            return count;
        }

        static int AddBoundsCorners(Vector3[] points, int count, Bounds bounds)
        {
            var c = bounds.center;
            var e = bounds.extents;
            for (var corner = 0; corner < 8; corner++)
            {
                if (count >= points.Length)
                    return count;

                points[count++] = c + new Vector3(
                    (corner & 1) == 0 ? -e.x : e.x,
                    (corner & 2) == 0 ? -e.y : e.y,
                    (corner & 4) == 0 ? -e.z : e.z);
            }

            return count;
        }

        void HoldRenderersInView(int count)
        {
            var renderers = _avatarRenderers;
            if (renderers == null || count <= 0)
                return;

            var world = new Bounds(_points[0], Vector3.zero);
            for (var i = 1; i < count; i++)
                world.Encapsulate(_points[i]);
            world.Expand(BoundsMargin);

            for (var i = 0; i < renderers.Length; i++)
            {
                var skinned = renderers[i] as SkinnedMeshRenderer;
                if (skinned == null)
                    continue;

                skinned.updateWhenOffscreen = true;
                skinned.allowOcclusionWhenDynamic = false;
                skinned.localBounds = WorldAabbToLocal(skinned.transform, world);
            }
        }

        static Bounds WorldAabbToLocal(Transform transform, Bounds world)
        {
            var center = world.center;
            var extents = world.extents;
            var local = new Bounds(transform.InverseTransformPoint(center), Vector3.zero);
            for (var corner = 0; corner < 8; corner++)
            {
                var worldCorner = center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                local.Encapsulate(transform.InverseTransformPoint(worldCorner));
            }

            return local;
        }

        static bool SaneRenderer(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return false;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                return false;

            var size = renderer.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return longest >= RendererExtentMin && longest <= RendererExtentMax;
        }

        float BodyHeight(int count)
        {
            if (count <= 0)
                return 1.6f;

            float minY = _points[0].y;
            float maxY = _points[0].y;
            for (var i = 1; i < count; i++)
            {
                float y = _points[i].y;
                if (y < minY)
                    minY = y;
                if (y > maxY)
                    maxY = y;
            }

            float height = maxY - minY;
            return height > 0.01f ? height : 0.01f;
        }

        void LogCamera(int pointCount)
        {
            if (!logCamera || _camera == null)
                return;

            var listed = _avatarRenderers != null ? _avatarRenderers : _renderers;
            int rendererCount = listed != null ? listed.Length : 0;
            int skinnedCount = 0;
            // Shown zoom, bounds, and isVisible change every frame while the camera eases.
            // The signature uses the zoom target and the renderer set, so a smooth move logs once.
            int signature = pointCount * 17 + rendererCount * 13 + Mathf.RoundToInt(_modelScale * 100f);
            signature = signature * 31 + Mathf.RoundToInt(_targetZoom * 100f);
            signature ^= _camera.cullingMask;
            signature = signature * 2 + (_camera.useOcclusionCulling ? 1 : 0);
            if (listed != null)
            {
                for (var i = 0; i < listed.Length; i++)
                {
                    var renderer = listed[i];
                    if (renderer == null)
                        continue;
                    if (renderer is SkinnedMeshRenderer)
                        skinnedCount++;
                    signature = signature * 31 + renderer.gameObject.layer;
                    signature = signature * 2 + (renderer.enabled ? 1 : 0);
                }
            }

            signature = signature * 31 + skinnedCount;
            if (signature == _logSignature)
                return;

            _logSignature = signature;
            var text = new StringBuilder(256 + rendererCount * 96);
            text.Append("Camera: zoom=").Append(_shownZoom.ToString("0.00", CultureInfo.InvariantCulture));
            text.Append(" size=").Append(Mathf.RoundToInt(_modelScale * 100f)).Append('%');
            text.Append(" near=").Append(_camera.nearClipPlane.ToString("0.00", CultureInfo.InvariantCulture));
            text.Append(" far=").Append(_camera.farClipPlane.ToString("0.0", CultureInfo.InvariantCulture));
            text.Append(" mask=").Append(_camera.cullingMask.ToString("x8", CultureInfo.InvariantCulture));
            text.Append(" occlusion=").Append(_camera.useOcclusionCulling ? 1 : 0);
            text.Append(" points=").Append(pointCount);
            text.Append(" renderers=").Append(rendererCount);
            text.Append(" skinned=").Append(skinnedCount);
            if (listed != null)
            {
                for (var i = 0; i < listed.Length; i++)
                {
                    var renderer = listed[i];
                    if (renderer == null)
                        continue;

                    int layer = renderer.gameObject.layer;
                    bool inMask = (_camera.cullingMask & (1 << layer)) != 0;
                    var bounds = renderer.bounds;
                    var skinned = renderer as SkinnedMeshRenderer;
                    text.Append(" | ");
                    text.Append(renderer.name);
                    text.Append(" en=").Append(renderer.enabled ? 1 : 0);
                    text.Append(" vis=").Append(renderer.isVisible ? 1 : 0);
                    text.Append(" layer=").Append(layer);
                    text.Append(':').Append(LayerMask.LayerToName(layer));
                    text.Append(" inMask=").Append(inMask ? 1 : 0);
                    text.Append(" boundsCenter=(");
                    AppendVec(text, bounds.center);
                    text.Append(") boundsSize=(");
                    AppendVec(text, bounds.size);
                    text.Append(')');
                    if (skinned != null)
                    {
                        text.Append(" skinned=1 offscreen=").Append(skinned.updateWhenOffscreen ? 1 : 0);
                        text.Append(" localSize=(");
                        AppendVec(text, skinned.localBounds.size);
                        text.Append(')');
                    }
                }
            }

            Debug.Log(text.ToString());
        }

        static void AppendVec(StringBuilder text, Vector3 value)
        {
            text.Append(value.x.ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            text.Append(value.y.ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            text.Append(value.z.ToString("0.00", CultureInfo.InvariantCulture));
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
