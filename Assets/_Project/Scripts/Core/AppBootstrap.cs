using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Scene entry point. Starts every <see cref="ICompanionStartup"/> on this GameObject,
    /// including the overlay window.
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("AI Companion bootstrap ready.");
            StartCompanionHooks();
        }

        public void StartCompanionHooks()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var startup = behaviours[i] as ICompanionStartup;
                if (startup != null)
                    startup.StartCompanion();
            }
        }
    }
}
