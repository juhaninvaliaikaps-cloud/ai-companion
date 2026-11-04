using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// A tiny IK handle for one hand. It lives on the Animator object because Unity only calls
    /// OnAnimatorIK there, and it eases its weight so a drag blends in and out instead of
    /// snapping the arm away from the animation. The target is clamped to a human reach volume,
    /// so a drag cannot stretch the arm or fold it behind her back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarHandIk : MonoBehaviour
    {
        public const float BlendSpeed = 8f;
        public const float FollowSpeed = 18f;
        public const float UpperArmFraction = 0.55f;

        Animator _animator;
        AvatarIKGoal _goal = AvatarIKGoal.RightHand;
        Vector3 _requested;
        Vector3 _smoothed;
        float _weight;
        bool _active;
        bool _hasSmoothed;

        void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public bool Active
        {
            get { return _active; }
        }

        public AvatarIKGoal Goal
        {
            get { return _goal; }
        }

        public void Begin(AvatarIKGoal goal, Vector3 worldPosition)
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator == null)
                return;

            _goal = goal;
            _requested = worldPosition;
            _smoothed = worldPosition;
            _hasSmoothed = true;
            _active = true;
        }

        public void MoveTo(Vector3 worldPosition)
        {
            _requested = worldPosition;
        }

        public void Release()
        {
            _active = false;
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null)
                return;

            var step = Mathf.Clamp01(Time.deltaTime * BlendSpeed);
            _weight = Mathf.MoveTowards(_weight, _active ? 1f : 0f, step);
            if (_weight <= 0f)
                return;

            var target = ClampToReach(_requested);
            if (!_hasSmoothed)
            {
                _smoothed = target;
                _hasSmoothed = true;
            }
            else
            {
                _smoothed = Vector3.Lerp(_smoothed, target, Mathf.Clamp01(Time.deltaTime * FollowSpeed));
            }

            _animator.SetIKPositionWeight(_goal, _weight);
            _animator.SetIKPosition(_goal, _smoothed);
        }

        /// <summary>
        /// Keeps the target inside the arm's reach and in front of the shoulder plane. Without
        /// this the pointer could pull the hand anywhere, which reads like a helicopter rotor.
        /// </summary>
        Vector3 ClampToReach(Vector3 world)
        {
            var root = _animator.transform;
            var shoulder = _animator.GetBoneTransform(_goal == AvatarIKGoal.LeftHand ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lowerArm = _animator.GetBoneTransform(_goal == AvatarIKGoal.LeftHand ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = _animator.GetBoneTransform(_goal == AvatarIKGoal.LeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var head = _animator.GetBoneTransform(HumanBodyBones.Head);
            var hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (shoulder == null || lowerArm == null || hand == null || head == null || hips == null)
                return world;

            var shoulderLocal = root.InverseTransformPoint(shoulder.position);
            var lowerLocal = root.InverseTransformPoint(lowerArm.position);
            var handLocal = root.InverseTransformPoint(hand.position);
            var headLocal = root.InverseTransformPoint(head.position);
            var hipsLocal = root.InverseTransformPoint(hips.position);
            var targetLocal = root.InverseTransformPoint(world);

            var bounds = HandReachLimit.ForShoulder(shoulderLocal, lowerLocal, handLocal, headLocal, hipsLocal);
            var clamped = HandReachLimit.Clamp(targetLocal, shoulderLocal, bounds);
            return root.TransformPoint(clamped);
        }
    }
}
