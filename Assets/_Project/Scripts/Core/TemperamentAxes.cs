using System;

namespace AICompanion.Core
{
    /// <summary>
    /// Nine temperament sliders, 0 to 100, middle 50 (JUH-136 phase 1). They describe her nature.
    /// The mood engine still only sees gains, half-lives, a baseline and a rest bias; those come
    /// from <see cref="TemperamentMood"/>. Curiosity is stored and shown, and does not change a mood
    /// yet. Attachment, forgiveness and sensuality are not sliders.
    /// A persona file stores a preset id, or these numbers when the profile is custom. Old files
    /// have no numbers: the preset's point is used.
    /// </summary>
    [Serializable]
    public sealed class TemperamentAxes
    {
        public const int Min = 0;
        public const int Max = 100;
        public const int Middle = 50;
        public const int Count = 9;

        public const int Warmth = 0;
        public const int Volatility = 1;
        public const int Assertiveness = 2;
        public const int Playfulness = 3;
        public const int Openness = 4;
        public const int Energy = 5;
        public const int Directness = 6;
        public const int Confidence = 7;
        public const int Curiosity = 8;

        /// <summary>Mood id for a profile that is not one of the named presets. Not a picker id.</summary>
        public const string CustomId = "custom";

        public static readonly string[] Names =
        {
            "Warmth", "Volatility", "Assertiveness", "Playfulness", "Openness",
            "Energy", "Directness", "Confidence", "Curiosity",
        };

        public static readonly string[] Hints =
        {
            "0 cold, 100 warm",
            "0 steady, 100 reactive",
            "0 yielding, 100 initiating",
            "0 serious, 100 playful",
            "0 guarded, 100 open about feelings",
            "0 still, 100 animated",
            "0 hints, 100 says it straight",
            "0 insecure, 100 self-assured",
            "0 reacts, 100 asks",
        };

        public float warmth = Middle;
        public float volatility = Middle;
        public float assertiveness = Middle;
        public float playfulness = Middle;
        public float openness = Middle;
        public float energy = Middle;
        public float directness = Middle;
        public float confidence = Middle;
        public float curiosity = Middle;

        public static TemperamentAxes Neutral()
        {
            return new TemperamentAxes();
        }

        /// <summary>The named preset's point. An unknown id is the middle of every slider.</summary>
        public static TemperamentAxes ForPreset(string idOrName)
        {
            var id = Canonical(idOrName);
            var axes = Neutral();
            if (id == PersonaTemperaments.CalmId)
            {
                axes.Set(Warmth, 55);
                axes.Set(Volatility, 15);
                axes.Set(Assertiveness, 40);
                axes.Set(Playfulness, 35);
                axes.Set(Openness, 40);
                axes.Set(Energy, 30);
                axes.Set(Directness, 60);
                axes.Set(Confidence, 75);
                axes.Set(Curiosity, 40);
            }
            else if (id == PersonaTemperaments.CheerfulId)
            {
                axes.Set(Warmth, 80);
                axes.Set(Volatility, 30);
                axes.Set(Assertiveness, 55);
                axes.Set(Playfulness, 80);
                axes.Set(Openness, 70);
                axes.Set(Energy, 75);
                axes.Set(Directness, 65);
                axes.Set(Confidence, 70);
                axes.Set(Curiosity, 60);
            }
            else if (id == PersonaTemperaments.ShyId)
            {
                axes.Set(Warmth, 60);
                axes.Set(Volatility, 55);
                axes.Set(Assertiveness, 25);
                axes.Set(Playfulness, 35);
                axes.Set(Openness, 30);
                axes.Set(Energy, 35);
                axes.Set(Directness, 30);
                axes.Set(Confidence, 20);
                axes.Set(Curiosity, 40);
            }
            else if (id == PersonaTemperaments.TsundereId)
            {
                // High warmth, low directness: she cares and will not say so.
                axes.Set(Warmth, 85);
                axes.Set(Volatility, 65);
                axes.Set(Assertiveness, 60);
                axes.Set(Playfulness, 55);
                axes.Set(Openness, 35);
                axes.Set(Energy, 60);
                axes.Set(Directness, 15);
                axes.Set(Confidence, 50);
                axes.Set(Curiosity, 45);
            }
            else if (id == PersonaTemperaments.MoodyId)
            {
                axes.Set(Warmth, 30);
                axes.Set(Volatility, 85);
                axes.Set(Assertiveness, 45);
                axes.Set(Playfulness, 25);
                axes.Set(Openness, 40);
                axes.Set(Energy, 25);
                axes.Set(Directness, 50);
                axes.Set(Confidence, 35);
                axes.Set(Curiosity, 30);
            }

            return axes;
        }

