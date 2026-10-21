using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Scene entry point. Later phases attach chat, voice, the avatar, and the overlay here.
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("AI Companion bootstrap ready.");
        }
    }
}
