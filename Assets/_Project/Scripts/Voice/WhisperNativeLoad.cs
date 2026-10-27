using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AICompanion.AI;
using UnityEngine;

namespace AICompanion.Voice
{
    /// <summary>
    /// Loads the whisper.cpp CPU libraries from their own folder before the
    /// first P/Invoke. Windows does not search that folder for sibling DLLs
    /// when Unity loads <c>libwhisper.dll</c> by a full path.
    /// </summary>
    public static class WhisperNativeLoad
    {
        public const string LibraryFile = "libwhisper.dll";

        /// <summary>LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS.</summary>
        public const uint SearchFlags = 0x00000100u | 0x00001000u;

        static readonly string[] LoadOrder =
        {
            "ggml-vulkan.dll",
            "ggml-base.dll",
            "ggml-cpu.dll",
            "ggml.dll",
            "libwhisper.dll",
        };

        static bool _ready;

        public static string PluginDirectory { get; private set; }

        public static string LoadDetail { get; private set; }

        public static string[] CandidateDirectories(string dataPath)
        {
            if (string.IsNullOrEmpty(dataPath))
                return Array.Empty<string>();

            var trimmed = dataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var found = new string[2];
            var count = 0;
            var parent = Path.GetDirectoryName(trimmed);
            if (!string.IsNullOrEmpty(parent))
            {
                found[count++] = Path.Combine(parent, "Packages", "com.whisper.unity", "Plugins", "Windows");
            }

            found[count++] = Path.Combine(trimmed, "Plugins", "x86_64");
            if (count == found.Length)
                return found;

            var packed = new string[count];
            Array.Copy(found, packed, count);
            return packed;
        }

        public static string FindDirectory(string dataPath)
        {
            var candidates = CandidateDirectories(dataPath);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate))
                    continue;
                if (!File.Exists(Path.Combine(candidate, LibraryFile)))
                    continue;
                return Path.GetFullPath(candidate);
            }

            return null;
        }

        public static bool TryPrepare(out string detail)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (_ready)
            {
                detail = LoadDetail;
                return true;
            }

            try
            {
                var dir = FindDirectory(Application.dataPath);
                PluginDirectory = dir;
                if (string.IsNullOrEmpty(dir))
                {
                    LoadDetail = "plugin directory missing";
                    detail = LoadDetail;
                    return false;
                }

                for (var i = 0; i < LoadOrder.Length; i++)
                {
                    var name = LoadOrder[i];
                    var file = Path.Combine(dir, name);
                    if (!File.Exists(file))
                    {
                        LoadDetail = name + " missing";
                        detail = LoadDetail;
                        return false;
                    }

                    var handle = LoadLibraryExW(Path.GetFullPath(file), IntPtr.Zero, SearchFlags);
                    if (handle == IntPtr.Zero)
                    {
                        var code = Marshal.GetLastWin32Error();
                        var problem = name + " win32=" + code.ToString(CultureInfo.InvariantCulture);
                        if (name == "ggml-cpu.dll" && code == 126)
                            problem += " (VC++ 2015-2022 x64 redist)";
                        LoadDetail = problem;
                        detail = LoadDetail;
                        return false;
                    }
                }

                _ready = true;
                LoadDetail = dir;
                detail = dir;
                return true;
            }
            catch (Exception ex)
            {
                LoadDetail = ex.GetType().Name;
                detail = LoadDetail;
                return false;
            }
#else
            detail = "not-windows";
            return true;
#endif
        }

        public static string FailureLine(string modelFile, string exceptionType, string exceptionMessage, string pluginPath, string loadDetail)
        {
            var type = OneToken(exceptionType, "Exception", 48);
            var model = OneToken(modelFile, "model", 80);
            var path = string.IsNullOrWhiteSpace(pluginPath) ? "-" : pluginPath.Trim();
            var message = Flatten(ChatErrors.Scrub(exceptionMessage));
            if (message.Length > 120)
                message = message.Substring(0, 120);

            var extra = string.Empty;
            var detail = Flatten(ChatErrors.Scrub(loadDetail));
            if (detail.Length > 0 && detail != path)
                extra = " load=" + detail;

            var head = "STT: transcribe failed " + type + " " + model;
            var middle = message.Length == 0 ? string.Empty : " " + message;
            var tail = " path=" + path;
            var line = head + middle + tail + extra;
            if (line.Length <= 500)
                return line;

            var budget = 500 - head.Length - tail.Length;
            if (budget < 1)
                return Fit(head + tail, 500);

            var room = budget;
            if (extra.Length >= room)
            {
                extra = extra.Substring(0, room);
                middle = string.Empty;
            }
            else if (middle.Length + extra.Length > room)
            {
                middle = middle.Substring(0, room - extra.Length);
            }

            return head + middle + tail + extra;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32", EntryPoint = "LoadLibraryExW", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
#endif

        static string OneToken(string value, string fallback, int max)
        {
            var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (text.Length > max)
                text = text.Substring(0, max);
            return text;
        }

        static string Flatten(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            var pendingSpace = false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '\r' || c == '\n' || c == '\t')
                    c = ' ';
                if (c == ' ')
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }

                if (pendingSpace)
                    sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
            }

            return sb.ToString();
        }

        static string Fit(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value ?? string.Empty;
            return value.Substring(0, max);
        }
    }
}
