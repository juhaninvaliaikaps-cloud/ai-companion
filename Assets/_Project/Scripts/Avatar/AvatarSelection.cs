using System;
using UnityEngine;

namespace AICompanion.Avatar
{
    public enum AvatarPreference
    {
        Auto = 0,
        LocalPrefab = 1,
        VrmSample = 2,
        FbxSample = 3
    }

    /// <summary>
    /// Replaces one clip of the shared controller with a clip that stays on this
    /// machine, matched by the base clip name. Used for local assets such as Asuna,
    /// whose animations cannot be committed to the repository.
    /// </summary>
    [Serializable]
    public sealed class AnimationClipOverride
    {
        [Tooltip("Base clip to replace, for example Walk or Idle.")]
        public string clipName;

        public AnimationClip clip;
    }

    /// <summary>
    /// Which humanoid to show. Assign <see cref="localPrefab"/> to a model that
    /// stays on this machine, such as the Asset Store Asuna prefab. An empty
    /// reference falls through to the committed CC0 sample, then the capsule.
    /// </summary>
    [CreateAssetMenu(fileName = "AvatarSelection", menuName = "AI Companion/Avatar Selection")]
    public sealed class AvatarSelection : ScriptableObject
    {
        public AvatarPreference preference = AvatarPreference.Auto;
        public GameObject localPrefab;
        public string streamingVrmRelativePath = "Avatars/CompanionSample.vrm";
        public string fbxResourcePath = "Avatars/CompanionSample";

        /// <summary>Fine-tune in pixels for a model whose foot bones sit off the sole (default 0).</summary>
        public float groundOffsetPx;

        /// <summary>Local clip replacements, matched by base clip name. Default empty.</summary>
        public AnimationClipOverride[] animationOverrides = Array.Empty<AnimationClipOverride>();
    }
}
