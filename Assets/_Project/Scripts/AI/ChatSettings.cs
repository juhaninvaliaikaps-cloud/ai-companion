using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>
    /// Which chat backend is active. This asset holds no API key.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatSettings", menuName = "AI Companion/Chat Settings")]
    public sealed class ChatSettings : ScriptableObject
    {
        public string providerId = ChatDefaults.GrokId;
        public string model = ChatDefaults.Model;
        public string baseUrl = ChatDefaults.BaseUrl;
        public string localModel = ChatDefaults.LocalModel;
        public string localBaseUrl = ChatDefaults.LocalBaseUrl;
        public float timeoutSeconds = ChatDefaults.TimeoutSeconds;
        public float temperature = ChatDefaults.Temperature;
        public int maxTokens = ChatDefaults.MaxTokens;
        public string systemPrompt = ChatDefaults.SystemPrompt;

        public ChatSettings RuntimeCopy()
        {
            var copy = CreateInstance<ChatSettings>();
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.providerId = providerId;
            copy.model = model;
            copy.baseUrl = baseUrl;
            copy.localModel = localModel;
            copy.localBaseUrl = localBaseUrl;
            copy.timeoutSeconds = timeoutSeconds;
            copy.temperature = temperature;
            copy.maxTokens = maxTokens;
            copy.systemPrompt = systemPrompt;
            return copy;
        }
    }
}
