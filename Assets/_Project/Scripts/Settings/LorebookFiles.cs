using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AICompanion.Core;

namespace AICompanion.Settings
{
    /// <summary>What a lorebook import found, for the Studio's status line.</summary>
    public sealed class LoreImport
    {
        public bool Ok;
        public string Error = string.Empty;
        public Lorebook Book;
        public readonly List<string> Notes = new List<string>();
    }

    /// <summary>
    /// JUH-156: reads lorebooks in the shapes other apps write, tolerantly:
    /// <list type="bullet">
    /// <item>SillyTavern World Info: <c>{"entries": {"0": {key, keysecondary, content, comment, constant, selective, order, disable}}}</c>, entries keyed by number or as an array.</item>
    /// <item>A character card's book (V2/V3, Chub, Character Tavern): <c>character_book</c> with <c>entries: [{keys, secondary_keys, content, name, enabled, insertion_order, constant, selective}]</c>, alone or inside a card's <c>data</c>.</item>
    /// <item>NovelAI (<c>text</c>, <c>displayName</c>) and Agnaistic (<c>entry</c>, <c>keywords</c>) entries.</item>
    /// </list>
    /// {{char}} and {{user}} become her name and "the user". Text that looks like a key is dropped.
    /// </summary>
    public static class LorebookImport
    {
        public const string SecretNote = "Some entries looked like they held an API key or password, so they were left out.";
        public const string TrimmedNote = "Some entries were longer than 4,000 characters and were shortened.";
        public const string LimitNote = "Only the first 500 entries were kept.";

        public static LoreImport FromJson(string json, string herName)
        {
            var result = new LoreImport();
            object root;
            if (!JsonTree.TryParse(json, out root))
            {
                result.Error = "That isn't a JSON file Happi can read.";
                return result;
            }

            var top = JsonTree.Object(root);
            var bookObject = FindBook(top);
            if (bookObject == null)
            {
                result.Error = "That JSON has no lorebook entries.";
                return result;
            }

            var book = new Lorebook
            {
                Name = Clean(JsonTree.String(bookObject, "name")),
                Adult = AdultFlag(bookObject) || CardIsAdult(top),
            };
            var her = string.IsNullOrWhiteSpace(herName) ? "She" : herName.Trim();
            var secret = false;
            var trimmed = false;
            var limited = false;
            foreach (var item in Entries(JsonTree.Get(bookObject, "entries")))
            {
                if (book.Entries.Count >= Lorebook.MaxEntries)
                {
                    limited = true;
                    break;
                }

                var entry = Entry(item, her, ref secret, ref trimmed);
                if (entry != null)
                    book.Entries.Add(entry);
            }

            if (book.Entries.Count == 0)
            {
                result.Error = "That lorebook has no entries with text.";
                return result;
            }

            if (secret)
                result.Notes.Add(SecretNote);
            if (trimmed)
                result.Notes.Add(TrimmedNote);
            if (limited)
                result.Notes.Add(LimitNote);
            result.Book = book;
            result.Ok = true;
            return result;
        }

        /// <summary>The lorebook inside a card's JSON, or null when the card has none.</summary>
        public static Lorebook FromCardJson(string cardJson, string herName, bool adultCard)
        {
            object root;
            if (!JsonTree.TryParse(cardJson, out root))
                return null;
            var top = JsonTree.Object(root);
            var data = JsonTree.Object(top, "data") ?? top;
            if (JsonTree.Object(data, "character_book") == null)
                return null;
            var import = FromJson(cardJson, herName);
            if (!import.Ok)
                return null;
            if (adultCard)
                import.Book.Adult = true;
            return import.Book;
        }

        static Dictionary<string, object> FindBook(Dictionary<string, object> top)
        {
            if (top == null)
                return null;
            if (JsonTree.Get(top, "entries") != null)
                return top;
            var book = JsonTree.Object(top, "character_book");
            if (book != null)
                return book;
            var data = JsonTree.Object(top, "data");
            if (data != null)
            {
                book = JsonTree.Object(data, "character_book");
                if (book != null)
                    return book;
                if (JsonTree.Get(data, "entries") != null)
                    return data;
            }

            return null;
        }