        /// <summary>The preset these numbers land on, or null when they are a custom profile. The middle is Balanced.</summary>
        public static string MatchingPresetId(TemperamentAxes axes)
        {
            if (axes == null)
                return PersonaTemperaments.DefaultId;
            for (var i = 0; i < PersonaTemperaments.Ids.Length; i++)
            {
                var id = PersonaTemperaments.Ids[i];
                if (axes.Same(ForPreset(id)))
                    return id;
            }

            return null;
        }

        /// <summary>True when any slider has left the middle. All 50s is "no opinion" from a reading.</summary>
        public static bool HasOpinion(TemperamentAxes axes)
        {
            if (axes == null)
                return false;
            for (var i = 0; i < Count; i++)
            {
                if (axes.Value(i) != Middle)
                    return true;
            }

            return false;
        }

        /// <summary>Whole number 0 to 100. A blank or invalid number is the middle.</summary>
        public static int Round(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return Middle;
            var rounded = (int)Math.Round(value);
            if (rounded < Min)
                return Min;
            if (rounded > Max)
                return Max;
            return rounded;
        }

        public int Value(int axis)
        {
            return Round(Raw(axis));
        }

        public void Set(int axis, float value)
        {
            var rounded = Round(value);
            switch (axis)
            {
                case Warmth: warmth = rounded; break;
                case Volatility: volatility = rounded; break;
                case Assertiveness: assertiveness = rounded; break;
                case Playfulness: playfulness = rounded; break;
                case Openness: openness = rounded; break;
                case Energy: energy = rounded; break;
                case Directness: directness = rounded; break;
                case Confidence: confidence = rounded; break;
                case Curiosity: curiosity = rounded; break;
            }
        }

        public void Clamp()
        {
            for (var i = 0; i < Count; i++)
                Set(i, Raw(i));
        }

        public TemperamentAxes Clone()
        {
            var copy = Neutral();
            for (var i = 0; i < Count; i++)
                copy.Set(i, Value(i));
            return copy;
        }

        public bool Same(TemperamentAxes other)
        {
            if (other == null)
                return false;
            for (var i = 0; i < Count; i++)
            {
                if (Value(i) != other.Value(i))
                    return false;
            }

            return true;
        }

        /// <summary>Mix of two points. <paramref name="share"/> is the first one's part, 0 to 1.</summary>
        public static TemperamentAxes Mix(TemperamentAxes a, TemperamentAxes b, float share)
        {
            a = a ?? Neutral();
            b = b ?? Neutral();
            share = CharacterSheet.Clamp(share, 0f, 1f);
            var mixed = Neutral();
            var rest = 1f - share;
            for (var i = 0; i < Count; i++)
                mixed.Set(i, a.Value(i) * share + b.Value(i) * rest);
            return mixed;
        }

        /// <summary>The sliders furthest from the middle, for a settings line. All near 50 is "Balanced".</summary>
        public string Summary()
        {
            var first = -1;
            var second = -1;
            var third = -1;
            var firstDistance = 0;
            var secondDistance = 0;
            var thirdDistance = 0;
            for (var i = 0; i < Count; i++)
            {
                var distance = Math.Abs(Value(i) - Middle);
                if (distance > firstDistance)
                {
                    third = second;
                    thirdDistance = secondDistance;
                    second = first;
                    secondDistance = firstDistance;
                    first = i;
                    firstDistance = distance;
                }
                else if (distance > secondDistance)
                {
                    third = second;
                    thirdDistance = secondDistance;
                    second = i;
                    secondDistance = distance;
                }
                else if (distance > thirdDistance)
                {
                    third = i;
                    thirdDistance = distance;
                }
            }

            if (first < 0 || firstDistance < 10)
                return PersonaTemperaments.Get(PersonaTemperaments.DefaultId).Name;
            var text = Names[first] + " " + Value(first);
            if (second >= 0 && secondDistance >= 10)
                text += ", " + Names[second] + " " + Value(second);
            if (third >= 0 && thirdDistance >= 10)
                text += ", " + Names[third] + " " + Value(third);
            return text;
        }

