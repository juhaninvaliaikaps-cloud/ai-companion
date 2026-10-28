using UnityEngine;

namespace AICompanion.Voice
{
    /// <summary>
    /// Spoken-reply options. This asset holds no API key.
    /// Cloud voices stay off for the local chat provider unless
    /// <see cref="allowCloudTtsForLocalReplies"/> is turned on.
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceSettings", menuName = "AI Companion/Voice Settings")]
    public sealed class VoiceSettings : ScriptableObject
    {
        public bool enabled = true;
        public string voiceId = "eve";
        public string language = "en";
        public float speed = 1f;
        public float volume = 1f;
        public bool allowCloudTtsForLocalReplies;
        public float timeoutSeconds = 20f;
        public int sampleRate = 24000;

        public VoiceSettings RuntimeCopy()
        {
            var copy = CreateInstance<VoiceSettings>();
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.enabled = enabled;
            copy.voiceId = voiceId;
            copy.language = language;
            copy.speed = speed;
            copy.volume = volume;
            copy.allowCloudTtsForLocalReplies = allowCloudTtsForLocalReplies;
            copy.timeoutSeconds = timeoutSeconds;
            copy.sampleRate = sampleRate;
            return copy;
        }
    }
}
