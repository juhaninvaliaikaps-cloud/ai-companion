using System;
using System.Collections.Generic;
using UnityEngine;
using UniVRM10;
using VRM;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Samples the TTS <see cref="AudioSource"/> on this object and turns it into mouth weights.
    /// One Console line, prefixed "LipSync:", names the target chosen for the loaded avatar.
    /// Spoken text is never logged.
    /// VRM 1.0 uses expression SetWeight. A VRM 0.x proxy uses SetValues. Blendshapes
    /// are written by <see cref="AvatarAnimationController"/> so emotion brows and smiles stay put.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12000)]
    public sealed class LipSyncDriver : MonoBehaviour, IMouthDriver
    {
        const int SampleSize = 256;
        static readonly ShapeSlot[] NoShapes = new ShapeSlot[0];

        [SerializeField, Range(0f, 1.5f)] float strength = LipSyncTuning.DefaultStrength;
        [SerializeField, Range(0f, 32f)] float sensitivity = LipSyncTuning.DefaultSensitivity;
        [SerializeField, Range(0f, 0.2f)] float attackSeconds = LipSyncTuning.DefaultAttackSeconds;
        [SerializeField, Range(0f, 0.4f)] float releaseSeconds = LipSyncTuning.DefaultReleaseSeconds;
        [SerializeField, Range(0f, 0.2f)] float silenceThreshold = LipSyncTuning.DefaultSilenceThreshold;
        [SerializeField, Range(-30f, 30f)] float jawPitchDegrees = LipSyncTuning.DefaultJawPitchDegrees;

        readonly float[] _wave = new float[SampleSize];
        readonly float[] _spectrum = new float[SampleSize];
        readonly KeyValuePair<BlendShapeKey, float>[] _proxyValues = new KeyValuePair<BlendShapeKey, float>[5];

        AudioSource _source;
        AvatarAnimationController _face;
        GameObject _instance;
        ILipSyncTarget _custom;
        Vrm10Instance _vrm;
        VRMBlendShapeProxy _proxy;
        Transform _jaw;
        Quaternion _jawBind;
        bool _hasJawBind;
        ShapeSlot[] _shapes = NoShapes;
        LipSyncFrame _frame;
        LipSyncTargetKind _kind;
        string _logged;
        bool _driveVrm;
        bool _driveProxy;
        bool _vrmWarned;
        bool _vrmKeysReady;
        bool _proxyKeysReady;
        ExpressionKey _aa;
        ExpressionKey _ih;
        ExpressionKey _ou;
        ExpressionKey _ee;
        ExpressionKey _oh;
        BlendShapeKey _proxyA;
        BlendShapeKey _proxyI;
        BlendShapeKey _proxyU;
        BlendShapeKey _proxyE;
        BlendShapeKey _proxyO;

        struct ShapeSlot
        {
            public SkinnedMeshRenderer Renderer;
            public int Index;
            public ExpressionPreset Preset;
            public string Name;
        }

        public MouthWeights Weights => _frame.Weights;

        public float MouthOverride => _frame.MouthOverride;

        public LipSyncTargetKind TargetKind => _custom != null ? _custom.Kind : _kind;

        public void SetTarget(ILipSyncTarget target)
        {
            _custom = target;
            _logged = null;
            Select(_instance);
        }

        public void Bind(GameObject instance)
        {
            _instance = instance;
            _logged = null;
            Select(instance);
        }

        void OnEnable()
        {
            _face = GetComponent<AvatarAnimationController>();
        }

        void LateUpdate()
        {
            if (!isActiveAndEnabled)
                return;

            Sample(Time.deltaTime);
            ApplyTargets();
        }

        void Sample(float deltaTime)
        {
            var tuning = CurrentTuning();
            var rms = 0f;
            var viseme = MouthViseme.Rest;
            var source = Source();
            if (source != null && source.isPlaying && TryRead(source, tuning.SilenceThreshold, out rms))
                viseme = LipSyncMath.Estimate(_spectrum, HzPerBin(), rms, tuning.SilenceThreshold);

            _frame = LipSyncMath.Advance(_frame, rms, viseme, deltaTime, tuning);
        }

        void ApplyTargets()
        {
            var weights = _frame.Weights;
            if (_custom != null)
            {
                _custom.Apply(weights);
                return;
            }

            if (_kind == LipSyncTargetKind.Vrm)
            {
                ApplyVrm(weights);
                ApplyProxy(weights);
                return;
            }

            if (_kind == LipSyncTargetKind.JawBone)
            {
                ApplyJaw(weights);
                return;
            }

            if (_kind == LipSyncTargetKind.BlendShapes && _face == null)
                ApplyShapes(weights);
        }

        void Select(GameObject instance)
        {
            _driveVrm = false;
            _driveProxy = false;
            _vrm = null;
            _proxy = null;
            _jaw = null;
            _hasJawBind = false;
            _shapes = NoShapes;
            _vrmWarned = false;
            _frame = default;

            if (_custom != null)
            {
                _kind = _custom.Kind;
                LogChoice(LipSyncTargetSelection.FormatDiagnostic(_kind, _custom.Description));
                return;
            }

            if (instance == null)
            {
                _kind = LipSyncTargetKind.None;
                LogChoice(LipSyncTargetSelection.FormatDiagnostic(_kind, null));
                return;
            }

            _vrm = instance.GetComponentInChildren<Vrm10Instance>(true);
            _proxy = instance.GetComponentInChildren<VRMBlendShapeProxy>(true);
            var vrmDetail = string.Empty;
            if (TryVrmDetail(_vrm, out vrmDetail))
                _driveVrm = true;
            else if (TryProxyDetail(_proxy, out vrmDetail))
                _driveProxy = true;

            _shapes = CollectShapes(instance, out var shapeNames);
            _jaw = FindJaw(instance);
            if (_jaw != null)
            {
                _jawBind = _jaw.localRotation;
                _hasJawBind = true;
            }
            _kind = LipSyncTargetSelection.Choose(_driveVrm || _driveProxy, _shapes.Length, _jaw != null);

            string detail = null;
            switch (_kind)
            {
                case LipSyncTargetKind.Vrm:
                    detail = vrmDetail;
                    break;
                case LipSyncTargetKind.BlendShapes:
                    detail = LipSyncTargetSelection.JoinNames(shapeNames);
                    break;
                case LipSyncTargetKind.JawBone:
                    detail = _jaw != null ? _jaw.name : null;
                    break;
            }

            LogChoice(LipSyncTargetSelection.FormatDiagnostic(_kind, detail));
        }

        LipSyncTuning CurrentTuning()
        {
            return new LipSyncTuning
            {
                Strength = strength,
                Sensitivity = sensitivity,
                AttackSeconds = attackSeconds,
                ReleaseSeconds = releaseSeconds,
                SilenceThreshold = silenceThreshold
            }.Sanitized();
        }

        AudioSource Source()
        {
            if (_source == null)
                _source = GetComponent<AudioSource>();
            return _source;
        }

        bool TryRead(AudioSource source, float silence, out float rms)
        {
            rms = 0f;
            try
            {
                source.GetOutputData(_wave, 0);
                rms = LipSyncMath.Rms(_wave);
                if (rms < silence)
                    return true;

                source.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
            }
            catch (Exception)
            {
                rms = 0f;
                return false;
            }

            return true;
        }

        float HzPerBin()
        {
            var rate = AudioSettings.outputSampleRate;
            if (rate <= 0)
                rate = 48000;
            return rate * 0.5f / _spectrum.Length;
        }

        void ApplyVrm(MouthWeights weights)
        {
            if (!_driveVrm || _vrm == null)
                return;

            try
            {
                var runtime = _vrm.Runtime;
                if (runtime == null || runtime.Expression == null)
                    return;

                EnsureVrmKeys();
                var expression = runtime.Expression;
                expression.SetWeight(_aa, weights.A);
                expression.SetWeight(_ih, weights.I);
                expression.SetWeight(_ou, weights.U);
                expression.SetWeight(_ee, weights.E);
                expression.SetWeight(_oh, weights.O);
            }
            catch (Exception)
            {
                if (_vrmWarned)
                    return;
                _vrmWarned = true;
                Debug.LogWarning("LipSync: VRM expression apply failed");
            }
        }

        void ApplyProxy(MouthWeights weights)
        {
            if (!_driveProxy || _proxy == null)
                return;

            EnsureProxyKeys();
            _proxyValues[0] = new KeyValuePair<BlendShapeKey, float>(_proxyA, weights.A);
            _proxyValues[1] = new KeyValuePair<BlendShapeKey, float>(_proxyI, weights.I);
            _proxyValues[2] = new KeyValuePair<BlendShapeKey, float>(_proxyU, weights.U);
            _proxyValues[3] = new KeyValuePair<BlendShapeKey, float>(_proxyE, weights.E);
            _proxyValues[4] = new KeyValuePair<BlendShapeKey, float>(_proxyO, weights.O);
            _proxy.SetValues(_proxyValues);
        }

        void ApplyJaw(MouthWeights weights)
        {
            if (_jaw == null || !_hasJawBind)
                return;

            var pitch = jawPitchDegrees * weights.Opening;
            _jaw.localRotation = pitch == 0f
                ? _jawBind
                : _jawBind * Quaternion.Euler(pitch, 0f, 0f);
        }

        void ApplyShapes(MouthWeights weights)
        {
            for (var i = 0; i < _shapes.Length; i++)
            {
                var renderer = _shapes[i].Renderer;
                if (renderer == null)
                    continue;
                renderer.SetBlendShapeWeight(_shapes[i].Index, Channel(_shapes[i].Preset, weights) * 100f);
            }
        }

        void EnsureVrmKeys()
        {
            if (_vrmKeysReady)
                return;

            _aa = ExpressionKey.Aa;
            _ih = ExpressionKey.Ih;
            _ou = ExpressionKey.Ou;
            _ee = ExpressionKey.Ee;
            _oh = ExpressionKey.Oh;
            _vrmKeysReady = true;
        }

        void EnsureProxyKeys()
        {
            if (_proxyKeysReady)
                return;

            _proxyA = BlendShapeKey.CreateFromPreset(BlendShapePreset.A);
            _proxyI = BlendShapeKey.CreateFromPreset(BlendShapePreset.I);
            _proxyU = BlendShapeKey.CreateFromPreset(BlendShapePreset.U);
            _proxyE = BlendShapeKey.CreateFromPreset(BlendShapePreset.E);
            _proxyO = BlendShapeKey.CreateFromPreset(BlendShapePreset.O);
            _proxyKeysReady = true;
        }

        static bool TryVrmDetail(Vrm10Instance instance, out string detail)
        {
            detail = null;
            var expression = instance != null && instance.Vrm != null ? instance.Vrm.Expression : null;
            if (expression == null)
                return false;

            var list = LipSyncTargetSelection.VrmList(
                expression.Aa != null,
                expression.Ih != null,
                expression.Ou != null,
                expression.Ee != null,
                expression.Oh != null);
            if (list.Length == 0)
                return false;

            detail = "expressions " + list;
            return true;
        }

        static bool TryProxyDetail(VRMBlendShapeProxy proxy, out string detail)
        {
            detail = null;
            var avatar = proxy != null ? proxy.BlendShapeAvatar : null;
            var clips = avatar != null ? avatar.Clips : null;
            if (clips == null)
                return false;

            var aa = false;
            var ih = false;
            var ou = false;
            var ee = false;
            var oh = false;
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip == null)
                    continue;
                switch (clip.Preset)
                {
                    case BlendShapePreset.A:
                        aa = true;
                        break;
                    case BlendShapePreset.I:
                        ih = true;
                        break;
                    case BlendShapePreset.U:
                        ou = true;
                        break;
                    case BlendShapePreset.E:
                        ee = true;
                        break;
                    case BlendShapePreset.O:
                        oh = true;
                        break;
                }
            }

            var list = LipSyncTargetSelection.VrmList(aa, ih, ou, ee, oh);
            if (list.Length == 0)
                return false;

            detail = "blendshape proxy " + list;
            return true;
        }

        static ShapeSlot[] CollectShapes(GameObject instance, out List<string> names)
        {
            names = new List<string>();
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var found = new List<ShapeSlot>(8);
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
                    if (!LipSyncTargetSelection.TryClassifyMouth(shapeName, out var preset))
                        continue;

                    found.Add(new ShapeSlot
                    {
                        Renderer = renderer,
                        Index = shape,
                        Preset = preset,
                        Name = shapeName
                    });
                    if (!names.Contains(shapeName))
                        names.Add(shapeName);
                }
            }

            return found.Count == 0 ? NoShapes : found.ToArray();
        }

        static Transform FindJaw(GameObject instance)
        {
            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                var bone = animator.GetBoneTransform(HumanBodyBones.Jaw);
                if (bone != null)
                    return bone;
            }

            var transforms = instance.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (LipSyncTargetSelection.IsJawBoneName(transforms[i].name))
                    return transforms[i];
            }

            return null;
        }

        static float Channel(ExpressionPreset preset, MouthWeights weights)
        {
            switch (preset)
            {
                case ExpressionPreset.Aa:
                    return weights.A;
                case ExpressionPreset.Ih:
                    return weights.I;
                case ExpressionPreset.Ou:
                    return weights.U;
                case ExpressionPreset.Ee:
                    return weights.E;
                case ExpressionPreset.Oh:
                    return weights.O;
                default:
                    return 0f;
            }
        }

        void LogChoice(string line)
        {
            if (line == _logged)
                return;
            _logged = line;
            Debug.Log(line);
        }
    }
}
