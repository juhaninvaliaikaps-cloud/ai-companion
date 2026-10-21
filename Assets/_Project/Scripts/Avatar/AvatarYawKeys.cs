using UnityEngine;

namespace AICompanion.Avatar
{
    /// <summary>
    /// PlayerPrefs keys for the extra yaw of each loadable humanoid.
    /// The capsule (Auto) has no key, so a placeholder drag is not stored.
    /// </summary>
    public static class AvatarYawKeys
    {
        public const string Prefix = "ai-companion.avatar-yaw.";

        public static bool TryKey(AvatarPreference source, out string key)
        {
            switch (source)
            {
                case AvatarPreference.LocalPrefab:
                case AvatarPreference.VrmSample:
                case AvatarPreference.FbxSample:
                    key = Prefix + source;
                    return true;
                default:
                    key = null;
                    return false;
            }
        }

        public static float Read(AvatarPreference source)
        {
            if (!TryKey(source, out var key))
                return 0f;

            return PlayerPrefs.GetFloat(key, 0f);
        }

        public static void Write(AvatarPreference source, float yawDegrees, bool save)
        {
            if (!TryKey(source, out var key))
                return;

            PlayerPrefs.SetFloat(key, yawDegrees);
            if (save)
                PlayerPrefs.Save();
        }
    }
}
