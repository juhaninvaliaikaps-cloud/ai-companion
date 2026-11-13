using System;
using System.Globalization;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-119: how close she feels to the user. Pure state, saved per persona by
    /// <see cref="RelationshipStore"/>. Chat does not change the numbers yet; the system
    /// leaves <see cref="SecondsTogether"/> to the keeper and the later relationship leaf.
    /// </summary>
    public sealed class RelationshipState
    {
        public const int FileVersion = 1;

        float _affection;
        float _familiarity;
        double _secondsTogether;

        /// <summary>-100 (hurt) to 100 (devoted). Default 0.</summary>
        public float Affection
        {
            get { return _affection; }
            set { _affection = float.IsNaN(value) ? 0f : Mathf.Clamp(value, -100f, 100f); }
        }

        /// <summary>0 (stranger) to 100 (family). Default 0.</summary>
        public float Familiarity
        {
            get { return _familiarity; }
            set { _familiarity = float.IsNaN(value) ? 0f : Mathf.Clamp(value, 0f, 100f); }
        }

        /// <summary>Real seconds spent with the chat visible. Never negative.</summary>
        public double SecondsTogether
        {
            get { return _secondsTogether; }
            set { _secondsTogether = double.IsNaN(value) || value < 0d ? 0d : value; }
        }

        /// <summary>Adds frame time. NaN and negative values are ignored by the setter.</summary>
        public void AddSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0d)
                return;
            SecondsTogether = _secondsTogether + seconds;
        }

        /// <summary>"Relationship: +35, familiarity 12" for the mood readout.</summary>
        public string Readout()
        {
            return "Relationship: " + Affection.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture)
                + ", familiarity " + Familiarity.ToString("0.#", CultureInfo.InvariantCulture);
        }

        [Serializable]
        class RelationshipFile
        {
            public int version;
            public long savedUtcTicks;
            public float affection;
            public float familiarity;
            public double secondsTogether;
        }

        /// <summary>
        /// The relationship as JSON for the encrypted per-persona file. The timestamp is
        /// written for diagnostics; this state does not decay with time.
        /// </summary>
        public string ToJson(DateTime nowUtc)
        {
            var file = new RelationshipFile
            {
                version = FileVersion,
                savedUtcTicks = nowUtc.ToUniversalTime().Ticks,
                affection = float.IsNaN(_affection) ? 0f : _affection,
                familiarity = float.IsNaN(_familiarity) ? 0f : _familiarity,
                secondsTogether = double.IsNaN(_secondsTogether) ? 0d : _secondsTogether,
            };
            return JsonUtility.ToJson(file, true);
        }

        /// <summary>
        /// Reads a relationship file. Empty, broken and unknown files give the defaults;
        /// NaN numbers are treated as the default. Never returns null.
        /// </summary>
        public static RelationshipState FromJson(string json)
        {
            var state = new RelationshipState();
            if (string.IsNullOrWhiteSpace(json))
                return state;

            RelationshipFile file;
            try
            {
                file = JsonUtility.FromJson<RelationshipFile>(json);
            }
            catch (ArgumentException)
            {
                return state;
            }

            if (file == null)
                return state;

            state.Affection = file.affection;
            state.Familiarity = file.familiarity;
            state.SecondsTogether = file.secondsTogether;
            return state;
        }
    }
}
