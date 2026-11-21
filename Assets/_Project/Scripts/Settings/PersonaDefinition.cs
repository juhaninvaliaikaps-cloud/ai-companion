using System;
using System.Globalization;
using System.Text;
using AICompanion.AI;
using AICompanion.Core;

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
        public const int MaxChatterboxVoiceIdLength = 64;
        public const int MaxCloudVoiceIdLength = 40;
        public const int MaxPathLength = 260;

        // JUH-150 (Persona Studio).
        public const int MaxTaglineLength = 120;
        public const int MaxDescriptionLength = 32000;
        public const int MaxGreetingLength = 1000;
        public const int MaxExampleLinesLength = 4000;
        public const int MaxAdultNotesLength = 8000;

        // JUH-155.
        public const int MaxScenarioLength = 4000;

        public int Version = 1;
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public string Personality = string.Empty;
        public string SpeakingStyle = string.Empty;
        public string PiperVoiceId = string.Empty;

        /// <summary>Kokoro voice name, for example af_heart. Blank keeps the current one.</summary>
        public string KokoroVoiceId = string.Empty;
        public string ChatterboxVoiceId = string.Empty;
        public string CloudVoiceId = string.Empty;
        public string ProviderId = string.Empty;
        public string Model = string.Empty;
        public string AvatarSource = string.Empty;
        public string PortraitFolder = string.Empty;

        /// <summary>
        /// Temperament preset id (JUH-103 phase 3), such as "tsundere". Blank is the default
        /// (balanced) temperament when <see cref="Axes"/> is null. See <see cref="PersonaTemperaments"/>.
        /// </summary>
        public string Temperament = string.Empty;

        /// <summary>
        /// "auto" (the character sheet decides, JUH-118), "manual" (the preset or the sliders), or
        /// blank for files from before this key: see <see cref="TemperamentModes.IsManual"/>.
        /// </summary>
        public string TemperamentMode = string.Empty;

        /// <summary>
        /// Nine sliders for a custom profile (JUH-136). Null on a preset and on files written
        /// before the sliders existed: those load the preset's point.
        /// </summary>
        public TemperamentAxes Axes;

        /// <summary>JUH-150: one line under her name in the persona list.</summary>
        public string Tagline = string.Empty;

        /// <summary>
        /// JUH-150: the full description as written or imported, up to 32,000 characters.
        /// <see cref="Personality"/> stays the text the chat model sees; the Studio fills it from
        /// this (whole when it fits, condensed by Pisara when it does not). Empty on old files.
        /// </summary>
        public string Description = string.Empty;

        /// <summary>JUH-150: her first message in a new chat. Optional.</summary>
        public string Greeting = string.Empty;

        /// <summary>JUH-150: a few sample exchanges that show how she talks.</summary>
        public string ExampleLines = string.Empty;

        /// <summary>
        /// JUH-150: 18+ notes. Kept apart from the description so they never reach a cloud
        /// model: they are added to the prompt only while the 18+ gate is open, and the gate
        /// keeps chat local (<see cref="AdultNotesNow"/>).
        /// </summary>
        public string AdultNotes = string.Empty;

        /// <summary>
        /// JUH-155: the user's own scenario: her world, what's going on, where they are and how
        /// they met. Empty lets Pisara make those up. It is where the story starts; the scene
        /// tracker (JUH-129) follows where the chat goes from there. 18+ details belong in
        /// <see cref="AdultNotes"/>, which never reach a cloud model.
        /// </summary>
        public string Scenario = string.Empty;

        /// <summary>
        /// JUH-151: true when <see cref="Personality"/> is Pisara's condensed version of a
        /// description too long for the chat model. Editing the description clears it.
        /// </summary>
        public bool Condensed;

        /// <summary>JUH-150: true when she has a picture (stored encrypted beside her file).</summary>
        public bool HasPicture;

        /// <summary>JUH-150: "round" or "square", how her picture is shown.</summary>
        public string PictureShape = PersonaPictureShapes.Round;

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
                ChatterboxVoiceId = ChatterboxVoiceId,
                CloudVoiceId = CloudVoiceId,
                ProviderId = ProviderId,
                Model = Model,
                AvatarSource = AvatarSource,
                PortraitFolder = PortraitFolder,
                Temperament = Temperament,
                TemperamentMode = TemperamentMode,
                Axes = Axes == null ? null : Axes.Clone(),
                Tagline = Tagline,
                Description = Description,
                Greeting = Greeting,
                ExampleLines = ExampleLines,
                AdultNotes = AdultNotes,
                Scenario = Scenario,
                HasPicture = HasPicture,
                PictureShape = PictureShape,
                Condensed = Condensed
            };
        }

        /// <summary>
        /// Saves a custom profile, or the named preset when the sliders land on one. Landing on a
        /// preset clears the numbers so the file stays a preset id.
        /// </summary>
        public void SetAxes(TemperamentAxes axes)
        {
            if (axes == null)
            {
                Axes = null;
                return;
            }

            var copy = axes.Clone();
            var match = TemperamentAxes.MatchingPresetId(copy);
            if (match != null)
            {
                Temperament = PersonaTemperaments.Normalize(match);
                Axes = null;
                return;
            }

            Temperament = string.Empty;
            Axes = copy;
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
            ChatterboxVoiceId = CleanToken(ChatterboxVoiceId, MaxChatterboxVoiceIdLength);
            CloudVoiceId = CleanToken(CloudVoiceId, MaxCloudVoiceIdLength);
            string provider;
            ProviderId = PersonaProviders.TryNormalize(ProviderId, out provider) ? provider : string.Empty;
            Model = CleanToken(Model, CompanionSettings.MaxModelLength);
            string avatar;
            AvatarSource = PersonaAvatars.TryNormalize(AvatarSource, out avatar) ? avatar : string.Empty;
            PortraitFolder = CleanPath(PortraitFolder);
            Temperament = PersonaTemperaments.Normalize(Temperament);
            TemperamentMode = TemperamentModes.Normalize(TemperamentMode);
            if (Axes != null)
                Axes.Clamp();
            Tagline = CleanToken(Tagline, MaxTaglineLength);
            Description = CleanProse(Description, MaxDescriptionLength);
            Greeting = CleanProse(Greeting, MaxGreetingLength);
            ExampleLines = CleanProse(ExampleLines, MaxExampleLinesLength);
            AdultNotes = CleanProse(AdultNotes, MaxAdultNotesLength);
            Scenario = CleanProse(Scenario, MaxScenarioLength);
            PictureShape = PersonaPictureShapes.Normalize(PictureShape);
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
        public const string ScenarioLabel = CharacterSheetPrompt.ScenarioHeading;
        public const string ExamplesLabel = "Examples of how you talk (follow the voice, not the exact words):";

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

            var text = personality.Trim();
            var style = persona == null ? string.Empty : persona.SpeakingStyle;
            if (!string.IsNullOrWhiteSpace(style))
                text = text + "\n\n" + StyleLabel + style.Trim();

            // JUH-155: the user's scenario, where the story starts.
            var scenario = persona == null ? string.Empty : persona.Scenario;
            if (!string.IsNullOrWhiteSpace(scenario))
                text = text + "\n\n" + ScenarioLabel + "\n" + scenario.Trim();

            // JUH-150: sample lines help a small model keep her voice.
            var examples = persona == null ? string.Empty : persona.ExampleLines;
            if (!string.IsNullOrWhiteSpace(examples))
                text = text + "\n\n" + ExamplesLabel + "\n" + examples.Trim();
            return text;
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
            if (TryString(json, "chatterboxVoiceId", out text))
                persona.ChatterboxVoiceId = text;
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
            if (TryString(json, "temperament", out text))
                persona.Temperament = text;
            if (TryString(json, "temperamentMode", out text))
                persona.TemperamentMode = text;
            TemperamentAxes axes;
            if (TryAxes(json, out axes))
                persona.Axes = axes;
            if (TryString(json, "tagline", out text))
                persona.Tagline = text;
            if (TryString(json, "description", out text))
                persona.Description = text;
            if (TryString(json, "greeting", out text))
                persona.Greeting = text;
            if (TryString(json, "exampleLines", out text))
                persona.ExampleLines = text;
            if (TryString(json, "adultNotes", out text))
                persona.AdultNotes = text;
            if (TryString(json, "scenario", out text))
                persona.Scenario = text;
            bool flag;
            if (TryBool(json, "hasPicture", out flag))
                persona.HasPicture = flag;
            if (TryBool(json, "condensed", out flag))
                persona.Condensed = flag;
            if (TryString(json, "pictureShape", out text))
                persona.PictureShape = text;

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
            String(sb, "chatterboxVoiceId", source.ChatterboxVoiceId);
            String(sb, "cloudVoiceId", source.CloudVoiceId);
            String(sb, "providerId", source.ProviderId);
            String(sb, "model", source.Model);
            String(sb, "avatarSource", source.AvatarSource);
            String(sb, "portraitFolder", source.PortraitFolder);
            String(sb, "temperament", source.Temperament);
            String(sb, "temperamentMode", source.TemperamentMode);
            if (source.Axes != null)
                Axes(sb, source.Axes);
            String(sb, "tagline", source.Tagline);
            String(sb, "description", source.Description);
            String(sb, "greeting", source.Greeting);
            String(sb, "exampleLines", source.ExampleLines);
            String(sb, "adultNotes", source.AdultNotes);
            String(sb, "scenario", source.Scenario);
            sb.Append("  \"hasPicture\": ").Append(source.HasPicture ? "true" : "false").Append(",\n");
            String(sb, "pictureShape", source.PictureShape);
            sb.Append("  \"condensed\": ").Append(source.Condensed ? "true" : "false").Append(",\n");
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

        static void Axes(StringBuilder sb, TemperamentAxes axes)
        {
            sb.Append("  \"temperamentAxes\": {");
            Axis(sb, "warmth", axes.Value(TemperamentAxes.Warmth), true);
            Axis(sb, "volatility", axes.Value(TemperamentAxes.Volatility), false);
            Axis(sb, "assertiveness", axes.Value(TemperamentAxes.Assertiveness), false);
            Axis(sb, "playfulness", axes.Value(TemperamentAxes.Playfulness), false);
            Axis(sb, "openness", axes.Value(TemperamentAxes.Openness), false);
            Axis(sb, "energy", axes.Value(TemperamentAxes.Energy), false);
            Axis(sb, "directness", axes.Value(TemperamentAxes.Directness), false);
            Axis(sb, "confidence", axes.Value(TemperamentAxes.Confidence), false);
            Axis(sb, "curiosity", axes.Value(TemperamentAxes.Curiosity), false);
            sb.Append(" }\n");
        }

        static void Axis(StringBuilder sb, string name, int value, bool first)
        {
            if (!first)
                sb.Append(',');
            sb.Append(" \"");
            sb.Append(name);
            sb.Append("\": ");
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        static bool TryAxes(string json, out TemperamentAxes axes)
        {
            axes = null;
            if (string.IsNullOrEmpty(json))
                return false;
            var key = "\"temperamentAxes\"";
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
                if (cursor >= json.Length || json[cursor] != '{')
                    return false;
                var end = json.IndexOf('}', cursor);
                if (end < 0)
                    return false;
                var body = json.Substring(cursor, end - cursor + 1);
                var parsed = TemperamentAxes.Neutral();
                parsed.Set(TemperamentAxes.Warmth, AxisNumber(body, "warmth"));
                parsed.Set(TemperamentAxes.Volatility, AxisNumber(body, "volatility"));
                parsed.Set(TemperamentAxes.Assertiveness, AxisNumber(body, "assertiveness"));
                parsed.Set(TemperamentAxes.Playfulness, AxisNumber(body, "playfulness"));
                parsed.Set(TemperamentAxes.Openness, AxisNumber(body, "openness"));
                parsed.Set(TemperamentAxes.Energy, AxisNumber(body, "energy"));
                parsed.Set(TemperamentAxes.Directness, AxisNumber(body, "directness"));
                parsed.Set(TemperamentAxes.Confidence, AxisNumber(body, "confidence"));
                parsed.Set(TemperamentAxes.Curiosity, AxisNumber(body, "curiosity"));
                axes = parsed;
                return true;
            }

            return false;
        }

        static float AxisNumber(string body, string name)
        {
            var key = "\"" + name + "\"";
            var at = body.IndexOf(key, StringComparison.Ordinal);
            if (at < 0)
                return TemperamentAxes.Middle;
            var cursor = Skip(body, at + key.Length);
            if (cursor >= body.Length || body[cursor] != ':')
                return TemperamentAxes.Middle;
            cursor = Skip(body, cursor + 1);
            var end = cursor;
            if (end < body.Length && (body[end] == '-' || body[end] == '+'))
                end++;
            var dot = false;
            while (end < body.Length && (char.IsDigit(body[end]) || (body[end] == '.' && !dot)))
            {
                if (body[end] == '.')
                    dot = true;
                end++;
            }

            if (end == cursor)
                return TemperamentAxes.Middle;
            double number;
            if (!double.TryParse(body.Substring(cursor, end - cursor), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                || double.IsNaN(number))
                return TemperamentAxes.Middle;
            return (float)number;
        }

        static bool TryString(string json, string field, out string value)
        {
            return JsonText.TryReadStringField(json, field, out value);
        }

        static bool TryBool(string json, string field, out bool value)
        {
            return JsonText.TryReadBoolField(json, field, out value);
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

    public static class PersonaPictureShapes
    {
        public const string Round = "round";
        public const string Square = "square";

        public static string Normalize(string shape)
        {
            return string.Equals((shape ?? string.Empty).Trim(), Square, StringComparison.OrdinalIgnoreCase) ? Square : Round;
        }
    }

    /// <summary>
    /// JUH-150: from the full description to the text the chat model sees. The local model
    /// has a small context, so at most <see cref="ModelBudget"/> characters go in as they are;
    /// a longer description is cut at a paragraph or sentence end until Pisara condenses it.
    /// </summary>
    public static class PersonaDescription
    {
        public const int ModelBudget = PersonaDefinition.MaxPersonalityLength;

        public static bool Fits(string description)
        {
            return (description ?? string.Empty).Trim().Length <= ModelBudget;
        }

        public static string ModelText(string description)
        {
            var text = (description ?? string.Empty).Replace("\r\n", "\n").Trim();
            if (text.Length <= ModelBudget)
                return text;
            var cut = text.LastIndexOf("\n\n", ModelBudget, StringComparison.Ordinal);
            if (cut < ModelBudget / 2)
                cut = Math.Max(text.LastIndexOf(". ", ModelBudget - 1, StringComparison.Ordinal) + 1, 0);
            if (cut < ModelBudget / 2)
                cut = ModelBudget;
            return text.Substring(0, cut).TrimEnd();
        }

        /// <summary>
        /// JUH-151: sets the chat text from the description before a save. A description that
        /// fits is used whole; a long one keeps Pisara's condensed text when there is one,
        /// otherwise it is cut. A persona with no description keeps its personality.
        /// </summary>
        public static void ApplyTo(PersonaDefinition persona)
        {
            if (persona == null || string.IsNullOrWhiteSpace(persona.Description))
                return;
            if (Fits(persona.Description) || !persona.Condensed || string.IsNullOrWhiteSpace(persona.Personality))
            {
                persona.Personality = ModelText(persona.Description);
                persona.Condensed = false;
                return;
            }

            if (persona.Personality.Length > ModelBudget)
                persona.Personality = ModelText(persona.Personality);
        }

        /// <summary>The heading a folded speaking style gets at the end of her description.</summary>
        public const string StyleHeading = "How she talks: ";

        /// <summary>
        /// JUH-155: the Studio has no Speaking style box. An old speaking style moves to the end
        /// of her description, so nothing is lost. True when something moved; the caller saves.
        /// </summary>
        public static bool FoldStyle(PersonaDefinition persona)
        {
            if (persona == null || string.IsNullOrWhiteSpace(persona.SpeakingStyle))
                return false;
            var style = persona.SpeakingStyle.Trim();
            var description = (persona.Description ?? string.Empty).Trim();
            if (description.IndexOf(style, StringComparison.Ordinal) < 0)
            {
                var added = StyleHeading + style;
                description = description.Length == 0 ? added : description + "\n\n" + added;
                if (description.Length > PersonaDefinition.MaxDescriptionLength)
                    description = description.Substring(0, PersonaDefinition.MaxDescriptionLength);
            }

            persona.Description = description;

            // A condensed persona keeps Pisara's condensed text: the style is added to it too.
            if (persona.Condensed)
            {
                var chat = (persona.Personality ?? string.Empty).Trim();
                if (chat.IndexOf(style, StringComparison.Ordinal) < 0)
                {
                    var withStyle = chat.Length == 0 ? StyleHeading + style : chat + "\n\n" + StyleHeading + style;
                    if (withStyle.Length <= ModelBudget)
                        persona.Personality = withStyle;
                }
            }

            persona.SpeakingStyle = string.Empty;
            return true;
        }

        /// <summary>"1,234 / 32,000 characters" and what the local model sees.</summary>
        public static string Meter(string description)
        {
            var length = (description ?? string.Empty).Trim().Length;
            var count = length.ToString("N0", CultureInfo.InvariantCulture) + " / "
                        + PersonaDefinition.MaxDescriptionLength.ToString("N0", CultureInfo.InvariantCulture) + " characters.";
            if (length == 0)
                return count;
            return Fits(description)
                ? count + " The chat model sees all of it."
                : count + " The chat model sees the first " + ModelText(description).Length.ToString("N0", CultureInfo.InvariantCulture)
                  + ". Pisara reads all of it and condenses the rest into her character sheet.";
        }
    }

    /// <summary>
    /// JUH-150: her picture, a PNG kept encrypted beside her persona file as
    /// <c>&lt;id&gt;.picture</c> (base64 inside <see cref="ProtectedLocalFile"/>), so a private
    /// picture is as safe as her description.
    /// </summary>
    public static class PersonaPictureStore
    {
        public const int MaxBytes = 4 * 1024 * 1024;

        /// <summary>JUH-154: goes up whenever a picture is saved or deleted, so cached textures refresh.</summary>
        public static int Revision { get; private set; }

        public static string PathFor(string personasFolder, string id)
        {
            var clean = PersonaIds.Normalize(id);
            if (string.IsNullOrEmpty(personasFolder) || clean.Length == 0)
                return null;
            return System.IO.Path.Combine(personasFolder, clean + ".picture");
        }

        public static bool Save(string personasFolder, string id, byte[] png)
        {
            var path = PathFor(personasFolder, id);
            if (path == null || png == null || png.Length == 0 || png.Length > MaxBytes)
                return false;
            try
            {
                System.IO.Directory.CreateDirectory(personasFolder);
                var saved = ProtectedLocalFile.Write(path, Convert.ToBase64String(png));
                if (saved)
                    Revision++;
                return saved;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static byte[] Load(string personasFolder, string id)
        {
            var path = PathFor(personasFolder, id);
            if (path == null || !System.IO.File.Exists(path))
                return null;
            try
            {
                var read = ProtectedLocalFile.Read(path);
                if (read.Status != LocalReadStatus.Ready || string.IsNullOrEmpty(read.Text))
                    return null;
                return Convert.FromBase64String(read.Text.Trim());
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Delete(string personasFolder, string id)
        {
            var path = PathFor(personasFolder, id);
            try
            {
                if (path != null && System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    Revision++;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>JUH-150: starting points for a new persona, one per temperament preset.</summary>
    public static class PersonaTemplates
    {
        public static readonly string[] Ids =
        {
            PersonaTemperaments.CalmId, PersonaTemperaments.CheerfulId, PersonaTemperaments.ShyId,
            PersonaTemperaments.TsundereId, PersonaTemperaments.MoodyId,
        };

        public static PersonaDefinition Create(string templateId, string id)
        {
            var temperament = PersonaTemperaments.Normalize(templateId);
            var persona = PersonaDefinition.CreateStarter(id);
            persona.DisplayName = string.Empty;
            persona.Temperament = temperament;
            persona.TemperamentMode = TemperamentModes.Manual;
            // JUH-155: how she talks is part of the description.
            persona.Description = Text(temperament) + "\n\n" + PersonaDescription.StyleHeading + Style(temperament);
            persona.Personality = PersonaDescription.ModelText(persona.Description);
            persona.SpeakingStyle = string.Empty;
            persona.Tagline = string.Empty;
            return persona;
        }

        public static string Name(string templateId)
        {
            var preset = PersonaTemperaments.Get(PersonaTemperaments.Normalize(templateId));
            return preset != null ? preset.Name : templateId;
        }

        static string Text(string id)
        {
            switch (id)
            {
                case PersonaTemperaments.CheerfulId:
                    return "You are a bright, warm companion who lives on the user's desktop. You get excited about small things, cheer the user on, and love to joke around. You notice when they're down and try to lift them up.";
                case PersonaTemperaments.ShyId:
                    return "You are a gentle, shy companion who lives on the user's desktop. You get flustered easily and speak softly, but you care a lot and slowly open up as you get to know the user.";
                case PersonaTemperaments.TsundereId:
                    return "You are a proud, sharp-tongued companion who lives on the user's desktop. You care about the user a lot but would never admit it; your worry comes out as scolding and your praise comes out grudging.";
                case PersonaTemperaments.MoodyId:
                    return "You are a moody, dry-humoured companion who lives on the user's desktop. Your mood swings with the day; you're hard to impress, but when someone earns your respect you stay loyal.";
                default:
                    return "You are a calm, thoughtful companion who lives on the user's desktop. You listen more than you talk, give steady advice, and keep a quiet sense of humour.";
            }
        }

        static string Style(string id)
        {
            switch (id)
            {
                case PersonaTemperaments.CheerfulId: return "Lively and warm, with exclamation marks.";
                case PersonaTemperaments.ShyId: return "Soft, hesitant, sometimes stammering.";
                case PersonaTemperaments.TsundereId: return "Short, prickly, embarrassed when caught caring.";
                case PersonaTemperaments.MoodyId: return "Dry, clipped, sarcastic.";
                default: return "Quiet and brief.";
            }
        }
    }
}
