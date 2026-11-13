using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>Temperament weights from the reading, 0 to 1 per preset. See <see cref="PersonaTemperamentBlend"/>.</summary>
    [Serializable]
    public sealed class SheetTemperament
    {
        public float calm;
        public float cheerful;
        public float shy;
        public float tsundere;
        public float moody;
        public string reason = string.Empty;

        public float Weight(string presetId)
        {
            switch (presetId)
            {
                case PersonaTemperaments.CalmId: return calm;
                case PersonaTemperaments.CheerfulId: return cheerful;
                case PersonaTemperaments.ShyId: return shy;
                case PersonaTemperaments.TsundereId: return tsundere;
                case PersonaTemperaments.MoodyId: return moody;
                default: return 0f;
            }
        }

        public void Set(string presetId, float weight)
        {
            var value = CharacterSheet.Clamp(weight, 0f, 1f);
            switch (presetId)
            {
                case PersonaTemperaments.CalmId: calm = value; break;
                case PersonaTemperaments.CheerfulId: cheerful = value; break;
                case PersonaTemperaments.ShyId: shy = value; break;
                case PersonaTemperaments.TsundereId: tsundere = value; break;
                case PersonaTemperaments.MoodyId: moody = value; break;
            }
        }
    }

    /// <summary>Who she is to the user when they first meet, read from the persona prompt (JUH-115).</summary>
    [Serializable]
    public sealed class SheetRelationship
    {
        /// <summary>One of <see cref="RelationshipRoles.All"/>. Unknown values become "stranger".</summary>
        public string role = RelationshipRoles.Stranger;

        /// <summary>The persona prompt's own word for it, such as "stepsister" or "coworker". May be empty.</summary>
        public string label = string.Empty;

        /// <summary>How much she likes the user at the start, -100 to 100.</summary>
        public float affection;

        /// <summary>How well they know each other at the start, 0 to 100.</summary>
        public float familiarity;

        /// <summary>
        /// True only when the persona prompt says they share a past. Seeded memories may
        /// mention the user only when this is true. Always false for a stranger.
        /// </summary>
        public bool sharedHistory;

        public string reason = string.Empty;
    }

    /// <summary>The relationship roles the sheet may name.</summary>
    public static class RelationshipRoles
    {
        public const string Stranger = "stranger";
        public const string Acquaintance = "acquaintance";
        public const string Friend = "friend";
        public const string CloseFriend = "close_friend";
        public const string Family = "family";
        public const string Partner = "partner";
        public const string Rival = "rival";

        public static readonly string[] All = { Stranger, Acquaintance, Friend, CloseFriend, Family, Partner, Rival };

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Stranger;
            var token = value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i] == token)
                    return All[i];
            }

            return Stranger;
        }
    }

    /// <summary>What lands badly or well with her, in her own terms. Short phrases.</summary>
    [Serializable]
    public sealed class SheetLanding
    {
        public List<string> touchy = new List<string>();
        public List<string> winsHerOver = new List<string>();
    }

    [Serializable]
    public sealed class SheetWeekEntry
    {
        /// <summary>How many days before the user arrived, 0 to 14.</summary>
        public int daysAgo;
        public string text = string.Empty;
    }

    [Serializable]
    public sealed class SheetThread
    {
        /// <summary>Something she is in the middle of, waiting for or dreading.</summary>
        public string text = string.Empty;

        /// <summary>One line each for how it could turn out. The away line picks one.</summary>
        public string goodOutcome = string.Empty;
        public string badOutcome = string.Empty;
    }

    /// <summary>Her life before the user arrived.</summary>
    [Serializable]
    public sealed class SheetWorld
    {
        public List<string> backstory = new List<string>();
        public List<SheetWeekEntry> lastWeek = new List<SheetWeekEntry>();
        public List<SheetThread> openThreads = new List<SheetThread>();

        /// <summary>What she was doing when the user arrived. Opens the first scene.</summary>
        public string rightNow = string.Empty;
    }

    /// <summary>
    /// The character sheet (JUH-117, "Pisara"): what the model read out of one persona
    /// prompt, written once and cached. Every leaf reads from it; no leaf has its own
    /// detector. <see cref="Parse"/> is tolerant of prose and code fences around the JSON,
    /// and <see cref="Normalize"/> clamps every number and caps every list, so a sheet that
    /// came back from here is always safe to use. See docs/PISARA.md.
    /// </summary>
    [Serializable]
    public sealed class CharacterSheet
    {
        public const int CurrentVersion = 1;

        public const string SourceModel = "model";
        public const string SourceKeywords = "keywords";
        public const string SourceDefault = "default";

        public const int MaxBackstory = 20;
        public const int MaxWeekEntries = 8;
        public const int MaxThreads = 3;
        public const int MaxLandingPhrases = 5;
        public const int MaxDaysAgo = 14;
        public const int MaxLineLength = 200;
        public const int MaxPhraseLength = 80;
        public const int MaxLabelLength = 40;

        public int version = CurrentVersion;

        /// <summary>Set by code, never by the model: the hash of the prompt this sheet was read from.</summary>
        public string promptHash = string.Empty;

        /// <summary>Set by code: "model", "keywords" (the offline fallback) or "default".</summary>
        public string source = SourceDefault;

        /// <summary>Set by code: when the sheet was made, ISO 8601 UTC.</summary>
        public string createdAtUtc = string.Empty;

        public SheetTemperament temperament = new SheetTemperament();
        public SheetRelationship relationship = new SheetRelationship();
        public SheetLanding landing = new SheetLanding();
        public SheetWorld world = new SheetWorld();

        /// <summary>A neutral sheet: balanced temperament, a stranger at 0 and 0, an empty world.</summary>
        public static CharacterSheet Neutral(string personaPrompt)
        {
            var sheet = new CharacterSheet { promptHash = HashPrompt(personaPrompt), source = SourceDefault };
            sheet.Normalize();
            return sheet;
        }

        /// <summary>True when this sheet was read from this prompt, so no new reading is needed.</summary>
        public bool IsFor(string personaPrompt)
        {
            return !string.IsNullOrEmpty(promptHash) && promptHash == HashPrompt(personaPrompt);
        }

        /// <summary>
        /// A stable hash of the persona prompt: line endings and outer whitespace do not
        /// change it, any other edit does.
        /// </summary>
        public static string HashPrompt(string personaPrompt)
        {
            var text = (personaPrompt ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Reads a sheet out of a model reply. Prose, a code fence or a trailing comma around
        /// the JSON object is fine. Returns null when there is no usable object, so the caller
        /// falls back. The result is normalized; <see cref="promptHash"/>, <see cref="source"/>
        /// and <see cref="createdAtUtc"/> are left for the caller to set.
        /// </summary>
        public static CharacterSheet Parse(string modelReply)
        {
            var json = ExtractObject(modelReply);
            if (json == null)
                return null;
            CharacterSheet sheet;
            try
            {
                sheet = JsonUtility.FromJson<CharacterSheet>(json);
            }
            catch (Exception)
            {
                return null;
            }

            if (sheet == null)
                return null;
            // The model does not get to claim these.
            sheet.promptHash = string.Empty;
            sheet.source = SourceModel;
            sheet.createdAtUtc = string.Empty;
            sheet.Normalize();
            return sheet;
        }

        /// <summary>Reads a sheet this app saved, keeping the hash, source and date. Null when unreadable.</summary>
        public static CharacterSheet FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                var sheet = JsonUtility.FromJson<CharacterSheet>(json);
                if (sheet == null)
                    return null;
                sheet.Normalize();
                return sheet;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string ToJson()
        {
            Normalize();
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>Clamps every number, trims and caps every text and list, and fills missing parts.</summary>
        public void Normalize()
        {
            version = CurrentVersion;
            promptHash = promptHash ?? string.Empty;
            createdAtUtc = createdAtUtc ?? string.Empty;
            if (source != SourceModel && source != SourceKeywords)
                source = SourceDefault;

            if (temperament == null)
                temperament = new SheetTemperament();
            temperament.calm = Clamp(temperament.calm, 0f, 1f);
            temperament.cheerful = Clamp(temperament.cheerful, 0f, 1f);
            temperament.shy = Clamp(temperament.shy, 0f, 1f);
            temperament.tsundere = Clamp(temperament.tsundere, 0f, 1f);
            temperament.moody = Clamp(temperament.moody, 0f, 1f);
            temperament.reason = Line(temperament.reason, MaxLineLength);

            if (relationship == null)
                relationship = new SheetRelationship();
            relationship.role = RelationshipRoles.Normalize(relationship.role);
            relationship.label = Line(relationship.label, MaxLabelLength);
            relationship.affection = Clamp(relationship.affection, -100f, 100f);
            relationship.familiarity = Clamp(relationship.familiarity, 0f, 100f);
            if (relationship.role == RelationshipRoles.Stranger)
                relationship.sharedHistory = false;
            relationship.reason = Line(relationship.reason, MaxLineLength);

            if (landing == null)
                landing = new SheetLanding();
            landing.touchy = Lines(landing.touchy, MaxLandingPhrases, MaxPhraseLength);
            landing.winsHerOver = Lines(landing.winsHerOver, MaxLandingPhrases, MaxPhraseLength);

            if (world == null)
                world = new SheetWorld();
            world.backstory = Lines(world.backstory, MaxBackstory, MaxLineLength);
            world.rightNow = Line(world.rightNow, MaxLineLength);

            var week = new List<SheetWeekEntry>();
            if (world.lastWeek != null)
            {
                for (var i = 0; i < world.lastWeek.Count && week.Count < MaxWeekEntries; i++)
                {
                    var entry = world.lastWeek[i];
                    if (entry == null)
                        continue;
                    var text = Line(entry.text, MaxLineLength);
                    if (text.Length == 0)
                        continue;
                    week.Add(new SheetWeekEntry { daysAgo = Math.Max(0, Math.Min(MaxDaysAgo, entry.daysAgo)), text = text });
                }
            }

            world.lastWeek = week;

            var threads = new List<SheetThread>();
            if (world.openThreads != null)
            {
                for (var i = 0; i < world.openThreads.Count && threads.Count < MaxThreads; i++)
                {
                    var thread = world.openThreads[i];
                    if (thread == null)
                        continue;
                    var text = Line(thread.text, MaxLineLength);
                    if (text.Length == 0)
                        continue;
                    threads.Add(new SheetThread
                    {
                        text = text,
                        goodOutcome = Line(thread.goodOutcome, MaxLineLength),
                        badOutcome = Line(thread.badOutcome, MaxLineLength),
                    });
                }
            }

            world.openThreads = threads;
        }

        internal static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
                return Math.Max(min, Math.Min(max, 0f));
            return Math.Max(min, Math.Min(max, value));
        }

        /// <summary>One clean line: no line breaks, single spaces, trimmed, capped.</summary>
        static string Line(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var sb = new StringBuilder(Math.Min(text.Length, maxLength));
            var space = false;
            for (var i = 0; i < text.Length && sb.Length < maxLength; i++)
            {
                var c = text[i];
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    space = sb.Length > 0;
                    continue;
                }

                if (space)
                {
                    sb.Append(' ');
                    space = false;
                    if (sb.Length >= maxLength)
                        break;
                }

                sb.Append(c);
            }

            return sb.ToString().TrimEnd();
        }

        static List<string> Lines(List<string> source, int maxCount, int maxLength)
        {
            var result = new List<string>();
            if (source == null)
                return result;
            for (var i = 0; i < source.Count && result.Count < maxCount; i++)
            {
                var line = Line(source[i], maxLength);
                if (line.Length > 0 && !result.Contains(line))
                    result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// The first complete JSON object in the text, with trailing commas removed. Null when
        /// there is none. Braces inside strings do not count.
        /// </summary>
        public static string ExtractObject(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var start = text.IndexOf('{');
            if (start < 0)
                return null;

            var sb = new StringBuilder(text.Length - start);
            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (inString)
                {
                    sb.Append(c);
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    sb.Append(c);
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                {
                    // Drop a trailing comma before the closer.
                    var last = sb.Length - 1;
                    while (last >= 0 && char.IsWhiteSpace(sb[last]))
                        last--;
                    if (last >= 0 && sb[last] == ',')
                        sb.Remove(last, 1);
                    depth--;
                }

                sb.Append(c);
                if (depth == 0)
                    return sb.ToString();
                if (depth < 0)
                    return null;
            }

            return null;
        }
    }
}
