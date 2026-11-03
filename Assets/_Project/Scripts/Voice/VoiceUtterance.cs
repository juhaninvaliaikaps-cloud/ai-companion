using System;
using System.IO;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.Voice
{
    /// <summary>
    /// The engine for one reply. Local and private replies are Piper even when the
    /// Piper file is missing. They never fall through to cloud TTS.
    /// </summary>
    public struct VoiceUtterance
    {
        public const string DefaultPiperVoiceId = "en_US-lessac-medium";

        public bool Local;
        public bool Private;
        public string ProviderLabel;
        public string Model;
        public string Engine;
        public string Voice;
        public string EngineId;
        public string PiperPath;

        public bool UsesCloudTts
        {
            get { return !Local; }
        }

        public string LogLine()
        {
            return "Voice route: provider=" + ProviderLabel
                + " model=" + Model
                + " private=" + (Private ? "true" : "false")
                + " engine=" + Engine
                + " voice=" + Voice;
        }

        public static VoiceUtterance Decide(SpeechOrigin origin, VoiceSettings voice, Func<string, bool> fileExists)
        {
            var privateMode = origin.Private;
            var chatId = origin.ChatProviderId;
            var local = !VoiceRoute.UsesCloudTts(privateMode, chatId);
            var model = string.IsNullOrWhiteSpace(origin.Model)
                ? (local ? ChatDefaults.LocalModel : ChatDefaults.Model)
                : origin.Model.Trim();
            var plan = new VoiceUtterance
            {
                Local = local,
                Private = privateMode,
                ProviderLabel = local ? "Local" : "Grok",
                Model = model,
                Engine = local ? "Piper" : "xAI",
                EngineId = local ? VoiceRoute.Local : VoiceRoute.Cloud
            };

            if (!local)
            {
                var cloud = voice != null ? voice.voiceId : null;
                plan.Voice = string.IsNullOrWhiteSpace(cloud) ? "eve" : cloud.Trim();
                return plan;
            }

            // A local reply can use either local engine. Kokoro wins when the user picked it
            // and its files are present; otherwise Piper, which still never falls to cloud.
            if (voice != null && string.Equals(voice.provider, KokoroTtsProvider.Id, StringComparison.OrdinalIgnoreCase) &&
                KokoroReady(voice, fileExists))
            {
                plan.Engine = "Kokoro";
                plan.EngineId = KokoroTtsProvider.Id;
                plan.Voice = string.IsNullOrWhiteSpace(voice.kokoroVoice) ? DefaultKokoroVoiceId : voice.kokoroVoice.Trim();
                return plan;
            }

            var requested = voice != null ? voice.piperVoice : null;
            var resolved = ResolvePiper(requested, voice != null ? voice.piperExe : null, fileExists);
            plan.PiperPath = resolved;
            plan.Voice = VoiceLabel(resolved, requested);
            return plan;
        }

        public const string DefaultKokoroVoiceId = "af_heart";

        public static bool KokoroReady(VoiceSettings voice, Func<string, bool> fileExists)
        {
            if (voice == null)
                return false;

            if (fileExists == null)
                return false;

            return Exists(voice.kokoroPython, fileExists)
                && Exists(voice.kokoroScript, fileExists)
                && Exists(voice.kokoroModel, fileExists)
                && Exists(voice.kokoroVoices, fileExists);
        }

        static bool Exists(string path, Func<string, bool> fileExists)
        {
            return !string.IsNullOrWhiteSpace(path) && fileExists(path.Trim());
        }

        public static string ResolvePiper(string currentPath, string piperExe, Func<string, bool> fileExists)
        {
            if (fileExists != null && !string.IsNullOrWhiteSpace(currentPath) && fileExists(currentPath.Trim()))
                return currentPath.Trim();

            if (fileExists == null)
                return string.Empty;

            var folders = new[] { DirectoryOf(currentPath), DirectoryOf(piperExe) };
            var fileName = DefaultPiperVoiceId + ".onnx";
            for (var i = 0; i < folders.Length; i++)
            {
                if (string.IsNullOrEmpty(folders[i]))
                    continue;
                var candidate = Path.Combine(folders[i], fileName);
                if (fileExists(candidate))
                    return candidate;
            }

            return string.Empty;
        }

        static string VoiceLabel(string resolvedPath, string requestedPath)
        {
            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                var name = Path.GetFileNameWithoutExtension(resolvedPath.Trim());
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            if (!string.IsNullOrWhiteSpace(requestedPath))
            {
                var name = Path.GetFileNameWithoutExtension(requestedPath.Trim());
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            return DefaultPiperVoiceId;
        }

        static string DirectoryOf(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                return Path.GetDirectoryName(path.Trim());
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
