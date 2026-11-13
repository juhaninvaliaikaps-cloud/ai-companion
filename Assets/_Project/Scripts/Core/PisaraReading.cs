using System;
using System.Globalization;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// How a persona's temperament is chosen (JUH-118). Stored in the persona file as
    /// <c>temperamentMode</c>. Blank is the rule for files written before this key existed.
    /// </summary>
    public static class TemperamentModes
    {
        public const string Auto = "auto";
        public const string Manual = "manual";

        /// <summary>"auto", "manual" or "" (not set).</summary>
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var token = value.Trim();
            if (string.Equals(token, Auto, StringComparison.OrdinalIgnoreCase))
                return Auto;
            if (string.Equals(token, Manual, StringComparison.OrdinalIgnoreCase))
                return Manual;
            return string.Empty;
        }

        /// <summary>
        /// True when the temperament is picked by hand, so the sheet does not decide it.
        /// With no mode stored: a built-in persona keeps its hand-picked value, and a user
        /// persona is manual only when it names a preset.
        /// </summary>
        public static bool IsManual(string mode, string preset, bool builtIn)
        {
            var normal = Normalize(mode);
            if (normal == Manual)
                return true;
            if (normal == Auto)
                return false;
            if (builtIn)
                return true;
            return PersonaTemperaments.Normalize(preset).Length > 0;
        }
    }

    /// <summary>The temperament a persona runs on, and the line persona settings show for it.</summary>
    public struct TemperamentChoice
    {
        /// <summary>Never null.</summary>
        public PersonaTemperament Temperament;

        /// <summary>True when it comes from the sheet.</summary>
        public bool Auto;

        /// <summary>"Temperament: Auto (Shy 60%, Cheerful 40%): reason" or "Temperament: Tsundere (your pick)".</summary>
        public string Line;
    }

    /// <summary>
    /// Pure helpers around one reading (JUH-118): which temperament applies, what persona
    /// settings and the log show, and how a sheet is stamped before it is saved.
    /// </summary>
    public static class PisaraReading
    {
        public const string LogPrefix = "Pisara reading: ";
        public const string WaitingText = "reading her description...";

        /// <summary>A manual pick wins; otherwise the sheet decides, and no sheet is Balanced.</summary>
        public static TemperamentChoice Choose(string mode, string preset, bool builtIn, CharacterSheet sheet)
        {
            if (TemperamentModes.IsManual(mode, preset, builtIn))
            {
                var picked = PersonaTemperaments.Get(preset);
                return new TemperamentChoice
                {
                    Temperament = picked,
                    Auto = false,
                    Line = "Temperament: " + picked.Name + (builtIn && TemperamentModes.Normalize(mode).Length == 0 ? " (built-in)" : " (your pick)"),
                };
            }

            if (sheet == null)
            {
                return new TemperamentChoice
                {
                    Temperament = PersonaTemperament.Default,
                    Auto = true,
                    Line = "Temperament: Auto (" + PersonaTemperament.Default.Name + "): " + WaitingText,
                };
            }

            var reading = PersonaTemperamentBlend.From(sheet.temperament);
            return new TemperamentChoice
            {
                Temperament = reading.Temperament ?? PersonaTemperament.Default,
                Auto = true,
                Line = "Temperament: Auto (" + reading.Summary + ")" + Reason(sheet.temperament == null ? null : sheet.temperament.reason, sheet),
            };
        }

        /// <summary>"Relationship: stepsister (family), affection 25, familiarity 70: grew up together".</summary>
        public static string RelationshipLine(CharacterSheet sheet)
        {
            if (sheet == null || sheet.relationship == null)
                return "Relationship: " + WaitingText;
            var r = sheet.relationship;
            var role = RelationshipRoles.Normalize(r.role).Replace('_', ' ');
            var who = string.IsNullOrWhiteSpace(r.label) ? role : r.label.Trim() + " (" + role + ")";
            return "Relationship: " + who
                + ", affection " + Whole(r.affection)
                + ", familiarity " + Whole(r.familiarity)
                + (r.sharedHistory ? ", shared past" : string.Empty)
                + Reason(r.reason, sheet);
        }

        /// <summary>Where the sheet came from, for persona settings.</summary>
        public static string SourceLine(CharacterSheet sheet)
        {
            if (sheet == null)
                return "Not read yet.";
            if (sheet.source == CharacterSheet.SourceModel)
                return "Read by the model" + When(sheet) + ".";
            if (sheet.source == CharacterSheet.SourceKeywords)
                return "The model did not answer, so this is a keyword guess" + When(sheet) + ". Re-read tries the model again.";
            return "The model did not answer, so these are neutral defaults" + When(sheet) + ". Re-read tries the model again.";
        }

        /// <summary>The short text after "Pisara reading: persona = ". No backstory: the log is not encrypted.</summary>
        public static string Summary(CharacterSheet sheet)
        {
            if (sheet == null)
                return "none";
            var sb = new StringBuilder();
            sb.Append(PersonaTemperamentBlend.From(sheet.temperament).Summary);
            var r = sheet.relationship ?? new SheetRelationship();
            sb.Append("; ").Append(RelationshipRoles.Normalize(r.role));
            sb.Append(", affection ").Append(Whole(r.affection));
            sb.Append(", familiarity ").Append(Whole(r.familiarity));
            var world = sheet.world ?? new SheetWorld();
            sb.Append("; world ").Append(world.backstory == null ? 0 : world.backstory.Count).Append(" facts, ");
            sb.Append(world.lastWeek == null ? 0 : world.lastWeek.Count).Append(" week entries, ");
            sb.Append(world.openThreads == null ? 0 : world.openThreads.Count).Append(" threads");
            sb.Append("; source ").Append(string.IsNullOrEmpty(sheet.source) ? CharacterSheet.SourceDefault : sheet.source);
            return sb.ToString();
        }

        /// <summary><c>Pisara reading: friend = Cheerful 72%; friend, affection 40, ...</c></summary>
        public static string LogLine(string personaId, CharacterSheet sheet)
        {
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return LogPrefix + (id.Length == 0 ? "(default)" : id) + " = " + Summary(sheet);
        }

        static readonly string[] KinWords =
        {
            "sister", "brother", "sibling", "mother", "father", "mom", "mum", "dad", "parent", "daughter", "son",
            "cousin", "aunt", "uncle", "niece", "nephew", "grandmother", "grandfather", "grandma", "grandpa",
            "granddaughter", "grandson", "twin",
        };

        /// <summary>
        /// True when the label names a relative ("stepsister", "older brother", "mother-in-law").
        /// Whole words only, so "sorority sister" counts but "person" or "season" do not.
        /// </summary>
        public static bool IsKinLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;
            var words = label.ToLowerInvariant().Split(new[] { ' ', '-', '_', '/', ',', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < words.Length; i++)
            {
                var word = words[i];
                if (word.StartsWith("step", StringComparison.Ordinal) && word.Length > 4)
                    word = word.Substring(4);
                else if (word.StartsWith("half", StringComparison.Ordinal) && word.Length > 4)
                    word = word.Substring(4);
                for (var k = 0; k < KinWords.Length; k++)
                {
                    if (word == KinWords[k])
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Local models often call a stepsister a "close friend". The label is the description's
        /// own word, so a kinship label makes the role family. A partner stays a partner.
        /// </summary>
        public static void ApplyKinship(CharacterSheet sheet)
        {
            if (sheet == null || sheet.relationship == null)
                return;
            var r = sheet.relationship;
            if (r.role == RelationshipRoles.Family || r.role == RelationshipRoles.Partner)
                return;
            if (IsKinLabel(r.label))
                r.role = RelationshipRoles.Family;
        }

        /// <summary>From this familiarity up, the model itself says they know each other well.</summary>
        public const float SharedHistoryFamiliarity = 50f;

        /// <summary>
        /// Makes the model's own answers agree: local models sometimes rate a stepsister of six
        /// years at familiarity 60 and still answer "no shared past". Someone who is not a
        /// stranger and is known this well shares a past. Never the other way round, and never
        /// for a stranger.
        /// </summary>
        public static void ApplySharedHistory(CharacterSheet sheet)
        {
            if (sheet == null || sheet.relationship == null)
                return;
            var r = sheet.relationship;
            if (r.role != RelationshipRoles.Stranger && r.familiarity >= SharedHistoryFamiliarity)
                r.sharedHistory = true;
        }

        /// <summary>
        /// Stamps the fields the model may not set, and clamps everything. Call before saving.
        /// </summary>
        public static CharacterSheet Finish(CharacterSheet sheet, string personaPrompt, string source, DateTime utcNow)
        {
            if (sheet == null)
                sheet = new CharacterSheet();
            sheet.version = CharacterSheet.CurrentVersion;
            sheet.promptHash = CharacterSheet.HashPrompt(personaPrompt);
            sheet.source = source == CharacterSheet.SourceModel || source == CharacterSheet.SourceKeywords
                ? source
                : CharacterSheet.SourceDefault;
            sheet.createdAtUtc = utcNow.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            sheet.Normalize();
            return sheet;
        }

        /// <summary>
        /// The sheet when the model gave nothing usable: the keyword scorer's temperament when
        /// there is one, neutral defaults for everything else.
        /// </summary>
        public static CharacterSheet Fallback(string personaPrompt, SheetTemperament keywords, DateTime utcNow)
        {
            var sheet = CharacterSheet.Neutral(personaPrompt);
            if (keywords == null)
                return Finish(sheet, personaPrompt, CharacterSheet.SourceDefault, utcNow);
            sheet.temperament = keywords;
            return Finish(sheet, personaPrompt, CharacterSheet.SourceKeywords, utcNow);
        }

        static string Reason(string reason, CharacterSheet sheet)
        {
            if (sheet != null && sheet.source != CharacterSheet.SourceModel)
                return string.Empty;
            return string.IsNullOrWhiteSpace(reason) ? string.Empty : ": " + reason.Trim();
        }

        static string When(CharacterSheet sheet)
        {
            DateTime at;
            if (string.IsNullOrEmpty(sheet.createdAtUtc)
                || !DateTime.TryParse(sheet.createdAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out at))
                return string.Empty;
            return " on " + at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        static string Whole(float value)
        {
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The offline temperament guess for when no model answers (JUH-114 scorer, built by
    /// DeepSeek in JUH-119). That class is not on main yet, so this returns null and the
    /// fallback is neutral defaults. When <c>TemperamentKeywordScorer</c> lands, map its scores
    /// into a <see cref="SheetTemperament"/> here; nothing else needs to change.
    /// </summary>
    public static class PisaraKeywords
    {
        /// <summary>Weights from the keyword scorer, or null when there is no scorer or no clear match.</summary>
        public static SheetTemperament Temperament(string personaPrompt)
        {
            return null;
        }
    }
}
