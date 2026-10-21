using System.Collections;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AICompanion.Tests.PlayMode
{
    public class AppBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator Awake_LogsReadyMessage()
        {
            LogAssert.Expect(LogType.Log, "AI Companion bootstrap ready.");

            var host = new GameObject("AppBootstrap test");
            host.AddComponent<AppBootstrap>();

            yield return null;

            Object.Destroy(host);
        }
    }
}
