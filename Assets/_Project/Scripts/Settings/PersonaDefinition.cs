using System;
using System.Globalization;
using System.Text;
using AICompanion.AI;

namespace AICompanion.Settings
{
    /// <summary>
    /// One persona file. Empty voice, model, and avatar fields mean "keep the current
    /// setting". An empty portrait folder means the standard portraits folder.
    /// </summary>
    public sealed class PersonaDefinition
    {
        public const int MaxPersonalityLength = 4000;
        public const int MaxSpeakingStyleLength = 600;
        public const int MaxPiperVoiceIdLength = 120;
        public const int MaxKokoroVoiceIdLength = 64;
        public const int MaxCloudVoiceIdLength = 40;
        public const int MaxPathLength = 260;

        public int Version = 1;
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public string Personality = string.Empty;
        public string SpeakingStyle = string.Empty;
        public string PiperVoiceId = string.Empty;

        /// <summary>Kokoro voice name, for example af_heart. Blank keeps the current one.</summary>
        public string KokoroVoiceId = string.Empty;
        public string CloudVoiceId = string.Empty;
        public string ProviderId = string.Empty;
        public string Model = string.Empty;
        public string AvatarSource = string.Empty;
        public string PortraitFolder = string.Empty;

        public static PersonaDefinition CreateStarter(string id)
        {
            return new PersonaDefinition
            {
                Version = 1,
                Id = id ?? string.Empty,
                DisplayName = "Leia",
                Personality = "You are Leia, a calm desktop companion sitting beside the user. Reply in one or two short sentences. Stay kind, light, and appropriate for any audience.",
                SpeakingStyle = "Quiet and brief."
            };
        }

        public PersonaDefinition Copy()
        {
            return new PersonaDefinition
            {
                Version = Version,
                Id = Id,
                DisplayName = DisplayName,
                Personality = Personality,
                SpeakingStyle = SpeakingStyle,
                PiperVoiceId = PiperVoiceId,
                KokoroVoiceId = KokoroVoiceId,
                CloudVoiceId = CloudVoiceId,
                ProviderId = ProviderId,
                Model = Model,
                AvatarSource = AvatarSource,
                PortraitFolder = PortraitFolder
            };
        }

        public bool Normalize()
        {
            if (Version <= 0)
                Version = 1;
            Id = PersonaIds.Normalize(Id);
            DisplayName = CompanionPersona.SanitizeName(DisplayName);
            Personality = CleanProse(Personality, MaxPersonalityLength);
            SpeakingStyle = CleanProse(SpeakingStyle, MaxSpeakingStyleLength);
            PiperVoiceId = CleanToken(PiperVoiceId, MaxPiperVoiceIdLength);
            KokoroVoiceId = CleanToken(KokoroVoiceId, MaxKokoroVoiceIdLength);
            CloudVoiceId = CleanToken(CloudVoiceId, MaxCloudVoiceIdLength);
            string provider;
            ProviderId = PersonaProviders.TryNormalize(ProviderId, out provider) ? provider : string.Empty;
            Model = CleanToken(Model, CompanionSettings.MaxModelLength);
            string avatar;
            AvatarSource = PersonaAvatars.TryNormalize(AvatarSource, out avatar) ? avatar : string.Empty;
            PortraitFolder = CleanPath(PortraitFolder);
            return Id.Length > 0;
        }

        static string CleanProse(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var trimmed = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (CompanionPersona.LooksLikeSecret(trimmed))
                return string.Empty;
            if (trimmed.Length > max)
                trimmed = trimmed.Substring(0, max);
            return trimmed;
        }

        static string CleanToken(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var trimmed = value.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
            if (trimmed.Length == 0 || CompanionPersona.LooksLikeSecret(trimmed))
                return string.Empty;
            if (trimmed.Length > max)
                trimmed = trimmed.Substring(0, max);
            return trimmed;
        }