        static bool AdultFlag(Dictionary<string, object> book)
        {
            if (JsonTree.Bool(book, "happi_adult", false))
                return true;
            return JsonTree.Bool(JsonTree.Object(book, "extensions"), "happi_adult", false);
        }

        static bool CardIsAdult(Dictionary<string, object> top)
        {
            var data = JsonTree.Object(top, "data") ?? top;
            var tags = JsonTree.Get(data, "tags") as List<object>;
            if (tags == null)
                return false;
            var words = new List<string>();
            for (var i = 0; i < tags.Count; i++)
                if (tags[i] is string tag)
                    words.Add(tag);
            return CharacterCards.IsAdult(words.ToArray());
        }

        static IEnumerable<object> Entries(object entries)
        {
            if (entries is List<object> list)
            {
                for (var i = 0; i < list.Count; i++)
                    yield return list[i];
                yield break;
            }

            if (entries is Dictionary<string, object> map)
            {
                // SillyTavern keys entries by number; keep their numeric order.
                var keys = new List<string>(map.Keys);
                keys.Sort((a, b) =>
                {
                    int x, y;
                    var ax = int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out x);
                    var by = int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
                    if (ax && by)
                        return x.CompareTo(y);
                    return string.CompareOrdinal(a, b);
                });
                for (var i = 0; i < keys.Count; i++)
                    yield return map[keys[i]];
            }
        }

        static LoreEntry Entry(object item, string her, ref bool secret, ref bool trimmed)
        {
            var o = JsonTree.Object(item);
            if (o == null)
                return null;
            var content = First(o, "content", "text", "entry");
            content = CharacterCards.ReplaceNames(content.Replace("\r\n", "\n").Trim(), her, "the user");
            if (content.Length == 0)
                return null;
            if (CompanionPersona.LooksLikeSecret(content))
            {
                secret = true;
                return null;
            }

            if (content.Length > Lorebook.MaxEntryLength)
            {
                content = content.Substring(0, Lorebook.MaxEntryLength).TrimEnd();
                trimmed = true;
            }

            var entry = new LoreEntry
            {
                Name = Clean(First(o, "name", "comment", "displayName")),
                Content = content,
                Constant = JsonTree.Bool(o, "constant", false),
                Selective = JsonTree.Bool(o, "selective", false),
                CaseSensitive = JsonTree.Bool(o, "case_sensitive", JsonTree.Bool(o, "caseSensitive", false)),
            };
            entry.Enabled = JsonTree.Bool(o, "enabled", true) && !JsonTree.Bool(o, "disable", false);
            double order;
            if (JsonTree.TryNumber(o, "insertion_order", out order) || JsonTree.TryNumber(o, "order", out order))
                entry.Order = (int)Math.Max(-100000, Math.Min(100000, Math.Round(order)));
            Keys(entry.Keys, JsonTree.Get(o, "keys") ?? JsonTree.Get(o, "key") ?? JsonTree.Get(o, "keywords"), her);
            Keys(entry.SecondaryKeys, JsonTree.Get(o, "secondary_keys") ?? JsonTree.Get(o, "keysecondary"), her);
            if (entry.Name.Length > 120)
                entry.Name = entry.Name.Substring(0, 120);

            // An entry with no keys can only ever be constant; keep it, but it never triggers by itself.
            return entry;
        }

        static void Keys(List<string> into, object value, string her)
        {
            var raw = new List<string>();
            if (value is List<object> list)
            {
                for (var i = 0; i < list.Count; i++)
                    if (list[i] is string s)
                        raw.Add(s);
            }
            else if (value is string text)
            {
                raw.AddRange(text.Split(','));
            }

            for (var i = 0; i < raw.Count && into.Count < Lorebook.MaxKeys; i++)
            {
                var key = CharacterCards.ReplaceNames(raw[i], her, "the user").Trim();
                if (key.Length == 0 || key.Length > Lorebook.MaxKeyLength || CompanionPersona.LooksLikeSecret(key))
                    continue;
                if (!into.Contains(key))
                    into.Add(key);
            }
        }

