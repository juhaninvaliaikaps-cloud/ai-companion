using System;
using System.Globalization;
using System.IO;
using System.Text;
using AICompanion.AI;
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

        public int Version = CurrentVersion;
        public string ProviderId = ChatDefaults.GrokId;
        public string Model = ChatDefaults.Model;
        public string LocalModel = ChatDefaults.LocalModel;
        public string LocalBaseUrl = ChatDefaults.LocalBaseUrl;
        public float TimeoutSeconds = ChatDefaults.TimeoutSeconds;
        public string AvatarSource = AvatarSourceNames.Auto;
        public bool TtsEnabled = true;
        public string VoiceId = "eve";
        public string TtsLanguage = "en";
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
        public string CompanionName = string.Empty;
        public string Relationship = RelationshipStyles.Friend;
        public bool ShowExpressionPortrait;

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
            Model = CleanToken(Model, ChatDefaults.Model, 80);
            LocalModel = CleanToken(LocalModel, ChatDefaults.LocalModel, 80);
            LocalBaseUrl = CleanUrl(LocalBaseUrl, ChatDefaults.LocalBaseUrl);
            if (float.IsNaN(TimeoutSeconds) || float.IsInfinity(TimeoutSeconds) || TimeoutSeconds < 1f || TimeoutSeconds > 300f)
                TimeoutSeconds = ChatDefaults.TimeoutSeconds;
            AvatarSource = AvatarSourceNames.Normalize(AvatarSource);
            VoiceId = CleanToken(VoiceId, "eve", 40);
            TtsLanguage = CleanToken(TtsLanguage, "en", 16);
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

        public static string HistoryFile(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            return Path.Combine(folder, HistoryFileName);
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
            if (TryBool(json, "ttsEnabled", out flag))
                settings.TtsEnabled = flag;
            if (TryString(json, "voiceId", out text))
                settings.VoiceId = text;
            if (TryString(json, "ttsLanguage", out text))
                settings.TtsLanguage = text;
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
            if (TryString(json, "companionName", out text))
                settings.CompanionName = text;
            if (TryString(json, "relationship", out text))
                settings.Relationship = text;
            if (TryBool(json, "showExpressionPortrait", out flag))
                settings.ShowExpressionPortrait = flag;
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
            String(sb, "localModel", source.LocalModel);
            String(sb, "localBaseUrl", source.LocalBaseUrl);
            Number(sb, "timeoutSeconds", source.TimeoutSeconds, false);
            String(sb, "avatarSource", source.AvatarSource);
            Bool(sb, "ttsEnabled", source.TtsEnabled);
            String(sb, "voiceId", source.VoiceId);
            String(sb, "ttsLanguage", source.TtsLanguage);
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
            String(sb, "companionName", source.CompanionName);
            String(sb, "relationship", source.Relationship);
            Bool(sb, "showExpressionPortrait", source.ShowExpressionPortrait);
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
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static CompanionSettings Load(string path, SettingsLegacyPrefs legacy)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return CompanionSettingsJson.Parse(null, legacy);

            try
            {
                return CompanionSettingsJson.Parse(File.ReadAllText(path, Utf8), SettingsLegacyPrefs.None);
            }
            catch (Exception)
            {
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

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, json, Utf8);
        }
    }
}
