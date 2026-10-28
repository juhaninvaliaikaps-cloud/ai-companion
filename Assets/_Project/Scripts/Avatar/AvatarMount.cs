using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AICompanion.Core;
using UniVRM10;
using UnityEngine;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Loads the configured humanoid and hides the placeholder capsule while it is showing.
    /// Click-through uses the framebuffer silhouette, not this object's bounds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarMount : MonoBehaviour, ICompanionStartup, IAvatarDescriptorService
    {
        [SerializeField] AvatarSelection selection;
        [SerializeField] GameObject placeholder;
        [SerializeField] KeyCode cycleKey = KeyCode.F7;

        GameObject _instance;
        AvatarDescriptor _current;
        CancellationTokenSource _cancel;
        AvatarPreference _loaded = AvatarPreference.Auto;
        AvatarPreference _shownRequest = AvatarPreference.Auto;
        int _generation;
        bool _started;

        public AvatarDescriptor Current => _current;

        public AvatarPreference LoadedPreference => _loaded;

        public event Action<AvatarPreference> PreferenceChanged;

        public void Select(AvatarPreference preference)
        {
            if (!_started)
                StartCompanion();
            if (_shownRequest == preference)
                return;
            Show(preference);
        }

        public void ResetRotation()
        {
            var turn = GetComponent<IAvatarTurntable>();
            if (turn != null)
                turn.ResetYaw();
        }

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            if (placeholder == null)
            {
                var found = GameObject.Find("Placeholder Avatar");
                if (found != null)
                    placeholder = found;
            }

            var preference = selection != null ? selection.preference : AvatarPreference.Auto;
            Show(preference);
        }

        void Start()
        {
            StartCompanion();
        }

        void Update()
        {
            if (!_started || DebugInputGuard.IsBlocked(cycleKey) || !Input.GetKeyDown(cycleKey))
                return;

            Cycle();
        }

        void OnDestroy()
        {
            CancelLoad();
        }

        public bool TryGetBlendShapeNames(out IReadOnlyList<string> names)
        {
            if (_current == null)
            {
                names = Array.Empty<string>();
                return false;
            }

            names = _current.BlendShapeNames;
            return true;
        }

        public bool TryGetExpression(ExpressionPreset preset, out IReadOnlyList<string> blendShapeNames)
        {
            if (_current == null)
            {
                blendShapeNames = Array.Empty<string>();
                return false;
            }

            return _current.TryGetExpression(preset, out blendShapeNames);
        }

        void Cycle()
        {
            var order = new[]
            {
                AvatarPreference.LocalPrefab,
                AvatarPreference.VrmSample,
                AvatarPreference.FbxSample
            };

            var current = _loaded;
            var start = 0;
            for (var i = 0; i < order.Length; i++)
            {
                if (order[i] == current)
                {
                    start = i + 1;
                    break;
                }
            }

            for (var step = 0; step < order.Length; step++)
            {
                var candidate = order[(start + step) % order.Length];
                if (candidate == AvatarPreference.LocalPrefab && !HasLocalPrefab())
                    continue;
                if (candidate == AvatarPreference.VrmSample && !VrmFileExists())
                    continue;
                if (candidate == AvatarPreference.FbxSample && !FbxResourceExists())
                    continue;

                Show(candidate);
                return;
            }

            ShowCapsule();
        }

        void Show(AvatarPreference preference)
        {
            _shownRequest = preference;
            if (preference == AvatarPreference.Auto)
                preference = ResolveAuto();

            switch (preference)
            {
                case AvatarPreference.LocalPrefab:
                    ShowLocalPrefab();
                    break;
                case AvatarPreference.VrmSample:
                    ShowVrm();
                    break;
                case AvatarPreference.FbxSample:
                    ShowFbx();
                    break;
                default:
                    ShowCapsule();
                    break;
            }
        }

        AvatarPreference ResolveAuto()
        {
            if (HasLocalPrefab())
                return AvatarPreference.LocalPrefab;
            if (VrmFileExists())
                return AvatarPreference.VrmSample;
            if (FbxResourceExists())
                return AvatarPreference.FbxSample;
            return AvatarPreference.Auto;
        }

        void ShowLocalPrefab()
        {
            var prefab = selection != null ? selection.localPrefab : null;
            if (prefab == null)
            {
                Debug.LogWarning("Avatar Selection has no local prefab. Showing the placeholder capsule. Assign the Asuna prefab on AvatarSelection, or press F7 to try a sample.");
                ShowCapsule();
                return;
            }

            Adopt(Instantiate(prefab), null, AvatarPreference.LocalPrefab);
        }

        void ShowFbx()
        {
            var path = selection != null ? selection.fbxResourcePath : "Avatars/CompanionSample";
            var prefab = string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("FBX avatar resource '" + path + "' was not found. Showing the placeholder capsule.");
                ShowCapsule();
                return;
            }

            Adopt(Instantiate(prefab), null, AvatarPreference.FbxSample);
        }

        void ShowVrm()
        {
            var relative = selection != null ? selection.streamingVrmRelativePath : "Avatars/CompanionSample.vrm";
            var path = StreamingPath(relative);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning("VRM file '" + relative + "' was not found under StreamingAssets. Showing the placeholder capsule.");
                ShowCapsule();
                return;
            }

            var generation = BeginLoad();
            LoadVrm(path, generation);
        }

        async void LoadVrm(string path, int generation)
        {
            try
            {
                var instance = await VrmAvatarLoader.LoadAsync(path, _cancel.Token);
                if (generation != _generation || instance == null)
                {
                    if (instance != null)
                        DestroyLoaded(instance.gameObject);
                    return;
                }

                Adopt(instance.gameObject, instance, AvatarPreference.VrmSample);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (generation != _generation)
                    return;

                Debug.LogWarning("VRM load failed for '" + path + "'. " + exception.Message);
                ShowCapsule();
            }
        }

        void Adopt(GameObject instance, Vrm10Instance vrm, AvatarPreference source)
        {
            if (instance == null)
            {
                ShowCapsule();
                return;
            }

            CancelLoad();
            ClearInstance();
            _instance = instance;
            _instance.transform.SetParent(transform, false);
            AvatarInspection.EnsureVisibleAlpha(_instance);

            var animator = _instance.GetComponentInChildren<Animator>();
            var report = AvatarInspection.Inspect(animator);
            var descriptor = AvatarDescriptor.Create(_instance.name, report, AvatarInspection.BlendShapeNames(_instance));
            if (vrm != null)
            {
                var extra = new List<KeyValuePair<ExpressionPreset, string>>();
                VrmAvatarLoader.CollectPresetShapes(vrm, extra);
                descriptor = descriptor.WithExpressions(extra);
            }

            _current = descriptor;
            _loaded = source;
            SetPlaceholderVisible(false);
            NotifyAnimator();

            if (!report.IsValid)
                Debug.LogError(report.Message);

            Debug.Log("Avatar ready: " + descriptor.Summary());
            PublishPreference();
        }

        void ShowCapsule()
        {
            CancelLoad();
            ClearInstance();
            _current = null;
            _loaded = AvatarPreference.Auto;
            SetPlaceholderVisible(true);
            NotifyAnimator();
            PublishPreference();
        }

        void PublishPreference()
        {
            var handler = PreferenceChanged;
            if (handler != null)
                handler(_loaded);
        }

        void NotifyAnimator()
        {
            var driver = GetComponent<AvatarAnimationController>();
            if (driver != null)
                driver.Attach(_instance, _current, _loaded);

            var lips = GetComponent<LipSyncDriver>();
            if (lips == null)
                lips = gameObject.AddComponent<LipSyncDriver>();
            lips.Bind(_instance);
        }

        int BeginLoad()
        {
            CancelLoad();
            _cancel = new CancellationTokenSource();
            return ++_generation;
        }

        void CancelLoad()
        {
            _generation++;
            if (_cancel == null)
                return;

            _cancel.Cancel();
            _cancel.Dispose();
            _cancel = null;
        }

        void ClearInstance()
        {
            if (_instance != null)
                DestroyLoaded(_instance);
            _instance = null;
        }

        void SetPlaceholderVisible(bool visible)
        {
            if (placeholder == null)
                return;

            var renderers = placeholder.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visible;

            var colliders = placeholder.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                colliders[i].enabled = visible;
        }

        bool HasLocalPrefab()
        {
            return selection != null && selection.localPrefab != null;
        }

        bool VrmFileExists()
        {
            var relative = selection != null ? selection.streamingVrmRelativePath : "Avatars/CompanionSample.vrm";
            var path = StreamingPath(relative);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        bool FbxResourceExists()
        {
            var path = selection != null ? selection.fbxResourcePath : "Avatars/CompanionSample";
            if (string.IsNullOrEmpty(path))
                return false;

            var prefab = Resources.Load<GameObject>(path);
            return prefab != null;
        }

        static string StreamingPath(string relative)
        {
            if (string.IsNullOrEmpty(relative))
                return null;

            var parts = relative.Replace('\\', '/').Split('/');
            return Path.Combine(Application.streamingAssetsPath, Path.Combine(parts));
        }

        static void DestroyLoaded(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
