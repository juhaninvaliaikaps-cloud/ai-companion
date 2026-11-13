using System;
using System.Globalization;
using System.IO;
using System.Text;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Overlay;
using AICompanion.Voice;

namespace AICompanion.Settings
{
    public struct SettingsLegacyPrefs
    {
        public bool HasHideTaskbar;
        public bool HideTaskbar;
        public bool HasScale;
        public float Scale;
        public bool HasMuted;
        public bool Muted;
        public bool HasPaused;
        public bool Paused;

        public static SettingsLegacyPrefs None => default;
    }

    /// <summary>
    /// Per-user configuration. The xAI API key is not a field on purpose.
    /// </summary>
    public sealed class CompanionSettings
    {
        public const int CurrentVersion = 1;
        public const int MaxModelLength = 200;

        public int Version = CurrentVersion;
        public string ProviderId = ChatDefaults.GrokId;
        public string Model = ChatDefaults.Model;

        /// <summary>
        /// OpenAI-compatible endpoint for the cloud provider. ChatDefaults.BaseUrl (xAI) is
        /// the default; point it at DeepSeek, OpenRouter, Groq or any other compatible API.
        /// </summary>
        public string BaseUrl = ChatDefaults.BaseUrl;
        public string LocalModel = ChatDefaults.LocalModel;
        public string LocalBaseUrl = ChatDefaults.LocalBaseUrl;
        public float TimeoutSeconds = ChatDefaults.TimeoutSeconds;
        public string AvatarSource = AvatarSourceNames.Auto;
        public bool CursorLookAt = true;
        public bool TtsEnabled = true;
        public string VoiceId = "eve";
        public string TtsLanguage = "en";

        /// <summary>"xai" for the cloud voice, "piper" for the offline binary.</summary>
        public string TtsProvider = "xai";
        public string PiperExe = string.Empty;
        public string PiperVoice = string.Empty;
        public string KokoroPython = string.Empty;
        public string KokoroScript = string.Empty;
        public string KokoroModel = string.Empty;
        public string KokoroVoices = string.Empty;
        public string KokoroVoice = "af_heart";
        public float Volume = 1f;
        public bool Muted;
        public bool SttEnabled = true;
        public string PushToTalkKey = "V";
        public string WhisperModel = SttSettings.DefaultModelFile;
        public string SttLanguage = "auto";
        public bool AlwaysOnTop = true;
        public float Scale = 1f;
        public string ClickThrough = OverlayClickThroughPolicy.SilhouetteName;
        public int IdleFrameRate = OverlayFrameRate.Idle;
        public bool HideTaskbarButton = true;
        public bool StartWithWindows;
        public bool Paused;
        public bool ChatHistory;
        public bool PrivateMode;

        /// <summary>Manual window capture. Off until the user turns it on.</summary>
        public bool ScreenLookEnabled;

        /// <summary>
        /// Explicit opt-in before a selected-window image is sent to the cloud vision provider.
        /// </summary>
        public bool ScreenCloudConsent;

        /// <summary>
        /// Ollama tag for the vision model. Empty until the user sets one.
        /// The text chat model is a different field and is not assumed to read images.
        /// </summary>
        public string LocalVisionModel = string.Empty;

        /// <summary>True captures a whole display instead of the selected window.</summary>
        public bool ScreenCaptureWholeMonitor;

        /// <summary>
        /// Separate consent for whole-display capture. Window capture never widens into this.
        /// </summary>
        public bool ScreenWholeMonitorConsent;

        /// <summary>Chosen display device name, for example \\.\DISPLAY1. Empty uses the primary display.</summary>
        public string ScreenMonitorDevice = string.Empty;

        /// <summary>Lets her request a look during chat after she has consent. Off by default.</summary>
        public bool ScreenAutoLook;

        /// <summary>Temporary hold on automatic looks. The permission above stays granted.</summary>
        public bool ScreenAutoLookPaused;

        /// <summary>Let the vision model write the screen remark instead of reloading the chat model.</summary>
        public bool ScreenFastReply;

        public string CompanionName = string.Empty;
        public string Relationship = RelationshipStyles.Friend;

        /// <summary>Active persona id. Empty keeps the setup-wizard prompt.</summary>
        public string PersonaId = string.Empty;

        /// <summary>
        /// Optional portrait set. Empty uses %APPDATA%\ProjectLeia\portraits.
        /// A missing folder also falls back to that standard folder.
        /// </summary>
        public string PortraitFolder = string.Empty;
        public bool ShowExpressionPortrait;

