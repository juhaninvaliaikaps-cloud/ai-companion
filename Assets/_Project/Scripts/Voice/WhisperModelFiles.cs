using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Voice
{
    /// <summary>
    /// JUH-165: where the Whisper model is. It is not in git (148 MB), so a build made from a git
    /// worktree ships without it. Happi looks in the build's StreamingAssets first, then in her
    /// data folder (%APPDATA%\ProjectLeia\Whisper), which survives every rebuild and is where
    /// "Download speech model" saves it.
    /// </summary>
    public static class WhisperModelFiles
    {
        public const string FolderName = "Whisper";

        /// <summary>A real model is far bigger than this; a stub or a failed download isn't.</summary>
        public const long MinBytes = 1000000;

        public const string DownloadHost = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

        /// <summary>The data-folder copy: &lt;ProjectLeia&gt;/Whisper/&lt;file&gt;, or null.</summary>
        public static string DataPath(string dataFolder, string modelFile)
        {
            if (string.IsNullOrEmpty(dataFolder))
                return null;
            return Path.Combine(dataFolder, FolderName, SttSettings.SanitizeModelFile(modelFile));
        }

        public static string BuildPath(string streamingAssets, string modelFile)
        {
            if (string.IsNullOrEmpty(streamingAssets))
                return null;
            return Path.Combine(streamingAssets, FolderName, SttSettings.SanitizeModelFile(modelFile));
        }

        /// <summary>
        /// The first usable model: the file the user chose in Settings, then the build's, then the
        /// data folder's. Null when none is there.
        /// </summary>
        public static string Find(string streamingAssets, string dataFolder, string modelFile, string chosenFile = null)
        {
            var chosen = ChosenPath(chosenFile);
            if (Usable(chosen))
                return chosen;
            var build = BuildPath(streamingAssets, modelFile);
            if (Usable(build))
                return build;
            var data = DataPath(dataFolder, modelFile);
            return Usable(data) ? data : null;
        }

        /// <summary>
        /// JUH-165: a model file the user picked anywhere on the PC. Only a full path to a .bin
        /// counts; anything else is ignored.
        /// </summary>
        public static string ChosenPath(string chosenFile)
        {
            var path = (chosenFile ?? string.Empty).Trim().Trim('"');
            if (path.Length == 0 || path.Length > 1024 || !path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                return null;
            try
            {
                return Path.IsPathRooted(path) ? Path.GetFullPath(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Usable(string path)
        {
            try
            {
                return !string.IsNullOrEmpty(path) && File.Exists(path) && new FileInfo(path).Length >= MinBytes;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Only whisper.cpp's own file names are downloaded (ggml-base.bin, ggml-small-q5_1.bin, …),
        /// so the setting can't point the download anywhere else.
        /// </summary>
        public static bool Downloadable(string modelFile)
        {
            var name = modelFile ?? string.Empty;
            if (!name.StartsWith("ggml-", StringComparison.Ordinal) || !name.EndsWith(".bin", StringComparison.Ordinal) || name.Length > 64)
                return false;
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '.' && c != '_')
                    return false;
            }

            return name.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        public static string DownloadUrl(string modelFile)
        {
            return Downloadable(modelFile) ? DownloadHost + modelFile : null;
        }

        /// <summary>whisper.cpp model files start with the magic 0x67676d6c, little-endian ("lmgg").</summary>
        public static bool LooksLikeModel(byte[] header)
        {
            return header != null && header.Length >= 4
                   && header[0] == 0x6C && header[1] == 0x6D && header[2] == 0x67 && header[3] == 0x67;
        }

        public static bool LooksLikeModelFile(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var header = new byte[4];
                    return stream.Read(header, 0, 4) == 4 && LooksLikeModel(header);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>The outcome of a model download, for the settings status line.</summary>
    public sealed class WhisperDownloadResult
    {
        public bool Ok;
        public string Message = string.Empty;
        public string Path = string.Empty;
    }

    /// <summary>
    /// JUH-165: downloads a whisper.cpp model into her data folder. The file is written next to
    /// the target as ".part", checked (size and the ggml magic), then moved into place, so a
    /// broken download never replaces a working model. It's a plain file download, not an AI
    /// call, and nothing about the user is sent.
    /// </summary>
    public static class WhisperModelDownloader
    {
        public static async Task<WhisperDownloadResult> DownloadAsync(string dataFolder, string modelFile, Action<float> progress, CancellationToken token)
        {
            var result = new WhisperDownloadResult();
            var url = WhisperModelFiles.DownloadUrl(modelFile);
            var target = WhisperModelFiles.DataPath(dataFolder, modelFile);
            if (url == null || target == null)
            {
                result.Message = "Only whisper.cpp model files (ggml-….bin) can be downloaded.";
                return result;
            }

            var part = target + ".part";
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                if (File.Exists(part))
                    File.Delete(part);
            }
            catch (Exception ex)
            {
                result.Message = "Couldn't write to " + System.IO.Path.GetDirectoryName(target) + " (" + ex.GetType().Name + ").";
                return result;
            }

            using (var request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(part) { removeFileOnAbort = true };
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (token.IsCancellationRequested)
                    {
                        request.Abort();
                        result.Message = "Download cancelled.";
                        return result;
                    }

                    if (progress != null)
                        progress(request.downloadProgress);
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    TryDelete(part);
                    result.Message = "The download failed (" + (string.IsNullOrEmpty(request.error) ? "no answer" : request.error) + ").";
                    return result;
                }
            }

            if (!WhisperModelFiles.Usable(part) || !WhisperModelFiles.LooksLikeModelFile(part))
            {
                TryDelete(part);
                result.Message = "The downloaded file isn't a Whisper model, so it wasn't used.";
                return result;
            }

            try
            {
                if (File.Exists(target))
                    File.Delete(target);
                File.Move(part, target);
            }
            catch (Exception ex)
            {
                TryDelete(part);
                result.Message = "Couldn't save the model (" + ex.GetType().Name + ").";
                return result;
            }

            result.Ok = true;
            result.Path = target;
            result.Message = "Speech model ready: " + target;
            Debug.Log("STT: downloaded " + modelFile + " to the data folder");
            return result;
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }
    }
}
