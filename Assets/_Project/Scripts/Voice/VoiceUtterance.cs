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
            // An open intimate gate counts as private even if the origin's flag is stale.
            var privateMode = IntimateExpressions.KeepsLocal(origin.Private);
            var chatId = origin.ChatProviderId;
            var local = !VoiceRoute.UsesCloudTts(privateMode, chatId);
            var model = string.IsNullOrWhiteSpace(origin.Model)
                ? (local ? ChatDefaults.LocalModel : ChatDefaults.Model)
                : origin.Model.Trim();
            var plan = new VoiceUtterance
            {
                Local = local,
                Private = privateMode,
                ProviderLabel = string.Equals(chatId, ChatDefaults.CustomId, StringComparison.OrdinalIgnoreCase)
                    ? "Custom"
                    : (local ? "Local" : "Grok"),
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
                plan.Voice = KokoroVoiceName(voice.kokoroVoice);
                return plan;
            }

            var requested = voice != null ? voice.piperVoice : null;
            var resolved = ResolvePiper(requested, voice != null ? voice.piperExe : null, fileExists);
            plan.PiperPath = resolved;
            plan.Voice = VoiceLabel(resolved, requested);
            return plan;
        }

        /// <summary>
        /// The Player.log line for one synthesis request, named after the engine that will
        /// actually run it. Kokoro and Piper stay on this machine; only xAI is a cloud call.
        /// </summary>
        public static string RequestLine(string engineId, VoiceSettings voice, int chars)
        {
            var id = engineId == null ? string.Empty : engineId.Trim();
            var head = "Voice: TTS request starting (" + chars.ToString() + " chars, engine=";
            if (string.Equals(id, PiperTtsProvider.Id, StringComparison.OrdinalIgnoreCase))
            {
                var piper = voice != null ? voice.piperVoice : string.Empty;
                var label = string.IsNullOrWhiteSpace(piper) ? DefaultPiperVoiceId : Path.GetFileNameWithoutExtension(piper.Trim());
                return head + "Piper, voice=" + label + ", local)";
            }

            if (string.Equals(id, KokoroTtsProvider.Id, StringComparison.OrdinalIgnoreCase))
                return head + "Kokoro, voice=" + KokoroVoiceName(voice != null ? voice.kokoroVoice : null) + ", local)";

            if (string.Equals(id, VoiceRoute.Cloud, StringComparison.OrdinalIgnoreCase))
            {
                var cloud = voice != null && !string.IsNullOrWhiteSpace(voice.voiceId) ? voice.voiceId.Trim() : "eve";
                var rate = TtsRequestJson.NormalizeSampleRate(voice != null ? voice.sampleRate : 24000);
                return head + "xAI, voice=" + cloud + ", pcm, " + rate.ToString() + " Hz, cloud)";
            }

            return head + (id.Length == 0 ? "none" : id) + ")";
        }

        public const string DefaultKokoroVoiceId = "af_heart";

        /// <summary>
        /// Kokoro voice ids look like af_heart. A path pasted into the persona field must not
        /// reach the engine, so anything that is not that shape falls back to the default.
        /// </summary>
        public static string KokoroVoiceName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DefaultKokoroVoiceId;

            var trimmed = value.Trim();
            if (trimmed.Length > 32)
                return DefaultKokoroVoiceId;

            var underscore = trimmed.IndexOf('_');
            if (underscore != 2)
                return DefaultKokoroVoiceId;

            for (var i = 0; i < trimmed.Length; i++)
            {
                var c = trimmed[i];
                var ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                    return DefaultKokoroVoiceId;
            }

            return trimmed;
        }

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
