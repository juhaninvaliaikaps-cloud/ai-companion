using System;
using System.Collections;
using System.IO;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// A developer tool for checking the chat by hand without typing (JUH-122). Start the app with
    /// <c>-pisara-chat &lt;file&gt;</c>: each non-empty line of the file is sent as a user message
    /// through the real chat (the same path as a typed or spoken message, with the active persona,
    /// model, privacy and relationship). After each reply it saves two window screenshots next to
    /// the file (<c>chat-01a.png</c> at 0.3 s, <c>chat-01b.png</c> at 0.9 s), so the floating pieces
    /// near her can be seen, then quits. It changes that persona's chat and relationship like a
    /// real conversation would. Use it with <c>-newui</c> and <c>-allowmulti</c>.
    /// </summary>
    public sealed class PisaraChatTool : MonoBehaviour
    {
        public const string Flag = "-pisara-chat";
        public const float ReplyTimeoutSeconds = 180f;

        public static bool Requested(string[] args)
        {
            return PisaraReadingTool.Value(args, Flag) != null;
        }

        IEnumerator Start()
        {
            var file = PisaraReadingTool.Value(Environment.GetCommandLineArgs(), Flag);
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                Debug.LogError("Pisara chat tool: file not found.");
                Application.Quit(2);
                yield break;
            }

            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            var lines = File.ReadAllLines(file);

            // Let the persona, her reading, the relationship and the new UI load first.
            yield return new WaitForSecondsRealtime(12f);
            var chat = GetComponent<ChatPanel>();
            if (chat == null)
            {
                Debug.LogError("Pisara chat tool: no chat panel.");
                Application.Quit(3);
                yield break;
            }

            var sent = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                sent++;
                Debug.Log("Pisara chat tool: " + sent + " > " + line);
                if (!chat.SubmitUserText(line))
                {
                    Debug.LogWarning("Pisara chat tool: the chat did not take message " + sent);
                    continue;
                }

                var waited = 0f;
                yield return null;
                while (chat.TranscriptBusy && waited < ReplyTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                var stem = Path.Combine(folder, "chat-" + sent.ToString("00"));
                yield return new WaitForSecondsRealtime(0.3f);
                ScreenCapture.CaptureScreenshot(stem + "a.png");
                yield return new WaitForSecondsRealtime(0.6f);
                ScreenCapture.CaptureScreenshot(stem + "b.png");
                yield return new WaitForSecondsRealtime(2.5f);
            }

            Debug.Log("Pisara chat tool: done, " + sent + " messages");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit(0);
        }
    }
}
