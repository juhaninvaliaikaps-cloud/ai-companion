using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-116: one persona's Innocent–Perverted state. <see cref="Current"/> is where she is
    /// now, <see cref="Baseline"/> where she drifts back to (the reading, or the user's slider).
    /// The reading's own value and reason are kept so "Use the reading" can go back to it.
    /// </summary>
    public sealed class InnocenceState
    {
        public const int FileVersion = 1;

        float _current = InnocenceMath.Default;
        float _baseline = InnocenceMath.Default;
        float _atLastMoment = InnocenceMath.Default;

        public float Current
        {
            get { return _current; }
            set { _current = InnocenceMath.Clamp(value); }
        }

        public float Baseline
        {
            get { return _baseline; }
            set { _baseline = InnocenceMath.Clamp(value); }
        }

        /// <summary>Where she was at her last intimate moment; drifting back is computed from it.</summary>
        public float AtLastMoment
        {
            get { return _atLastMoment; }
            set { _atLastMoment = InnocenceMath.Clamp(value); }
        }

        /// <summary>UTC ticks of the last intimate moment, or 0 for none.</summary>
        public long LastMomentTicks;

        /// <summary>True once the user set the slider: a new reading then changes nothing until "Use the reading".</summary>
        public bool UserSet;

        /// <summary>Prompt hash of the last reading, or empty when she was never read.</summary>
        public string ReadHash = string.Empty;

        /// <summary>The reading's own value, or -1 when there is none.</summary>
        public float ReadValue = -1f;

        public string ReadReason = string.Empty;

        /// <summary>
        /// JUH-170: her libido from the same reading (0 to 100), or -1 when the reading had none
        /// (a reading from before JUH-170: the keeper then reads once more for it).
        /// A file saved before the key existed also loads as -1. JsonUtility does not apply
        /// the -1 initializer, so a missing key would otherwise arrive as 0 and look already read.
        /// </summary>
        public float ReadLibido = -1f;

        public bool HasReading
        {
            get { return ReadValue >= 0f && !string.IsNullOrEmpty(ReadHash); }
        }

        /// <summary>Days since the last intimate moment, or NaN when there was none.</summary>
        public double DaysSinceMoment(DateTime nowUtc)
        {
            if (LastMomentTicks <= 0L)
                return double.NaN;
            return (nowUtc.ToUniversalTime() - new DateTime(LastMomentTicks, DateTimeKind.Utc)).TotalDays;
        }

        /// <summary>Sets her somewhere new and starts drifting from there (the slider, a reading, Start over).</summary>
        public void SetAt(float value, float baseline)
        {
            Current = value;
            Baseline = baseline;
            AtLastMoment = Current;
            LastMomentTicks = 0L;
        }

        public string Readout()
        {
            return "Innocence: " + Current.ToString("0", CultureInfo.InvariantCulture) + " (" + InnocenceMath.BandName(Current) + ")";
        }

        [Serializable]
        class InnocenceFile
        {
            public int version;
            public float current;
            public float baseline;
            public float atLastMoment;
            public long lastMomentTicks;
            public bool userSet;
            public string readHash;
            public float readValue = -1f;
            public string readReason;

            // The initializer does not survive JsonUtility.FromJson. FromJson treats a missing key as -1.
            public float readLibido = -1f;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(new InnocenceFile
            {
                version = FileVersion,
                current = Current,
                baseline = Baseline,
                atLastMoment = AtLastMoment,
                lastMomentTicks = LastMomentTicks,
                userSet = UserSet,
                readHash = ReadHash ?? string.Empty,
                readValue = ReadValue,
                readReason = ReadReason ?? string.Empty,
                readLibido = ReadLibido,
            }, true);
        }

        /// <summary>Reads the file; empty or broken files give the defaults. Never null.</summary>
        public static InnocenceState FromJson(string json)
        {
            var state = new InnocenceState();
            if (string.IsNullOrWhiteSpace(json))
                return state;
            InnocenceFile file;
            try
            {
                file = JsonUtility.FromJson<InnocenceFile>(json);
            }
            catch (ArgumentException)
            {
                return state;
            }

            if (file == null)
                return state;
            state.Current = file.current;
            state.Baseline = file.baseline;
            state.AtLastMoment = file.atLastMoment;
            state.LastMomentTicks = file.lastMomentTicks > 0L ? file.lastMomentTicks : 0L;
            state.UserSet = file.userSet;
            state.ReadHash = file.readHash ?? string.Empty;
            state.ReadValue = float.IsNaN(file.readValue) || file.readValue < 0f ? -1f : InnocenceMath.Clamp(file.readValue);
            state.ReadReason = file.readReason ?? string.Empty;
            // JUH-170 / JUH-188: JsonUtility leaves an omitted float at 0 and skips the -1 initializer.
            // InnocenceKeeper treats ReadLibido >= 0 as already read, so a file from before the key
            // existed would stick at libido 0 and never get the libido-only pass. A missing key is
            // not yet read. An explicit 0 stays 0 (the same rule as a missing temperament slider).
            state.ReadLibido = HasJsonKey(json, "readLibido")
                ? (float.IsNaN(file.readLibido) || file.readLibido < 0f ? -1f : DesireMath.ClampLibido(file.readLibido))
                : -1f;
            return state;
        }

        /// <summary>
        /// True when <paramref name="json"/> has <paramref name="key"/> as an object key.
        /// A mention inside a string value does not count. JsonUtility cannot tell a missing
        /// float from 0, so the caller checks the text.
        /// </summary>
        static bool HasJsonKey(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return false;
            var needle = "\"" + key + "\"";
            var inString = false;
            var escape = false;
            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c != '"')
                    continue;
                if (i + needle.Length <= json.Length
                    && string.CompareOrdinal(json, i, needle, 0, needle.Length) == 0)
                {
                    var after = i + needle.Length;
                    while (after < json.Length && char.IsWhiteSpace(json[after]))
                        after++;
                    if (after < json.Length && json[after] == ':')
                        return true;
                }

                inString = true;
            }

            return false;
        }
    }

    /// <summary>
    /// JUH-116 on disk: one encrypted file per persona (<c>innocence.json</c> or
    /// <c>innocence-&lt;persona&gt;.json</c>) next to the relationship file. Wipe and export
    /// include it (<see cref="LocalDataInventory.IsInnocenceName"/>).
    /// </summary>
    public static class InnocenceStore
    {
        public const string FileName = "innocence.json";

        public static string FileFor(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return Path.Combine(folder, id.Length == 0 ? FileName : "innocence-" + id + ".json");
        }

        public static InnocenceState Load(string path)
        {
            var read = ProtectedLocalFile.Read(path);
            return read.Status == LocalReadStatus.Ready ? InnocenceState.FromJson(read.Text) : new InnocenceState();
        }

        public static bool Save(string path, InnocenceState state)
        {
            if (string.IsNullOrEmpty(path) || state == null)
                return false;
            return ProtectedLocalFile.Write(path, state.ToJson());
        }
    }

    /// <summary>
    /// JUH-116: the hand-off between the innocence keeper (settings side) and chat, like
    /// <see cref="RelationshipNow"/>. Main thread only.
    /// </summary>
    public static class InnocenceNow
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

        /// <summary>The prompt with the innocence line after it, or unchanged when there is none (the gate is closed).</summary>
        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_line.Length == 0)
                return body;
            return body.Length == 0 ? _line : body + "\n\n" + _line;
        }

        public static void Reset()
        {
            _line = string.Empty;
        }
    }
}