        /// <summary>"3d" for the humanoid model, "video" for the looping 2D clip.</summary>
        public string AvatarMode = AvatarModeNames.ThreeD;

        /// <summary>Optional clip folder. Empty uses %APPDATA%\ProjectLeia\video-avatar.</summary>
        public string VideoAvatarFolder = string.Empty;

        /// <summary>Share of the largest size that fits the window, 0.2 to 1.</summary>
        public float VideoAvatarSize = VideoAvatarLayout.DefaultSize;

        /// <summary>Sideways shift as a share of the window width. Negative is left.</summary>
        public float VideoAvatarOffsetX;

        /// <summary>Upward shift as a share of the window height. Negative sinks the clip below the window edge.</summary>
        public float VideoAvatarOffsetY;

        /// <summary>Walking speed of the 2D avatar in clip heights per second. 0 uses the walk clip's own value.</summary>
        public float VideoAvatarWalkSpeed;

        /// <summary>Plays the sound of clips the manifest marks with "sound": true. Off by default.</summary>
        public bool PlayClipSounds;

        /// <summary>Clip sound volume, 0 to 1.</summary>
        public float ClipSoundVolume = VideoClipSound.DefaultVolume;

        /// <summary>Adult drawn expressions from the separate portraits-adult folder. Off by default.</summary>
        public bool IntimateExpressions;

        /// <summary>Shows her current mood and the three strongest moods in a corner (JUH-103 debug). Off by default.</summary>
        public bool ShowMoodDebug;

        /// <summary>JUH-104: the new Happi UI (radial wheel, new chat and settings). Off by default; -newui turns it on for one launch.</summary>
        public bool NewUiPreview;

        /// <summary>JUH-108: speak narration lines too in the new chat. Off by default, so narration is shown but not voiced.</summary>
        public bool VoiceNarration;

        /// <summary>JUH-104: where the new chat box sits, as its centre in 0..1 of the overlay window. -1 = default (bottom centre).</summary>
        public float ChatBoxX = -1f;
        public float ChatBoxY = -1f;

        /// <summary>
        /// JUH-104: the chat box place around her. -1 = default (spot 5 under her with the 2D avatar, else spot 4),
        /// 0 = where the user dropped it (ChatBoxOffsetX/Y), 1..5 = a snap spot. Supersedes
        /// ChatBoxX/ChatBoxY (window fractions), which are still read and written but no longer used.
        /// </summary>
        public int ChatBoxSnap = -1;

        /// <summary>Free placement: the box centre relative to her head, in pixels at 100% scaling.</summary>
        public float ChatBoxOffsetX;
        public float ChatBoxOffsetY;

        /// <summary>
        /// JUH-104: where the 2D expression portrait sits with the new UI. 0 = automatic beside her
        /// head (as on main), 1 = docked on the chat box left side, 2 = docked on its right side,
        /// 3 = where the user dropped it (PortraitOffsetX/Y from her head, pixels at 100%).
        /// </summary>
        public int PortraitDock;
        public float PortraitOffsetX;
        public float PortraitOffsetY;

        /// <summary>Shows the old debug text (topmost, fps, emotion, rotate/size/zoom) top-left. Off by default; the mood readout has its own switch.</summary>
        public bool ShowDebugInfo;

        /// <summary>JUH-108: the words she is saying light up in the new chat. On by default.</summary>
        public bool SpokenGlow = true;

        /// <summary>Strength of the spoken-text glow, 0 (subtle) to 1 (strong).</summary>
        public float SpokenGlowIntensity = 0.6f;

        public static CompanionSettings CreateDefault()
        {
            return new CompanionSettings();
        }

