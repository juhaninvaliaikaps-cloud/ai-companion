using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class AppBootstrapStartupTests
    {
        [Test]
        public void Awake_StartsCompanionHooksOnTheSameObject()
        {
            var host = new GameObject("bootstrap");
            try
            {
                var startup = host.AddComponent<RecordingStartup>();
                var bootstrap = host.AddComponent<AppBootstrap>();
                startup.Calls = 0;
                bootstrap.StartCompanionHooks();
                Assert.AreEqual(1, startup.Calls);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }

    public sealed class RecordingStartup : MonoBehaviour, ICompanionStartup
    {
        public int Calls;

        public void StartCompanion()
        {
            Calls++;
        }
    }
}
