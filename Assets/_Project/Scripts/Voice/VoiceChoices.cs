using System;
using System.Collections.Generic;
using System.IO;

namespace AICompanion.Voice
{
    /// <summary>
    /// JUH-181: the voice drop-downs for a persona (Persona Studio's Voice step). One list per
    /// engine, so her private-mode (local) and public-mode (cloud) voices are set together.
    /// Every list can start with an empty entry, which keeps the Voice tab's voice. A saved id
    /// that is not in the list is kept as an option, so opening a persona never loses it.
    /// Pure, for the view and tests; the folder listing is done by the caller.
    /// </summary>
    public static class VoiceChoices
    {
        /// <summary>Kokoro's bundled voices, the same list as the Voice tab.</summary>
        public static readonly string[] Kokoro =
        {
            "af_heart", "af_bella", "af_nicole", "af_sky", "bf_emma", "bf_isabella", "bm_george", "bm_lewis",
            "am_adam", "am_michael", "am_puck", "am_santa", "hm_omega", "hm_psi", "jf_alpha", "jm_kumo",
        };

        /// <summary>xAI's built-in voices (docs.x.ai, 2026-11). eve is the default.</summary>
        public static readonly string[] Cloud = { "eve", "ara", "rex", "sal", "leo" };

        public const string Piper = "Piper";
        public const string KokoroName = "Kokoro";
        public const string Chatterbox = "Chatterbox";

        /// <summary>
        /// The local engine her private-mode replies use, by the Voice tab's choice: Chatterbox
        /// or Kokoro when picked, otherwise Piper (the same order as <see cref="VoiceUtterance"/>).
        /// </summary>
        public static string LocalEngine(string ttsProvider)
        {
            if (string.Equals(ttsProvider, ChatterboxTtsProvider.Id, StringComparison.OrdinalIgnoreCase))
                return Chatterbox;
            if (string.Equals(ttsProvider, KokoroTtsProvider.Id, StringComparison.OrdinalIgnoreCase))
                return KokoroName;
            return Piper;
        }

        /// <summary>The three local engines with the one in use first.</summary>
        public static List<string> LocalOrder(string ttsProvider)
        {
            var first = LocalEngine(ttsProvider);
            var order = new List<string>(3) { first };
            if (first != Piper)
                order.Add(Piper);
            if (first != KokoroName)
                order.Add(KokoroName);
            if (first != Chatterbox)
                order.Add(Chatterbox);
            return order;
        }

        /// <summary>
        /// Piper voice ids from the .onnx files in its folder: the file name without the
        /// extension (what <c>PersonaVoices</c> resolves), sorted, without duplicates.
        /// </summary>
        public static List<string> PiperIds(IEnumerable<string> onnxFiles, bool includeEmpty, string current)
        {
            var ids = new List<string>();
            if (onnxFiles != null)
            {
                foreach (var file in onnxFiles)
                {
                    if (string.IsNullOrWhiteSpace(file))
                        continue;
                    var name = Path.GetFileNameWithoutExtension(file.Trim());
                    if (!string.IsNullOrEmpty(name))
                        AddUnique(ids, name);
                }
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return Finish(ids, includeEmpty, current);
        }

        public static List<string> KokoroIds(bool includeEmpty, string current)
        {
            return Finish(new List<string>(Kokoro), includeEmpty, current);
        }

        public static List<string> CloudIds(bool includeEmpty, string current)
        {
            return Finish(new List<string>(Cloud), includeEmpty, current);
        }

        /// <summary>The folder the Piper voices are in: next to the chosen voice, else next to piper.exe. Empty when neither is set.</summary>
        public static string PiperFolder(string piperVoice, string piperExe)
        {
            var path = !string.IsNullOrWhiteSpace(piperVoice) ? piperVoice.Trim() : (piperExe ?? string.Empty).Trim();
            if (path.Length == 0)
                return string.Empty;
            try
            {
                return Path.GetDirectoryName(path) ?? string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        static List<string> Finish(List<string> ids, bool includeEmpty, string current)
        {
            if (!string.IsNullOrWhiteSpace(current))
                AddUnique(ids, current.Trim());
            if (includeEmpty)
                ids.Insert(0, string.Empty);
            return ids;
        }

        static void AddUnique(List<string> ids, string id)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            ids.Add(id);
        }
    }
}
