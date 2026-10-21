using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;
using UnityEngine.Events;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Plays the shared humanoid controller and eases expression blendshapes.
    /// Animator update mode stays on Normal, so clips keep advancing when the
    /// overlay drops the player to 15 fps. They just step with a larger delta.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class AvatarAnimationController : MonoBehaviour, IAvatarAnimator
    {
        static readonly int TalkingHash = Animator.StringToHash(AvatarAnimatorParameters.Talking);
        static readonly int EmotionHash = Animator.StringToHash(AvatarAnimatorParameters.Emotion);
        static readonly int IntensityHash = Animator.StringToHash(AvatarAnimatorParameters.EmotionIntensity);
        static readonly ShapeBinding[] NoBindings = new ShapeBinding[0];
        static readonly KeyCode[] DebugKeys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Keypad1,
            KeyCode.Keypad2,
            KeyCode.Keypad3,
            KeyCode.Keypad4,
            KeyCode.Keypad5,
            KeyCode.T
        };

        [SerializeField] RuntimeAnimatorController controller;

        readonly AvatarAnimatorModel _model = new AvatarAnimatorModel();
        ShapeBinding[] _bindings = NoBindings;
        Animator _animator;
        bool _hasTalking;
        bool _hasEmotion;
        bool _hasIntensity;
        bool _warnedMissingController;
        float _talkAmount;
        float _talkTime;
        FacePose _face;
        UnityAction _beforeRender;

        struct ShapeBinding
        {
            public SkinnedMeshRenderer Renderer;
            public int Index;
            public ExpressionPreset Preset;
        }

        public bool IsTalking => _model.IsTalking;

        public Emotion Emotion => _model.Emotion;

        public float EmotionIntensity => _model.EmotionIntensity;

        public void SetTalking(bool talking)
        {
            _model.SetTalking(talking);
            PushParameters();
        }

        public void SetEmotion(Emotion emotion, float intensity)
        {
            _model.SetEmotion(emotion, intensity);
            PushParameters();
        }

        public void Attach(GameObject instance, AvatarDescriptor descriptor)
        {
            _animator = null;
            _hasTalking = false;
            _hasEmotion = false;
            _hasIntensity = false;
            _bindings = NoBindings;
            if (instance == null)
                return;

            BindAnimator(instance);
            _bindings = BindShapes(instance, descriptor);
            PushParameters();
        }

        void OnEnable()
        {
            if (_beforeRender == null)
                _beforeRender = ApplyCurrentFace;
            Application.onBeforeRender += _beforeRender;
        }

        void OnDisable()
        {
            if (_beforeRender != null)
                Application.onBeforeRender -= _beforeRender;
        }

        void Update()
        {
            if (!Input.anyKeyDown)
                return;

            var changed = false;
            for (var i = 0; i < DebugKeys.Length; i++)
            {
                if (Input.GetKeyDown(DebugKeys[i]))
                    changed |= _model.TryApplyDebugKey(DebugKeys[i]);
            }

            if (changed)
                PushParameters();
        }

        void LateUpdate()
        {
            AdvanceFace(Time.deltaTime);
            ApplyFace(_face);
        }

        void BindAnimator(GameObject instance)
        {
            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
                return;

            _animator = animator;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.speed = 1f;

            if (controller == null)
            {
                if (!_warnedMissingController)
                {
                    _warnedMissingController = true;
                    Debug.LogWarning("Companion animator controller is not assigned. Body clips are skipped. Face expressions still apply.");
                }

                return;
            }

            animator.runtimeAnimatorController = controller;
            CacheParameters(animator);
        }

        void CacheParameters(Animator animator)
        {
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.name == AvatarAnimatorParameters.Talking && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasTalking = true;
                else if (parameter.name == AvatarAnimatorParameters.Emotion && parameter.type == AnimatorControllerParameterType.Int)
                    _hasEmotion = true;
                else if (parameter.name == AvatarAnimatorParameters.EmotionIntensity && parameter.type == AnimatorControllerParameterType.Float)
                    _hasIntensity = true;
            }
        }

        void PushParameters()
        {
            if (_animator == null)
                return;

            if (_hasTalking)
                _animator.SetBool(TalkingHash, _model.IsTalking);
            if (_hasEmotion)
                _animator.SetInteger(EmotionHash, (int)_model.Emotion);
            if (_hasIntensity)
                _animator.SetFloat(IntensityHash, _model.EmotionIntensity);
        }

        static ShapeBinding[] BindShapes(GameObject instance, AvatarDescriptor descriptor)
        {
            var lookup = new Dictionary<string, ExpressionPreset>(System.StringComparer.Ordinal);
            if (descriptor != null && descriptor.Expressions != null)
            {
                foreach (var pair in descriptor.Expressions)
                {
                    var names = pair.Value;
                    if (names == null)
                        continue;
                    for (var i = 0; i < names.Count; i++)
                    {
                        var shapeName = names[i];
                        if (!string.IsNullOrEmpty(shapeName))
                            lookup[shapeName] = pair.Key;
                    }
                }
            }

            var found = new List<ShapeBinding>(32);
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                var mesh = renderer.sharedMesh;
                if (mesh == null)
                    continue;

                var count = mesh.blendShapeCount;
                for (var shape = 0; shape < count; shape++)
                {
                    var shapeName = mesh.GetBlendShapeName(shape);
                    if (string.IsNullOrEmpty(shapeName))
                        continue;

                    if (!lookup.TryGetValue(shapeName, out var preset))
                        preset = ExpressionPresetMap.Classify(shapeName);
                    if (preset == ExpressionPreset.None)
                        continue;

                    found.Add(new ShapeBinding
                    {
                        Renderer = renderer,
                        Index = shape,
                        Preset = preset
                    });
                }
            }

            return found.Count == 0 ? NoBindings : found.ToArray();
        }

        void AdvanceFace(float deltaTime)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            var step = EmotionFace.BlendStep(deltaTime, EmotionFace.BlendSeconds);
            var targetAmount = _model.IsTalking ? 1f : 0f;
            _talkAmount += (targetAmount - _talkAmount) * step;

            if (_talkAmount > 0.001f)
            {
                _talkTime += deltaTime;
                if (_talkTime >= 60f)
                    _talkTime %= 60f;
            }

            var target = EmotionFace.Combine(
                EmotionFace.EmotionPose(_model.Emotion, _model.EmotionIntensity),
                EmotionFace.TalkPose(_talkTime, _talkAmount));
            _face = EmotionFace.Approach(_face, target, deltaTime, EmotionFace.BlendSeconds);
        }

        void ApplyCurrentFace()
        {
            ApplyFace(_face);
        }

        void ApplyFace(FacePose pose)
        {
            var bindings = _bindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                var renderer = bindings[i].Renderer;
                if (renderer == null)
                    continue;
                renderer.SetBlendShapeWeight(bindings[i].Index, pose.UnityWeight(bindings[i].Preset));
            }
        }
    }
}