        public void Normalize()
        {
            Version = CurrentVersion;
            ProviderId = string.Equals(ProviderId, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase)
                ? ChatDefaults.LocalId
                : ChatDefaults.GrokId;
            Model = CleanToken(Model, ChatDefaults.Model, MaxModelLength);
            BaseUrl = CleanUrl(BaseUrl, ChatDefaults.BaseUrl);
            LocalModel = CleanToken(LocalModel, ChatDefaults.LocalModel, MaxModelLength);
            LocalVisionModel = CleanToken(LocalVisionModel, string.Empty, MaxModelLength);
            ScreenMonitorDevice = CleanToken(ScreenMonitorDevice, string.Empty, 64);
            LocalBaseUrl = CleanUrl(LocalBaseUrl, ChatDefaults.LocalBaseUrl);
            if (float.IsNaN(TimeoutSeconds) || float.IsInfinity(TimeoutSeconds) || TimeoutSeconds < 1f || TimeoutSeconds > 300f)
                TimeoutSeconds = ChatDefaults.TimeoutSeconds;
            AvatarSource = AvatarSourceNames.Normalize(AvatarSource);
            VoiceId = CleanToken(VoiceId, "eve", 40);
            TtsLanguage = CleanToken(TtsLanguage, "en", 16);
        TtsProvider = CleanToken(TtsProvider, "xai", 16);
        PiperExe = CleanToken(PiperExe, string.Empty, 260);
        PiperVoice = CleanToken(PiperVoice, string.Empty, 260);
        KokoroPython = CleanToken(KokoroPython, string.Empty, 260);
        KokoroScript = CleanToken(KokoroScript, string.Empty, 260);
        KokoroModel = CleanToken(KokoroModel, string.Empty, 260);
        KokoroVoices = CleanToken(KokoroVoices, string.Empty, 260);
        KokoroVoice = CleanToken(KokoroVoice, "af_heart", 32);
            if (float.IsNaN(Volume) || float.IsInfinity(Volume) || Volume < 0f)
                Volume = 0f;
            if (Volume > 1f)
                Volume = 1f;
            PushToTalkKey = PushToTalkKeyName.Normalize(PushToTalkKey);
            WhisperModel = SttSettings.SanitizeModelFile(WhisperModel);
            SttLanguage = CleanToken(string.IsNullOrWhiteSpace(SttLanguage) ? "auto" : SttLanguage, "auto", 16).ToLowerInvariant();
            if (float.IsNaN(Scale) || float.IsInfinity(Scale))
                Scale = 1f;
            if (Scale < OverlayWindowSize.MinScale)
                Scale = OverlayWindowSize.MinScale;
            if (Scale > OverlayWindowSize.MaxScale)
                Scale = OverlayWindowSize.MaxScale;
            ClickThrough = OverlayClickThroughPolicy.Name(OverlayClickThroughPolicy.Parse(ClickThrough));
            if (IdleFrameRate < 1)
                IdleFrameRate = 1;
            if (IdleFrameRate > OverlayFrameRate.Active)
                IdleFrameRate = OverlayFrameRate.Active;
            CompanionName = CompanionPersona.SanitizeName(CompanionName);
            Relationship = RelationshipStyles.Normalize(Relationship);
            PersonaId = PersonaIds.Normalize(PersonaId);
            PortraitFolder = PersonaDefinition.CleanPath(PortraitFolder);
            AvatarMode = AvatarModeNames.Normalize(AvatarMode);
            VideoAvatarFolder = PersonaDefinition.CleanPath(VideoAvatarFolder);
            if (VideoAvatarFolders.LooksRemote(VideoAvatarFolder))
                VideoAvatarFolder = string.Empty;
            VideoAvatarSize = VideoAvatarLayout.ClampSize(VideoAvatarSize);
            VideoAvatarOffsetX = VideoAvatarLayout.ClampOffsetX(VideoAvatarOffsetX);
            VideoAvatarOffsetY = VideoAvatarLayout.ClampOffsetY(VideoAvatarOffsetY);
            if (float.IsNaN(VideoAvatarWalkSpeed) || float.IsInfinity(VideoAvatarWalkSpeed) || VideoAvatarWalkSpeed < 0f)
                VideoAvatarWalkSpeed = 0f;
            VideoAvatarWalkSpeed = Math.Min(VideoAvatarWalkSpeed, VideoAvatarManifest.MaxWalkSpeed);
            ClipSoundVolume = VideoClipSound.ClampVolume(ClipSoundVolume);
            ChatBoxX = CleanFraction(ChatBoxX);
            ChatBoxY = CleanFraction(ChatBoxY);
            if (ChatBoxSnap < -1 || ChatBoxSnap > 5)
                ChatBoxSnap = -1;
            ChatBoxOffsetX = CleanOffset(ChatBoxOffsetX);
            ChatBoxOffsetY = CleanOffset(ChatBoxOffsetY);
            if (PortraitDock < 0 || PortraitDock > 3)
                PortraitDock = 0;
            PortraitOffsetX = CleanOffset(PortraitOffsetX);
            PortraitOffsetY = CleanOffset(PortraitOffsetY);
            if (float.IsNaN(SpokenGlowIntensity) || float.IsInfinity(SpokenGlowIntensity))
                SpokenGlowIntensity = 0.6f;
            SpokenGlowIntensity = Math.Max(0f, Math.Min(1f, SpokenGlowIntensity));
        }

