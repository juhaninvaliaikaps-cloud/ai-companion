using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AICompanion.Voice;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-188: a truly separate OS window for an adult clip. It uses ffplay so the clip gets
    /// its own native window with an X button and normal corner resizing, independent of the
    /// avatar overlay.
    /// </summary>
    public static class AdultExternalPlayer
    {
        static readonly Dictionary<string, Process> Running = new Dictionary<string, Process>();
        static bool _audioPriority;
        static string _player;

        public static bool Available
        {
            get { return Application.platform == RuntimePlatform.WindowsPlayer && Player.Length > 0; }
        }

        public static bool IsActive(string cue)
        {
            Process process;
            return Running.TryGetValue(cue ?? string.Empty, out process) && process != null && !process.HasExited;
        }

        public static bool TryShow(string cue, string clipPath, bool loop, string title)
        {
            if (!Available || string.IsNullOrEmpty(clipPath) || !File.Exists(clipPath))
                return false;
            if (IsActive(cue))
                return true;

            var args = "-autoexit "
                + (loop ? "-loop 0 " : string.Empty)
                + "-alwaysontop "
                + "-window_title \"" + (title ?? "Happi").Replace("\"", "'") + "\" "
                + "-x 544 -y 960 "
                + "\"" + clipPath + "\"";
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Player,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
                if (process == null)
                    return false;
                Running[cue] = process;
                SetAudioPriority(true);
                UnityEngine.Debug.Log("Adult external window: " + Path.GetFileName(clipPath));
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("Adult external window failed: " + e.Message);
                return false;
            }
        }

        public static void Hide(string cue)
        {
            Process process;
            if (!Running.TryGetValue(cue ?? string.Empty, out process))
                return;
            Running.Remove(cue);
            Stop(process);
            if (Running.Count == 0)
                SetAudioPriority(false);
        }

        public static void HideAll()
        {
            foreach (var process in Running.Values)
                Stop(process);
            Running.Clear();
            SetAudioPriority(false);
        }

        /// <summary>Drops dead windows and releases voice priority when the last one closes.</summary>
        public static void Tick()
        {
            if (Running.Count == 0)
                return;
            var dead = new List<string>();
            foreach (var pair in Running)
            {
                if (pair.Value == null || pair.Value.HasExited)
                    dead.Add(pair.Key);
            }
            for (var i = 0; i < dead.Count; i++)
                Running.Remove(dead[i]);
            if (Running.Count == 0)
                SetAudioPriority(false);
        }

        static string Player
        {
            get
            {
                if (_player != null)
                    return _player;
                var candidates = new List<string>
                {
                    Path.Combine(Application.streamingAssetsPath, "ffplay.exe"),
                    Path.Combine(Application.dataPath, "..", "ffplay.exe"),
                };
                var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (var folder in path.Split(';'))
                {
                    if (folder.Length > 0)
                        candidates.Add(Path.Combine(folder, "ffplay.exe"));
                }
                candidates.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft\\WinGet\\Links\\ffplay.exe"));
                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = Path.GetFullPath(candidates[i]);
                    if (File.Exists(candidate))
                    {
                        _player = candidate;
                        return _player;
                    }
                }
                _player = string.Empty;
                return _player;
            }
        }

        static void Stop(Process process)
        {
            if (process == null)
                return;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
                // The player may already have closed by its native X button.
            }
        }

        static void SetAudioPriority(bool on)
        {
            if (_audioPriority == on)
                return;
            _audioPriority = on;
            var voice = UnityEngine.Object.FindFirstObjectByType<VoiceSpeaker>();
            if (voice != null)
                voice.SetClipAudioPriority(on);
        }
    }
}
