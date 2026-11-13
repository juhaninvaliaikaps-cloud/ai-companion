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
            if (!SingleInstanceGuard.TryAcquire())
            {
                Debug.Log("Backseat Catgirl is already running. This second instance will exit.");
                Application.Quit();
                return;
            }

            Debug.Log(AppName.Display + " bootstrap ready.");
            StartCompanionHooks();
        }

        private void OnApplicationQuit()
        {
            SingleInstanceGuard.Release();
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
