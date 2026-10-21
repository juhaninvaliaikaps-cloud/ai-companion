using System;
using UnityEngine;

namespace AICompanion.Voice
{
    /// <summary>
    /// Push-to-talk options. This asset holds no audio and no API key.
    /// The model weights stay out of the repository.
    /// </summary>
    [CreateAssetMenu(fileName = "SttSettings", menuName = "AI Companion/Speech To Text Settings")]
    public sealed class SttSettings : ScriptableObject
    {
        public const string DefaultModelFile = "ggml-base.bin";
        public const int MinSamples16k = 3200;

        public bool enabled = true;
        public KeyCode pushToTalkKey = KeyCode.V;
        public string language = "auto";
        public string modelFile = DefaultModelFile;
        public string microphoneDevice = "";
        public int maxRecordSeconds = 30;

        public KeyCode PushToTalkKey
        {
            get
            {
                var key = pushToTalkKey;
                if (key == KeyCode.None || Clashes(key))
                    return KeyCode.V;
                return key;
            }
        }

        public string LanguageCode
        {
            get
            {
                var value = string.IsNullOrWhiteSpace(language) ? "auto" : language.Trim().ToLowerInvariant();
                if (value.Length > 16)
                    value = value.Substring(0, 16);
                return value;
            }
        }

        public string ModelFileName
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(modelFile) ? DefaultModelFile : modelFile.Trim();
                name = name.Replace('\\', '/');
                var slash = name.LastIndexOf('/');
                if (slash >= 0)
                    name = name.Substring(slash + 1);
                if (name.Length == 0 || name.IndexOf("..", StringComparison.Ordinal) >= 0)
                    return DefaultModelFile;
                if (!name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                    return DefaultModelFile;
                return name;
            }
        }

        public int MaxRecordSeconds
        {
            get
            {
                if (maxRecordSeconds < 1)
                    return 30;
                if (maxRecordSeconds > 120)
                    return 120;
                return maxRecordSeconds;
            }
        }

        public static bool Clashes(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                case KeyCode.Alpha5:
                case KeyCode.Keypad1:
                case KeyCode.Keypad2:
                case KeyCode.Keypad3:
                case KeyCode.Keypad4:
                case KeyCode.Keypad5:
                case KeyCode.T:
                case KeyCode.R:
                case KeyCode.F6:
                case KeyCode.F7:
                case KeyCode.F8:
                case KeyCode.F9:
                case KeyCode.F10:
                case KeyCode.Escape:
                    return true;
                default:
                    return false;
            }
        }
    }
}
