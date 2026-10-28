using System;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Blink, breath, idle sway, and cursor look-at on top of the posed humanoid.
    /// Bone offsets are applied in LateUpdate after the Animator. The blink weight
    /// is handed to <see cref="AvatarAnimationController"/> so it shares the face
    /// write with emotions and lip sync and does not replace mouth shapes.
    /// The Gestures layer is left untouched. Sway and look fade while that layer
    /// plays Wave, Nod, Shrug, Laugh, Walk, or Turn180, and while Walking is set.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12050)]
    public sealed class ProceduralLife : MonoBehaviour
    {
        static readonly int WalkingHash = Animator.StringToHash("Walking");
        static readonly int[] GestureHashes = BuildGestureHashes();

        readonly System.Random _random = new System.Random();
        readonly List<string> _meshNames = new List<string>(32);

        BlinkScheduler _blink;
        IdleVariationPlayer _idle;
        CursorLookModel _look;
        AvatarAnimationController _face;
        IScreenCursor _cursor;
        Camera _camera;
        Animator _animator;
        Transform _spine;
        Transform _chest;
        Transform _neck;
        Transform _head;
        Transform _leftEye;
        Transform _rightEye;
        string _blinkLog;
        int _gestureLayer = -1;
        bool _hasWalking;
        bool _cursorLookEnabled = true;
        bool _started;
        float _bodyWeight = 1f;
        float _lookWeight;
        float _breathTime;

        public bool CursorLookEnabled
        {
            get { return _cursorLookEnabled; }
            set { _cursorLookEnabled = value; }
        }

        void OnEnable()
        {
            if (_face == null)
                _face = GetComponent<AvatarAnimationController>();
            if (_cursor == null)
                _cursor = GetComponent<IScreenCursor>();
            if (_started)
                return;

            _started = true;
            _blink = BlinkScheduler.Start(Next01(), Next01());
            _idle = IdleVariationPlayer.Begin(0, Next01());
        }

        public void Bind(GameObject instance, AvatarDescriptor descriptor)
        {
            _animator = null;
            _spine = null;
            _chest = null;
            _neck = null;
            _head = null;
            _leftEye = null;
            _rightEye = null;
            _gestureLayer = -1;
            _hasWalking = false;
            _camera = null;

            if (_face == null)
                _face = GetComponent<AvatarAnimationController>();
            if (_face != null)
                _face.SetProceduralBlink(0f);

            LogBlink(instance, descriptor);
            if (instance == null)
                return;

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
                return;

            _animator = animator;
            _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (_chest == null)
                _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            _neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            _rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            CacheGesture(animator);
        }

        void LateUpdate()
        {
            var deltaTime = Time.deltaTime;
            if (deltaTime < 0f)
                deltaTime = 0f;

            if (!_started)
                OnEnable();

            _blink = _blink.Tick(deltaTime, Next01(), Next01(), Next01());
            if (_face == null)
                _face = GetComponent<AvatarAnimationController>();
            if (_face != null)
                _face.SetProceduralBlink(_blink.Weight);

            _idle = _idle.Advance(deltaTime, Next01(), Next01());
            _breathTime += deltaTime;
            if (_breathTime > 1000f)
                _breathTime -= 1000f;

            var suppress = Suppress();
            _bodyWeight = GestureActivity.FadeWeight(_bodyWeight, suppress, deltaTime, GestureActivity.FadeSeconds);
            _lookWeight = GestureActivity.FadeWeight(
                _lookWeight,
                suppress || !_cursorLookEnabled,
                deltaTime,
                GestureActivity.FadeSeconds);

            float cursorX;
            float cursorY;
            var aimed = TryAim(out cursorX, out cursorY, out var deltaX, out var deltaY);
            _look = _look.Advance(_cursorLookEnabled && !suppress, aimed, deltaX, deltaY, cursorX, cursorY, deltaTime);

            if (_spine == null && _chest == null && _neck == null && _head == null)
                return;

            var pose = ProceduralPose.Compose(
                _idle.Sample(),
                BreathingMotion.ChestPitch(_breathTime),
                _look.Head,
                _look.Eyes,
                _bodyWeight,
                _lookWeight);
            pose = ProceduralPose.FoldMissingNeck(pose, _neck != null);
            var root = _animator != null ? _animator.transform : null;
            if (root == null)
                return;

            var bodyYaw = root.up;
            var bodyPitch = root.right;
            var bodyRoll = root.forward;
            var lookYaw = bodyYaw;
            var lookPitch = bodyPitch;
            var camera = ViewCamera();
            if (camera != null)
            {
                lookYaw = camera.transform.up;
                lookPitch = camera.transform.right;
            }

            Add(_spine, pose.SpinePitch, pose.SpineYaw, pose.SpineRoll, bodyPitch, bodyYaw, bodyRoll);
            Add(_chest, pose.ChestPitch, 0f, 0f, bodyPitch, bodyYaw, bodyRoll);
            Add(_neck, pose.NeckPitch, pose.NeckYaw, 0f, lookPitch, lookYaw, bodyRoll);
            Add(_head, pose.HeadPitch, pose.HeadYaw, pose.HeadRoll, lookPitch, lookYaw, bodyRoll);
            Add(_leftEye, pose.EyePitch, pose.EyeYaw, 0f, lookPitch, lookYaw, bodyRoll);
            Add(_rightEye, pose.EyePitch, pose.EyeYaw, 0f, lookPitch, lookYaw, bodyRoll);
        }

        bool TryAim(out float cursorX, out float cursorY, out float deltaX, out float deltaY)
        {
            cursorX = 0f;
            cursorY = 0f;
            deltaX = 0f;
            deltaY = 0f;
            if (_cursor == null)
                _cursor = GetComponent<IScreenCursor>();

            var hasCursor = _cursor != null && _cursor.TryGetUnityScreenCursor(out cursorX, out cursorY);
            if (!hasCursor)
            {
                var mouse = Input.mousePosition;
                cursorX = mouse.x;
                cursorY = mouse.y;
            }

            if (_head == null)
                return false;

            var camera = ViewCamera();
            if (camera == null)
                return false;

            var screen = camera.WorldToScreenPoint(_head.position);
            if (screen.z <= 0.05f)
                return false;

            deltaX = cursorX - screen.x;
            deltaY = cursorY - screen.y;
            return true;
        }

        bool Suppress()
        {
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
                return false;
            if (_hasWalking && _animator.GetBool(WalkingHash))
                return true;
            if (_gestureLayer < 0 || _gestureLayer >= _animator.layerCount)
                return false;

            var current = _animator.GetCurrentAnimatorStateInfo(_gestureLayer);
            if (IsGesture(current.shortNameHash))
                return true;
            if (!_animator.IsInTransition(_gestureLayer))
                return false;

            var next = _animator.GetNextAnimatorStateInfo(_gestureLayer);
            return IsGesture(next.shortNameHash);
        }

        Camera ViewCamera()
        {
            if (_camera == null)
                _camera = Camera.main;
            return _camera;
        }

        void CacheGesture(Animator animator)
        {
            if (animator.runtimeAnimatorController == null)
                return;

            var layers = animator.layerCount;
            for (var i = 0; i < layers; i++)
            {
                if (string.Equals(animator.GetLayerName(i), "Gestures", StringComparison.Ordinal))
                {
                    _gestureLayer = i;
                    break;
                }
            }

            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == WalkingHash)
                {
                    _hasWalking = true;
                    break;
                }
            }
        }

        void LogBlink(GameObject instance, AvatarDescriptor descriptor)
        {
            _meshNames.Clear();
            if (instance != null)
            {
                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (var r = 0; r < renderers.Length; r++)
                {
                    var mesh = renderers[r].sharedMesh;
                    if (mesh == null)
                        continue;

                    var count = mesh.blendShapeCount;
                    for (var shape = 0; shape < count; shape++)
                    {
                        var name = mesh.GetBlendShapeName(shape);
                        if (!string.IsNullOrEmpty(name))
                            _meshNames.Add(name);
                    }
                }
            }

            IReadOnlyList<string> mapped = null;
            if (descriptor != null && descriptor.TryGetExpression(ExpressionPreset.Blink, out var blinkNames))
                mapped = blinkNames;

            var selected = BlinkShapeSelection.Collect(_meshNames, mapped);
            _meshNames.Clear();
            var line = BlinkShapeSelection.FormatLog(selected);
            if (string.Equals(line, _blinkLog, StringComparison.Ordinal))
                return;

            _blinkLog = line;
            Debug.Log(line);
        }

        float Next01()
        {
            return (float)_random.NextDouble();
        }

        static bool IsGesture(int shortNameHash)
        {
            for (var i = 0; i < GestureHashes.Length; i++)
            {
                if (GestureHashes[i] == shortNameHash)
                    return true;
            }

            return false;
        }

        static int[] BuildGestureHashes()
        {
            var names = GestureActivity.StateNames;
            var hashes = new int[names.Length];
            for (var i = 0; i < names.Length; i++)
                hashes[i] = Animator.StringToHash(names[i]);
            return hashes;
        }

        static void Add(Transform bone, float pitch, float yaw, float roll, Vector3 pitchAxis, Vector3 yawAxis, Vector3 rollAxis)
        {
            if (bone == null)
                return;
            if (pitch > -0.01f && pitch < 0.01f && yaw > -0.01f && yaw < 0.01f && roll > -0.01f && roll < 0.01f)
                return;

            var delta = Quaternion.AngleAxis(yaw, yawAxis)
                * Quaternion.AngleAxis(pitch, pitchAxis)
                * Quaternion.AngleAxis(roll, rollAxis);
            bone.rotation = delta * bone.rotation;
        }
    }
}