        public static string CleanPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var trimmed = value.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
            if (trimmed.Length == 0 || CompanionPersona.LooksLikeSecret(trimmed))
                return string.Empty;
            if (trimmed.Length > MaxPathLength)
                trimmed = trimmed.Substring(0, MaxPathLength);
            return trimmed;
        }
    }

    public static class PersonaIds
    {
        public const int MaxLength = 40;

        public static string Normalize(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            var raw = id.Trim().ToLowerInvariant();
            var builder = new StringBuilder(raw.Length);
            for (var i = 0; i < raw.Length && builder.Length < MaxLength; i++)
            {
                var c = raw[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
                {
                    builder.Append(c);
                }
                else if (char.IsWhiteSpace(c) && builder.Length > 0 && builder[builder.Length - 1] != '-')
                {
                    builder.Append('-');
                }
            }

            while (builder.Length > 0 && builder[builder.Length - 1] == '-')
                builder.Length--;

            var start = 0;
            while (start < builder.Length && builder[start] == '-')
                start++;
            if (start > 0)
                builder.Remove(0, start);
            return builder.ToString();
        }

        public static string Unique(string seed, Func<string, bool> taken)
        {
            var baseId = Normalize(seed);
            if (baseId.Length == 0)
                baseId = "persona";
            if (taken == null || !taken(baseId))
                return baseId;

            for (var n = 2; n < 1000; n++)
            {
                var suffix = "-" + n.ToString(CultureInfo.InvariantCulture);
                var room = MaxLength - suffix.Length;
                if (room < 1)
                    room = 1;
                var stem = baseId.Length > room ? baseId.Substring(0, room).Trim('-') : baseId;
                if (stem.Length == 0)
                    stem = "persona";
                var id = stem + suffix;
                if (id.Length > MaxLength)
                    id = id.Substring(0, MaxLength).Trim('-');
                if (!taken(id))
                    return id;
            }

            return baseId;
        }
    }

    public static class PersonaProviders
    {
        public static bool TryNormalize(string value, out string providerId)
        {
            providerId = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var token = value.Trim();
            if (string.Equals(token, ChatDefaults.GrokId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "xai", StringComparison.OrdinalIgnoreCase))
            {
                providerId = ChatDefaults.GrokId;
                return true;
            }

            if (string.Equals(token, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                providerId = ChatDefaults.LocalId;
                return true;
            }

            return false;
        }
    }

    public static class PersonaAvatars
    {
        public static bool TryNormalize(string value, out string source)
        {
            source = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var token = value.Trim();
            if (string.Equals(token, AvatarSourceNames.Auto, StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, AvatarSourceNames.Local, StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, AvatarSourceNames.Vrm, StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, AvatarSourceNames.Fbx, StringComparison.OrdinalIgnoreCase))
            {
                source = token.ToLowerInvariant();
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Personality and speaking style, without the director suffix. An empty personality
    /// falls back to the setup-wizard prompt.
    /// </summary>
    public static class PersonaText
    {
        public const string StyleLabel = "Speaking style: ";

        public static string Build(PersonaDefinition persona, string fallbackName, string fallbackRelationship)
        {
            var personality = persona == null ? string.Empty : persona.Personality;
            if (string.IsNullOrWhiteSpace(personality))
            {
                var name = persona != null && !string.IsNullOrWhiteSpace(persona.DisplayName)
                    ? persona.DisplayName
                    : fallbackName;
                personality = CompanionPersona.Build(name, fallbackRelationship);
            }

            var style = persona == null ? string.Empty : persona.SpeakingStyle;
            if (string.IsNullOrWhiteSpace(style))
                return personality.Trim();

            return personality.TrimEnd() + "\n\n" + StyleLabel + style.Trim();
        }
    }

    public static class PersonaJson
    {
        public static PersonaDefinition Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            if (json[0] == '\uFEFF')
                json = json.Substring(1);

            var persona = new PersonaDefinition();
            int version;
            if (TryInt(json, "version", out version))
                persona.Version = version;

            string text;
            if (TryString(json, "id", out text))
                persona.Id = text;
            if (TryString(json, "displayName", out text))
                persona.DisplayName = text;
            if (TryString(json, "personality", out text) || TryString(json, "systemPrompt", out text))
                persona.Personality = text;
            if (TryString(json, "speakingStyle", out text))
                persona.SpeakingStyle = text;
            if (TryString(json, "piperVoiceId", out text))
                persona.PiperVoiceId = text;
            if (TryString(json, "kokoroVoiceId", out text))
                persona.KokoroVoiceId = text;
            if (TryString(json, "cloudVoiceId", out text) || TryString(json, "voiceId", out text))
                persona.CloudVoiceId = text;
            if (TryString(json, "providerId", out text) || TryString(json, "provider", out text))
                persona.ProviderId = text;
            if (TryString(json, "model", out text))
                persona.Model = text;
            if (TryString(json, "avatarSource", out text) || TryString(json, "avatar", out text))
                persona.AvatarSource = text;
            if (TryString(json, "portraitFolder", out text))
                persona.PortraitFolder = text;

            if (!persona.Normalize())
                return null;
            return persona;
        }

        public static string Write(PersonaDefinition persona)
        {
            var source = persona == null ? new PersonaDefinition() : persona.Copy();
            source.Normalize();
            var sb = new StringBuilder();
            sb.Append("{\n");
            Number(sb, "version", source.Version);
            String(sb, "id", source.Id);
            String(sb, "displayName", source.DisplayName);
            String(sb, "personality", source.Personality);
            String(sb, "speakingStyle", source.SpeakingStyle);
            String(sb, "piperVoiceId", source.PiperVoiceId);
            String(sb, "kokoroVoiceId", source.KokoroVoiceId);
            String(sb, "cloudVoiceId", source.CloudVoiceId);
            String(sb, "providerId", source.ProviderId);
            String(sb, "model", source.Model);
            String(sb, "avatarSource", source.AvatarSource);
            String(sb, "portraitFolder", source.PortraitFolder);
            if (sb.Length >= 2 && sb[sb.Length - 2] == ',')
            {
                sb.Length -= 2;
                sb.Append('\n');
            }

            sb.Append("}\n");
            return sb.ToString();
        }

        static void String(StringBuilder sb, string field, string value)
        {
            sb.Append("  \"");
            sb.Append(field);
            sb.Append("\": ");
            JsonText.AppendString(sb, value ?? string.Empty);
            sb.Append(",\n");
        }

        static void Number(StringBuilder sb, string field, int value)
        {
            sb.Append("  \"");
            sb.Append(field);
            sb.Append("\": ");
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\n");
        }

        static bool TryString(string json, string field, out string value)
        {
            return JsonText.TryReadStringField(json, field, out value);
        }

        static bool TryInt(string json, string field, out int value)
        {
            value = 0;
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
                while (end < json.Length && char.IsDigit(json[end]))
                    end++;
                if (end == cursor)
                    return false;
                return int.TryParse(json.Substring(cursor, end - cursor), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
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
}
