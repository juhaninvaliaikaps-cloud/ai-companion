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
    }
}