        static float CleanOffset(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;
            return Math.Max(-4000f, Math.Min(4000f, value));
        }

        /// <summary>A 0..1 position, or -1 for "not set".</summary>
        static float CleanFraction(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                return -1f;
            return value > 1f ? 1f : value;
        }

        static string CleanToken(string value, string fallback, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            var trimmed = value.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
            if (trimmed.Length == 0)
                return fallback;
            if (trimmed.Length > max)
                trimmed = trimmed.Substring(0, max);
            return trimmed;
        }

        static string CleanUrl(string value, string fallback)
        {
            var safe = ConnectionProbe.SafeBase(value);
            return safe ?? fallback;
        }
    }

    public static class AvatarSourceNames
    {
        public const string Auto = "auto";
        public const string Local = "local";
        public const string Vrm = "vrm";
        public const string Fbx = "fbx";

        public static string Normalize(string name)
        {
            return From(To(name));
        }

        public static string From(AICompanion.Avatar.AvatarPreference preference)
        {
            switch (preference)
            {
                case AICompanion.Avatar.AvatarPreference.LocalPrefab:
                    return Local;
                case AICompanion.Avatar.AvatarPreference.VrmSample:
                    return Vrm;
                case AICompanion.Avatar.AvatarPreference.FbxSample:
                    return Fbx;
                default:
                    return Auto;
            }
        }

        public static AICompanion.Avatar.AvatarPreference To(string name)
        {
            if (string.Equals(name, Local, StringComparison.OrdinalIgnoreCase))
                return AICompanion.Avatar.AvatarPreference.LocalPrefab;
            if (string.Equals(name, Vrm, StringComparison.OrdinalIgnoreCase))
                return AICompanion.Avatar.AvatarPreference.VrmSample;
            if (string.Equals(name, Fbx, StringComparison.OrdinalIgnoreCase))
                return AICompanion.Avatar.AvatarPreference.FbxSample;
            return AICompanion.Avatar.AvatarPreference.Auto;
        }
    }

    public static class PushToTalkKeyName
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "V";

            var trimmed = name.Trim().ToUpperInvariant();
            if (trimmed.Length == 1 && trimmed[0] >= 'A' && trimmed[0] <= 'Z' && trimmed != "T" && trimmed != "R")
                return trimmed;

