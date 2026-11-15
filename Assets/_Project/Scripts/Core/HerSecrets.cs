using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>JUH-123: which list a secret comes from.</summary>
    public enum SecretGroup
    {
        /// <summary>"What stings her": <see cref="SheetLanding.touchy"/>, ids t1..t5.</summary>
        Stings = 0,

        /// <summary>"What wins her over": <see cref="SheetLanding.winsHerOver"/>, ids w1..w5.</summary>
        WinsHerOver = 1,
    }

    /// <summary>One secret: its prompt id ("t2"), its group, its text, and the stable key it is saved under.</summary>
    public struct SecretItem
    {
        public string Id;
        public SecretGroup Group;
        public string Text;
        public string Key;
    }

    /// <summary>What a cue's <c>secret=</c> did.</summary>
    public enum SecretHitKind
    {
        /// <summary>No secret named, or an id that is not one of hers.</summary>
        None = 0,

        /// <summary>Found for the first time: revealed, and a critical hit (Jussi, 2026-11-15).</summary>
        First = 1,

        /// <summary>Already found: an ordinary landing.</summary>
        Again = 2,
    }

    /// <summary>
    /// JUH-123: her secrets are the likes and dislikes in her character sheet (the landing
    /// lists). Each has an id in the prompt (t1..t5, w1..w5) so the model can name the one a
    /// message hit with <c>secret="t2"</c> in its cue tag. What was found is saved by a hash of
    /// the text, not the id, so a re-read that keeps the same phrase keeps it found.
    /// </summary>
    public static class HerSecrets
    {
        /// <summary>The cue tag attribute: <c>secret="t2"</c>.</summary>
        public const string Attribute = "secret";

        public const string StingsPrefix = "t";
        public const string WinsPrefix = "w";

        /// <summary>
        /// The sentence added to the cue instruction while relationship changes are on. Jussi
        /// (2026-11-15): she reacts in words, in character, when one is found.
        /// </summary>
        public const string Instruction =
            "If the user's message clearly hits one of the things you are touchy about or warm to (their ids are in brackets, " +
            "for example (t2)), also put secret=\"t2\" in the tag, and react in your reply as if they just found out something " +
            "about you that you had not told them. Leave secret out for anything less clear.";

        /// <summary>The secrets in a landing, stings first, in sheet order. Blank phrases are skipped and get no id.</summary>
        public static List<SecretItem> From(SheetLanding landing)
        {
            var items = new List<SecretItem>(CharacterSheet.MaxLandingPhrases * 2);
            if (landing == null)
                return items;
            Add(items, landing.touchy, SecretGroup.Stings, StingsPrefix);
            Add(items, landing.winsHerOver, SecretGroup.WinsHerOver, WinsPrefix);
            return items;
        }

        static void Add(List<SecretItem> items, List<string> phrases, SecretGroup group, string prefix)
        {
            if (phrases == null)
                return;
            var n = 0;
            for (var i = 0; i < phrases.Count && n < CharacterSheet.MaxLandingPhrases; i++)
            {
                var text = Clean(phrases[i]);
                if (text.Length == 0)
                    continue;
                n++;
                items.Add(new SecretItem { Id = prefix + n, Group = group, Text = text, Key = Key(text) });
            }
        }

        /// <summary>The phrase as shown and as put in the prompt: trimmed, without a closing full stop.</summary>
        public static string Clean(string phrase)
        {
            return string.IsNullOrWhiteSpace(phrase) ? string.Empty : phrase.Trim().TrimEnd('.').Trim();
        }

        /// <summary>
        /// The saved key: an FNV-1a hash of the lower-case phrase with its spaces collapsed, as 8
        /// hex digits. Stable across runs and machines (string.GetHashCode is not).
        /// </summary>
        public static string Key(string phrase)
        {
            var text = Clean(phrase).ToLowerInvariant();
            unchecked
            {
                var hash = 2166136261u;
                var space = false;
                for (var i = 0; i < text.Length; i++)
                {
                    var c = text[i];
                    if (char.IsWhiteSpace(c))
                    {
                        space = true;
                        continue;
                    }

                    if (space)
                    {
                        hash = (hash ^ ' ') * 16777619u;
                        space = false;
                    }

                    hash = (hash ^ c) * 16777619u;
                }

                return hash.ToString("x8");
            }
        }

        /// <summary>The cue's secret value as an id ("t2"): lower case, letters and digits only, at most 4 characters. Empty otherwise.</summary>
        public static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var text = value.Trim().Trim('(', ')', '[', ']').ToLowerInvariant();
            if (text.Length == 0 || text.Length > 4)
                return string.Empty;
            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsLetterOrDigit(text[i]))
                    return string.Empty;
            }

            return text;
        }

        /// <summary>The secret with this id, or false when she has none by that id.</summary>
        public static bool TryFind(List<SecretItem> items, string id, out SecretItem item)
        {
            item = default(SecretItem);
            var wanted = SanitizeId(id);
            if (wanted.Length == 0 || items == null)
                return false;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Id, wanted, StringComparison.Ordinal))
                {
                    item = items[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What a cue's <paramref name="id"/> does, without changing anything. An id that is not
        /// one of hers is <see cref="SecretHitKind.None"/>.
        /// </summary>
        public static SecretHitKind Check(SecretsState state, List<SecretItem> items, string id, out SecretItem item)
        {
            if (!TryFind(items, id, out item))
                return SecretHitKind.None;
            return state != null && state.IsFound(item.Key) ? SecretHitKind.Again : SecretHitKind.First;
        }

        /// <summary>
        /// The prompt list with ids: <c> Touchy about: (t1) being called childish, (t2) being rushed.</c>
        /// Empty when the list is.
        /// </summary>
        public static void AppendPromptList(StringBuilder sb, string lead, List<SecretItem> items, SecretGroup group)
        {
            var any = false;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Group != group)
                    continue;
                sb.Append(any ? ", " : lead).Append('(').Append(items[i].Id).Append(") ").Append(items[i].Text);
                any = true;
            }

            if (any)
                sb.Append('.');
        }
    }

    /// <summary>JUH-123: which of her secrets were found, for one persona. Saved encrypted (<see cref="SecretsStore"/>).</summary>
    public sealed class SecretsState
    {
        public const int FileVersion = 1;

        readonly List<string> _found = new List<string>();

        /// <summary>The key of the newest find (the gold outline in Profile); empty for none.</summary>
        public string NewestKey = string.Empty;

        public int FoundCount
        {
            get { return _found.Count; }
        }

        public bool IsFound(string key)
        {
            return !string.IsNullOrEmpty(key) && _found.Contains(key);
        }

        /// <summary>Marks a secret found. True when it was not found before.</summary>
        public bool Reveal(string key)
        {
            if (string.IsNullOrEmpty(key) || _found.Contains(key))
                return false;
            _found.Add(key);
            NewestKey = key;
            return true;
        }

        public void Clear()
        {
            _found.Clear();
            NewestKey = string.Empty;
        }

        /// <summary>How many of <paramref name="items"/> are found. A find whose phrase left the sheet does not count.</summary>
        public int FoundAmong(List<SecretItem> items)
        {
            var n = 0;
            if (items == null)
                return 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (IsFound(items[i].Key))
                    n++;
            }

            return n;
        }

        [Serializable]
        class SecretsFile
        {
            public int version;
            public List<string> found = new List<string>();
            public string newest;
        }

        public string ToJson()
        {
            var file = new SecretsFile { version = FileVersion, newest = NewestKey ?? string.Empty };
            file.found.AddRange(_found);
            return JsonUtility.ToJson(file, true);
        }

        public static SecretsState FromJson(string json)
        {
            var state = new SecretsState();
            if (string.IsNullOrWhiteSpace(json))
                return state;
            SecretsFile file;
            try
            {
                file = JsonUtility.FromJson<SecretsFile>(json);
            }
            catch (ArgumentException)
            {
                return state;
            }

            if (file == null || file.found == null)
                return state;
            for (var i = 0; i < file.found.Count; i++)
            {
                var key = (file.found[i] ?? string.Empty).Trim().ToLowerInvariant();
                if (key.Length > 0 && !state._found.Contains(key))
                    state._found.Add(key);
            }

            var newest = (file.newest ?? string.Empty).Trim().ToLowerInvariant();
            state.NewestKey = state._found.Contains(newest) ? newest : string.Empty;
            return state;
        }
    }

    /// <summary>
    /// JUH-123: where the found secrets live, next to the relationship: her-secrets.json and
    /// her-secrets-&lt;persona&gt;.json, encrypted. Not "secrets.json": that name is the API keys
    /// (<see cref="LocalDataInventory.IsSecretFile"/>). Wipe and export include it
    /// (<see cref="LocalDataInventory.IsHerSecretsName"/>).
    /// </summary>
    public static class SecretsStore
    {
        public const string FileName = "her-secrets.json";

        public static string FileFor(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return Path.Combine(folder, id.Length == 0 ? FileName : "her-secrets-" + id + ".json");
        }

        public static SecretsState Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new SecretsState();
            var read = ProtectedLocalFile.Read(path);
            return read.Status == LocalReadStatus.Ready ? SecretsState.FromJson(read.Text) : new SecretsState();
        }

        public static bool Save(string path, SecretsState state)
        {
            if (string.IsNullOrEmpty(path) || state == null)
                return false;
            return ProtectedLocalFile.Write(path, state.ToJson());
        }
    }

    /// <summary>One chip in Profile → Personality → Her secrets.</summary>
    public struct ProfileSecret
    {
        public SecretGroup Group;
        public bool Found;

        /// <summary>The phrase when found; "Not found yet" otherwise.</summary>
        public string Text;

        /// <summary>The newest find: drawn with a gold outline.</summary>
        public bool Newest;
    }

    /// <summary>JUH-123: the "Her secrets" section of the Profile panel.</summary>
    public sealed class ProfileSecrets
    {
        public const string Locked = "Not found yet";

        public readonly List<ProfileSecret> Items = new List<ProfileSecret>(CharacterSheet.MaxLandingPhrases * 2);
        public int Found;
        public int Total;

        /// <summary>"Her secrets · 3 of 8 found".</summary>
        public string Title
        {
            get { return "Her secrets · " + Found + " of " + Total + " found"; }
        }

        public static ProfileSecrets Build(List<SecretItem> items, SecretsState state)
        {
            var view = new ProfileSecrets();
            if (items == null)
                return view;
            for (var i = 0; i < items.Count; i++)
            {
                var found = state != null && state.IsFound(items[i].Key);
                view.Items.Add(new ProfileSecret
                {
                    Group = items[i].Group,
                    Found = found,
                    Text = found ? items[i].Text : Locked,
                    Newest = found && state != null && string.Equals(state.NewestKey, items[i].Key, StringComparison.Ordinal),
                });
                if (found)
                    view.Found++;
            }

            view.Total = items.Count;
            return view;
        }
    }
}
