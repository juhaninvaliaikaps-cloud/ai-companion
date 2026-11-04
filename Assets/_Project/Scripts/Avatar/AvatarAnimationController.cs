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
            "laughing",
            "neutral"
        };

        int _expressionCursor = -1;

        [SerializeField] RuntimeAnimatorController controller;
        [SerializeField] AvatarSelection selection;

        readonly AvatarAnimatorModel _model = new AvatarAnimatorModel();
        ShapeBinding[] _bindings = NoBindings;
        readonly HashSet<ExpressionPreset> _boundPresets = new HashSet<ExpressionPreset>();
        Animator _animator;
        Transform _root;
        AvatarPreference _source = AvatarPreference.Auto;
        float _extraYaw;
        float _walkDirection;
        float _walkYaw;
        float _walkYawApplied;
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
        readonly BlushOverlay _blush = new BlushOverlay();
        readonly ExpressionHold _expressionHold = new ExpressionHold();
        UnityAction _beforeRender;
        ICompanionPause _pause;
        bool _pauseSearched;
        Coroutine _turn;
        Emotion _loggedExpression;
        bool _hasLoggedExpression;

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
            // Mouth only. The held expression stays, and the animator Talking bool
            // stays untouched so the Talk body clip cannot replace the face.
            _model.SetTalking(talking);
            PushParameters();
        }

        public void SetEmotion(Emotion emotion, float intensity)
        {
            _model.SetEmotion(emotion, intensity);
            PushParameters();
            var face = FaceEmotion.Resolve(_expressionName, _model.Emotion);
            _expressionHold.Apply(face, _model.EmotionIntensity, _expressionName, UtteranceActive());
            NoteFace();
        }

        /// <summary>Drives the Walk state on the gesture layer while she moves along the desktop.</summary>
        public void SetWalking(bool walking)
        {
            if (_animator != null && _hasWalking)
                _animator.SetBool(WalkingHash, walking);
        }

        public void SetWalkingDirection(float direction)
        {
            _walkDirection = float.IsNaN(direction) ? 0f : Mathf.Clamp(direction, -1f, 1f);
        }

        /// <summary>
        /// Turns the root toward the walk direction on top of the saved per-avatar yaw, so
        /// the turntable value survives. The applied offset is tracked so it can be removed
        /// when she stops or the yaw is reset.
        /// </summary>
        void UpdateWalkFacing(float delta)
        {
            if (_root == null)
                return;

            var target = WalkFacing.Offset(_walkDirection, _extraYaw);
            if (delta > 0f)
            {
                var blend = 1f - Mathf.Pow(0.5f, delta / 0.22f);
                _walkYaw += (target - _walkYaw) * blend;
            }
            else
            {
                _walkYaw = target;
            }

            var step = _walkYaw - _walkYawApplied;
            if (Mathf.Abs(step) < 0.01f)
            {
                _walkYawApplied = _walkYaw;
                return;
            }

            _root.Rotate(0f, step, 0f, Space.World);
            _walkYawApplied = _walkYaw;
        }

        /// <summary>Drives the fall pose while she drops to the taskbar.</summary>
        public bool IsFalling { get; private set; }

        public void SetFalling(bool falling)
        {
            IsFalling = falling;
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

            // Upper-body gestures win, then the full-body gesture layer, then the base layer.
            var layer = -1;
            if (_animator.HasState(AvatarAnimatorLayers.UpperBody, hash))
                layer = AvatarAnimatorLayers.UpperBody;
            else if (_animator.HasState(AvatarAnimatorLayers.Gestures, hash))
                layer = AvatarAnimatorLayers.Gestures;
            else if (_animator.HasState(0, hash))
                layer = 0;

            if (layer < 0)
                return false;

            if (layer != 0)
            {
                _lastGestureRequest = Time.unscaledTime;
                _animator.SetLayerWeight(layer, 1f);
            }

            _animator.CrossFade(hash, Mathf.Max(0f, crossfadeSeconds), layer, 0f);
            Debug.Log("Gesture " + stateName + " on layer " + layer + " (weight " + _animator.GetLayerWeight(layer).ToString("0.00") + ")");

            if (stateName == AvatarAnimatorStates.Turn180)
                StartTurn();

            return true;
        }

        string _expressionName = string.Empty;

        /// <summary>
        /// Portrait expression name for this reply. The director writes it, and the hold
        /// keeps it until speech has finished so the 2D frame cannot fall back to neutral
        /// while the blendshapes are still on the expression.
        /// </summary>
        public string ExpressionName
        {
            get
            {
                if (!string.IsNullOrEmpty(_expressionName))
                    return _expressionName;
                return _expressionHold.Portrait;
            }
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
            _boundPresets.Clear();
            _proceduralBlink = 0f;
            if (instance == null)
            {
                _blush.Release();
                return;
            }

            BindAnimator(instance);
            _bindings = BindShapes(instance, descriptor);
            RememberBound();
            AttachBlush();
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
            if (_root != null)
            {
                var total = _extraYaw + _walkYawApplied;
                if (Mathf.Abs(total) > 0.0001f)
                    _root.Rotate(0f, -total, 0f, Space.World);
            }

            _extraYaw = 0f;
            _walkYaw = 0f;
            _walkYawApplied = 0f;
            _walkDirection = 0f;
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
            _blush.Release();
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
            {
                _expressionName = string.Empty;
                PushParameters();
                _expressionHold.Apply(_model.Emotion, _model.EmotionIntensity, string.Empty, UtteranceActive());
                NoteFace();
            }

            if (Input.GetKeyDown(KeyCode.F9) && !DebugInputGuard.IsBlocked(KeyCode.F9))
                CycleExpression();
        }

        void CycleExpression()
        {
            _expressionCursor = (_expressionCursor + 1) % DebugExpressions.Length;
            var name = DebugExpressions[_expressionCursor];
            ExpressionName = name == "neutral" ? string.Empty : name;
            var face = FaceEmotion.Resolve(_expressionName, Emotion.Neutral);
            SetEmotion(face, 1f);
            Debug.Log("Expression preview: " + name);
        }

        void NoteFace()
        {
            var face = FaceEmotion.Resolve(_expressionName, _model.Emotion);
            if (_hasLoggedExpression && face == _loggedExpression)
                return;

            _hasLoggedExpression = true;
            _loggedExpression = face;
            Debug.Log(FaceEmotion.FormatLog(face));
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
            UpdateWalkFacing(Time.deltaTime);
            UpdateGestureLayerWeight();
            if (ReleaseExpression(Time.deltaTime))
                NoteFace();
            AdvanceFace(Time.deltaTime);
            ApplyFace(DisplayedFace());
        }

        /// <summary>
        /// True while this reply is still being spoken. Synthesis and the clip queue
        /// count, so a pause between sentences does not drop the expression.
        /// </summary>
        bool UtteranceActive()
        {
            var speech = GetComponent<ISpeechOutput>();
            var behaviour = speech as Object;
            if (behaviour != null && speech.IsSpeaking)
                return true;
            return _model.IsTalking;
        }

        /// <summary>
        /// Drops the reply's expression once speech has finished and the short tail
        /// has elapsed. The portrait name and the body emotion go with it, so the
        /// 2D still returns to neutral on the same frame as the blendshapes.
        /// </summary>
        bool ReleaseExpression(float deltaTime)
        {
            if (!_expressionHold.Tick(deltaTime, UtteranceActive()))
                return false;

            _expressionName = string.Empty;
            _model.SetEmotion(Emotion.Neutral, 1f);
            return true;
        }

        /// <summary>
        /// The gesture layer has an Empty state with no motion and writeDefaultValues off,
        /// so it keeps the last gesture pose unless its weight is released. Fade the weight
        /// to zero while Empty is active, which hands the body back to the base layer.
        /// </summary>
        float _lastGestureRequest = -10f;

        void UpdateGestureLayerWeight()
        {
            ReleaseLayerWeight(AvatarAnimatorLayers.Gestures);
            ReleaseLayerWeight(AvatarAnimatorLayers.UpperBody);
        }

        /// <summary>
        /// Hands a layer back to the base layer once its Empty state is current, and fades it in
        /// when a state takes over. Both the full-body and the upper-body gesture layers use it.
        /// </summary>
        void ReleaseLayerWeight(int layer)
        {
            if (_animator == null || _animator.layerCount <= layer)
                return;

            // Right after a request the layer must stay up even if the transition has not
            // started yet, otherwise the weight race hides the whole gesture.
            if (Time.unscaledTime - _lastGestureRequest < 0.6f)
            {
                if (_animator.GetLayerWeight(layer) < 0.999f)
                    _animator.SetLayerWeight(layer, 1f);
                return;
            }

            if (_animator.IsInTransition(layer))
                return;

            var info = _animator.GetCurrentAnimatorStateInfo(layer);
            var current = _animator.GetLayerWeight(layer);

            if (info.IsName(AvatarAnimatorStates.Empty))
            {
                if (current > 0.001f)
                    _animator.SetLayerWeight(layer, 0f);
                return;
            }

            if (current < 0.999f)
                _animator.SetLayerWeight(layer, Mathf.MoveTowards(current, 1f, Time.deltaTime * 8f));
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
            // The Talk state still listens for this bool. Leaving it true plays that
            // body clip, and the face goes back to the clip's talking pose.
            if (_hasTalking && animator.GetBool(TalkingHash))
                animator.SetBool(TalkingHash, false);
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

            // Talking and emotions are face-only performances. Writing the Emotion parameter
            // switched the base layer to a body clip, and that is where the floating and the
            // swimming arms came from. The face and the portrait show the emotion; the body
            // stays in Idle, in Walk, or in whatever the gesture layer is playing.
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
            var boundNames = new List<KeyValuePair<ExpressionPreset, string>>(32);
            var unclassified = 0;
            var unclassifiedSample = new List<string>(8);
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
                    {
                        unclassified++;
                        if (unclassifiedSample.Count < 8)
                            unclassifiedSample.Add(shapeName);
                        continue;
                    }

                    boundNames.Add(new KeyValuePair<ExpressionPreset, string>(preset, shapeName));
                    found.Add(new ShapeBinding
                    {
                        Renderer = renderer,
                        Index = shape,
                        Preset = preset
                    });
                }
            }

            LogCoverage(boundNames, unclassified, unclassifiedSample);
            return found.Count == 0 ? NoBindings : found.ToArray();
        }

        void RememberBound()
        {
            _boundPresets.Clear();
            for (var i = 0; i < _bindings.Length; i++)
                _boundPresets.Add(_bindings[i].Preset);
        }

        static void LogCoverage(List<KeyValuePair<ExpressionPreset, string>> boundNames, int unclassified, List<string> unclassifiedSample)
        {
            var bound = new HashSet<ExpressionPreset>();
            for (var i = 0; i < boundNames.Count; i++)
                bound.Add(boundNames[i].Key);

            Debug.Log(ExpressionCoverage.FormatBound(boundNames));
            Debug.Log(ExpressionCoverage.FormatUnclassified(unclassified, unclassifiedSample));

            var library = ExpressionPresetLibrary.Active;
            var emotions = new[]
            {
                Emotion.Happy,
                Emotion.Sad,
                Emotion.Surprised,
                Emotion.Thinking,
                Emotion.Embarrassed,
                Emotion.Shy,
                Emotion.Flustered,
                Emotion.Concerned
            };
            for (var i = 0; i < emotions.Length; i++)
            {
                var emotion = emotions[i];
                Debug.Log(ExpressionCoverage.Format(emotion, library.Get(emotion), bound));
            }
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
            var faceEmotion = _expressionHold.Active ? _expressionHold.Emotion : Emotion.Neutral;
            var intensity = _expressionHold.Active ? _expressionHold.Intensity : 0f;
            var emotionPose = EmotionFace.ProjectOnto(
                EmotionFace.EmotionPose(faceEmotion, intensity),
                _boundPresets);
            var target = EmotionFace.Combine(
                emotionPose,
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
                var weight = pose.UnityWeight(bindings[i].Preset);
                if (bindings[i].Preset == ExpressionPreset.Blush && !BlushSettings.Enabled)
                    weight = 0f;
                renderer.SetBlendShapeWeight(bindings[i].Index, weight);
            }

            _blush.SetIntensity(Mathf.Clamp01(pose.Blush + TouchBlush()));
        }

        float _touchBlush;
        float _touchBlushUntil;

        /// <summary>
        /// A short blush from a touch reaction. It fades linearly and adds to the pose blush, so
        /// an emotion that already blushes stays consistent.
        /// </summary>
        public void AddTouchBlush(float amount, float seconds)
        {
            _touchBlush = Mathf.Clamp01(Mathf.Max(_touchBlush, amount));
            _touchBlushUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }

        float TouchBlush()
        {
            if (_touchBlush <= 0f)
                return 0f;

            var left = _touchBlushUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                _touchBlush = 0f;
                return 0f;
            }

            var fade = Mathf.Clamp01(left / 0.75f);
            return _touchBlush * fade;
        }

        void AttachBlush()
        {
            _blush.Release();
            var hasShape = false;
            for (var i = 0; i < _bindings.Length; i++)
            {
                if (_bindings[i].Preset == ExpressionPreset.Blush)
                {
                    hasShape = true;
                    break;
                }
            }

            Transform head = null;
            if (_animator != null && _animator.isHuman)
                head = _animator.GetBoneTransform(HumanBodyBones.Head);

            var ready = false;
            if (BlushSettings.Enabled && !hasShape && head != null)
                ready = _blush.Attach(head);

            Debug.Log(BlushOverlay.FormatLog(BlushSettings.Enabled, hasShape, head != null, ready || hasShape));
        }
    }
}
