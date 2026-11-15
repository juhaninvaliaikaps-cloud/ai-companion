using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>JUH-125: the three groups of her 18+ secrets.</summary>
    public enum IntimateSecretGroup
    {
        /// <summary>A genre and the specific thing in it ("BDSM: being tied up"). Ids k1..k3.</summary>
        Kink = 0,

        /// <summary>Where she gets more aroused than elsewhere. Ids s1..s3.</summary>
        Sensitive = 1,

        /// <summary>Something she is not eager to try while her innocence is high. Ids y1..y3.</summary>
        Shy = 2,
    }

    /// <summary>One 18+ secret: its prompt id ("k1"), its group, its text, and the key it is saved under.</summary>
    public struct IntimateSecretItem
    {
        public string Id;
        public IntimateSecretGroup Group;
        public string Text;
        public string Key;
    }

    /// <summary>One kink from the reading: the genre and the specific thing.</summary>
    [Serializable]
    public sealed class IntimateKink
    {
        public string genre = string.Empty;
        public string detail = string.Empty;

        /// <summary>"BDSM: being tied up", or just one of them when the other is empty.</summary>
        public string Text
        {
            get
            {
                var g = HerSecrets.Clean(genre);
                var d = HerSecrets.Clean(detail);
                if (g.Length == 0)
                    return d;
                return d.Length == 0 ? g : g + ": " + d;
            }
        }
    }

    /// <summary>
    /// JUH-125: how many secrets of each group a persona has, 1 to 3, picked once from her
    /// persona id and description, so a re-read of the same description gives the same counts.
    /// Kinks and sensitive areas lean toward more, shy-to-do toward fewer (Jussi, 2026-11-15:
    /// proposed odds, to be tuned).
    /// </summary>
    public static class IntimateSecretCounts
    {
        public const int Max = 3;

        /// <summary>The odds of 1, 2 and 3 kinks or sensitive areas.</summary>
        public static readonly float[] MoreWeights = { 0.2f, 0.4f, 0.4f };

        /// <summary>The odds of 1, 2 and 3 things she is shy to do.</summary>
        public static readonly float[] FewerWeights = { 0.5f, 0.35f, 0.15f };

        public static void For(string personaId, string promptHash, out int kinks, out int sensitive, out int shy)
        {
            var seed = (personaId ?? string.Empty) + "\n" + (promptHash ?? string.Empty);
            kinks = Pick(Unit(seed + "\nkinks"), MoreWeights);
            sensitive = Pick(Unit(seed + "\nsensitive"), MoreWeights);
            shy = Pick(Unit(seed + "\nshy"), FewerWeights);
        }

        /// <summary>1, 2 or 3 for a number in [0, 1) by the weights.</summary>
        public static int Pick(double unit, float[] weights)
        {
            var total = 0f;
            for (var i = 0; i < weights.Length; i++)
                total += Math.Max(0f, weights[i]);
            if (total <= 0f)
                return 1;
            var at = unit * total;
            var sum = 0d;
            for (var i = 0; i < weights.Length; i++)
            {
                sum += Math.Max(0f, weights[i]);
                if (at < sum)
                    return i + 1;
            }

            return weights.Length;
        }

        /// <summary>A stable number in [0, 1) from the text (FNV-1a), the same on every machine.</summary>
        public static double Unit(string text)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < text.Length; i++)
                    hash = (hash ^ text[i]) * 16777619u;
                // Mix the low bits up so close seeds spread out.
                hash ^= hash >> 15;
                hash *= 0x2c1b3c6du;
                hash ^= hash >> 12;
                return hash / 4294967296d;
            }
        }
    }

    /// <summary>
    /// JUH-125: her 18+ secrets for one persona: the local model's reading and which were found.
    /// Saved encrypted (<see cref="IntimateSecretsStore"/>). Only used while the 18+ gate is open.
    /// </summary>
    public sealed class IntimateSecretsState
    {
        public const int FileVersion = 1;
        public const int MaxGenreLength = 30;
        public const int MaxDetailLength = 60;

        /// <summary>The description hash the reading was made for; empty before the first reading.</summary>
        public string ReadHash = string.Empty;

        public readonly List<IntimateKink> Kinks = new List<IntimateKink>();
        public readonly List<string> Sensitive = new List<string>();
        public readonly List<string> Shy = new List<string>();

        readonly SecretsState _found = new SecretsState();

        public bool HasReading
        {
            get { return ReadHash.Length > 0 && (Kinks.Count + Sensitive.Count + Shy.Count) > 0; }
        }

        public SecretsState Found
        {
            get { return _found; }
        }

        /// <summary>The secrets with their ids: kinks k1.., sensitive areas s1.., shy-to-do y1...</summary>
        public List<IntimateSecretItem> Items()
        {
            var items = new List<IntimateSecretItem>(IntimateSecretCounts.Max * 3);
            for (var i = 0; i < Kinks.Count; i++)
                AddItem(items, IntimateSecretGroup.Kink, "k", Kinks[i] != null ? Kinks[i].Text : string.Empty);
            for (var i = 0; i < Sensitive.Count; i++)
                AddItem(items, IntimateSecretGroup.Sensitive, "s", Sensitive[i]);
            for (var i = 0; i < Shy.Count; i++)
                AddItem(items, IntimateSecretGroup.Shy, "y", Shy[i]);
            return items;
        }

        static void AddItem(List<IntimateSecretItem> items, IntimateSecretGroup group, string prefix, string text)
        {
            var clean = HerSecrets.Clean(text);
            if (clean.Length == 0)
                return;
            var n = 1;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Group == group)
                    n++;
            }

            // The group goes into the key, so the same words as a kink and as a shy-to-do stay apart.
            items.Add(new IntimateSecretItem { Id = prefix + n, Group = group, Text = clean, Key = HerSecrets.Key(prefix + ":" + clean) });
        }

        /// <summary>A new reading: the lists are replaced, cut to the counts; finds whose text is still there stay found.</summary>
        public void SetReading(string hash, List<IntimateKink> kinks, List<string> sensitive, List<string> shy, int kinkCount, int sensitiveCount, int shyCount)
        {
            ReadHash = hash ?? string.Empty;
            Kinks.Clear();
            Sensitive.Clear();
            Shy.Clear();
            if (kinks != null)
            {
                for (var i = 0; i < kinks.Count && Kinks.Count < Math.Min(kinkCount, IntimateSecretCounts.Max); i++)
                {
                    var k = kinks[i];
                    if (k == null)
                        continue;
                    var kink = new IntimateKink { genre = Cut(k.genre, MaxGenreLength), detail = Cut(k.detail, MaxDetailLength) };
                    if (kink.Text.Length > 0)
                        Kinks.Add(kink);
                }
            }

            CopyPhrases(sensitive, Sensitive, sensitiveCount);
            CopyPhrases(shy, Shy, shyCount);
        }

        static void CopyPhrases(List<string> from, List<string> to, int count)
        {
            if (from == null)
                return;
            for (var i = 0; i < from.Count && to.Count < Math.Min(count, IntimateSecretCounts.Max); i++)
            {
                var text = Cut(from[i], MaxDetailLength);
                if (text.Length > 0)
                    to.Add(text);
            }
        }

        static string Cut(string text, int max)
        {
            var clean = HerSecrets.Clean(text);
            return clean.Length <= max ? clean : clean.Substring(0, max).TrimEnd();
        }

        /// <summary>What a cue's <c>secret=</c> does, without changing anything. An unknown id is None.</summary>
        public SecretHitKind Check(string id, out IntimateSecretItem item)
        {
            item = default(IntimateSecretItem);
            var wanted = HerSecrets.SanitizeId(id);
            if (wanted.Length == 0)
                return SecretHitKind.None;
            var items = Items();
            for (var i = 0; i < items.Count; i++)
            {
                if (!string.Equals(items[i].Id, wanted, StringComparison.Ordinal))
                    continue;
                item = items[i];
                return _found.IsFound(item.Key) ? SecretHitKind.Again : SecretHitKind.First;
            }

            return SecretHitKind.None;
        }

        [Serializable]
        class FileData
        {
            public int version;
            public string readHash;
            public List<IntimateKink> kinks = new List<IntimateKink>();
            public List<string> sensitive = new List<string>();
            public List<string> shy = new List<string>();
            public string found;
        }

        public string ToJson()
        {
            var file = new FileData { version = FileVersion, readHash = ReadHash ?? string.Empty, found = _found.ToJson() };
            file.kinks.AddRange(Kinks);
            file.sensitive.AddRange(Sensitive);
            file.shy.AddRange(Shy);
            return JsonUtility.ToJson(file, true);
        }

        public static IntimateSecretsState FromJson(string json)
        {
            var state = new IntimateSecretsState();
            if (string.IsNullOrWhiteSpace(json))
                return state;
            FileData file;
            try
            {
                file = JsonUtility.FromJson<FileData>(json);
            }
            catch (ArgumentException)
            {
                return state;
            }

            if (file == null)
                return state;
            state.SetReading(file.readHash, file.kinks, file.sensitive, file.shy, IntimateSecretCounts.Max, IntimateSecretCounts.Max, IntimateSecretCounts.Max);
            var found = SecretsState.FromJson(file.found);
            state._found.CopyFrom(found);
            return state;
        }

        /// <summary>The prefix of an 18+ secret id: k, s or y. Her ordinary secrets use t and w.</summary>
        public static bool IsIntimateId(string id)
        {
            var clean = HerSecrets.SanitizeId(id);
            return clean.Length > 1 && (clean[0] == 'k' || clean[0] == 's' || clean[0] == 'y');
        }
    }

    /// <summary>
    /// JUH-125: the prompt line for her 18+ secrets. Empty while the gate is closed. The shy
    /// part follows her innocence (JUH-116): she warms to those things slowly as it drops
    /// (Jussi, 2026-11-15), with no fixed point where they unlock.
    /// </summary>
    public static class IntimateSecretsPrompt
    {
        public const string Instruction =
            "During an intimate moment, when the scene clearly does one of these, put secret=\"k1\" (its id) in the tag. " +
            "The first time, react as if they just found out something about you that you had not told them.";

        /// <summary>How she feels about the things she is shy to do, by innocence band (0 very innocent .. 4 perverted).</summary>
        public static string ShyPhrase(float innocence)
        {
            switch (InnocenceMath.Band(innocence))
            {
                case 0: return "You shy away from the things you are shy to do; it takes a lot of patience and reassurance before you would even consider them.";
                case 1: return "You are reluctant about the things you are shy to do; you might agree only slowly, with reassurance.";
                case 2: return "You hesitate about the things you are shy to do, but you can be coaxed into them.";
                case 3: return "You are curious about the things you used to be shy to do, and only a little shy now.";
                default: return "You are no longer shy about the things you used to be shy to do; you are open to them.";
            }
        }

        /// <summary>The line, or empty when the gate is closed or there is nothing read yet.</summary>
        public static string Line(List<IntimateSecretItem> items, float innocence, bool gateOpen)
        {
            if (!gateOpen || items == null || items.Count == 0)
                return string.Empty;
            var sb = new StringBuilder(320);
            sb.Append("Your intimate secrets (18+, only for intimate moments).");
            Append(sb, " Kinks: ", items, IntimateSecretGroup.Kink);
            Append(sb, " Sensitive areas: ", items, IntimateSecretGroup.Sensitive);
            if (Append(sb, " Shy to do: ", items, IntimateSecretGroup.Shy))
                sb.Append(' ').Append(ShyPhrase(innocence));
            sb.Append(' ').Append(Instruction);
            return sb.ToString();
        }

        static bool Append(StringBuilder sb, string lead, List<IntimateSecretItem> items, IntimateSecretGroup group)
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
            return any;
        }
    }

    /// <summary>
    /// JUH-125: the hand-off between the keeper and chat, like <see cref="InnocenceNow"/>.
    /// <see cref="Append"/> adds nothing while the 18+ gate is closed, whatever was published.
    /// </summary>
    public static class IntimateSecretsNow
    {
        static string _line = string.Empty;

        public static string PromptLine
        {
            get { return _line; }
        }

        public static void SetPromptLine(string line)
        {
            _line = string.IsNullOrWhiteSpace(line) ? string.Empty : line.Trim();
        }

        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_line.Length == 0 || !IntimateExpressions.Active)
                return body;
            return body.Length == 0 ? _line : body + "\n\n" + _line;
        }

        public static void Reset()
        {
            _line = string.Empty;
        }
    }

    /// <summary>
    /// JUH-125: where the 18+ secrets live: intimate-secrets.json and
    /// intimate-secrets-&lt;persona&gt;.json, encrypted. Wipe and export include it
    /// (<see cref="LocalDataInventory.IsIntimateSecretsName"/>).
    /// </summary>
    public static class IntimateSecretsStore
    {
        public const string FileName = "intimate-secrets.json";

        public static string FileFor(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return Path.Combine(folder, id.Length == 0 ? FileName : "intimate-secrets-" + id + ".json");
        }

        public static IntimateSecretsState Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new IntimateSecretsState();
            var read = ProtectedLocalFile.Read(path);
            return read.Status == LocalReadStatus.Ready ? IntimateSecretsState.FromJson(read.Text) : new IntimateSecretsState();
        }

        public static bool Save(string path, IntimateSecretsState state)
        {
            if (string.IsNullOrEmpty(path) || state == null)
                return false;
            return ProtectedLocalFile.Write(path, state.ToJson());
        }
    }

    /// <summary>One chip in Profile → Personality → Her 18+ secrets.</summary>
    public struct ProfileIntimateSecret
    {
        public IntimateSecretGroup Group;
        public bool Found;
        public string Text;
        public bool Newest;
    }

    /// <summary>JUH-125: the 18+ section of the Profile panel. Built only while the gate is open.</summary>
    public sealed class ProfileIntimateSecrets
    {
        public readonly List<ProfileIntimateSecret> Items = new List<ProfileIntimateSecret>(IntimateSecretCounts.Max * 3);
        public int Found;
        public int Total;

        /// <summary>"How shy she is now: in between" from her innocence.</summary>
        public string ShyLine = string.Empty;

        /// <summary>A reading in progress, a failure, or "not read yet".</summary>
        public string Status = string.Empty;

        public string Title
        {
            get { return "Her 18+ secrets · " + Found + " of " + Total + " found"; }
        }

        public static ProfileIntimateSecrets Build(IntimateSecretsState state, float innocence, string status)
        {
            var view = new ProfileIntimateSecrets { Status = status ?? string.Empty };
            if (state == null)
                return view;
            var items = state.Items();
            for (var i = 0; i < items.Count; i++)
            {
                var found = state.Found.IsFound(items[i].Key);
                view.Items.Add(new ProfileIntimateSecret
                {
                    Group = items[i].Group,
                    Found = found,
                    Text = found ? items[i].Text : ProfileSecrets.Locked,
                    Newest = found && string.Equals(state.Found.NewestKey, items[i].Key, StringComparison.Ordinal),
                });
                if (found)
                    view.Found++;
            }

            view.Total = items.Count;
            view.ShyLine = "How shy she is now: " + InnocenceMath.BandName(innocence);
            return view;
        }
    }
}
