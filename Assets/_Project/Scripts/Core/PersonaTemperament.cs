using System;

namespace AICompanion.Core
{
    /// <summary>
    /// How one persona's moods behave (JUH-103 phase 3). There are no new moods; a
    /// temperament only scales how fast each mood builds and fades, and where she settles:
    /// <list type="bullet">
    /// <item><see cref="Baseline"/>: a mood she settles back to, held at <see cref="BaselineStrength"/> (enough to show).</item>
    /// <item><see cref="RestBias"/>: a mood kept at a low floor (below showing), so it shows sooner.</item>
    /// <item><see cref="Gain"/>: multiplies every push into a mood.</item>
    /// <item><see cref="HalfLifeScale"/>: multiplies a mood's half-life (above 1 fades slower).</item>
    /// </list>
    /// <see cref="Default"/> is exactly the phase 1/2 behaviour. Intimate is never a baseline or a
    /// bias, and its gain is only used while Intimate is allowed (<see cref="IntimateExpressions.Active"/>).
    /// Applied by <see cref="CompanionMoodState"/> (decay) and <see cref="CompanionMoodTable"/> (pushes) only.
    /// </summary>
    public sealed class PersonaTemperament
    {
        public const float BaselineStrength = 0.35f;
        public const float MaxRestBias = 0.25f;
        public const float MinScale = 0.25f;
        public const float MaxScale = 3f;

        /// <summary>
        /// Seconds a floor takes to pull a weaker mood about two thirds of the way up. Moods start
        /// on their floors and never fade below them, so this only matters for an older mood file.
        /// </summary>
        public const float RestPullSeconds = 600f;

        readonly float[] _gain = Ones();
        readonly float[] _halfLife = Ones();

        /// <summary>Preset id, such as "tsundere". "default" for <see cref="Default"/>.</summary>
        public string Id { get; private set; }

        /// <summary>The name for the readout and the picker, such as "Tsundere".</summary>
        public string Name { get; private set; }

        public CompanionMood Baseline { get; private set; }
        public CompanionMood RestBias { get; private set; }
        public float RestBiasStrength { get; private set; }

        public PersonaTemperament(string id, string name)
        {
            Id = string.IsNullOrWhiteSpace(id) ? PersonaTemperaments.DefaultId : id.Trim().ToLowerInvariant();
            Name = string.IsNullOrWhiteSpace(name) ? Id : name.Trim();
            Baseline = CompanionMood.Content;
            RestBias = CompanionMood.Content;
        }

        public static PersonaTemperament Default
        {
            get { return PersonaTemperaments.Get(PersonaTemperaments.DefaultId); }
        }

        /// <summary>True when every number is the phase 1/2 one.</summary>
        public bool IsNeutral
        {
            get
            {
                if (Baseline != CompanionMood.Content || RestBias != CompanionMood.Content)
                    return false;
                for (var i = 0; i < _gain.Length; i++)
                {
                    if (_gain[i] != 1f || _halfLife[i] != 1f)
                        return false;
                }

                return true;
            }
        }

        /// <summary>The multiplier for a push into this mood. Intimate is never boosted unless it is allowed.</summary>
        public float Gain(CompanionMood mood, bool intimateAllowed)
        {
            var index = (int)mood;
            if (index <= 0 || index >= _gain.Length)
                return 1f;
            if (mood == CompanionMood.Intimate && !intimateAllowed)
                return 1f;
            return _gain[index];
        }

        public float HalfLifeScale(CompanionMood mood)
        {
            var index = (int)mood;
            return index > 0 && index < _halfLife.Length ? _halfLife[index] : 1f;
        }

        /// <summary>
        /// The floor this temperament keeps under a mood at rest: <see cref="BaselineStrength"/>
        /// for the baseline, the bias strength for the bias, else 0. Never for Intimate.
        /// </summary>
        public float RestFloor(CompanionMood mood)
        {
            if (mood == CompanionMood.Content || mood == CompanionMood.Intimate)
                return 0f;
            var floor = 0f;
            if (mood == Baseline)
                floor = BaselineStrength;
            if (mood == RestBias && RestBiasStrength > floor)
                floor = RestBiasStrength;
            return floor;
        }

        // ---- building one ---------------------------------------------------------------------

        public PersonaTemperament WithGain(CompanionMood mood, float scale)
        {
            Set(_gain, mood, scale);
            return this;
        }

        public PersonaTemperament WithHalfLife(CompanionMood mood, float scale)
        {
            Set(_halfLife, mood, scale);
            return this;
        }

        public PersonaTemperament WithBaseline(CompanionMood mood)
        {
            Baseline = mood == CompanionMood.Intimate || (int)mood < 0 || (int)mood >= CompanionMoodNames.Count
                ? CompanionMood.Content
                : mood;
            return this;
        }

        public PersonaTemperament WithRestBias(CompanionMood mood, float strength)
        {
            if (mood == CompanionMood.Intimate || (int)mood <= 0 || (int)mood >= CompanionMoodNames.Count
                || float.IsNaN(strength) || strength <= 0f)
            {
                RestBias = CompanionMood.Content;
                RestBiasStrength = 0f;
                return this;
            }

            RestBias = mood;
            RestBiasStrength = Math.Min(MaxRestBias, strength);
            return this;
        }

