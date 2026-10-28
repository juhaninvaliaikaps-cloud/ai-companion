using System.Collections;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;
using UnityEngine.Events;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Plays the shared humanoid controller, turns the model to face the camera,
    /// and eases expression blendshapes.
    /// Animator update mode stays on Normal, so clips keep advancing when the
    /// overlay drops the player to 15 fps. They just step with a larger delta.
    /// Root motion stays off, so that yaw is not overwritten by the clips.
    /// Face weights run after UniVRM (execution order 11000) and after
    /// <see cref="LipSyncDriver"/> (12000), so speech can replace mouth shapes
    /// without clearing brows or smiles.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12100)]
    public sealed class AvatarAnimationController : MonoBehaviour, IAvatarAnimator, IAvatarTurntable, IAvatarAnchor, IAnimationStatePlayer, IAvatarLocomotion, IExpressionHint
    {
        static readonly int TalkingHash = Animator.StringToHash(AvatarAnimatorParameters.Talking);
        static readonly int EmotionHash = Animator.StringToHash(AvatarAnimatorParameters.Emotion);
        static readonly int IntensityHash = Animator.StringToHash(AvatarAnimatorParameters.EmotionIntensity);
        static readonly int WalkingHash = Animator.StringToHash(AvatarAnimatorParameters.Walking);
        static readonly int FallingHash = Animator.StringToHash(AvatarAnimatorParameters.Falling);
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
            KeyCode.T,
            KeyCode.F9
        };

        /// <summary>
        /// F9 cycles the portrait expressions so the stills and the transitions can be
        /// checked without a model reply. "neutral" clears the hint.
        /// </summary>
        static readonly string[] DebugExpressions =
        {
            "concerned",
            "embarrassed",
            "shy",
            "flustered",
            "embarrassed_worried",
            "angry",
            "teasing",
            "excited",
            "loving",
            "sleepy",
            "happy",
            "neutral"
        };

        int _expressionCursor = -1;

        [SerializeField] RuntimeAnimatorController controller;
        [SerializeField] AvatarSelection selection;

        readonly AvatarAnimatorModel _model = new AvatarAnimatorModel();
        ShapeBinding[] _bindings = NoBindings;
        Animator _animator;
        Transform _root;
        AvatarPreference _source = AvatarPreference.Auto;
        float _extraYaw;
        bool _hasTalking;
        bool _hasEmotion;
        bool _hasIntensity;
        bool _hasWalking;
        bool _hasFalling;
        bool _warnedMissingController;
        float _talkAmount;
        float _talkTime;
        float _proceduralBlink;
        FacePose _face;
        LipSyncDriver _lips;
        UnityAction _beforeRender;
        ICompanionPause _pause;
        bool _pauseSearched;
        Coroutine _turn;

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

        /// <summary>Drives the Walk state on the gesture layer while she moves along the desktop.</summary>
        public void SetWalking(bool walking)
        {
            if (_animator != null && _hasWalking)
                _animator.SetBool(WalkingHash, walking);
        }

        /// <summary>Drives the fall pose while she drops to the taskbar.</summary>
        public void SetFalling(bool falling)
        {
            if (_animator != null && _hasFalling)
                _animator.SetBool(FallingHash, falling);
        }

        /// <summary>
        /// Eye blink from <see cref="ProceduralLife"/>. Combined at display time so the
        /// 0.3s emotion ease does not smear the close, and so lip sync keeps the mouth.
        /// </summary>
        public void SetProceduralBlink(float weight)
        {
            if (float.IsNaN(weight) || weight < 0f)
                weight = 0f;
            else if (weight > 1f)
                weight = 1f;
            _proceduralBlink = weight;
        }

        /// <summary>
        /// Plays a named state on layer 0 when the attached controller has it. The
        /// director uses this for gestures; an emotion-only cue stays on the parameter
        /// path so the controller's own transitions keep working. Between gestures the
        /// next parameter change or the next cue pulls the state machine back.
        /// </summary>
        public bool TryPlayState(string stateName, float crossfadeSeconds)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
                return false;

            var hash = Animator.StringToHash(stateName);
            var layer = _animator.HasState(AvatarAnimatorLayers.Gestures, hash) ? AvatarAnimatorLayers.Gestures : 0;
            if (!_animator.HasState(layer, hash))
                return false;

            _animator.CrossFade(hash, Mathf.Max(0f, crossfadeSeconds), layer, 0f);

            if (layer == AvatarAnimatorLayers.Gestures && stateName == AvatarAnimatorStates.Turn180)
                StartTurn();

            return true;
        }

        string _expressionName = string.Empty;

        /// <summary>Portrait expression name named by the animation director, empty when none.</summary>
        public string ExpressionName
        {
            get { return _expressionName; }
            set { _expressionName = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant(); }
        }

        void StartTurn()
        {
            if (_turn != null)
                StopCoroutine(_turn);

            _turn = StartCoroutine(TurnInPlace());
        }

        IEnumerator TurnInPlace()
        {
            if (_root == null)
            {
                _turn = null;
                yield break;
            }

            const float duration = 0.9f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _root.Rotate(0f, 180f * Time.deltaTime / duration, 0f, Space.World);
                yield return null;
            }

            _turn = null;
        }

        public void Attach(GameObject instance, AvatarDescriptor descriptor, AvatarPreference source)
        {
            _animator = null;
            _root = null;
            _source = source;
            _extraYaw = AvatarYawKeys.Read(source);
            _hasTalking = false;
            _hasEmotion = false;
            _hasIntensity = false;
            _hasWalking = false;
            _hasFalling = false;
            _bindings = NoBindings;
            _proceduralBlink = 0f;
            if (instance == null)
                return;

            BindAnimator(instance);
            _bindings = BindShapes(instance, descriptor);
            PushParameters();
            if (_animator == null)
                return;

            _root = instance.transform;
            FaceCamera(_root);
        }

        public void AddYawPixels(float deltaPixels)
        {
            var delta = AvatarFacing.DragDelta(deltaPixels);
            if (_root != null && Mathf.Abs(delta) > 0f)
                _root.Rotate(0f, delta, 0f, Space.World);

            _extraYaw = AvatarFacing.ApplyDrag(_extraYaw, deltaPixels);
            AvatarYawKeys.Write(_source, _extraYaw, false);
        }

        public void CommitYaw()
        {
            AvatarYawKeys.Write(_source, _extraYaw, true);
        }

        public bool TryGetAnchor(out Vector3 worldPoint)
        {
            if (_root == null)
            {
                worldPoint = default;
                return false;
            }

            worldPoint = _root.position + Vector3.up;
            return true;
        }

        public void ResetYaw()
        {
            if (_root != null && Mathf.Abs(_extraYaw) > 0.0001f)
                _root.Rotate(0f, -_extraYaw, 0f, Space.World);

            _extraYaw = 0f;
            AvatarYawKeys.Write(_source, 0f, true);
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
            CommitYaw();
        }

        void Update()
        {
            if (!Input.anyKeyDown)
                return;
            if (!CompanionPause.ReactionsAllowed(IsPaused()))
                return;

            var changed = false;
            for (var i = 0; i < DebugKeys.Length; i++)
            {
                if (DebugInputGuard.IsBlocked(DebugKeys[i]))
                    continue;
                if (Input.GetKeyDown(DebugKeys[i]))
                    changed |= _model.TryApplyDebugKey(DebugKeys[i]);
            }

            if (changed)
                PushParameters();

            if (Input.GetKeyDown(KeyCode.F9) && !DebugInputGuard.IsBlocked(KeyCode.F9))
                CycleExpression();
        }

        void CycleExpression()
        {
            _expressionCursor = (_expressionCursor + 1) % DebugExpressions.Length;
            var name = DebugExpressions[_expressionCursor];
            ExpressionName = name == "neutral" ? string.Empty : name;
            Debug.Log("Expression preview: " + name);
        }

        bool IsPaused()
        {
            if (!_pauseSearched)
            {
                _pauseSearched = true;
                _pause = GetComponent<ICompanionPause>();
            }

            return _pause != null && _pause.IsPaused;
        }

        void LateUpdate()
        {
            UpdateGestureLayerWeight();
            AdvanceFace(Time.deltaTime);
            ApplyFace(DisplayedFace());
        }

        /// <summary>
        /// The gesture layer has an Empty state with no motion and writeDefaultValues off,
        /// so it keeps the last gesture pose unless its weight is released. Fade the weight
        /// to zero while Empty is active, which hands the body back to the base layer.
        /// </summary>
        void UpdateGestureLayerWeight()
        {
            if (_animator == null || _animator.layerCount <= AvatarAnimatorLayers.Gestures)
                return;

            var info = _animator.GetCurrentAnimatorStateInfo(AvatarAnimatorLayers.Gestures);
            var empty = info.IsName(AvatarAnimatorStates.Empty);
            var current = _animator.GetLayerWeight(AvatarAnimatorLayers.Gestures);
            var target = empty ? 0f : 1f;
            if (Mathf.Abs(current - target) < 0.001f)
                return;

            _animator.SetLayerWeight(
                AvatarAnimatorLayers.Gestures,
                Mathf.MoveTowards(current, target, Time.deltaTime * 6f));
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

            animator.runtimeAnimatorController = ResolveController();
            CacheParameters(animator);
        }

        bool TryHumanoidForward(out Vector3 forward)
        {
            forward = Vector3.forward;
            if (_animator == null || !_animator.isHuman)
                return false;

            var hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            var spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            var left = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var right = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (hips == null || spine == null || left == null || right == null)
                return false;

            return AvatarFacing.TryBodyForward(hips.position, spine.position, left.position, right.position, out forward);
        }

        void FaceCamera(Transform root)
        {
            var yaw = _extraYaw;
            var camera = Camera.main;
            if (camera != null)
            {
                if (!TryHumanoidForward(out var forward))
                    forward = root.forward;
                yaw += AvatarFacing.YawDegreesToward(forward, camera.transform.position - root.position);
            }

            if (Mathf.Abs(yaw) > 0.001f)
                root.Rotate(0f, yaw, 0f, Space.World);
        }

        /// <summary>
        /// The shared controller, or an override controller when the local avatar
        /// selection names replacement clips. Overrides are matched by base clip name,
        /// so a local walk clip replaces the CC0 walk without touching the repository.
        /// </summary>
        RuntimeAnimatorController ResolveController()
        {
            var source = selection;
            if (source == null)
            {
                var mount = GetComponent<AvatarMount>();
                if (mount != null)
                    source = mount.Selection;
            }

            var overrides = source != null ? source.animationOverrides : null;
            if (overrides == null || overrides.Length == 0)
                return controller;

            var baseClips = controller.animationClips;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrides.Length);
            for (var i = 0; i < overrides.Length; i++)
            {
                var entry = overrides[i];
                if (entry == null || entry.clip == null || string.IsNullOrWhiteSpace(entry.clipName))
                    continue;

                for (var c = 0; c < baseClips.Length; c++)
                {
                    var baseClip = baseClips[c];
                    if (baseClip == null || baseClip.name != entry.clipName)
                        continue;

                    pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(baseClip, entry.clip));
                    break;
                }
            }

            if (pairs.Count == 0)
                return controller;

            var overrideController = new AnimatorOverrideController(controller);
            overrideController.ApplyOverrides(pairs);
            return overrideController;
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
                else if (parameter.name == AvatarAnimatorParameters.Walking && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasWalking = true;
                else if (parameter.name == AvatarAnimatorParameters.Falling && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasFalling = true;
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
                    if (preset == ExpressionPreset.None && LipSyncTargetSelection.TryClassifyMouth(shapeName, out var mouth))
                        preset = mouth;
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

            var lips = Lips();
            var ownership = 0f;
            var handsMouth = lips != null && LipSyncTargetSelection.DrivesSharedMouth(lips.TargetKind);
            if (handsMouth)
                ownership = lips.MouthOverride;

            var step = EmotionFace.BlendStep(deltaTime, EmotionFace.BlendSeconds);
            var targetAmount = _model.IsTalking ? 1f : 0f;
            _talkAmount += (targetAmount - _talkAmount) * step;

            if (_talkAmount > 0.001f)
            {
                _talkTime += deltaTime;
                if (_talkTime >= 60f)
                    _talkTime %= 60f;
            }

            var procedural = handsMouth
                ? EmotionFace.ProceduralTalkScale(_talkAmount, ownership)
                : _talkAmount;
            var target = EmotionFace.Combine(
                EmotionFace.EmotionPose(_model.Emotion, _model.EmotionIntensity),
                EmotionFace.TalkPose(_talkTime, procedural));
            _face = EmotionFace.Approach(_face, target, deltaTime, EmotionFace.BlendSeconds);
        }

        void ApplyCurrentFace()
        {
            ApplyFace(DisplayedFace());
        }

        FacePose DisplayedFace()
        {
            var lips = Lips();
            var pose = lips == null || !LipSyncTargetSelection.DrivesSharedMouth(lips.TargetKind)
                ? _face
                : EmotionFace.OverrideMouth(_face, lips.Weights, lips.MouthOverride);
            return EmotionFace.ApplyProceduralBlink(pose, _proceduralBlink);
        }

        LipSyncDriver Lips()
        {
            if (_lips == null)
                _lips = GetComponent<LipSyncDriver>();
            return _lips;
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
