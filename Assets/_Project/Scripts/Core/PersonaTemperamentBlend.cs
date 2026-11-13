using System;

namespace AICompanion.Core
{
    /// <summary>What the sheet's temperament weights come to.</summary>
    public struct TemperamentReading
    {
        /// <summary>A shared preset, or a new blended temperament. Never null.</summary>
        public PersonaTemperament Temperament;

        /// <summary>True when two presets were mixed.</summary>
        public bool IsBlend;

        /// <summary>For the settings line and the log: "Shy 60%, Cheerful 40%", "Cheerful 72%" or "Balanced".</summary>
        public string Summary;
    }

    /// <summary>
    /// Turns the sheet's temperament weights into a temperament (JUH-114, as part of JUH-117):
    /// <list type="bullet">
    /// <item>nothing reaches <see cref="MinConfidence"/>: Balanced;</item>
    /// <item>one preset clearly leads: that preset, unchanged;</item>
    /// <item>the second is close (<see cref="CloseRatio"/> of the first): the two are mixed by weight.</item>
    /// </list>
    /// A mix averages gains and half-lives. It keeps the leader's baseline only when the leader
    /// holds <see cref="BaselineShare"/>, and scales rest biases by share. Intimate is never
    /// touched, and every number goes through the same clamps as the presets.
    /// </summary>
    public static class PersonaTemperamentBlend
    {
        public const float MinConfidence = 0.35f;
        public const float CloseRatio = 0.5f;
        public const float MinSecond = 0.2f;
        public const float BaselineShare = 0.6f;
        public const string BlendId = "blend";

        public static TemperamentReading From(SheetTemperament weights)
        {
            var first = string.Empty;
            var second = string.Empty;
            var firstWeight = 0f;
            var secondWeight = 0f;
            if (weights != null)
            {
                // Picker order breaks ties, so the same sheet always gives the same result.
                for (var i = 0; i < PersonaTemperaments.Ids.Length; i++)
                {
                    var id = PersonaTemperaments.Ids[i];
                    if (id == PersonaTemperaments.DefaultId)
                        continue;
                    var weight = CharacterSheet.Clamp(weights.Weight(id), 0f, 1f);
                    if (weight > firstWeight)
                    {
                        second = first;
                        secondWeight = firstWeight;
                        first = id;
                        firstWeight = weight;
                    }
                    else if (weight > secondWeight)
                    {
                        second = id;
                        secondWeight = weight;
                    }
                }
            }

            if (firstWeight < MinConfidence)
            {
                return new TemperamentReading
                {
                    Temperament = PersonaTemperament.Default,
                    Summary = PersonaTemperament.Default.Name,
                };
            }

            var lead = PersonaTemperaments.Get(first);
            if (secondWeight < MinSecond || secondWeight < firstWeight * CloseRatio)
            {
                return new TemperamentReading
                {
                    Temperament = lead,
                    Summary = lead.Name + " " + Percent(firstWeight) + "%",
                };
            }

            var other = PersonaTemperaments.Get(second);
            var share = firstWeight / (firstWeight + secondWeight);
            var leadPercent = Percent(share);
            var summary = lead.Name + " " + leadPercent + "%, " + other.Name + " " + (100 - leadPercent) + "%";
            return new TemperamentReading
            {
                Temperament = Mix(lead, other, share, summary),
                IsBlend = true,
                Summary = summary,
            };
        }

        /// <summary>Mixes two temperaments; <paramref name="share"/> is the first one's part, 0 to 1.</summary>
        public static PersonaTemperament Mix(PersonaTemperament a, PersonaTemperament b, float share, string name)
        {
            a = a ?? PersonaTemperament.Default;
            b = b ?? PersonaTemperament.Default;
            share = CharacterSheet.Clamp(share, 0f, 1f);
            var rest = 1f - share;
            var mixed = new PersonaTemperament(BlendId, name);
            for (var i = 1; i < CompanionMoodNames.Count; i++)
            {
                var mood = (CompanionMood)i;
                if (mood == CompanionMood.Intimate)
                    continue;
                mixed.WithGain(mood, a.Gain(mood, true) * share + b.Gain(mood, true) * rest);
                mixed.WithHalfLife(mood, a.HalfLifeScale(mood) * share + b.HalfLifeScale(mood) * rest);
            }

            var leader = share >= rest ? a : b;
            var leaderShare = Math.Max(share, rest);
            if (leaderShare >= BaselineShare)
                mixed.WithBaseline(leader.Baseline);

            var biasA = a.RestBiasStrength * share;
            var biasB = b.RestBiasStrength * rest;
            if (biasA > 0f && biasA >= biasB)
                mixed.WithRestBias(a.RestBias, biasA);
            else if (biasB > 0f)
                mixed.WithRestBias(b.RestBias, biasB);
            return mixed;
        }

        static int Percent(float value)
        {
            return (int)Math.Round(CharacterSheet.Clamp(value, 0f, 1f) * 100f);
        }
    }
}
