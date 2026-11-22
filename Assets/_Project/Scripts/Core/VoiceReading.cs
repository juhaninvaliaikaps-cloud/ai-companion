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
        public const int MaxSamples = 2;

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
        public string[] samples = new string[0];

        public bool IsEmpty
        {
            get
            {
                return summary.Length == 0 && register.Length == 0 && rhythm.Length == 0 && vocabulary.Length == 0
                    && habits.Length == 0 && never.Length == 0 && happy.Length == 0 && annoyed.Length == 0
                    && flustered.Length == 0 && sad.Length == 0 && affectionate.Length == 0 && stranger.Length == 0
                    && close.Length == 0 && samples.Length == 0;
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
            samples = VoiceText.Items(samples, MaxSamples, MaxLine);
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
                samples = u.samples.Length > 0 ? u.samples : r.samples,
            };
        }

        /// <summary>The sheet in a model reply, or null. Prose or a code fence around the object is fine.</summary>
        public static VoiceSheet Parse(string reply)
        {
            var json = CharacterSheet.ExtractObject(reply);
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
        public string[] samples = new string[0];

        public bool IsEmpty
        {
            get { return holdingBack.Length == 0 && lettingGo.Length == 0 && habits.Length == 0 && never.Length == 0 && samples.Length == 0; }
        }

        public IntimateVoice Normalize()
        {
            holdingBack = VoiceText.Line(holdingBack);
            lettingGo = VoiceText.Line(lettingGo);
            habits = VoiceText.Items(habits, VoiceSheet.MaxItems);
            never = VoiceText.Items(never, VoiceSheet.MaxItems);
            samples = VoiceText.Items(samples, VoiceSheet.MaxSamples, VoiceSheet.MaxLine);
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
                samples = u.samples.Length > 0 ? u.samples : r.samples,
            };
        }

        public static IntimateVoice Parse(string reply)
        {
            var json = CharacterSheet.ExtractObject(reply);
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
        /// The 18+ input: the everyday input with her 18+ notes in the description, and her
        /// innocence band. Local only. <paramref name="personaPromptWithNotes"/> comes from
        /// <see cref="AdultNotesNow.ForReading"/>.
        /// </summary>
        public static string Intimate(string personaPromptWithNotes, TemperamentAxes axes, CharacterSheet sheet, float innocence)
        {
            return Everyday(personaPromptWithNotes, axes, sheet)
                + "\n- Innocent to perverted: " + InnocenceMath.Clamp(innocence).ToString("0", CultureInfo.InvariantCulture)
                + " of 100 (" + InnocenceMath.BandName(innocence) + ")";
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
    /// is experienced. Code adds the rules the model must not decide: samples are never repeated
    /// word for word, and only a confident or assertive one is told not to stammer.
    /// </summary>
    public static class VoicePrompt
    {
        public const string SamplesRule = "These show the feel only; never repeat them word for word.";
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

        /// <summary>The block, or empty when there is nothing read or written yet.</summary>
        public static string Block(string name, VoiceSheet voice, IntimateVoice intimate, CompanionMood mood, float moodStrength,
            float familiarity, bool gateOpen, float innocence, TemperamentAxes axes)
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
                Samples(voice.samples, sb);
            }

            if (adult)
            {
                var band = InnocenceMath.Band(innocence);
                string scene;
                if (band <= 1)
                    scene = Pick(intimate.holdingBack, intimate.lettingGo);
                else if (band >= 3)
                    scene = Pick(intimate.lettingGo, intimate.holdingBack);
                else
                    scene = Join(intimate.holdingBack, intimate.lettingGo);
                if (scene.Length > 0)
                    sb.Append("\n- In intimate scenes: ").Append(Sentence(scene).Trim());
                List("Intimate habits", intimate.habits, sb);
                List("Never in intimate scenes", intimate.never, sb);
                Samples(intimate.samples, sb);
            }

            sb.Append("\n- ").Append(Openings);
            if (axes != null && (axes.Value(TemperamentAxes.Confidence) >= PersonaManner.High || axes.Value(TemperamentAxes.Assertiveness) >= PersonaManner.High))
                sb.Append(' ').Append(NoStammer);
            return sb.ToString();
        }

        /// <summary>A key that changes exactly when <see cref="Block"/> would, for a given reading revision.</summary>
        public static int Key(VoiceSheet voice, CompanionMood mood, float moodStrength, float familiarity, bool gateOpen, float innocence)
        {
            var moodLine = MoodLine(voice, mood, moodStrength).Length > 0 ? (int)mood + 1 : 0;
            return moodLine
                + 100 * (familiarity >= CloseFrom ? 1 : 0)
                + 1000 * (gateOpen ? 1 + InnocenceMath.Band(innocence) : 0);
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

        static void Samples(string[] samples, StringBuilder sb)
        {
            if (samples == null || samples.Length == 0)
                return;
            sb.Append("\n- In her voice: ");
            for (var i = 0; i < samples.Length; i++)
            {
                if (i > 0)
                    sb.Append(" / ");
                sb.Append('"').Append(samples[i]).Append('"');
            }

            sb.Append(". ").Append(SamplesRule);
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

    /// <summary>JUH-171: the hand-off between the voice keeper and chat, like <see cref="InnocenceNow"/>. Main thread only.</summary>
    public static class VoiceNow
    {
        static string _block = string.Empty;
        static readonly List<string> _samples = new List<string>();

        public static string PromptBlock
        {
            get { return _block; }
        }

        /// <summary>The sample lines in the block now, for the repeated-phrase check.</summary>
        public static IList<string> Samples
        {
            get { return _samples; }
        }

        public static void Set(string block, string[] everydaySamples, string[] intimateSamples)
        {
            _block = string.IsNullOrWhiteSpace(block) ? string.Empty : block.Trim();
            _samples.Clear();
            if (_block.Length == 0)
                return;
            Add(everydaySamples);
            Add(intimateSamples);
        }

        static void Add(string[] samples)
        {
            if (samples == null)
                return;
            for (var i = 0; i < samples.Length; i++)
            {
                if (!string.IsNullOrEmpty(samples[i]))
                    _samples.Add(samples[i]);
            }
        }

        /// <summary>The prompt with the block at the end, or unchanged when there is none.</summary>
        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_block.Length == 0)
                return body;
            return body.Length == 0 ? _block : body + "\n\n" + _block;
        }

        public static void Reset()
        {
            _block = string.Empty;
            _samples.Clear();
        }
    }
}
