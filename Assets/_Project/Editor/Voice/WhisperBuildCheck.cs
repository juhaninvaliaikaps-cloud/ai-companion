using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AICompanion.Editor.Voice
{
    /// <summary>
    /// JUH-165: warns when a build has no Whisper model. The model is gitignored, so a build from a
    /// git worktree ships without it. That build still works: Happi then looks in
    /// %APPDATA%\ProjectLeia\Whisper, and Settings > Voice can download it there.
    /// </summary>
    public sealed class WhisperBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var path = Path.Combine(Application.dataPath, "StreamingAssets", "Whisper", "ggml-base.bin");
            if (File.Exists(path) && new FileInfo(path).Length > 1000000)
                return;
            Debug.LogWarning("Whisper: this build has no speech model (Assets/StreamingAssets/Whisper/ggml-base.bin is missing; "
                + "it isn't in git, so git worktrees don't have it). Happi will look in %APPDATA%\\ProjectLeia\\Whisper instead, "
                + "and Settings > Voice can download it. To ship it in the build, use AI Companion > Download Whisper Model (base) "
                + "or copy the file from the main project folder.");
        }
    }
}
