using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-171: how she talks, from the voice reading, the last Pisara reading. Short fixed fields
    /// so code can pick the line for her mood and closeness now. The model fills them; the user
    /// may overwrite any of them (<see cref="VoiceFile.UserEveryday"/>), and the user's text wins.
    /// </summary>
    [Serializable]
    public sealed class VoiceSheet
    {
        public const int MaxLine = 200;
        public const int MaxItem = 90;
        public const int MaxItems = 5;

        /// <summary>Lines in her voice per mood in the pool; the prompt gets 2 of them at a time, rotating.</summary>
        public const int MaxLinesPerMood = 3;

        public string summary = string.Empty;
        public string register = string.Empty;
        public string rhythm = string.Empty;
        public string vocabulary = string.Empty;
        public string[] habits = new string[0];
        public string[] never = new string[0];
        public string happy = string.Empty;
        public string annoyed = string.Empty;
        public string flustered = string.Empty;
        public string sad = string.Empty;
        public string affectionate = string.Empty;
        public string stranger = string.Empty;
        public string close = string.Empty;

        /// <summary>Her own lines per mood, the rotating pool (<see cref="VoicePrompt.Pool"/>).</summary>
        public VoiceLines lines = new VoiceLines();

        public bool IsEmpty
        {
            get
            {
                return summary.Length == 0 && register.Length == 0 && rhythm.Length == 0 && vocabulary.Length == 0
                    && habits.Length == 0 && never.Length == 0 && happy.Length == 0 && annoyed.Length == 0
                    && flustered.Length == 0 && sad.Length == 0 && affectionate.Length == 0 && stranger.Length == 0
                    && close.Length == 0 && (lines == null || lines.IsEmpty);
            }
        }

        /// <summary>Trims, caps lengths and counts, and replaces nulls. Returns this.</summary>
        public VoiceSheet Normalize()
        {
            summary = VoiceText.Line(summary);
            register = VoiceText.Line(register);
            rhythm = VoiceText.Line(rhythm);
            vocabulary = VoiceText.Line(vocabulary);
            habits = VoiceText.Items(habits, MaxItems);
            never = VoiceText.Items(never, MaxItems);
            happy = VoiceText.Line(happy);
            annoyed = VoiceText.Line(annoyed);
            flustered = VoiceText.Line(flustered);
            sad = VoiceText.Line(sad);
            affectionate = VoiceText.Line(affectionate);
            stranger = VoiceText.Line(stranger);
            close = VoiceText.Line(close);
            lines = (lines ?? new VoiceLines()).Normalize();
            return this;
        }

        /// <summary>Field by field: the user's text where it is not empty, the reading elsewhere. Either may be null.</summary>
        public static VoiceSheet Merge(VoiceSheet user, VoiceSheet read)
        {
            var u = (user ?? new VoiceSheet()).Normalize();
            var r = (read ?? new VoiceSheet()).Normalize();
            return new VoiceSheet
            {
                summary = VoiceText.Pick(u.summary, r.summary),
                register = VoiceText.Pick(u.register, r.register),
                rhythm = VoiceText.Pick(u.rhythm, r.rhythm),
                vocabulary = VoiceText.Pick(u.vocabulary, r.vocabulary),
                habits = u.habits.Length > 0 ? u.habits : r.habits,
                never = u.never.Length > 0 ? u.never : r.never,
                happy = VoiceText.Pick(u.happy, r.happy),
                annoyed = VoiceText.Pick(u.annoyed, r.annoyed),
                flustered = VoiceText.Pick(u.flustered, r.flustered),
                sad = VoiceText.Pick(u.sad, r.sad),
                affectionate = VoiceText.Pick(u.affectionate, r.affectionate),
                stranger = VoiceText.Pick(u.stranger, r.stranger),
                close = VoiceText.Pick(u.close, r.close),
                lines = VoiceLines.Merge(u.lines, r.lines),
            };
        }

        /// <summary>The sheet in a model reply, or null. Prose or a code fence around the object is fine.</summary>
        public static VoiceSheet Parse(string reply)
        {
            // JUH-178: a reply the model stopped halfway still gives the fields it finished.
            var json = CharacterSheet.ExtractObject(reply) ?? JsonRepair.CloseTruncated(reply);
            if (json == null)
                return null;
            VoiceSheet sheet;
            try
            {
                sheet = JsonUtility.FromJson<VoiceSheet>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (sheet == null)
                return null;
            sheet.Normalize();
            return sheet.IsEmpty ? null : sheet;
        }
    }

    /// <summary>JUH-171: how she talks in intimate scenes. 18+ only: read on the local model while the gate is open.</summary>
    [Serializable]
    public sealed class IntimateVoice
    {
        public string holdingBack = string.Empty;
        public string lettingGo = string.Empty;
        public string[] habits = new string[0];
        public string[] never = new string[0];

        /// <summary>Her own lines in intimate scenes, holding back and letting go: the rotating pool there.</summary>
        public IntimateLines lines = new IntimateLines();

        public bool IsEmpty
        {
            get { return holdingBack.Length == 0 && lettingGo.Length == 0 && habits.Length == 0 && never.Length == 0 && (lines == null || lines.IsEmpty); }
        }

        public IntimateVoice Normalize()
        {
            holdingBack = VoiceText.Line(holdingBack);
            lettingGo = VoiceText.Line(lettingGo);
            habits = VoiceText.Items(habits, VoiceSheet.MaxItems);
            never = VoiceText.Items(never, VoiceSheet.MaxItems);
            lines = (lines ?? new IntimateLines()).Normalize();
            return this;
        }

        public static IntimateVoice Merge(IntimateVoice user, IntimateVoice read)
        {
            var u = (user ?? new IntimateVoice()).Normalize();
            var r = (read ?? new IntimateVoice()).Normalize();
            return new IntimateVoice
            {
                holdingBack = VoiceText.Pick(u.holdingBack, r.holdingBack),
                lettingGo = VoiceText.Pick(u.lettingGo, r.lettingGo),
                habits = u.habits.Length > 0 ? u.habits : r.habits,
                never = u.never.Length > 0 ? u.never : r.never,
                lines = IntimateLines.Merge(u.lines, r.lines),
            };
        }

        public static IntimateVoice Parse(string reply)
        {
            // JUH-178: a reply the model stopped halfway still gives the fields it finished.
            var json = CharacterSheet.ExtractObject(reply) ?? JsonRepair.CloseTruncated(reply);
            if (json == null)
                return null;
            IntimateVoice voice;
            try
            {
                voice = JsonUtility.FromJson<IntimateVoice>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (voice == null)
                return null;
            voice.Normalize();
            return voice.IsEmpty ? null : voice;
        }
    }

    /// <summary>
    /// JUH-173: her own lines per mood, read by Pisara for this persona only. The prompt gets two
    /// at a time, rotating (<see cref="VoicePrompt.Pick"/>), so no single line is drilled in. Two
    /// lines that start with the same two words are not kept: the pool stays varied.
    /// </summary>
    [Serializable]
    public sealed class VoiceLines
    {
        public string[] content = new string[0];
        public string[] happy = new string[0];
        public string[] annoyed = new string[0];
        public string[] flustered = new string[0];
        public string[] sad = new string[0];
        public string[] affectionate = new string[0];

        public bool IsEmpty
        {
            get
            {
                return Count(content) + Count(happy) + Count(annoyed) + Count(flustered) + Count(sad) + Count(affectionate) == 0;
            }
        }

        static int Count(string[] items)
        {
            return items == null ? 0 : items.Length;
        }

        public VoiceLines Normalize()
        {
            var openings = new HashSet<string>(StringComparer.Ordinal);
            content = VoiceText.Varied(content, openings);
            happy = VoiceText.Varied(happy, openings);
            annoyed = VoiceText.Varied(annoyed, openings);
            flustered = VoiceText.Varied(flustered, openings);
            sad = VoiceText.Varied(sad, openings);
            affectionate = VoiceText.Varied(affectionate, openings);
            return this;
        }

        /// <summary>Mood by mood: the user's lines where they wrote some, the reading's elsewhere.</summary>
        public static VoiceLines Merge(VoiceLines user, VoiceLines read)
        {
            var u = user ?? new VoiceLines();
            var r = read ?? new VoiceLines();
            return new VoiceLines
            {
                content = Count(u.content) > 0 ? u.content : r.content ?? new string[0],
                happy = Count(u.happy) > 0 ? u.happy : r.happy ?? new string[0],
                annoyed = Count(u.annoyed) > 0 ? u.annoyed : r.annoyed ?? new string[0],
                flustered = Count(u.flustered) > 0 ? u.flustered : r.flustered ?? new string[0],
                sad = Count(u.sad) > 0 ? u.sad : r.sad ?? new string[0],
                affectionate = Count(u.affectionate) > 0 ? u.affectionate : r.affectionate ?? new string[0],
            };
        }

        /// <summary>The lines for a mood; empty for a mood without its own lines (sleepy, content uses <see cref="content"/>).</summary>
        public string[] For(CompanionMood mood)
        {
            switch (mood)
            {
                case CompanionMood.Happy: return happy;
                case CompanionMood.Annoyed: return annoyed;
                case CompanionMood.Flustered:
                case CompanionMood.Anxious: return flustered;
                case CompanionMood.Sad: return sad;
                case CompanionMood.Affectionate:
                case CompanionMood.Intimate: return affectionate;
                default: return new string[0];
            }
        }

        /// <summary>The everyday moods that have their own lines, in pool order.</summary>
        public static readonly string[] Keys = { "content", "happy", "annoyed", "flustered", "sad", "affectionate" };

        public string[] Get(string key)
        {
            switch (key)
            {
                case "content": return content ?? new string[0];
                case "happy": return happy ?? new string[0];
                case "annoyed": return annoyed ?? new string[0];
                case "flustered": return flustered ?? new string[0];
                case "sad": return sad ?? new string[0];
                case "affectionate": return affectionate ?? new string[0];
                default: return new string[0];
            }
        }

        public void Set(string key, string[] lines)
        {
            lines = lines ?? new string[0];
            switch (key)
            {
                case "content": content = lines; break;
                case "happy": happy = lines; break;
                case "annoyed": annoyed = lines; break;
                case "flustered": flustered = lines; break;
                case "sad": sad = lines; break;
                case "affectionate": affectionate = lines; break;
            }
        }

        public void AddAll(List<string> into)
        {
            Add(content, into);
            Add(happy, into);
            Add(annoyed, into);
            Add(flustered, into);
            Add(sad, into);
            Add(affectionate, into);
        }

        internal static void Add(string[] items, List<string> into)
        {
            if (items == null)
                return;
            for (var i = 0; i < items.Length; i++)
                into.Add(items[i]);
        }
    }

    /// <summary>JUH-173: her own lines in intimate scenes, 18+ only.</summary>
    [Serializable]
    public sealed class IntimateLines
    {
        public string[] holdingBack = new string[0];
        public string[] lettingGo = new string[0];

        public bool IsEmpty
        {
            get { return (holdingBack == null || holdingBack.Length == 0) && (lettingGo == null || lettingGo.Length == 0); }
        }

        public IntimateLines Normalize()
        {
            var openings = new HashSet<string>(StringComparer.Ordinal);
            holdingBack = VoiceText.Varied(holdingBack, openings);
            lettingGo = VoiceText.Varied(lettingGo, openings);
            return this;
        }

        public static IntimateLines Merge(IntimateLines user, IntimateLines read)
        {
            var u = user ?? new IntimateLines();
            var r = read ?? new IntimateLines();
            return new IntimateLines
            {
                holdingBack = u.holdingBack != null && u.holdingBack.Length > 0 ? u.holdingBack : r.holdingBack ?? new string[0],
                lettingGo = u.lettingGo != null && u.lettingGo.Length > 0 ? u.lettingGo : r.lettingGo ?? new string[0],
            };
        }

        public static readonly string[] Keys = { "holdingBack", "lettingGo" };

        public string[] Get(string key)
        {
            return key == "holdingBack" ? holdingBack ?? new string[0] : key == "lettingGo" ? lettingGo ?? new string[0] : new string[0];
        }

        public void Set(string key, string[] lines)
        {
            if (key == "holdingBack")
                holdingBack = lines ?? new string[0];
            else if (key == "lettingGo")
                lettingGo = lines ?? new string[0];
        }

        public void AddAll(List<string> into)
        {
            VoiceLines.Add(holdingBack, into);
            VoiceLines.Add(lettingGo, into);
        }
    }

    /// <summary>Small text helpers for the voice fields and the editor's list fields.</summary>
    public static class VoiceText
    {
        public static string Line(string value, int max = VoiceSheet.MaxLine)
        {
            var text = (value ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            return text.Length > max ? text.Substring(0, max).TrimEnd() : text;
        }

        public static string[] Items(string[] values, int maxCount, int maxLength = VoiceSheet.MaxItem)
        {
            var list = new List<string>();
            if (values != null)
            {
                for (var i = 0; i < values.Length && list.Count < maxCount; i++)
                {
                    var item = Line(values[i], maxLength).Trim('"', '“', '”');
                    if (item.Length > 0)
                        list.Add(item);
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// Up to <see cref="VoiceSheet.MaxLinesPerMood"/> lines, dropping any that starts with the
        /// same two words as a line already kept (in <paramref name="openings"/>, shared across moods).
        /// </summary>
        public static string[] Varied(string[] values, HashSet<string> openings)
        {
            var items = Items(values, VoiceSheet.MaxLinesPerMood * 2, VoiceSheet.MaxLine);
            var kept = new List<string>(VoiceSheet.MaxLinesPerMood);
            for (var i = 0; i < items.Length && kept.Count < VoiceSheet.MaxLinesPerMood; i++)
            {
                var words = RepeatedPhrases.Words(items[i]);
                var opening = words.Count == 0 ? string.Empty : words.Count == 1 ? words[0] : words[0] + " " + words[1];
                if (opening.Length > 0 && !openings.Add(opening))
                    continue;
                kept.Add(items[i]);
            }

            return kept.ToArray();
        }

        public static string Pick(string user, string read)
        {
            return string.IsNullOrEmpty(user) ? read ?? string.Empty : user;
        }

        /// <summary>The editor shows a list as one field, items split by ";".</summary>
        public static string Join(string[] items)
        {
            return items == null || items.Length == 0 ? string.Empty : string.Join("; ", items);
        }

        /// <summary>One editor field back to a list (empty items dropped).</summary>
        public static string[] Split(string text, int maxCount, int maxLength = VoiceSheet.MaxItem)
        {
            return Items(string.IsNullOrEmpty(text) ? new string[0] : text.Split(';'), maxCount, maxLength);
        }
    }

    /// <summary>
    /// JUH-171: what the voice reading is given. Built by code from the other readings, so the
    /// reading runs last and re-reads only when one of them changes (<see cref="Hash"/>).
    /// </summary>
    public static class VoiceInput
    {
        /// <summary>The everyday input: her description (gate-closed), her sliders, her mood temperament, how she starts with the user.</summary>
        public static string Everyday(string personaPrompt, TemperamentAxes axes, CharacterSheet sheet)
        {
            var sb = new StringBuilder();
            sb.Append("Her description:\n").Append((personaPrompt ?? string.Empty).Trim()).Append("\n\n");
            sb.Append("What the other readings found (already decided; build on them):\n");
            AppendAxes(sb, axes);
            if (sheet != null && sheet.temperament != null)
                sb.Append("- Mood temperament: ").Append(Temperament(sheet.temperament)).Append('\n');
            if (sheet != null && sheet.relationship != null)
            {
                var label = string.IsNullOrWhiteSpace(sheet.relationship.label) ? sheet.relationship.role : sheet.relationship.label;
                sb.Append("- How she starts with the user: ").Append(label ?? RelationshipRoles.Stranger).Append('\n');
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// The 18+ input: the everyday input with her 18+ notes in the description, her innocence
        /// and her libido (JUH-170; below 0 when there is none). Local only.
        /// <paramref name="personaPromptWithNotes"/> comes from <see cref="AdultNotesNow.ForReading"/>.
        /// </summary>
        public static string Intimate(string personaPromptWithNotes, TemperamentAxes axes, CharacterSheet sheet, float innocence, float libido = -1f)
        {
            var text = Everyday(personaPromptWithNotes, axes, sheet)
                + "\n- Innocent to perverted: " + InnocenceMath.Clamp(innocence).ToString("0", CultureInfo.InvariantCulture)
                + " of 100 (" + InnocenceMath.BandName(innocence) + ")";
            if (libido >= 0f)
                text += "\n- Libido, how relentless her needs are: " + DesireMath.ClampLibido(libido).ToString("0", CultureInfo.InvariantCulture)
                    + " of 100 (" + DesireMath.LibidoName(libido) + ")";
            return text;
        }

        /// <summary>
        /// The key the 18+ reading is saved under: bands, not exact values, so her slow drift in
        /// innocence (and a libido nudge) re-reads only when she moves into a new band.
        /// </summary>
        public static string IntimateHash(string personaPromptWithNotes, TemperamentAxes axes, CharacterSheet sheet, float innocence, float libido = -1f)
        {
            var key = Everyday(personaPromptWithNotes, axes, sheet) + "\ninnocence band " + InnocenceMath.Band(innocence)
                + (libido >= 0f ? "\nlibido band " + DesireMath.LibidoBand(libido) : string.Empty);
            return Hash(key);
        }

        /// <summary>
        /// The key a reading is saved under. The 18+ one uses the innocence band, not the exact
        /// value, so her slow drift re-reads only when she moves into a new band.
        /// </summary>
        public static string Hash(string input)
        {
            return CharacterSheet.HashPrompt(input ?? string.Empty);
        }

        static void AppendAxes(StringBuilder sb, TemperamentAxes axes)
        {
            if (axes == null)
                return;
            sb.Append("- Temperament sliders, 0 to 100 (50 is the middle): ");
            for (var i = 0; i < TemperamentAxes.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(TemperamentAxes.Names[i].ToLowerInvariant()).Append(' ').Append(axes.Value(i).ToString(CultureInfo.InvariantCulture));
            }

            sb.Append('\n');
        }

        static string Temperament(SheetTemperament t)
        {
            var names = new[] { "calm", "cheerful", "shy", "tsundere", "moody" };
            var weights = new[] { t.calm, t.cheerful, t.shy, t.tsundere, t.moody };
            var best = -1;
            for (var i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0.2f && (best < 0 || weights[i] > weights[best]))
                    best = i;
            }

            return best < 0 ? "balanced" : "mostly " + names[best];
        }
    }

    /// <summary>
    /// JUH-171: the "How she talks" block for chat, built by code from the reading. Only the line
    /// for her mood now and for how well she knows the user now goes in; the 18+ part only while
    /// the gate is open, said as "holding back" while she is innocent and "letting go" when she
    /// is experienced. Code adds the rules the model must not decide: her lines are never repeated
    /// word for word, and only a confident or assertive one is told not to stammer.
    /// </summary>
    public static class VoicePrompt
    {
        public const string Openings = "Vary how your sentences start.";
        public const string NoStammer = "Do not stammer or trail off with \"...\" unless she is truly lost for words.";

        /// <summary>Familiarity from which the "someone close" line is used.</summary>
        public const float CloseFrom = 50f;

        /// <summary>A mood under this strength picks no mood line.</summary>
        public const float MoodFrom = 0.25f;

        public static string MoodLine(VoiceSheet voice, CompanionMood mood, float strength)
        {
            if (voice == null || float.IsNaN(strength) || strength < MoodFrom)
                return string.Empty;
            switch (mood)
            {
                case CompanionMood.Happy: return voice.happy;
                case CompanionMood.Annoyed: return voice.annoyed;
                case CompanionMood.Flustered:
                case CompanionMood.Anxious: return voice.flustered;
                case CompanionMood.Sad: return voice.sad;
                case CompanionMood.Affectionate:
                case CompanionMood.Intimate: return voice.affectionate;
                default: return string.Empty;
            }
        }

        static string MoodWord(CompanionMood mood)
        {
            switch (mood)
            {
                case CompanionMood.Anxious: return "flustered";
                case CompanionMood.Intimate: return "affectionate";
                default: return CompanionMoodNames.Name(mood).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Which of the 18+ lines to use: 0 holding back, 1 both, 2 letting go. Her innocence
        /// decides first; very high desire (JUH-170, band 3 or more) moves her one step toward
        /// letting go, so even a shy one gives in. <paramref name="desire"/> below 0 means none.
        /// </summary>
        public static int IntimateStage(float innocence, float desire)
        {
            var band = InnocenceMath.Band(innocence);
            var stage = band <= 1 ? 0 : band == 2 ? 1 : 2;
            if (desire >= 0f && DesireMath.Band(desire) >= 3)
                stage = Math.Min(2, stage + 1);
            return stage;
        }

        /// <summary>The block, or empty when there is nothing read or written yet.</summary>
        public static string Block(string name, VoiceSheet voice, IntimateVoice intimate, CompanionMood mood, float moodStrength,
            float familiarity, bool gateOpen, float innocence, TemperamentAxes axes, float desire = -1f)
        {
            var everyday = voice != null && !voice.IsEmpty;
            var adult = gateOpen && intimate != null && !intimate.IsEmpty;
            if (!everyday && !adult)
                return string.Empty;

            var who = string.IsNullOrWhiteSpace(name) ? "she" : name.Trim();
            var sb = new StringBuilder("How ").Append(who).Append(" talks (keep to this in every reply):");
            if (everyday)
            {
                var overall = Sentence(voice.summary) + Labelled("Register", voice.register) + Labelled("Rhythm", voice.rhythm) + Labelled("Words", voice.vocabulary);
                if (overall.Length > 0)
                    sb.Append("\n- ").Append(overall.Trim());
                List("Habits", voice.habits, sb);
                List("Never", voice.never, sb);
                var moodLine = MoodLine(voice, mood, moodStrength);
                if (moodLine.Length > 0)
                    sb.Append("\n- Now, ").Append(MoodWord(mood)).Append(": ").Append(Sentence(moodLine).Trim());
                var closeness = familiarity >= CloseFrom ? voice.close : voice.stranger;
                if (closeness.Length > 0)
                    sb.Append("\n- With the user now (").Append(familiarity >= CloseFrom ? "someone close" : "still getting to know them").Append("): ").Append(Sentence(closeness).Trim());
            }

            if (adult)
            {
                var stage = IntimateStage(innocence, desire);
                string scene;
                if (stage == 0)
                    scene = Pick(intimate.holdingBack, intimate.lettingGo);
                else if (stage == 2)
                    scene = Pick(intimate.lettingGo, intimate.holdingBack);
                else
                    scene = Join(intimate.holdingBack, intimate.lettingGo);
                if (scene.Length > 0)
                    sb.Append("\n- In intimate scenes: ").Append(Sentence(scene).Trim());
                List("Intimate habits", intimate.habits, sb);
                List("Never in intimate scenes", intimate.never, sb);
            }

            sb.Append("\n- ").Append(Openings);
            if (axes != null && (axes.Value(TemperamentAxes.Confidence) >= PersonaManner.High || axes.Value(TemperamentAxes.Assertiveness) >= PersonaManner.High))
                sb.Append(' ').Append(NoStammer);
            return sb.ToString();
        }

        /// <summary>A key that changes exactly when <see cref="Block"/> or <see cref="Pool"/> would, for a given reading revision.</summary>
        public static int Key(VoiceSheet voice, CompanionMood mood, float moodStrength, float familiarity, bool gateOpen, float innocence, float desire = -1f)
        {
            var moodNow = !float.IsNaN(moodStrength) && moodStrength >= MoodFrom ? (int)mood + 1 : 0;
            return moodNow
                + 100 * (familiarity >= CloseFrom ? 1 : 0)
                + 1000 * (gateOpen ? 1 + IntimateStage(innocence, desire) : 0)
                + 10000 * (UsesIntimateLines(gateOpen, mood, moodStrength, desire) ? 1 : 0);
        }

        // ---- The rotating pool of her lines ----------------------------------------------

        public const string LinesIntro = "Lines only she would say, for the feel of this moment (never repeat them word for word):";

        /// <summary>Lines in the prompt per reply.</summary>
        public const int LinesShown = 2;

        /// <summary>
        /// True when this moment takes her intimate lines instead of her everyday ones: the gate is
        /// open and she is aroused (desire band 2 or more, JUH-170) or in an intimate mood.
        /// </summary>
        public static bool UsesIntimateLines(bool gateOpen, CompanionMood mood, float moodStrength, float desire)
        {
            if (!gateOpen)
                return false;
            if (desire >= 0f && DesireMath.Band(desire) >= 2)
                return true;
            return mood == CompanionMood.Intimate && !float.IsNaN(moodStrength) && moodStrength >= MoodFrom;
        }

        /// <summary>
        /// The lines that fit this moment, best first: in an intimate moment her holding-back or
        /// letting-go lines (by <see cref="IntimateStage"/>, both in between); otherwise her lines
        /// for the mood she is in, then her everyday ones.
        /// </summary>
        public static List<string> Pool(VoiceSheet voice, IntimateVoice intimate, CompanionMood mood, float moodStrength, bool gateOpen, float innocence, float desire)
        {
            var pool = new List<string>();
            if (UsesIntimateLines(gateOpen, mood, moodStrength, desire) && intimate != null && intimate.lines != null && !intimate.lines.IsEmpty)
            {
                var stage = IntimateStage(innocence, desire);
                if (stage != 2)
                    VoiceLines.Add(intimate.lines.holdingBack, pool);
                if (stage != 0)
                    VoiceLines.Add(intimate.lines.lettingGo, pool);
                if (pool.Count > 0)
                    return pool;
            }

            if (voice == null || voice.lines == null)
                return pool;
            if (!float.IsNaN(moodStrength) && moodStrength >= MoodFrom)
                VoiceLines.Add(voice.lines.For(mood), pool);
            VoiceLines.Add(voice.lines.content, pool);
            return pool;
        }

        /// <summary>
        /// Two lines from the pool for this reply, rotating with <paramref name="turn"/> so each
        /// reply sees a different pair. A line she has echoed lately (<paramref name="echoed"/>)
        /// is skipped; when every line is skipped, none is shown.
        /// </summary>
        public static List<string> Pick(IList<string> pool, int turn, Func<string, bool> echoed)
        {
            var picked = new List<string>(LinesShown);
            if (pool == null || pool.Count == 0)
                return picked;
            var start = (Math.Max(0, turn) * LinesShown) % pool.Count;
            for (var i = 0; i < pool.Count && picked.Count < LinesShown; i++)
            {
                var line = pool[(start + i) % pool.Count];
                if (echoed != null && echoed(line))
                    continue;
                picked.Add(line);
            }

            return picked;
        }

        /// <summary>The prompt line for the picked lines, or empty.</summary>
        public static string LinesLine(IList<string> picked)
        {
            if (picked == null || picked.Count == 0)
                return string.Empty;
            var sb = new StringBuilder(LinesIntro).Append(' ');
            for (var i = 0; i < picked.Count; i++)
            {
                if (i > 0)
                    sb.Append(" / ");
                sb.Append('"').Append(picked[i]).Append('"');
            }

            return sb.ToString();
        }

        static string Pick(string first, string second)
        {
            return string.IsNullOrEmpty(first) ? second ?? string.Empty : first;
        }

        static string Join(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
                return b ?? string.Empty;
            if (string.IsNullOrEmpty(b))
                return a;
            return Sentence(a) + " When she lets go: " + Sentence(b);
        }

        static string Sentence(string text)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
                return string.Empty;
            var last = text[text.Length - 1];
            return (last == '.' || last == '!' || last == '?') ? text + " " : text + ". ";
        }

        static string Labelled(string label, string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : label + ": " + Sentence(value);
        }

        static void List(string label, string[] items, StringBuilder sb)
        {
            if (items == null || items.Length == 0)
                return;
            sb.Append("\n- ").Append(label).Append(": ").Append(string.Join("; ", items)).Append('.');
        }

    }

    /// <summary>One of her lines and how many replies it has been shown in.</summary>
    [Serializable]
    public sealed class LineUse
    {
        public string text = string.Empty;
        public int shown;
    }

    /// <summary>
    /// JUH-173: when a line of the model's leaves the pool, and when a mood needs new lines. A
    /// line retires after <see cref="RetireAfter"/> replies, or at once when she echoed it. A
    /// mood (or an 18+ stage) with <see cref="RefreshAt"/> lines or fewer left is refreshed. The
    /// user's own lines never retire.
    /// </summary>
    public static class VoicePool
    {
        public const int RetireAfter = 6;
        public const int RefreshAt = 1;
        public const int MaxRetired = 40;

        public static bool ShouldRetire(int shown, bool echoed)
        {
            return echoed || shown >= RetireAfter;
        }

        public static bool NeedsRefresh(string[] readLines, string[] userLines)
        {
            if (userLines != null && userLines.Length > 0)
                return false;
            return readLines == null || readLines.Length <= RefreshAt;
        }

        /// <summary><paramref name="lines"/> without <paramref name="line"/>.</summary>
        public static string[] Without(string[] lines, string line)
        {
            var kept = new List<string>();
            if (lines != null)
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!string.Equals(lines[i], line, StringComparison.Ordinal))
                        kept.Add(lines[i]);
                }
            }

            return kept.ToArray();
        }

        /// <summary>The lines left plus the new ones, varied and capped (the lines left first).</summary>
        public static string[] Refill(string[] left, string[] fresh, IList<string> retired)
        {
            var all = new List<string>();
            if (left != null)
                all.AddRange(left);
            if (fresh != null)
            {
                for (var i = 0; i < fresh.Length; i++)
                {
                    if (retired == null || !retired.Contains(fresh[i]))
                        all.Add(fresh[i]);
                }
            }

            return VoiceText.Varied(all.ToArray(), new HashSet<string>(StringComparer.Ordinal));
        }

        /// <summary>The retired list with <paramref name="line"/> added, capped to the newest <see cref="MaxRetired"/>.</summary>
        public static string[] Retire(string[] retired, string line)
        {
            var list = new List<string>(retired ?? new string[0]);
            if (!list.Contains(line))
                list.Add(line);
            while (list.Count > MaxRetired)
                list.RemoveAt(0);
            return list.ToArray();
        }
    }

    /// <summary>
    /// JUH-171 on disk: one encrypted file per persona (<c>voice.json</c> or
    /// <c>voice-&lt;persona&gt;.json</c>) with both readings, the hash each was read from, and the
    /// user's own text. Wipe and export include it (<see cref="LocalDataInventory.IsVoiceName"/>).
    /// </summary>
    [Serializable]
    public sealed class VoiceFile
    {
        public const int CurrentVersion = 1;
        public const string FileName = "voice.json";

        public int version = CurrentVersion;
        public VoiceSheet everyday = new VoiceSheet();
        public string everydayHash = string.Empty;
        public string everydayBy = string.Empty;
        public IntimateVoice intimate = new IntimateVoice();
        public string intimateHash = string.Empty;
        public string intimateBy = string.Empty;
        public VoiceSheet userEveryday = new VoiceSheet();
        public IntimateVoice userIntimate = new IntimateVoice();

        /// <summary>How often each of the model's lines has been shown (JUH-173, the rotating pool).</summary>
        public LineUse[] uses = new LineUse[0];

        /// <summary>Lines retired from the pool, newest last, so a refresh does not write them again.</summary>
        public string[] retired = new string[0];

        public VoiceSheet UserEveryday
        {
            get { return userEveryday ?? (userEveryday = new VoiceSheet()); }
        }

        public IntimateVoice UserIntimate
        {
            get { return userIntimate ?? (userIntimate = new IntimateVoice()); }
        }

        public VoiceSheet Everyday
        {
            get { return VoiceSheet.Merge(userEveryday, everyday); }
        }

        public IntimateVoice Intimate
        {
            get { return IntimateVoice.Merge(userIntimate, intimate); }
        }

        public static string FileFor(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return Path.Combine(folder, id.Length == 0 ? FileName : "voice-" + id + ".json");
        }

        public static VoiceFile Load(string path)
        {
            var read = ProtectedLocalFile.Read(path);
            if (read.Status != LocalReadStatus.Ready || string.IsNullOrWhiteSpace(read.Text))
                return new VoiceFile();
            try
            {
                var file = JsonUtility.FromJson<VoiceFile>(read.Text) ?? new VoiceFile();
                file.everyday = (file.everyday ?? new VoiceSheet()).Normalize();
                file.intimate = (file.intimate ?? new IntimateVoice()).Normalize();
                file.UserEveryday.Normalize();
                file.UserIntimate.Normalize();
                file.everydayHash = file.everydayHash ?? string.Empty;
                file.intimateHash = file.intimateHash ?? string.Empty;
                file.everydayBy = file.everydayBy ?? string.Empty;
                file.intimateBy = file.intimateBy ?? string.Empty;
                file.uses = file.uses ?? new LineUse[0];
                file.retired = file.retired ?? new string[0];
                return file;
            }
            catch (ArgumentException)
            {
                return new VoiceFile();
            }
        }

        public static bool Save(string path, VoiceFile file)
        {
            if (string.IsNullOrEmpty(path) || file == null)
                return false;
            return ProtectedLocalFile.Write(path, JsonUtility.ToJson(file, true));
        }
    }

    /// <summary>
    /// JUH-173: the hand-off between the voice keeper and chat, like <see cref="InnocenceNow"/>.
    /// The block goes at the end of the prompt; two of her lines go higher up, picked per reply
    /// from the pool for this moment (<see cref="AppendLines"/>). Main thread only.
    /// </summary>
    public static class VoiceNow
    {
        static string _block = string.Empty;
        static readonly List<string> _pool = new List<string>();
        static readonly List<string> _all = new List<string>();
        static readonly List<string> _shown = new List<string>();
        static readonly List<string> _echoed = new List<string>();

        public static string PromptBlock
        {
            get { return _block; }
        }

        /// <summary>The lines that fit this moment (the rotating pool).</summary>
        public static IList<string> Pool
        {
            get { return _pool; }
        }

        /// <summary>Every line of hers in use, for the copied-line check.</summary>
        public static IList<string> Samples
        {
            get { return _all; }
        }

        public static void Set(string block, IList<string> pool, IList<string> all)
        {
            _block = string.IsNullOrWhiteSpace(block) ? string.Empty : block.Trim();
            _pool.Clear();
            _all.Clear();
            if (pool != null)
                _pool.AddRange(pool);
            if (all != null)
                _all.AddRange(all);
        }

        /// <summary>The prompt with the block at the end, or unchanged when there is none.</summary>
        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_block.Length == 0)
                return body;
            return body.Length == 0 ? _block : body + "\n\n" + _block;
        }

        /// <summary>
        /// The prompt with two of her lines for this reply, or unchanged when there are none.
        /// <paramref name="turn"/> rotates them; a line one of <paramref name="recentReplies"/>
        /// echoed is skipped.
        /// </summary>
        public static string AppendLines(string prompt, int turn, IList<string> recentReplies, bool count = true)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_pool.Count == 0)
                return body;
            var picked = VoicePrompt.Pick(_pool, turn, l =>
            {
                if (!RepeatedPhrases.Echoes(recentReplies, l))
                    return false;
                if (count && !_echoed.Contains(l))
                    _echoed.Add(l);
                return true;
            });
            if (count)
                _shown.AddRange(picked);
            var line = VoicePrompt.LinesLine(picked);
            if (line.Length == 0)
                return body;
            return body.Length == 0 ? line : body + "\n" + line;
        }

        /// <summary>
        /// For the keeper: the lines shown and the lines she echoed since the last call, then
        /// cleared. Returns false (and touches nothing) when there were none.
        /// </summary>
        public static bool TakeUsage(List<string> shown, List<string> echoed)
        {
            if (_shown.Count == 0 && _echoed.Count == 0)
                return false;
            shown.AddRange(_shown);
            echoed.AddRange(_echoed);
            _shown.Clear();
            _echoed.Clear();
            return true;
        }

        public static void Reset()
        {
            _block = string.Empty;
            _pool.Clear();
            _all.Clear();
            _shown.Clear();
            _echoed.Clear();
        }
    }
}
