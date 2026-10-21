using System;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace AICompanion.Editor.Voice
{
    /// <summary>
    /// Fetches a multilingual ggml weight into StreamingAssets. The file is
    /// gitignored. This menu does not run in batch mode, so CI never downloads it.
    /// </summary>
    public static class WhisperModelDownload
    {
        const string ModelHost = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

        [MenuItem("AI Companion/Download Whisper Model (base)")]
        public static void DownloadBase()
        {
            Download("ggml-base.bin");
        }

        [MenuItem("AI Companion/Download Whisper Model (small)")]
        public static void DownloadSmall()
        {
            Download("ggml-small.bin");
        }

        static void Download(string fileName)
        {
            if (Application.isBatchMode)
                return;

            var folder = Path.Combine(Application.dataPath, "StreamingAssets", "Whisper");
            Directory.CreateDirectory(folder);
            var destination = Path.Combine(folder, fileName);
            if (File.Exists(destination))
            {
                var replace = EditorUtility.DisplayDialog(
                    "Whisper model",
                    fileName + " is already in StreamingAssets/Whisper. Download it again?",
                    "Replace",
                    "Cancel");
                if (!replace)
                    return;
            }

            var partial = destination + ".partial";
            var url = ModelHost + fileName;
            EditorUtility.DisplayProgressBar("Whisper model", "Starting " + fileName, 0f);
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AICompanion");
                    using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                    {
                        response.EnsureSuccessStatusCode();
                        var total = response.Content.Headers.ContentLength ?? -1L;
                        using (var source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        using (var file = File.Create(partial))
                        {
                            var buffer = new byte[256 * 1024];
                            long written = 0;
                            int read;
                            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                file.Write(buffer, 0, read);
                                written += read;
                                var progress = total > 0 ? (float)written / total : 0.5f;
                                var label = fileName + "  " + (written / (1024 * 1024)) + " MB";
                                if (EditorUtility.DisplayCancelableProgressBar("Whisper model", label, progress))
                                {
                                    file.Close();
                                    DeleteQuiet(partial);
                                    EditorUtility.DisplayDialog("Whisper model", "Download cancelled.", "OK");
                                    return;
                                }
                            }
                        }
                    }
                }

                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(partial, destination);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Whisper model",
                    "Saved StreamingAssets/Whisper/" + fileName + ". It stays out of git.",
                    "OK");
            }
            catch (Exception ex)
            {
                DeleteQuiet(partial);
                EditorUtility.DisplayDialog("Whisper model", "Download failed: " + ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void DeleteQuiet(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
                // The partial file is gitignored. A locked file can be removed later.
            }
        }
    }
}
