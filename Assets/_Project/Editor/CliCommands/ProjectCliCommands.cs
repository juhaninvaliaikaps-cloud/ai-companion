using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Unity.Pipeline.Commands
{
    /// <summary>
    /// Editor commands the local Unity CLI can run against the open project.
    /// Attributes are discovered from this assembly after the next domain reload.
    /// </summary>
    public static class ProjectCliCommands
    {
        const string ProjectRoot = "Assets/_Project";
        const string PlayerPath = "Builds/Windows/AICompanion.exe";

        [CliCommand(
            "run_editmode_tests",
            "Run EditMode tests and return pass, fail, and skip counts plus leaf failures.",
            Tags = new[] { "tests" })]
        public static object RunEditModeTests()
        {
            var listener = new EditModeRunListener();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                api.RegisterCallbacks(listener);
                var settings = new ExecutionSettings(new Filter { testMode = TestMode.EditMode })
                {
                    runSynchronously = true
                };
                api.Execute(settings);
            }
            finally
            {
                api.UnregisterCallbacks(listener);
                UnityEngine.Object.DestroyImmediate(api);
            }

            return new
            {
                finished = listener.Finished,
                passCount = listener.PassCount,
                failCount = listener.FailCount,
                skipCount = listener.SkipCount,
                inconclusiveCount = listener.InconclusiveCount,
                failures = listener.Failures,
                runError = listener.RunError,
                note = "EditMode only. ExecutionSettings.runSynchronously excludes multi-frame UnityTest tests."
            };
        }

        [CliCommand(
            "get_compile_and_console_errors",
            "Return the current compile-failure flag and compiler or console errors seen since this domain loaded.",
            Tags = new[] { "scripts/compile", "observability/console" })]
        public static object GetCompileAndConsoleErrors(
            [CliArg("limit", "Maximum recent entries to return from each list.")] int limit = 40)
        {
            return EditorDiagnosticsBuffer.Snapshot(limit);
        }

        [CliCommand(
            "list_project_avatars_and_clips",
            "List .vrm and .fbx models and AnimationClips under Assets/_Project.",
            Tags = new[] { "assets", "animation/clip" })]
        public static object ListProjectAvatarsAndClips()
        {
            var models = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { ProjectRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var extension = Path.GetExtension(path);
                if (extension.Equals(".vrm", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    models.Add(path);
                }
            }

            models.Sort(StringComparer.Ordinal);

            var clips = new List<ClipRecord>();
            var seenPaths = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ProjectRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seenPaths.Add(path))
                    continue;

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null)
                    continue;

                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        clips.Add(new ClipRecord { path = path, name = clip.name });
                }
            }

            clips.Sort((a, b) =>
            {
                var pathOrder = string.CompareOrdinal(a.path, b.path);
                return pathOrder != 0 ? pathOrder : string.CompareOrdinal(a.name, b.name);
            });

            return new { root = ProjectRoot, models, clips };
        }

        [CliCommand(
            "build_windows_player",
            "Build a Windows x64 development player to Builds/Windows. Requires an enabled scene.",
            Tags = new[] { "build" })]
        public static object BuildWindowsPlayer()
        {
            var scenes = new List<string>();
            var configured = EditorBuildSettings.scenes;
            if (configured != null)
            {
                foreach (var scene in configured)
                {
                    if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                        scenes.Add(scene.path);
                }
            }

            if (scenes.Count == 0)
            {
                return new
                {
                    built = false,
                    outputPath = PlayerPath,
                    error = "No enabled scene in Editor Build Settings. Save a scene under Assets/_Project/Scenes and enable it, then run this command again."
                };
            }

            var platformName = BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneWindows64);
            // Unverified public contract: "Architecture" is the setting name EditorUserBuildSettings
            // writes for StandaloneWindows64. It is not a documented constant.
            EditorUserBuildSettings.SetPlatformSettings(platformName, "Architecture", OSArchitecture.x64.ToString());

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            return new
            {
                built = summary.result == BuildResult.Succeeded,
                result = summary.result.ToString(),
                totalErrors = summary.totalErrors,
                totalWarnings = summary.totalWarnings,
                totalSize = summary.totalSize,
                outputPath = summary.outputPath,
                architecture = EditorUserBuildSettings.GetPlatformSettings(platformName, "Architecture")
            };
        }

        public sealed class ClipRecord
        {
            public string path;
            public string name;
        }
    }

    [InitializeOnLoad]
    static class EditorDiagnosticsBuffer
    {
        const int StoredLimit = 200;

        static readonly List<CompilerError> CompilerErrors = new List<CompilerError>();
        static readonly List<ConsoleError> ConsoleErrors = new List<ConsoleError>();

        static EditorDiagnosticsBuffer()
        {
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            Application.logMessageReceived += OnLog;
        }

        public static object Snapshot(int limit)
        {
            if (limit < 1)
                limit = 1;
            if (limit > StoredLimit)
                limit = StoredLimit;

            return new
            {
                scriptCompilationFailed = EditorUtility.scriptCompilationFailed,
                compilerErrors = Tail(CompilerErrors, limit),
                consoleErrors = Tail(ConsoleErrors, limit),
                note = "compilerErrors and consoleErrors contain only messages received after this Editor domain loaded. Unity publishes those through events, not a history query. scriptCompilationFailed still reports whether the Editor log currently contains compile errors. The built-in get_console_logs command reads the Pipeline console buffer."
            };
        }

        static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            if (messages == null)
                return;

            foreach (var message in messages)
            {
                if (message.type != CompilerMessageType.Error)
                    continue;

                CompilerErrors.Add(new CompilerError
                {
                    assemblyPath = assemblyPath,
                    message = message.message,
                    file = message.file,
                    line = message.line,
                    column = message.column
                });
            }

            Trim(CompilerErrors);
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            ConsoleErrors.Add(new ConsoleError
            {
                type = type.ToString(),
                message = condition,
                stackTrace = stackTrace
            });
            Trim(ConsoleErrors);
        }

        static void Trim<T>(List<T> entries)
        {
            var extra = entries.Count - StoredLimit;
            if (extra > 0)
                entries.RemoveRange(0, extra);
        }

        static List<T> Tail<T>(List<T> entries, int limit)
        {
            if (entries.Count <= limit)
                return new List<T>(entries);

            return entries.GetRange(entries.Count - limit, limit);
        }

        public sealed class CompilerError
        {
            public string assemblyPath;
            public string message;
            public string file;
            public int line;
            public int column;
        }

        public sealed class ConsoleError
        {
            public string type;
            public string message;
            public string stackTrace;
        }
    }

    sealed class EditModeRunListener : IErrorCallbacks
    {
        public int PassCount { get; private set; }
        public int FailCount { get; private set; }
        public int SkipCount { get; private set; }
        public int InconclusiveCount { get; private set; }
        public bool Finished { get; private set; }
        public string RunError { get; private set; }
        public List<FailureRecord> Failures { get; } = new List<FailureRecord>();

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            PassCount = result.PassCount;
            FailCount = result.FailCount;
            SkipCount = result.SkipCount;
            InconclusiveCount = result.InconclusiveCount;
            Finished = true;
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren || result.TestStatus != TestStatus.Failed)
                return;

            Failures.Add(new FailureRecord
            {
                fullName = result.FullName,
                name = result.Name,
                resultState = result.ResultState,
                message = result.Message
            });
        }

        public void OnError(string message)
        {
            RunError = message;
        }

        public sealed class FailureRecord
        {
            public string fullName;
            public string name;
            public string resultState;
            public string message;
        }
    }
}
