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

        /// <summary>"xai" for the cloud voice, "piper" for the offline binary.</summary>
        public string provider = "xai";

        /// <summary>Full path to piper.exe, empty when the local voice is not installed.</summary>
        public string piperExe = string.Empty;

        /// <summary>Full path to the Piper .onnx voice model.</summary>
        public string piperVoice = string.Empty;

        /// <summary>Python interpreter that has kokoro-onnx installed.</summary>
        public string kokoroPython = string.Empty;

        /// <summary>Full path to kokoro_server.py.</summary>
        public string kokoroScript = string.Empty;

        /// <summary>Full path to kokoro-v1.0.onnx.</summary>
        public string kokoroModel = string.Empty;

        /// <summary>Full path to voices-v1.0.bin.</summary>
        public string kokoroVoices = string.Empty;

        /// <summary>Kokoro voice name, for example af_heart.</summary>
        public string kokoroVoice = "af_heart";

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
            copy.provider = provider;
            copy.piperExe = piperExe;
            copy.piperVoice = piperVoice;
            copy.kokoroPython = kokoroPython;
            copy.kokoroScript = kokoroScript;
            copy.kokoroModel = kokoroModel;
            copy.kokoroVoices = kokoroVoices;
            copy.kokoroVoice = kokoroVoice;
            return copy;
        }
    }
}