        static void Set(float[] table, CompanionMood mood, float scale)
        {
            var index = (int)mood;
            if (index <= 0 || index >= table.Length || float.IsNaN(scale))
                return;
            table[index] = Math.Max(MinScale, Math.Min(MaxScale, scale));
        }

        static float[] Ones()
        {
            var values = new float[CompanionMoodNames.Count];
            for (var i = 0; i < values.Length; i++)
                values[i] = 1f;
            return values;
        }
    }

    /// <summary>
    /// The temperament presets the persona picker offers. A persona file stores the preset id
    /// ("temperament": "tsundere"), never raw numbers. A blank or unknown id is the default.
    /// </summary>
    public static class PersonaTemperaments
    {
        public const string DefaultId = "default";
        public const string CalmId = "calm";
        public const string CheerfulId = "cheerful";
        public const string ShyId = "shy";
        public const string TsundereId = "tsundere";
        public const string MoodyId = "moody";

        /// <summary>The picker order.</summary>
        public static readonly string[] Ids = { DefaultId, CalmId, CheerfulId, ShyId, TsundereId, MoodyId };

        static readonly PersonaTemperament[] Presets = Build();

        static PersonaTemperament[] Build()
        {
            return new[]
            {
                // Exactly phase 1/2.
                new PersonaTemperament(DefaultId, "Balanced"),

                // Hard to ruffle: worry and irritation build slowly and pass quickly.
                new PersonaTemperament(CalmId, "Calm")
                    .WithGain(CompanionMood.Annoyed, 0.7f).WithGain(CompanionMood.Anxious, 0.7f)
                    .WithGain(CompanionMood.Flustered, 0.8f)
                    .WithHalfLife(CompanionMood.Annoyed, 0.6f).WithHalfLife(CompanionMood.Anxious, 0.6f)
                    .WithHalfLife(CompanionMood.Sad, 0.8f),

                // Rests on a light smile; good moods build fast and last, bad ones pass.
                new PersonaTemperament(CheerfulId, "Cheerful")
                    .WithBaseline(CompanionMood.Happy)
                    .WithGain(CompanionMood.Happy, 1.3f).WithGain(CompanionMood.Affectionate, 1.1f)
                    .WithGain(CompanionMood.Sad, 0.8f).WithGain(CompanionMood.Annoyed, 0.8f)
                    .WithHalfLife(CompanionMood.Happy, 1.5f)
                    .WithHalfLife(CompanionMood.Sad, 0.7f).WithHalfLife(CompanionMood.Annoyed, 0.7f),

                // Flustered and anxious come easily and linger a little.
                new PersonaTemperament(ShyId, "Shy")
                    .WithRestBias(CompanionMood.Flustered, 0.15f)
                    .WithGain(CompanionMood.Flustered, 1.4f).WithGain(CompanionMood.Anxious, 1.2f)
                    .WithGain(CompanionMood.Affectionate, 0.9f).WithGain(CompanionMood.Annoyed, 0.8f)
                    .WithHalfLife(CompanionMood.Flustered, 1.5f),

                // Annoyed builds fast and lingers; affection builds slowly and fades fast.
                new PersonaTemperament(TsundereId, "Tsundere")
                    .WithRestBias(CompanionMood.Annoyed, 0.15f)
                    .WithGain(CompanionMood.Annoyed, 1.4f).WithGain(CompanionMood.Affectionate, 0.7f)
                    .WithGain(CompanionMood.Flustered, 1.2f)
                    .WithHalfLife(CompanionMood.Annoyed, 1.3f).WithHalfLife(CompanionMood.Affectionate, 0.7f),

                // Low moods come easily and stay; happiness is shorter.
                new PersonaTemperament(MoodyId, "Moody")
                    .WithRestBias(CompanionMood.Sad, 0.15f)
                    .WithGain(CompanionMood.Sad, 1.3f).WithGain(CompanionMood.Annoyed, 1.2f)
                    .WithGain(CompanionMood.Anxious, 1.2f).WithGain(CompanionMood.Happy, 0.8f)
                    .WithHalfLife(CompanionMood.Sad, 1.4f).WithHalfLife(CompanionMood.Annoyed, 1.2f)
                    .WithHalfLife(CompanionMood.Happy, 0.7f),
            };
        }

        /// <summary>The preset id for a stored value: a known id or name, else "" (the default).</summary>
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var token = value.Trim();
            for (var i = 0; i < Presets.Length; i++)
            {
                if (string.Equals(Presets[i].Id, token, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Presets[i].Name, token, StringComparison.OrdinalIgnoreCase))
                    return Presets[i].Id == DefaultId ? string.Empty : Presets[i].Id;
            }

            return string.Empty;
        }

        /// <summary>The preset for a stored value. Blank or unknown is <see cref="DefaultId"/>.</summary>
        public static PersonaTemperament Get(string value)
        {
            var id = Normalize(value);
            if (id.Length == 0)
                return Presets[0];
            for (var i = 0; i < Presets.Length; i++)
            {
                if (Presets[i].Id == id)
                    return Presets[i];
            }

            return Presets[0];
        }
    }
}