        static string First(Dictionary<string, object> o, params string[] keys)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                var text = JsonTree.String(o, keys[i]);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return string.Empty;
        }

        static string Clean(string text)
        {
            return (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }

    /// <summary>
    /// JUH-156: writes a lorebook as a V2 <c>character_book</c>, which <see cref="LorebookImport"/>
    /// reads back and other apps can import. The 18+ flag goes in <c>extensions.happi_adult</c>.
    /// </summary>
    public static class LorebookWriter
    {
        public static string ToJson(Lorebook book)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"name\": ").Append(Quote(book != null ? book.Name : string.Empty)).Append(",\n");
            sb.Append("  \"extensions\": { \"happi_adult\": ").Append(book != null && book.Adult ? "true" : "false").Append(" },\n");
            sb.Append("  \"entries\": [");
            var entries = book != null ? book.Entries : new List<LoreEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    { \"id\": ").Append(i.ToString(CultureInfo.InvariantCulture));
                sb.Append(", \"name\": ").Append(Quote(e.Name));
                sb.Append(", \"keys\": ").Append(List(e.Keys));
                sb.Append(", \"secondary_keys\": ").Append(List(e.SecondaryKeys));
                sb.Append(", \"content\": ").Append(Quote(e.Content));
                sb.Append(", \"enabled\": ").Append(e.Enabled ? "true" : "false");
                sb.Append(", \"constant\": ").Append(e.Constant ? "true" : "false");
                sb.Append(", \"selective\": ").Append(e.Selective ? "true" : "false");
                sb.Append(", \"case_sensitive\": ").Append(e.CaseSensitive ? "true" : "false");
                sb.Append(", \"insertion_order\": ").Append(e.Order.ToString(CultureInfo.InvariantCulture));
                sb.Append(", \"position\": \"before_char\", \"extensions\": {} }");
            }

            sb.Append(entries.Count > 0 ? "\n  ]\n}\n" : "]\n}\n");
            return sb.ToString();
        }

        static string List(List<string> items)
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(Quote(items[i]));
            }

            return sb.Append(']').ToString();
        }

        static string Quote(string text)
        {
            var sb = new StringBuilder("\"");
            var s = text ?? string.Empty;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.Append('"').ToString();
        }
    }

    /// <summary>JUH-156: her lorebook, kept encrypted beside her persona file as <c>&lt;id&gt;.lorebook</c>.</summary>
    public static class LorebookStore
    {
        public static string PathFor(string personasFolder, string id)
        {
            var clean = PersonaIds.Normalize(id);
            if (string.IsNullOrEmpty(personasFolder) || clean.Length == 0)
                return null;
            return System.IO.Path.Combine(personasFolder, clean + ".lorebook");
        }

        public static bool Save(string personasFolder, string id, Lorebook book)
        {
            var path = PathFor(personasFolder, id);
            if (path == null)
                return false;
            if (book == null || book.IsEmpty)
                return Delete(personasFolder, id);
            try
            {
                System.IO.Directory.CreateDirectory(personasFolder);
                return ProtectedLocalFile.Write(path, LorebookWriter.ToJson(book));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static Lorebook Load(string personasFolder, string id)
        {
            var path = PathFor(personasFolder, id);
            if (path == null || !System.IO.File.Exists(path))
                return null;
            try
            {
                var read = ProtectedLocalFile.Read(path);
                if (read.Status != LocalReadStatus.Ready || string.IsNullOrEmpty(read.Text))
                    return null;
                var import = LorebookImport.FromJson(read.Text, null);
                return import.Ok ? import.Book : null;
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
                    System.IO.File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
