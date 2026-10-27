using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AICompanion.Editor
{
    /// <summary>
    /// CI entry point. Linux EditMode compiles with UNITY_EDITOR defined, so the
    /// Windows player blocks never type-check there. This method runs under
    /// -buildTarget StandaloneWindows64 and fails the process if those scripts
    /// do not compile. It does not write a player.
    /// </summary>
    public static class WindowsPlayerScriptCheck
    {
        public static void VerifyStandaloneWindows64()
        {
            const BuildTarget target = BuildTarget.StandaloneWindows64;
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                var switched = EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone, target);
                if (!switched)
                {
                    Debug.LogError("Could not switch the active build target to StandaloneWindows64.");
                    EditorApplication.Exit(1);
                    return;
                }
            }

            if (EditorUtility.scriptCompilationFailed)
            {
                Debug.LogError("Windows player scripts failed to compile.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("Windows player scripts compiled for StandaloneWindows64.");
            // game-ci validateBuild accepts this exact line from its default reporter.
            Console.WriteLine("Build succeeded!");
            EditorApplication.Exit(0);
        }
    }
}