        public static string SliderLabel(int axis, float value)
        {
            if (axis < 0 || axis >= Count)
                return string.Empty;
            return Names[axis] + " " + Round(value) + " - " + Hints[axis];
        }

        float Raw(int axis)
        {
            switch (axis)
            {
                case Warmth: return warmth;
                case Volatility: return volatility;
                case Assertiveness: return assertiveness;
                case Playfulness: return playfulness;
                case Openness: return openness;
                case Energy: return energy;
                case Directness: return directness;
                case Confidence: return confidence;
                case Curiosity: return curiosity;
                default: return Middle;
            }
        }

        static string Canonical(string idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName))
                return PersonaTemperaments.DefaultId;
            var token = idOrName.Trim();
            for (var i = 0; i < PersonaTemperaments.Ids.Length; i++)
            {
                var preset = PersonaTemperaments.Get(PersonaTemperaments.Ids[i]);
                if (string.Equals(preset.Id, token, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(preset.Name, token, StringComparison.OrdinalIgnoreCase))
                    return preset.Id;
            }

            return null;
        }
    }

    /// <summary>
    /// Turns slider values into the mood numbers <see cref="PersonaTemperament"/> already applies.
    /// A point that lands on a named preset uses that preset's authored numbers, so existing
    /// personas keep the same gains, half-lives, baselines and biases.
    /// High volatility sulks or snaps and then recovers: negative half-lives stop at
    /// <see cref="MaxNegativeHalfLife"/>, and a rest bias stays at <see cref="MaxBias"/>, under the
    /// line where a mood would stay on screen. Intimate is never touched.
    /// </summary>
    public static class TemperamentMood
    {
        public const float MaxNegativeHalfLife = 1.5f;
        public const float MaxPositiveHalfLife = 1.6f;
        public const float MaxMoodGain = 1.6f;
        public const float MaxBias = 0.1f;

        public static PersonaTemperament From(TemperamentAxes axes)
        {
            var match = TemperamentAxes.MatchingPresetId(axes);
            if (match != null)
                return PersonaTemperaments.Get(match);

            axes = axes ?? TemperamentAxes.Neutral();
            var mood = new PersonaTemperament(TemperamentAxes.CustomId, "Custom");
            var gain = Ones();
            var half = Ones();
            var warmth = axes.Value(TemperamentAxes.Warmth);
            var volatility = axes.Value(TemperamentAxes.Volatility);
            var assertiveness = axes.Value(TemperamentAxes.Assertiveness);
            var playfulness = axes.Value(TemperamentAxes.Playfulness);
            var openness = axes.Value(TemperamentAxes.Openness);
            var energy = axes.Value(TemperamentAxes.Energy);
            var directness = axes.Value(TemperamentAxes.Directness);
            var confidence = axes.Value(TemperamentAxes.Confidence);

            // Warmth: affection and happiness come more easily. Annoyance comes a little less easily.
            Mul(gain, CompanionMood.Affectionate, Toward(warmth, 0.40f));
            Mul(gain, CompanionMood.Happy, Toward(warmth, 0.25f));
            Mul(half, CompanionMood.Happy, Toward(warmth, 0.20f));
            Mul(gain, CompanionMood.Annoyed, Toward(warmth, -0.15f));

            // Volatility: negative moods rise faster and take a little longer to fade, then they do fade.
            Mul(gain, CompanionMood.Annoyed, Toward(volatility, 0.40f));
            Mul(gain, CompanionMood.Anxious, Toward(volatility, 0.40f));
            Mul(gain, CompanionMood.Sad, Toward(volatility, 0.40f));
            Mul(gain, CompanionMood.Flustered, Toward(volatility, 0.40f));
            Mul(half, CompanionMood.Annoyed, Toward(volatility, 0.40f));
            Mul(half, CompanionMood.Anxious, Toward(volatility, 0.40f));
            Mul(half, CompanionMood.Sad, Toward(volatility, 0.40f));
            Mul(half, CompanionMood.Flustered, Toward(volatility, 0.40f));

            // Assertiveness: a yielding person flusters; an initiating one is easier to annoy.
            Mul(gain, CompanionMood.Annoyed, Toward(assertiveness, 0.25f));
            Mul(gain, CompanionMood.Flustered, Toward(assertiveness, -0.30f));
            Mul(gain, CompanionMood.Anxious, Toward(assertiveness, -0.20f));

            // Playfulness and energy colour the bright moods. They do not change how she speaks.
            Mul(gain, CompanionMood.Happy, Toward(playfulness, 0.20f));
            Mul(gain, CompanionMood.Sad, Toward(playfulness, -0.15f));
            Mul(gain, CompanionMood.Happy, Toward(energy, 0.20f));
            Mul(half, CompanionMood.Happy, Toward(energy, 0.30f));
            Mul(gain, CompanionMood.Flustered, Toward(energy, 0.10f));

            // Openness is about feelings, not how much she talks.
            Mul(gain, CompanionMood.Affectionate, Toward(openness, 0.12f));
            Mul(gain, CompanionMood.Sad, Toward(openness, 0.12f));
            Mul(gain, CompanionMood.Flustered, Toward(openness, 0.15f));

            // Low directness hides warmth (the tsundere shape) and shows fluster and annoyance instead.
            Mul(gain, CompanionMood.Affectionate, Toward(directness, 0.45f));
            Mul(half, CompanionMood.Affectionate, Toward(directness, 0.30f));
            Mul(gain, CompanionMood.Flustered, Toward(directness, -0.25f));
            Mul(gain, CompanionMood.Annoyed, Toward(directness, -0.20f));

            // Confidence: insecurity feeds worry and fluster.
            Mul(gain, CompanionMood.Anxious, Toward(confidence, -0.35f));
            Mul(gain, CompanionMood.Flustered, Toward(confidence, -0.25f));
            Mul(half, CompanionMood.Anxious, Toward(confidence, -0.25f));

            for (var i = 1; i < CompanionMoodNames.Count; i++)
            {
                var which = (CompanionMood)i;
                if (which == CompanionMood.Intimate)
                    continue;
                mood.WithGain(which, ClampGain(gain[i]));
                mood.WithHalfLife(which, ClampHalf(which, half[i]));
            }

            if (warmth >= 75 && playfulness >= 70 && energy >= 70 && volatility <= 40)
                mood.WithBaseline(CompanionMood.Happy);

            if (warmth >= 75 && directness <= 30 && assertiveness >= 50 && volatility >= 55)
                mood.WithRestBias(CompanionMood.Annoyed, MaxBias);
            else if (confidence <= 30 && assertiveness <= 30)
                mood.WithRestBias(CompanionMood.Flustered, MaxBias);
            else if (warmth <= 40 && energy <= 35 && volatility >= 70 && playfulness <= 35)
                mood.WithRestBias(CompanionMood.Sad, MaxBias);

            return mood;
        }

        /// <summary>Preset name when the sliders land on one, otherwise the short slider summary.</summary>
        public static string Label(TemperamentAxes axes)
        {
            axes = axes ?? TemperamentAxes.Neutral();
            var mood = From(axes);
            return mood.Id == TemperamentAxes.CustomId ? axes.Summary() : mood.Name;
        }

        static float Toward(int slider, float perUnit)
        {
            return 1f + perUnit * ((slider - TemperamentAxes.Middle) / 50f);
        }

        static void Mul(float[] table, CompanionMood mood, float factor)
        {
            var index = (int)mood;
            if (index > 0 && index < table.Length && !float.IsNaN(factor))
                table[index] *= factor;
        }

        static float ClampGain(float scale)
        {
            if (float.IsNaN(scale))
                return 1f;
            if (scale < PersonaTemperament.MinScale)
                return PersonaTemperament.MinScale;
            if (scale > MaxMoodGain)
                return MaxMoodGain;
            return scale;
        }

        static float ClampHalf(CompanionMood mood, float scale)
        {
            if (float.IsNaN(scale))
                return 1f;
            if (scale < PersonaTemperament.MinScale)
                return PersonaTemperament.MinScale;
            var cap = mood == CompanionMood.Annoyed || mood == CompanionMood.Anxious
                || mood == CompanionMood.Sad || mood == CompanionMood.Flustered
                ? MaxNegativeHalfLife
                : MaxPositiveHalfLife;
            if (scale > cap)
                return cap;
            return scale;
        }

        static float[] Ones()
        {
            var values = new float[CompanionMoodNames.Count];
            for (var i = 0; i < values.Length; i++)
                values[i] = 1f;
            return values;
        }
    }
}
