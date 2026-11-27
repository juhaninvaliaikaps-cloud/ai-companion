using UnityEngine;

namespace AICompanion.Settings
{
    public partial class SettingsPanel
    {
        /// <summary>
        /// The video avatar is added here, not in the scene file, so 3D mode costs one
        /// idle component and the scene does not change.
        /// </summary>
        void EnsureVideoAvatar()
        {
            if (GetComponent<VideoAvatar>() == null)
                gameObject.AddComponent<VideoAvatar>();
        }

    }
}
