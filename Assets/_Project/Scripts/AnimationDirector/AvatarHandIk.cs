using UnityEngine;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// A tiny IK handle for one hand. It lives on the Animator object because Unity only calls
    /// OnAnimatorIK there, and it eases its weight so a drag blends in and out instead of
    /// snapping the arm away from the animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarHandIk : MonoBehaviour
    {
        public const float BlendSpeed = 8f;

        Animator _animator;
        AvatarIKGoal _goal = AvatarIKGoal.RightHand;
        Vector3 _target;
        float _weight;
        bool _active;

        void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public bool HasAnimator
        {
            get { return _animator != null || (GetComponent<Animator>() != null); }
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
            _target = worldPosition;
            _active = true;
        }

        public void MoveTo(Vector3 worldPosition)
        {
            _target = worldPosition;
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

            var goal = _goal;
            _animator.SetIKPositionWeight(goal, _weight);
            _animator.SetIKPosition(goal, _target);
        }
    }
}
