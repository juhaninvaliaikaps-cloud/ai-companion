using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-170: one persona's desire and the user's libido setting. Libido's own reading lives
    /// with the innocence reading (<see cref="InnocenceState.ReadLibido"/>), which is the same
    /// local-only request.
    /// </summary>
    public sealed class DesireState
    {
        public const int FileVersion = 1;

        float _desire;
        float _userLibido = DesireMath.DefaultLibido;

        public float Desire
        {
            get { return _desire; }
            set { _desire = DesireMath.Clamp(value); }
        }

        /// <summary>UTC ticks of the last time step, or 0 when the state is new (never stepped).</summary>
        public long LastTicks;

        /// <summary>UTC ticks until which she is spent after a release, or 0.</summary>
        public long RefractoryUntilTicks;

        /// <summary>True once the user set the libido slider: a new reading then changes nothing until "Use the reading".</summary>
        public bool LibidoUserSet;

        public float UserLibido
        {
            get { return _userLibido; }
            set { _userLibido = DesireMath.ClampLibido(value); }
        }

        public bool IsNew
        {
            get { return LastTicks <= 0L; }
        }

        public bool InRefractory(DateTime nowUtc)
        {
            return RefractoryUntilTicks > 0L && nowUtc.ToUniversalTime().Ticks < RefractoryUntilTicks;
        }

        /// <summary>The libido she runs on: the user's setting, else the reading, else the middle.</summary>
        public float Libido(float readLibido)
        {
            if (LibidoUserSet)
                return UserLibido;
            return readLibido >= 0f ? DesireMath.ClampLibido(readLibido) : DesireMath.DefaultLibido;
        }

        public string Readout(float libido)
        {
            return "Desire: " + Desire.ToString("0", CultureInfo.InvariantCulture) + " (" + DesireMath.BandName(Desire) + "), libido "
                + DesireMath.ClampLibido(libido).ToString("0", CultureInfo.InvariantCulture);
        }

        [Serializable]
        class DesireFile
        {
            public int version;
            public float desire;
            public long lastTicks;
            public long refractoryUntilTicks;
            public bool libidoUserSet;
            public float userLibido = DesireMath.DefaultLibido;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(new DesireFile
            {
                version = FileVersion,
                desire = Desire,
                lastTicks = LastTicks,
                refractoryUntilTicks = RefractoryUntilTicks,
                libidoUserSet = LibidoUserSet,
                userLibido = UserLibido,
            }, true);
        }

        /// <summary>Reads the file; empty or broken files give a new state. Never null.</summary>
        public static DesireState FromJson(string json)
        {
            var state = new DesireState();
            if (string.IsNullOrWhiteSpace(json))
                return state;
            DesireFile file;
            try
            {
                file = JsonUtility.FromJson<DesireFile>(json);
            }
            catch (ArgumentException)
            {
                return state;
            }

            if (file == null)
                return state;
            state.Desire = file.desire;
            state.LastTicks = file.lastTicks > 0L ? file.lastTicks : 0L;
            state.RefractoryUntilTicks = file.refractoryUntilTicks > 0L ? file.refractoryUntilTicks : 0L;
            state.LibidoUserSet = file.libidoUserSet;
            state.UserLibido = file.userLibido;
            return state;
        }
    }

    /// <summary>
    /// JUH-170 on disk: one encrypted file per persona (<c>desire.json</c> or
    /// <c>desire-&lt;persona&gt;.json</c>). Wipe and export include it
    /// (<see cref="LocalDataInventory.IsDesireName"/>).
    /// </summary>
    public static class DesireStore
    {
        public const string FileName = "desire.json";

        public static string FileFor(string folder, string personaId)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var id = CompanionMoodStore.PersonaFileId(personaId);
            return Path.Combine(folder, id.Length == 0 ? FileName : "desire-" + id + ".json");
        }

        public static DesireState Load(string path)
        {
            var read = ProtectedLocalFile.Read(path);
            return read.Status == LocalReadStatus.Ready ? DesireState.FromJson(read.Text) : new DesireState();
        }

        public static bool Save(string path, DesireState state)
        {
            if (string.IsNullOrEmpty(path) || state == null)
                return false;
            return ProtectedLocalFile.Write(path, state.ToJson());
        }
    }

    /// <summary>One shown change of her desire, for the floating pieces.</summary>
    public struct DesireNotice
    {
        /// <summary>How much it moved.</summary>
        public float Delta;

        /// <summary>Where it is now.</summary>
        public float Desire;

        /// <summary>The release that spent it.</summary>
        public bool Release;
    }

    /// <summary>
    /// JUH-170: the hand-off between the desire keeper (settings side), chat and the floating
    /// pieces, like <see cref="InnocenceNow"/>. Main thread only. Empty and silent while the
    /// 18+ gate is closed.
    /// </summary>
    public static class DesireNow
    {
        static string _line = string.Empty;

        /// <summary>A shown change: a moment, a release, or time that moved it far enough to show.</summary>
        public static event Action<DesireNotice> Changed;

        public static string PromptLine
        {
            get { return _line; }
        }

        public static void SetPromptLine(string line)
        {
            _line = string.IsNullOrWhiteSpace(line) ? string.Empty : line.Trim();
        }

        /// <summary>The prompt with the desire line after it, or unchanged when there is none (the gate is closed).</summary>
        public static string Append(string prompt)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            if (_line.Length == 0)
                return body;
            return body.Length == 0 ? _line : body + "\n\n" + _line;
        }

        public static void Report(DesireNotice notice)
        {
            var handler = Changed;
            if (handler != null)
                handler(notice);
        }

        public static void Reset()
        {
            _line = string.Empty;
        }
    }
}
