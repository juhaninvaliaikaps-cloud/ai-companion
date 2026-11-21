using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>What a character card import gave: a draft persona, her picture and notes for the user.</summary>
    public sealed class CardImport
    {
        /// <summary>The draft, without an id. Null when the file could not be read (see <see cref="Error"/>).</summary>
        public PersonaDefinition Persona;

        /// <summary>The card's own PNG when it came as a picture; null for a .json card.</summary>
        public byte[] PicturePng;

        /// <summary>"chara_card_v2", "chara_card_v3" or "v1".</summary>
        public string Spec = string.Empty;

        /// <summary>True when the card is tagged 18+ and its scenario and personality went to the 18+ notes.</summary>
        public bool Adult;

        /// <summary>Things the user should know: fields that were left out, trimmed or moved.</summary>
        public readonly List<string> Notes = new List<string>();

        public string Error = string.Empty;

        public bool Ok
        {
            get { return Persona != null && Error.Length == 0; }
        }
    }

    /// <summary>The export formats for a persona.</summary>
    public enum CardExportFormat
    {
        /// <summary>A V2 character card: her picture as a PNG with a <c>chara</c> text chunk.</summary>
        CardPng = 0,

        /// <summary>The same card as plain V2 JSON.</summary>
        Json = 1,

        /// <summary>A zip with her persona file (Happi's own format) and her picture.</summary>
        Zip = 2,
    }

    /// <summary>
    /// JUH-153: SillyTavern / Chub character cards, in and out. Plain C#, no scene:
    /// <list type="bullet">
    /// <item>Reads V2 (<c>spec: chara_card_v2</c>, a PNG <c>tEXt</c> chunk named <c>chara</c>
    /// holding base64 JSON) and V3 (<c>chara_card_v3</c>, chunk <c>ccv3</c>), from a PNG or a
    /// .json file, and the older flat V1 JSON. The PNG chunks are read here, and no length in the
    /// file is trusted.</item>
    /// <item>Maps a card onto a persona draft within <see cref="PersonaDefinition"/>'s limits.
    /// A card tagged 18+ puts its scenario and personality into the 18+ notes, which never reach
    /// a cloud model.</item>
    /// <item>Writes a persona as a V2 card PNG (her picture or a plain placeholder), as V2 JSON,
    /// or as a zip of her persona file and picture. The 18+ notes are left out unless asked for.</item>
    /// </list>
    /// </summary>
    public static class CharacterCards
    {
        public const string SpecV2 = "chara_card_v2";
        public const string SpecV3 = "chara_card_v3";
        public const string KeywordV2 = "chara";
        public const string KeywordV3 = "ccv3";

        /// <summary>A card file larger than this is refused.</summary>
        public const int MaxFileBytes = 20 * 1024 * 1024;

        /// <summary>A single PNG chunk larger than this is refused (a card's text is far smaller).</summary>
        public const int MaxChunkBytes = 8 * 1024 * 1024;

        public const string IgnoredNote = "Creator notes, the system prompt and post-history instructions in the card were not imported: Happi writes its own instructions.";
        public const string AdultNote = "This card is tagged 18+. Its scenario and personality went into her 18+ notes, which stay on this PC and are used only while the 18+ gate is open.";

        static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        static readonly string[] AdultTags =
        {
            "nsfw", "nsfl", "adult", "18+", "r18", "r-18", "explicit", "lewd", "smut", "erotic", "erotica", "porn", "hentai", "sexual",
        };

        // ---- The card JSON ---------------------------------------------------------------------

        [Serializable]
        sealed class CardExtensions
        {
        }

        [Serializable]
        sealed class CardData
        {
            public string name = string.Empty;
            public string description = string.Empty;
            public string personality = string.Empty;
            public string scenario = string.Empty;
            public string first_mes = string.Empty;
            public string mes_example = string.Empty;
            public string creator_notes = string.Empty;
            public string system_prompt = string.Empty;
            public string post_history_instructions = string.Empty;
            public string[] alternate_greetings = new string[0];
            public string[] tags = new string[0];
            public string creator = string.Empty;
            public string character_version = string.Empty;
            public CardExtensions extensions = new CardExtensions();
        }

        [Serializable]
        sealed class CardFile
        {
            public string spec = string.Empty;
            public string spec_version = string.Empty;
            public CardData data;
        }

        // ---- Import ------------------------------------------------------------------------------

        /// <summary>
        /// Reads a card from a file's bytes: a PNG with a <c>chara</c> or <c>ccv3</c> chunk, or JSON.
        /// Never throws; a bad file gives <see cref="CardImport.Error"/>.
        /// </summary>
        public static CardImport Import(byte[] bytes)
        {
            var result = new CardImport();
            if (bytes == null || bytes.Length == 0)
            {
                result.Error = "That file is empty.";
                return result;
            }

            if (bytes.Length > MaxFileBytes)
            {
                result.Error = "That file is larger than 20 MB.";
                return result;
            }

            string json;
            if (IsPng(bytes))
            {
                string error;
                if (!TryReadCardText(bytes, out json, out error))
                {
                    result.Error = error;
                    return result;
                }

                result.PicturePng = bytes;
            }
            else
            {
                json = DecodeText(bytes);
            }

            return FromJson(json, result);
        }

        static CardImport FromJson(string json, CardImport result)
        {
            CardData data;
            string spec;
            if (!TryParseCard(json, out data, out spec))
            {
                result.Error = "That isn't a character card Happi can read (no card JSON found).";
                return result;
            }

            result.Spec = spec;
            result.Persona = Map(data, result);
            return result;
        }

        /// <summary>Parses V2/V3 (fields under <c>data</c>) or flat V1 JSON.</summary>
        static bool TryParseCard(string json, out CardData data, out string spec)
        {
            data = null;
            spec = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            var text = json.Trim();
            if (text.Length > 0 && text[0] == '﻿')
                text = text.Substring(1);
            if (!text.StartsWith("{", StringComparison.Ordinal))
                return false;
            try
            {
                var file = JsonUtility.FromJson<CardFile>(text);
                if (file != null && file.data != null && !string.IsNullOrWhiteSpace(file.data.name + file.data.description + file.data.first_mes))
                {
                    data = file.data;
                    spec = string.IsNullOrEmpty(file.spec) ? SpecV2 : file.spec.Trim();
                    return true;
                }

                var flat = JsonUtility.FromJson<CardData>(text);
                if (flat != null && !string.IsNullOrWhiteSpace(flat.name + flat.description + flat.first_mes))
                {
                    data = flat;
                    spec = "v1";
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        static PersonaDefinition Map(CardData data, CardImport result)
        {
            var persona = PersonaDefinition.CreateStarter(string.Empty);
            persona.Personality = string.Empty;
            persona.SpeakingStyle = string.Empty;
            persona.TemperamentMode = AICompanion.Core.TemperamentModes.Auto;

            var name = Clean(data.name);
            persona.DisplayName = CompanionPersona.SanitizeName(name);
            var her = string.IsNullOrWhiteSpace(persona.DisplayName) ? "She" : persona.DisplayName;

            result.Adult = IsAdult(data.tags);
            var description = ReplaceNames(Clean(data.description), her, "the user");
            var personality = ReplaceNames(Clean(data.personality), her, "the user");
            var scenario = ReplaceNames(Clean(data.scenario), her, "the user");

            var parts = new StringBuilder();
            Paragraph(parts, "Description", description);
            string adult = string.Empty;
            if (result.Adult)
            {
                var notes = new StringBuilder();
                Paragraph(notes, "Personality", personality);
                Paragraph(notes, "Scenario", scenario);
                adult = notes.ToString();
                if (adult.Length > 0)
                    result.Notes.Add(AdultNote);
            }
            else
            {
                Paragraph(parts, "Personality", personality);
                // JUH-155: the card's scenario has its own box.
                persona.Scenario = Limit(scenario, PersonaDefinition.MaxScenarioLength, "Her scenario", result);
            }

            persona.Description = Limit(parts.ToString(), PersonaDefinition.MaxDescriptionLength, "Her description", result);
            persona.AdultNotes = Limit(adult, PersonaDefinition.MaxAdultNotesLength, "Her 18+ notes", result);
            persona.Greeting = Limit(ReplaceNames(Clean(data.first_mes), her, "you"), PersonaDefinition.MaxGreetingLength, "Her greeting", result);
            persona.ExampleLines = Limit(ExampleLines(data.mes_example), PersonaDefinition.MaxExampleLinesLength, "Her example lines", result);

            if (!string.IsNullOrWhiteSpace(data.creator_notes) || !string.IsNullOrWhiteSpace(data.system_prompt) || !string.IsNullOrWhiteSpace(data.post_history_instructions))
                result.Notes.Add(IgnoredNote);
            if (data.alternate_greetings != null && data.alternate_greetings.Length > 0)
                result.Notes.Add("Only her first greeting was imported; the card has " + data.alternate_greetings.Length + " more.");
            return persona;
        }

        static void Paragraph(StringBuilder sb, string label, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            if (sb.Length > 0)
                sb.Append("\n\n");
            sb.Append(label).Append(":\n").Append(text.Trim());
        }

        static string Limit(string text, int max, string what, CardImport result)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length <= max)
                return text;
            result.Notes.Add(what + " was longer than " + max.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " characters and was cut.");
            return text.Substring(0, max).TrimEnd();
        }

        static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        /// <summary>True when one of the card's tags marks it as 18+ ("nsfw", "adult", "18+"…).</summary>
        public static bool IsAdult(IList<string> tags)
        {
            if (tags == null)
                return false;
            for (var i = 0; i < tags.Count; i++)
            {
                var tag = (tags[i] ?? string.Empty).Trim().ToLowerInvariant();
                for (var k = 0; k < AdultTags.Length; k++)
                {
                    if (tag == AdultTags[k])
                        return true;
                }
            }

            return false;
        }

        /// <summary>{{char}} and &lt;BOT&gt; become her name, {{user}} and &lt;USER&gt; the given word.</summary>
        public static string ReplaceNames(string text, string her, string user)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return ReplaceIgnoreCase(ReplaceIgnoreCase(ReplaceIgnoreCase(ReplaceIgnoreCase(text, "{{char}}", her), "<BOT>", her), "{{user}}", user), "<USER>", user);
        }

        /// <summary>
        /// The card's example messages in Happi's style: {{char}} and &lt;BOT&gt; become "Her",
        /// {{user}} and &lt;USER&gt; become "You", and the &lt;START&gt; separators go.
        /// </summary>
        public static string ExampleLines(string mesExample)
        {
            var text = ReplaceNames(Clean(mesExample), "Her", "You");
            text = ReplaceIgnoreCase(text, "<START>", string.Empty);
            var lines = text.Split('\n');
            var sb = new StringBuilder();
            var blank = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    blank = sb.Length > 0;
                    continue;
                }

                if (sb.Length > 0)
                    sb.Append(blank ? "\n\n" : "\n");
                sb.Append(line);
                blank = false;
            }

            return sb.ToString();
        }

        static string ReplaceIgnoreCase(string text, string find, string replace)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;
            var sb = new StringBuilder(text.Length);
            var at = 0;
            while (true)
            {
                var next = text.IndexOf(find, at, StringComparison.OrdinalIgnoreCase);
                if (next < 0)
                    break;
                sb.Append(text, at, next - at).Append(replace);
                at = next + find.Length;
            }

            sb.Append(text, at, text.Length - at);
            return sb.ToString();
        }

        // ---- PNG chunks ------------------------------------------------------------------------

        public static bool IsPng(byte[] bytes)
        {
            if (bytes == null || bytes.Length < PngSignature.Length)
                return false;
            for (var i = 0; i < PngSignature.Length; i++)
            {
                if (bytes[i] != PngSignature[i])
                    return false;
            }

            return true;
        }

        /// <summary>One chunk: its type and where its data is in the file.</summary>
        public struct PngChunk
        {
            public string Type;
            public int DataOffset;
            public int Length;
        }

        /// <summary>
        /// The chunks of a PNG, in order, up to IEND. Every length is checked against what is left
        /// of the file and against <see cref="MaxChunkBytes"/>; a bad one stops with an error.
        /// </summary>
        public static bool TryReadChunks(byte[] png, List<PngChunk> chunks, out string error)
        {
            error = string.Empty;
            if (!IsPng(png))
            {
                error = "That file isn't a PNG.";
                return false;
            }

            var at = PngSignature.Length;
            while (true)
            {
                if (png.Length - at < 12)
                {
                    error = "That PNG ends too early.";
                    return false;
                }

                var length = ReadUInt32(png, at);
                if (length > (uint)MaxChunkBytes || length > (uint)(png.Length - at - 12))
                {
                    error = "That PNG has a damaged or oversized chunk.";
                    return false;
                }

                var type = Encoding.ASCII.GetString(png, at + 4, 4);
                chunks.Add(new PngChunk { Type = type, DataOffset = at + 8, Length = (int)length });
                at += 12 + (int)length;
                if (type == "IEND")
                    return true;
            }
        }

        /// <summary>The card JSON in a PNG: the <c>ccv3</c> chunk when there is one, else <c>chara</c>.</summary>
        public static bool TryReadCardText(byte[] png, out string json, out string error)
        {
            json = null;
            var chunks = new List<PngChunk>();
            if (!TryReadChunks(png, chunks, out error))
                return false;

            string v2 = null;
            string v3 = null;
            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                if (chunk.Type != "tEXt")
                    continue;
                var zero = Array.IndexOf(png, (byte)0, chunk.DataOffset, chunk.Length);
                if (zero < 0)
                    continue;
                var keyword = Encoding.ASCII.GetString(png, chunk.DataOffset, zero - chunk.DataOffset);
                var text = Encoding.ASCII.GetString(png, zero + 1, chunk.DataOffset + chunk.Length - zero - 1);
                if (keyword == KeywordV3)
                    v3 = text;
                else if (keyword == KeywordV2)
                    v2 = text;
            }

            var base64 = v3 ?? v2;
            if (base64 == null)
            {
                error = "That PNG has no character card in it.";
                return false;
            }

            try
            {
                json = Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));
                return true;
            }
            catch (FormatException)
            {
                error = "The card text in that PNG is damaged.";
                return false;
            }
        }

        /// <summary>
        /// The PNG with one <c>tEXt</c> chunk holding the card (keyword <paramref name="keyword"/>)
        /// before IEND; earlier <c>chara</c> and <c>ccv3</c> chunks are dropped. Null when the PNG
        /// can't be read.
        /// </summary>
        public static byte[] WithCardChunk(byte[] png, string keyword, string json)
        {
            var chunks = new List<PngChunk>();
            string error;
            if (!TryReadChunks(png, chunks, out error))
                return null;

            var payload = Encoding.ASCII.GetBytes(keyword + "\0" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json ?? string.Empty)));
            using (var output = new MemoryStream(png.Length + payload.Length + 64))
            {
                output.Write(PngSignature, 0, PngSignature.Length);
                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    if (chunk.Type == "IEND")
                        WriteChunk(output, "tEXt", payload, 0, payload.Length);
                    if (chunk.Type == "tEXt" && IsCardChunk(png, chunk))
                        continue;
                    output.Write(png, chunk.DataOffset - 8, chunk.Length + 12);
                }

                return output.ToArray();
            }
        }

        static bool IsCardChunk(byte[] png, PngChunk chunk)
        {
            var zero = Array.IndexOf(png, (byte)0, chunk.DataOffset, chunk.Length);
            if (zero < 0)
                return false;
            var keyword = Encoding.ASCII.GetString(png, chunk.DataOffset, zero - chunk.DataOffset);
            return keyword == KeywordV2 || keyword == KeywordV3;
        }

        static void WriteChunk(Stream output, string type, byte[] data, int offset, int length)
        {
            WriteUInt32(output, (uint)length);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes, 0, 4);
            output.Write(data, offset, length);
            var crc = Crc32.Update(Crc32.Update(0xFFFFFFFFu, typeBytes, 0, 4), data, offset, length) ^ 0xFFFFFFFFu;
            WriteUInt32(output, crc);
        }

        static uint ReadUInt32(byte[] bytes, int at)
        {
            return ((uint)bytes[at] << 24) | ((uint)bytes[at + 1] << 16) | ((uint)bytes[at + 2] << 8) | bytes[at + 3];
        }

        static void WriteUInt32(Stream output, uint value)
        {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        static string DecodeText(byte[] bytes)
        {
            try
            {
                return new UTF8Encoding(false, false).GetString(bytes);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // ---- Export ------------------------------------------------------------------------------

        /// <summary>
        /// The persona as V2 card JSON. Her description, speaking style, scenario, greeting and
        /// example lines go in; {{char}} and {{user}} mark the speakers in the examples. The 18+
        /// notes are added after the scenario, tagged nsfw, only when <paramref name="includeAdult"/> is true.
        /// </summary>
        public static string ToCardJson(PersonaDefinition persona, bool includeAdult)
        {
            var source = persona ?? new PersonaDefinition();
            var description = string.IsNullOrWhiteSpace(source.Description) ? source.Personality : source.Description;
            var adult = includeAdult && !string.IsNullOrWhiteSpace(source.AdultNotes);
            var data = new CardData
            {
                name = source.DisplayName ?? string.Empty,
                description = (description ?? string.Empty).Trim(),
                personality = (source.SpeakingStyle ?? string.Empty).Trim(),
                scenario = adult ? JoinScenario(source.Scenario, source.AdultNotes) : (source.Scenario ?? string.Empty).Trim(),
                first_mes = (source.Greeting ?? string.Empty).Trim(),
                mes_example = ToCardExamples(source.ExampleLines),
                creator_notes = string.IsNullOrWhiteSpace(source.Tagline) ? "Exported from Happi." : source.Tagline.Trim(),
                tags = adult ? new[] { "nsfw" } : new string[0],
                creator = string.Empty,
                character_version = string.Empty,
            };
            var file = new CardFile { spec = SpecV2, spec_version = "2.0", data = data };
            return JsonUtility.ToJson(file, true);
        }

        /// <summary>JUH-155: an 18+ export carries her scenario and then her 18+ notes.</summary>
        static string JoinScenario(string scenario, string adultNotes)
        {
            var a = (scenario ?? string.Empty).Trim();
            var b = (adultNotes ?? string.Empty).Trim();
            if (a.Length == 0)
                return b;
            return b.Length == 0 ? a : a + "\n\n" + b;
        }

        /// <summary>Happi's "You:" and "Her:" example lines as card examples, after a &lt;START&gt;.</summary>
        public static string ToCardExamples(string exampleLines)
        {
            var text = Clean(exampleLines);
            if (text.Length == 0)
                return string.Empty;
            var lines = text.Split('\n');
            var sb = new StringBuilder("<START>");
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd();
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("Her:", StringComparison.OrdinalIgnoreCase))
                    line = "{{char}}:" + trimmed.Substring(4);
                else if (trimmed.StartsWith("You:", StringComparison.OrdinalIgnoreCase))
                    line = "{{user}}:" + trimmed.Substring(4);
                sb.Append('\n').Append(line);
            }

            return sb.ToString();
        }

        /// <summary>A V2 card PNG: her picture (or a plain placeholder) with the card in a <c>chara</c> chunk.</summary>
        public static byte[] ToCardPng(PersonaDefinition persona, byte[] picturePng, bool includeAdult)
        {
            var picture = IsPng(picturePng) ? picturePng : PlaceholderPng(256, 0x3B, 0x6F, 0xB6);
            return WithCardChunk(picture, KeywordV2, ToCardJson(persona, includeAdult))
                   ?? WithCardChunk(PlaceholderPng(256, 0x3B, 0x6F, 0xB6), KeywordV2, ToCardJson(persona, includeAdult));
        }

        /// <summary>
        /// A zip with <c>&lt;id&gt;.json</c> (Happi's persona file) and, when she has one,
        /// <c>&lt;id&gt;.png</c>. The 18+ notes are blanked unless <paramref name="includeAdult"/>.
        /// </summary>
        public static byte[] ToZip(PersonaDefinition persona, byte[] picturePng, bool includeAdult)
        {
            var copy = (persona ?? new PersonaDefinition()).Copy();
            if (!includeAdult)
                copy.AdultNotes = string.Empty;
            var id = PersonaIds.Normalize(copy.Id);
            if (id.Length == 0)
                id = "persona";
            var files = new List<KeyValuePair<string, byte[]>>
            {
                new KeyValuePair<string, byte[]>(id + ".json", new UTF8Encoding(false).GetBytes(PersonaJson.Write(copy))),
            };
            if (IsPng(picturePng))
                files.Add(new KeyValuePair<string, byte[]>(id + ".png", picturePng));
            return StoredZip.Write(files);
        }

        /// <summary>A file name for the export: her id with the format's extension.</summary>
        public static string FileName(PersonaDefinition persona, CardExportFormat format)
        {
            var id = persona == null ? string.Empty : PersonaIds.Normalize(persona.Id);
            if (id.Length == 0)
                id = "persona";
            switch (format)
            {
                case CardExportFormat.Json:
                    return id + ".card.json";
                case CardExportFormat.Zip:
                    return id + ".zip";
                default:
                    return id + ".card.png";
            }
        }

        /// <summary>The exported bytes for a format.</summary>
        public static byte[] Export(PersonaDefinition persona, byte[] picturePng, CardExportFormat format, bool includeAdult)
        {
            switch (format)
            {
                case CardExportFormat.Json:
                    return new UTF8Encoding(false).GetBytes(ToCardJson(persona, includeAdult));
                case CardExportFormat.Zip:
                    return ToZip(persona, picturePng, includeAdult);
                default:
                    return ToCardPng(persona, picturePng, includeAdult);
            }
        }

        /// <summary>
        /// A plain square PNG of one colour, written here (no image library): an RGB image whose
        /// pixel rows go into stored (uncompressed) zlib blocks.
        /// </summary>
        public static byte[] PlaceholderPng(int size, byte r, byte g, byte b)
        {
            size = Math.Max(1, Math.Min(size, 1024));
            var rowLength = 1 + size * 3;
            var raw = new byte[rowLength * size];
            for (var y = 0; y < size; y++)
            {
                var row = y * rowLength;
                raw[row] = 0;
                for (var x = 0; x < size; x++)
                {
                    raw[row + 1 + x * 3] = r;
                    raw[row + 2 + x * 3] = g;
                    raw[row + 3 + x * 3] = b;
                }
            }

            using (var output = new MemoryStream())
            {
                output.Write(PngSignature, 0, PngSignature.Length);
                var header = new byte[13];
                WriteBigEndian(header, 0, (uint)size);
                WriteBigEndian(header, 4, (uint)size);
                header[8] = 8;
                header[9] = 2;
                WriteChunk(output, "IHDR", header, 0, header.Length);
                var zlib = StoredZlib(raw);
                WriteChunk(output, "IDAT", zlib, 0, zlib.Length);
                WriteChunk(output, "IEND", new byte[0], 0, 0);
                return output.ToArray();
            }
        }

        static byte[] StoredZlib(byte[] data)
        {
            using (var output = new MemoryStream(data.Length + data.Length / 65535 * 5 + 16))
            {
                output.WriteByte(0x78);
                output.WriteByte(0x01);
                var at = 0;
                do
                {
                    var block = Math.Min(65535, data.Length - at);
                    var last = at + block >= data.Length;
                    output.WriteByte((byte)(last ? 1 : 0));
                    output.WriteByte((byte)(block & 0xFF));
                    output.WriteByte((byte)(block >> 8));
                    output.WriteByte((byte)(~block & 0xFF));
                    output.WriteByte((byte)((~block >> 8) & 0xFF));
                    output.Write(data, at, block);
                    at += block;
                }
                while (at < data.Length);

                uint a = 1;
                uint b = 0;
                for (var i = 0; i < data.Length; i++)
                {
                    a = (a + data[i]) % 65521;
                    b = (b + a) % 65521;
                }

                WriteUInt32(output, (b << 16) | a);
                return output.ToArray();
            }
        }

        static void WriteBigEndian(byte[] bytes, int at, uint value)
        {
            bytes[at] = (byte)(value >> 24);
            bytes[at + 1] = (byte)(value >> 16);
            bytes[at + 2] = (byte)(value >> 8);
            bytes[at + 3] = (byte)value;
        }
    }

    /// <summary>CRC-32 (the PNG and zip one).</summary>
    public static class Crc32
    {
        static readonly uint[] Table = BuildTable();

        static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }

            return table;
        }

        /// <summary>Feeds bytes into a running CRC (start with 0xFFFFFFFF, finish with ^ 0xFFFFFFFF).</summary>
        public static uint Update(uint crc, byte[] data, int offset, int length)
        {
            for (var i = 0; i < length; i++)
                crc = Table[(crc ^ data[offset + i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        public static uint Compute(byte[] data)
        {
            return Update(0xFFFFFFFFu, data, 0, data.Length) ^ 0xFFFFFFFFu;
        }
    }

    /// <summary>A minimal zip writer: stored (uncompressed) entries, which every unzip tool reads.</summary>
    public static class StoredZip
    {
        public static byte[] Write(IList<KeyValuePair<string, byte[]>> files)
        {
            using (var output = new MemoryStream())
            using (var central = new MemoryStream())
            {
                var count = 0;
                for (var i = 0; i < files.Count; i++)
                {
                    var name = Encoding.UTF8.GetBytes(files[i].Key);
                    var data = files[i].Value ?? new byte[0];
                    var crc = Crc32.Compute(data);
                    var offset = (uint)output.Position;

                    Le32(output, 0x04034B50);
                    Le16(output, 20);
                    Le16(output, 0x0800);
                    Le16(output, 0);
                    Le16(output, 0);
                    Le16(output, 0x21);
                    Le32(output, crc);
                    Le32(output, (uint)data.Length);
                    Le32(output, (uint)data.Length);
                    Le16(output, (ushort)name.Length);
                    Le16(output, 0);
                    output.Write(name, 0, name.Length);
                    output.Write(data, 0, data.Length);

                    Le32(central, 0x02014B50);
                    Le16(central, 20);
                    Le16(central, 20);
                    Le16(central, 0x0800);
                    Le16(central, 0);
                    Le16(central, 0);
                    Le16(central, 0x21);
                    Le32(central, crc);
                    Le32(central, (uint)data.Length);
                    Le32(central, (uint)data.Length);
                    Le16(central, (ushort)name.Length);
                    Le16(central, 0);
                    Le16(central, 0);
                    Le16(central, 0);
                    Le16(central, 0);
                    Le32(central, 0);
                    Le32(central, offset);
                    central.Write(name, 0, name.Length);
                    count++;
                }

                var centralOffset = (uint)output.Position;
                central.Position = 0;
                central.CopyTo(output);
                Le32(output, 0x06054B50);
                Le16(output, 0);
                Le16(output, 0);
                Le16(output, (ushort)count);
                Le16(output, (ushort)count);
                Le32(output, (uint)central.Length);
                Le32(output, centralOffset);
                Le16(output, 0);
                return output.ToArray();
            }
        }

        static void Le16(Stream s, ushort v)
        {
            s.WriteByte((byte)v);
            s.WriteByte((byte)(v >> 8));
        }

        static void Le32(Stream s, uint v)
        {
            s.WriteByte((byte)v);
            s.WriteByte((byte)(v >> 8));
            s.WriteByte((byte)(v >> 16));
            s.WriteByte((byte)(v >> 24));
        }
    }
}
