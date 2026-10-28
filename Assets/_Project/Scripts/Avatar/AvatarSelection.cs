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
    }
}