            switch (trimmed)
            {
                case "F1":
                case "F2":
                case "F3":
                case "F4":
                case "F5":
                case "F11":
                    return trimmed;
                default:
                    return "V";
            }
        }
    }

    public static class SettingsLocations
    {
        public const string FolderName = "ProjectLeia";
        public const string SettingsFileName = "settings.json";
        public const string HistoryFileName = "chat-history.json";

        public static string Folder(string applicationData, string persistentDataPath)
        {
            if (!string.IsNullOrEmpty(applicationData))
                return Path.Combine(applicationData, FolderName);
            if (!string.IsNullOrEmpty(persistentDataPath))
                return Path.Combine(persistentDataPath, FolderName);
            return null;
        }

        public static string SettingsFile(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            return Path.Combine(folder, SettingsFileName);
        }

        /// <summary>
        /// One transcript per persona, so two characters never share a conversation. An empty
        /// persona keeps the original single file, which is also the setup-name transcript.
        /// </summary>
        public static string HistoryFile(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;

            var id = HistoryId(personaId);
            if (string.IsNullOrEmpty(id))
                return Path.Combine(folder, HistoryFileName);
            return Path.Combine(folder, "chat-history-" + id + ".json");
        }

        public static string HistoryId(string personaId)
        {
            if (string.IsNullOrWhiteSpace(personaId))
                return string.Empty;

            var trimmed = personaId.Trim();
            var sb = new System.Text.StringBuilder(trimmed.Length);
            for (var i = 0; i < trimmed.Length && sb.Length < 40; i++)
            {
                var c = char.ToLowerInvariant(trimmed[i]);
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                        sb.Append('-');
                    continue;
                }

                var ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok)
                    continue;
                sb.Append(c);
            }

            return sb.ToString();
        }

        public static string HistoryFile(string folder)
        {
            return HistoryFile(folder, string.Empty);
        }

        public static string PersonasFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            return Path.Combine(folder, "personas");
        }
    }

    public static class ApiKeyMask
    {
        public const string SavedLabel = "Key saved";
        public const string MissingLabel = "No key saved";

        public static string Label(bool saved)
        {
            return saved ? SavedLabel : MissingLabel;
        }

        public static string Mask(string secret)
        {
            if (string.IsNullOrEmpty(secret))
                return string.Empty;
            return "••••••••";
        }
    }

    public static class CompanionSettingsSecrets
    {
        public static bool ContainsForbiddenSecretField(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            return HasKey(json, "apiKey")
                || HasKey(json, "xaiApiKey")
                || HasKey(json, "api_key")
                || HasKey(json, "authorization")
                || HasKey(json, "accessToken")
                || HasKey(json, "secret");
        }

        static bool HasKey(string json, string field)
        {
            var token = "\"" + field + "\"";
            var i = 0;
            while (i < json.Length)
            {
                var at = json.IndexOf(token, i, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                    return false;
                var cursor = Skip(json, at + token.Length);
                if (cursor < json.Length && json[cursor] == ':')
                    return true;
                i = at + token.Length;
            }

            return false;
        }

        static int Skip(string json, int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
            return index;
        }
    }

    public static class CompanionSettingsJson
    {
        public static CompanionSettings Parse(string json, SettingsLegacyPrefs legacy)
        {
            if (string.IsNullOrWhiteSpace(json))
                return FromLegacy(legacy);

            var settings = CompanionSettings.CreateDefault();
            string text;
            if (TryString(json, "providerId", out text) || TryString(json, "provider", out text))
                settings.ProviderId = text;
            if (TryString(json, "model", out text))
                settings.Model = text;
            if (TryString(json, "baseUrl", out text))
                settings.BaseUrl = text;
            if (TryString(json, "localModel", out text))
                settings.LocalModel = text;
            if (TryString(json, "localBaseUrl", out text))
                settings.LocalBaseUrl = text;
            double number;
            if (TryNumber(json, "timeoutSeconds", out number))
                settings.TimeoutSeconds = (float)number;
            if (TryString(json, "avatarSource", out text))
                settings.AvatarSource = text;
            bool flag;
            if (TryBool(json, "cursorLookAt", out flag))
                settings.CursorLookAt = flag;
            if (TryBool(json, "ttsEnabled", out flag))
                settings.TtsEnabled = flag;
            if (TryString(json, "voiceId", out text))
                settings.VoiceId = text;
            if (TryString(json, "ttsLanguage", out text))
                settings.TtsLanguage = text;
            if (TryString(json, "ttsProvider", out text))
                settings.TtsProvider = text;
            if (TryString(json, "piperExe", out text))
                settings.PiperExe = text;
            if (TryString(json, "piperVoice", out text))
                settings.PiperVoice = text;
            if (TryString(json, "kokoroPython", out text))
                settings.KokoroPython = text;
            if (TryString(json, "kokoroScript", out text))
                settings.KokoroScript = text;
            if (TryString(json, "kokoroModel", out text))
                settings.KokoroModel = text;
            if (TryString(json, "kokoroVoices", out text))
                settings.KokoroVoices = text;
            if (TryString(json, "kokoroVoice", out text))
                settings.KokoroVoice = text;
            if (TryNumber(json, "volume", out number))
                settings.Volume = (float)number;
            if (TryBool(json, "muted", out flag))
                settings.Muted = flag;
            if (TryBool(json, "sttEnabled", out flag))
                settings.SttEnabled = flag;
            if (TryString(json, "pushToTalkKey", out text))
                settings.PushToTalkKey = text;
            if (TryString(json, "whisperModel", out text))
                settings.WhisperModel = text;
            if (TryString(json, "sttLanguage", out text))
                settings.SttLanguage = text;
            if (TryBool(json, "alwaysOnTop", out flag))
                settings.AlwaysOnTop = flag;
            if (TryNumber(json, "scale", out number) || TryNumber(json, "windowScale", out number))
                settings.Scale = (float)number;
            if (TryString(json, "clickThrough", out text))
                settings.ClickThrough = text;
            if (TryNumber(json, "idleFrameRate", out number))
                settings.IdleFrameRate = (int)number;
            if (TryBool(json, "hideTaskbarButton", out flag) || TryBool(json, "hideTaskbar", out flag))
                settings.HideTaskbarButton = flag;
            if (TryBool(json, "startWithWindows", out flag))
                settings.StartWithWindows = flag;
            if (TryBool(json, "paused", out flag))
                settings.Paused = flag;
            if (TryBool(json, "chatHistory", out flag))
                settings.ChatHistory = flag;
            if (TryBool(json, "privateMode", out flag))
                settings.PrivateMode = flag;
            if (TryBool(json, "screenLookEnabled", out flag))
                settings.ScreenLookEnabled = flag;
            if (TryBool(json, "screenCloudConsent", out flag))
                settings.ScreenCloudConsent = flag;
            if (TryString(json, "localVisionModel", out text))
                settings.LocalVisionModel = text;
            if (TryBool(json, "screenWholeMonitorConsent", out flag))
                settings.ScreenWholeMonitorConsent = flag;
            if (TryBool(json, "screenCaptureWholeMonitor", out flag))
                settings.ScreenCaptureWholeMonitor = flag;
            if (TryString(json, "screenMonitorDevice", out text))
                settings.ScreenMonitorDevice = text;
            if (TryBool(json, "screenAutoLook", out flag))
                settings.ScreenAutoLook = flag;
            if (TryBool(json, "screenAutoLookPaused", out flag))
                settings.ScreenAutoLookPaused = flag;
            if (TryBool(json, "screenFastReply", out flag))
                settings.ScreenFastReply = flag;
            if (TryString(json, "companionName", out text))
                settings.CompanionName = text;
            if (TryString(json, "relationship", out text))
                settings.Relationship = text;
            if (TryString(json, "personaId", out text))
                settings.PersonaId = text;
            if (TryString(json, "portraitFolder", out text))
                settings.PortraitFolder = text;
            if (TryBool(json, "showExpressionPortrait", out flag))
                settings.ShowExpressionPortrait = flag;
            if (TryString(json, "avatarMode", out text))
                settings.AvatarMode = text;
            if (TryString(json, "videoAvatarFolder", out text))
                settings.VideoAvatarFolder = text;
            if (TryNumber(json, "videoAvatarSize", out number))
                settings.VideoAvatarSize = (float)number;
            if (TryNumber(json, "videoAvatarOffsetX", out number))
                settings.VideoAvatarOffsetX = (float)number;
            if (TryNumber(json, "videoAvatarOffsetY", out number))
                settings.VideoAvatarOffsetY = (float)number;
            if (TryNumber(json, "videoAvatarWalkSpeed", out number))
                settings.VideoAvatarWalkSpeed = (float)number;
            if (TryBool(json, "playClipSounds", out flag))
                settings.PlayClipSounds = flag;
            if (TryNumber(json, "clipSoundVolume", out number))
                settings.ClipSoundVolume = (float)number;
            if (TryBool(json, "intimateExpressions", out flag))
                settings.IntimateExpressions = flag;
            if (TryBool(json, "showMoodDebug", out flag))
                settings.ShowMoodDebug = flag;
            if (TryBool(json, "newUiPreview", out flag))
                settings.NewUiPreview = flag;
            if (TryBool(json, "voiceNarration", out flag))
                settings.VoiceNarration = flag;
            if (TryNumber(json, "chatBoxX", out number))
                settings.ChatBoxX = (float)number;
            if (TryNumber(json, "chatBoxY", out number))
                settings.ChatBoxY = (float)number;
            if (TryBool(json, "showDebugInfo", out flag))
                settings.ShowDebugInfo = flag;
            if (TryBool(json, "spokenGlow", out flag))
                settings.SpokenGlow = flag;
            if (TryNumber(json, "spokenGlowIntensity", out number))
                settings.SpokenGlowIntensity = (float)number;
            if (TryNumber(json, "chatBoxSnap", out number))
                settings.ChatBoxSnap = (int)Math.Round(number);
            if (TryNumber(json, "chatBoxOffsetX", out number))
                settings.ChatBoxOffsetX = (float)number;
            if (TryNumber(json, "chatBoxOffsetY", out number))
                settings.ChatBoxOffsetY = (float)number;
            if (TryNumber(json, "portraitDock", out number))
                settings.PortraitDock = (int)Math.Round(number);
            if (TryNumber(json, "portraitOffsetX", out number))
                settings.PortraitOffsetX = (float)number;
            if (TryNumber(json, "portraitOffsetY", out number))
                settings.PortraitOffsetY = (float)number;
            settings.Normalize();
            return settings;
        }

        public static string Write(CompanionSettings settings)
        {
            var source = settings ?? CompanionSettings.CreateDefault();
            source.Normalize();
            var sb = new StringBuilder();
            sb.Append("{\n");
            Number(sb, "version", source.Version, true);
            String(sb, "providerId", source.ProviderId);
            String(sb, "model", source.Model);
            String(sb, "baseUrl", source.BaseUrl);
            String(sb, "localModel", source.LocalModel);
            String(sb, "localBaseUrl", source.LocalBaseUrl);
            Number(sb, "timeoutSeconds", source.TimeoutSeconds, false);
            String(sb, "avatarSource", source.AvatarSource);
            Bool(sb, "cursorLookAt", source.CursorLookAt);
            Bool(sb, "ttsEnabled", source.TtsEnabled);
            String(sb, "voiceId", source.VoiceId);
            String(sb, "ttsLanguage", source.TtsLanguage);
            String(sb, "ttsProvider", source.TtsProvider);
            String(sb, "piperExe", source.PiperExe);
            String(sb, "piperVoice", source.PiperVoice);
            String(sb, "kokoroPython", source.KokoroPython);
            String(sb, "kokoroScript", source.KokoroScript);
            String(sb, "kokoroModel", source.KokoroModel);
            String(sb, "kokoroVoices", source.KokoroVoices);
            String(sb, "kokoroVoice", source.KokoroVoice);
            Number(sb, "volume", source.Volume, false);
            Bool(sb, "muted", source.Muted);
            Bool(sb, "sttEnabled", source.SttEnabled);
            String(sb, "pushToTalkKey", source.PushToTalkKey);
            String(sb, "whisperModel", source.WhisperModel);
            String(sb, "sttLanguage", source.SttLanguage);
            Bool(sb, "alwaysOnTop", source.AlwaysOnTop);
            Number(sb, "scale", source.Scale, false);
            String(sb, "clickThrough", source.ClickThrough);
            Number(sb, "idleFrameRate", source.IdleFrameRate, true);
            Bool(sb, "hideTaskbarButton", source.HideTaskbarButton);
            Bool(sb, "startWithWindows", source.StartWithWindows);
            Bool(sb, "paused", source.Paused);
            Bool(sb, "chatHistory", source.ChatHistory);
            Bool(sb, "privateMode", source.PrivateMode);
            Bool(sb, "screenLookEnabled", source.ScreenLookEnabled);
            Bool(sb, "screenCloudConsent", source.ScreenCloudConsent);
            String(sb, "localVisionModel", source.LocalVisionModel);
            Bool(sb, "screenWholeMonitorConsent", source.ScreenWholeMonitorConsent);
            Bool(sb, "screenCaptureWholeMonitor", source.ScreenCaptureWholeMonitor);
            String(sb, "screenMonitorDevice", source.ScreenMonitorDevice);
            Bool(sb, "screenAutoLook", source.ScreenAutoLook);
            Bool(sb, "screenAutoLookPaused", source.ScreenAutoLookPaused);
            Bool(sb, "screenFastReply", source.ScreenFastReply);
            String(sb, "companionName", source.CompanionName);
            String(sb, "relationship", source.Relationship);
            String(sb, "personaId", source.PersonaId);
            String(sb, "portraitFolder", source.PortraitFolder);
            Bool(sb, "showExpressionPortrait", source.ShowExpressionPortrait);
            String(sb, "avatarMode", source.AvatarMode);
            String(sb, "videoAvatarFolder", source.VideoAvatarFolder);
            Number(sb, "videoAvatarSize", source.VideoAvatarSize, false);
            Number(sb, "videoAvatarOffsetX", source.VideoAvatarOffsetX, false);
            Number(sb, "videoAvatarOffsetY", source.VideoAvatarOffsetY, false);
            Number(sb, "videoAvatarWalkSpeed", source.VideoAvatarWalkSpeed, false);
            Bool(sb, "playClipSounds", source.PlayClipSounds);
            Number(sb, "clipSoundVolume", source.ClipSoundVolume, false);
            Bool(sb, "intimateExpressions", source.IntimateExpressions);
            Bool(sb, "showMoodDebug", source.ShowMoodDebug);
            Bool(sb, "newUiPreview", source.NewUiPreview);
            Bool(sb, "voiceNarration", source.VoiceNarration);
            Number(sb, "chatBoxX", source.ChatBoxX, false);
            Number(sb, "chatBoxY", source.ChatBoxY, false);
            Bool(sb, "showDebugInfo", source.ShowDebugInfo);
            Bool(sb, "spokenGlow", source.SpokenGlow);
            Number(sb, "spokenGlowIntensity", source.SpokenGlowIntensity, false);
            Number(sb, "chatBoxSnap", source.ChatBoxSnap, true);
            Number(sb, "chatBoxOffsetX", source.ChatBoxOffsetX, false);
            Number(sb, "chatBoxOffsetY", source.ChatBoxOffsetY, false);
            Number(sb, "portraitDock", source.PortraitDock, true);
            Number(sb, "portraitOffsetX", source.PortraitOffsetX, false);
            Number(sb, "portraitOffsetY", source.PortraitOffsetY, false);
            if (sb.Length >= 2 && sb[sb.Length - 2] == ',')
            {
                sb.Length -= 2;
                sb.Append('\n');
            }

            sb.Append("}\n");
            return sb.ToString();
        }

        static CompanionSettings FromLegacy(SettingsLegacyPrefs legacy)
        {
            var settings = CompanionSettings.CreateDefault();
            if (legacy.HasHideTaskbar)
                settings.HideTaskbarButton = legacy.HideTaskbar;
            if (legacy.HasScale)
                settings.Scale = legacy.Scale;
            if (legacy.HasMuted)
                settings.Muted = legacy.Muted;
            if (legacy.HasPaused)
                settings.Paused = legacy.Paused;
            settings.Normalize();
            return settings;
        }

        static void String(StringBuilder sb, string field, string value)
        {
            sb.Append("  \"");
            sb.Append(field);
            sb.Append("\": ");
            JsonText.AppendString(sb, value ?? string.Empty);
            sb.Append(",\n");
        }

        static void Bool(StringBuilder sb, string field, bool value)
        {
            sb.Append("  \"");
            sb.Append(field);
            sb.Append("\": ");
            sb.Append(value ? "true" : "false");
            sb.Append(",\n");
        }

        static void Number(StringBuilder sb, string field, float value, bool integer)
        {
            sb.Append("  \"");
            sb.Append(field);
            sb.Append("\": ");
            if (integer)
                sb.Append(((int)value).ToString(CultureInfo.InvariantCulture));
            else
                sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(",\n");
        }

        static bool TryString(string json, string field, out string value)
        {
            return JsonText.TryReadStringField(json, field, out value);
        }

        static bool TryBool(string json, string field, out bool value)
        {
            if (JsonText.TryReadBoolField(json, field, out value))
                return true;

            double number;
            if (!TryNumber(json, field, out number))
            {
                value = false;
                return false;
            }

            value = Math.Abs(number) > double.Epsilon;
            return true;
        }

        static bool TryNumber(string json, string field, out double value)
        {
            value = 0d;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field))
                return false;

            var key = "\"" + field + "\"";
            var i = 0;
            while (i < json.Length)
            {
                var at = json.IndexOf(key, i, StringComparison.Ordinal);
                if (at < 0)
                    return false;
                if (at > 0)
                {
                    var prev = json[at - 1];
                    if (char.IsLetterOrDigit(prev) || prev == '_')
                    {
                        i = at + key.Length;
                        continue;
                    }
                }

                var cursor = Skip(json, at + key.Length);
                if (cursor >= json.Length || json[cursor] != ':')
                {
                    i = at + key.Length;
                    continue;
                }

                cursor = Skip(json, cursor + 1);
                var end = cursor;
                if (end < json.Length && (json[end] == '+' || json[end] == '-'))
                    end++;
                var start = end;
                while (end < json.Length && "0123456789.eE".IndexOf(json[end]) >= 0)
                    end++;
                if (end == start)
                {
                    i = cursor + 1;
                    continue;
                }

                var token = json.Substring(cursor, end - cursor);
                return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }

            return false;
        }

        static int Skip(string json, int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
            return index;
        }
    }

    public static class CompanionSettingsStore
    {
        public static CompanionSettings Load(string path, SettingsLegacyPrefs legacy)
        {
            var read = ProtectedLocalFile.Read(path);
            if (read.Status != LocalReadStatus.Ready)
                return CompanionSettingsJson.Parse(null, legacy);

            try
            {
                return CompanionSettingsJson.Parse(read.Text, SettingsLegacyPrefs.None);
            }
            catch (Exception)
            {
                ProtectedLocalFile.Quarantine(path);
                return CompanionSettingsJson.Parse(null, legacy);
            }
        }

        public static void Save(string path, CompanionSettings settings)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A settings path is required.", "path");

            var json = CompanionSettingsJson.Write(settings);
            if (CompanionSettingsSecrets.ContainsForbiddenSecretField(json))
                throw new InvalidOperationException("Settings cannot store an API key.");

            if (!ProtectedLocalFile.Write(path, json))
                throw new IOException("Settings were not written.");
        }
    }
}
