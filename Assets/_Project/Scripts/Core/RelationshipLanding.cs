using System;

namespace AICompanion.Core
{
    /// <summary>How the user's last message landed with her (JUH-117). She says it in her cue tag.</summary>
    public enum LandingEvent
    {
        Neutral = 0,
        Complimented = 1,
        Thanked = 2,
        Comforted = 3,
        Teased = 4,
        Insulted = 5,
        Dismissed = 6,
        Apologized = 7,
        Flirted = 8,
    }

    /// <summary>
    /// The <c>landed="..."</c> attribute of the cue tag. The model judges what the user's message
    /// meant; <see cref="RelationshipMath"/> decides what that does to the numbers.
    /// </summary>
    public static class LandingEvents
    {
        public const string Attribute = "landed";

        /// <summary>
        /// The sentence to add to the cue instruction (JUH-118 wires it in). The words here
        /// are the ones <see cref="Parse"/> reads.
        /// </summary>
        public const string Instruction =
            "Also put landed=\"...\" in the tag: how the user's last message landed with you. " +
            "One of complimented, thanked, comforted, teased, insulted, dismissed, apologized, flirted, neutral. " +
            "Judge it as she would, given how well you two know each other. Use neutral for ordinary talk.";

        static readonly string[] Names =
        {
            "neutral", "complimented", "thanked", "comforted", "teased", "insulted", "dismissed", "apologized", "flirted",
        };

        public static string Name(LandingEvent landing)
        {
            var index = (int)landing;
            return index >= 0 && index < Names.Length ? Names[index] : Names[0];
        }

        /// <summary>A missing or unknown word is <see cref="LandingEvent.Neutral"/>.</summary>
        public static LandingEvent Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LandingEvent.Neutral;
            switch (value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_'))
            {
                case "complimented":
                case "compliment":
                case "praised":
                case "praise":
                case "flattered":
                    return LandingEvent.Complimented;
                case "thanked":
                case "thanks":
                case "appreciated":
                    return LandingEvent.Thanked;
                case "comforted":
                case "comfort":
                case "supported":
                case "cared_for":
                    return LandingEvent.Comforted;
                case "teased":
                case "tease":
                case "joked":
                case "playful":
                    return LandingEvent.Teased;
                case "insulted":
                case "insult":
                case "hurt":
                case "mocked":
                case "offended":
                    return LandingEvent.Insulted;
                case "dismissed":
                case "ignored":
                case "brushed_off":
                    return LandingEvent.Dismissed;
                case "apologized":
                case "apologised":
                case "apology":
                case "apologized_to":
                case "apologised_to":
                    return LandingEvent.Apologized;
                case "flirted":
                case "flirt":
                case "flirted_with":
                    return LandingEvent.Flirted;
                default:
                    return LandingEvent.Neutral;
            }
        }
    }

    /// <summary>What one landed message does. <see cref="MoodPushes"/> may be empty, never null.</summary>
    public struct RelationshipChange
    {
        public LandingEvent Landing;
        public float Affection;
        public float Familiarity;
        public float AffectionDelta;
        public float FamiliarityDelta;
        public CompanionMoodPush[] MoodPushes;

        /// <summary>For Player.log: <c>Relationship: insulted → affection -6.2 (now 18.8), familiarity +0.1 (now 70.1)</c>.</summary>
        public string LogLine()
        {
            return "Relationship: " + LandingEvents.Name(Landing) + " → affection " + Signed(AffectionDelta)
                + " (now " + Affection.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                + "), familiarity " + Signed(FamiliarityDelta)
                + " (now " + Familiarity.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        static string Signed(float value)
        {
            return value.ToString("+0.#;-0.#;0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The relationship leaf (JUH-115, as part of JUH-117). Plain numbers, no model:
    /// <list type="bullet">
    /// <item>The same event lands differently by familiarity. A tease costs a stranger and helps
    /// someone close; an insult costs most from a stranger; being dismissed hurts most from
    /// someone close; a compliment from a stranger adds little and raises suspicion.</item>
    /// <item>Standing shades it. Kindness has diminishing returns near the top and lands weaker
    /// while she dislikes the user; slights are forgiven more when she likes them and sting more
    /// when she does not.</item>
    /// <item>Familiarity only grows: a little per exchange, and with real time together.</item>
    /// <item>Affection does not drift by itself. Negative affection is love-hate, and nothing
    /// here closes or opens the Intimate gate or pushes the Intimate mood.</item>
    /// </list>
    /// </summary>
    public static class RelationshipMath
    {
        public const float MinAffection = -100f;
        public const float MaxAffection = 100f;
        public const float MaxFamiliarity = 100f;

        /// <summary>Seconds of chat for familiarity to cover about 63 % of what is left: 20 hours.</summary>
        public const float FamiliaritySeconds = 72000f;

        /// <summary>Familiarity gained per exchange at 0, shrinking to nothing at 100.</summary>
        public const float FamiliarityPerExchange = 0.1f;

        // Affection change at intensity 0.5, for a stranger (familiarity 0) and for someone
        // close (familiarity 100). Indexed by LandingEvent.
        static readonly float[] StrangerDelta = { 0f, 1f, 1f, 2f, -2f, -10f, -3f, 2f, -1f };
        static readonly float[] CloseDelta = { 0f, 4f, 2f, 5f, 2f, -3f, -6f, 4f, 4f };

        // Mood nudges. Kept small: her own cue emotion already moves her mood.
        static readonly CompanionMoodPush[][] StrangerPushes =
        {
            new CompanionMoodPush[0],                                                                                            // neutral
            new[] { new CompanionMoodPush(CompanionMood.Flustered, 0.08f), new CompanionMoodPush(CompanionMood.Anxious, 0.08f) }, // complimented: suspicious
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.05f) },                                                         // thanked
            new[] { new CompanionMoodPush(CompanionMood.Flustered, 0.08f) },                                                     // comforted
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.12f) },                                                       // teased
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.18f), new CompanionMoodPush(CompanionMood.Anxious, 0.08f) },   // insulted
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.06f) },                                                       // dismissed
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.04f) },                                                         // apologized
            new[] { new CompanionMoodPush(CompanionMood.Flustered, 0.1f), new CompanionMoodPush(CompanionMood.Anxious, 0.06f) },  // flirted
        };

        static readonly CompanionMoodPush[][] ClosePushes =
        {
            new CompanionMoodPush[0],                                                                                               // neutral
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.1f), new CompanionMoodPush(CompanionMood.Affectionate, 0.1f) },     // complimented
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.08f), new CompanionMoodPush(CompanionMood.Affectionate, 0.04f) },   // thanked
            new[] { new CompanionMoodPush(CompanionMood.Affectionate, 0.15f) },                                                     // comforted
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.06f), new CompanionMoodPush(CompanionMood.Flustered, 0.08f) },      // teased: playful
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.1f), new CompanionMoodPush(CompanionMood.Sad, 0.04f) },           // insulted
            new[] { new CompanionMoodPush(CompanionMood.Sad, 0.15f) },                                                              // dismissed
            new[] { new CompanionMoodPush(CompanionMood.Affectionate, 0.1f) },                                                      // apologized
            new[] { new CompanionMoodPush(CompanionMood.Flustered, 0.1f), new CompanionMoodPush(CompanionMood.Affectionate, 0.1f) }, // flirted
        };

        // The least familiarity a role implies, so a "family" sheet cannot start as strangers.
        // A stranger cannot start above StrangerMaxFamiliarity.
        public const float StrangerMaxFamiliarity = 15f;

        public static float RoleFamiliarityFloor(string role)
        {
            switch (RelationshipRoles.Normalize(role))
            {
                case RelationshipRoles.Acquaintance: return 10f;
                case RelationshipRoles.Rival: return 25f;
                case RelationshipRoles.Friend: return 30f;
                case RelationshipRoles.CloseFriend: return 55f;
                case RelationshipRoles.Family: return 60f;
                case RelationshipRoles.Partner: return 70f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Where a new persona starts, from the sheet. The model's numbers are kept, inside
        /// what the role allows.
        /// </summary>
        public static void Start(SheetRelationship sheet, out float affection, out float familiarity)
        {
            if (sheet == null)
            {
                affection = 0f;
                familiarity = 0f;
                return;
            }

            affection = ClampAffection(sheet.affection);
            familiarity = ClampFamiliarity(sheet.familiarity);
            var role = RelationshipRoles.Normalize(sheet.role);
            if (role == RelationshipRoles.Stranger)
                familiarity = Math.Min(familiarity, StrangerMaxFamiliarity);
            else
                familiarity = Math.Max(familiarity, RoleFamiliarityFloor(role));
        }

        /// <summary>
        /// One landed message. <paramref name="intensity"/> is her cue intensity (0 to 1): a
        /// stronger reaction moves more, from half to one and a half times the table value.
        /// </summary>
        public static RelationshipChange Land(LandingEvent landing, float intensity, float affection, float familiarity)
        {
            affection = ClampAffection(affection);
            familiarity = ClampFamiliarity(familiarity);
            var index = (int)landing;
            if (index < 0 || index >= StrangerDelta.Length)
            {
                landing = LandingEvent.Neutral;
                index = 0;
            }

            var close = familiarity / MaxFamiliarity;
            var amount = 0.5f + AnimationCue.ClampIntensity(intensity);
            var raw = StrangerDelta[index] + (CloseDelta[index] - StrangerDelta[index]) * close;
            var delta = raw * amount * Standing(raw, affection);

            var newAffection = ClampAffection(affection + delta);
            // Every exchange, kind or not, is time spent knowing each other.
            var newFamiliarity = ClampFamiliarity(familiarity + FamiliarityPerExchange * (MaxFamiliarity - familiarity) / MaxFamiliarity);

            return new RelationshipChange
            {
                Landing = landing,
                Affection = newAffection,
                Familiarity = newFamiliarity,
                AffectionDelta = newAffection - affection,
                FamiliarityDelta = newFamiliarity - familiarity,
                MoodPushes = BlendPushes(StrangerPushes[index], ClosePushes[index], close),
            };
        }

        /// <summary>
        /// How her standing with the user scales a change. Kindness: weaker while she dislikes
        /// them (down to half), and with diminishing returns near the top (down to half). Slights:
        /// forgiven more when she likes them (down to 0.6), sharper when she does not (up to 1.3).
        /// </summary>
        public static float Standing(float delta, float affection)
        {
            var a = ClampAffection(affection) / MaxAffection;
            if (delta > 0f)
                return 1f - 0.5f * Math.Abs(a);
            if (delta < 0f)
                return a >= 0f ? 1f - 0.4f * a : 1f + 0.3f * -a;
            return 1f;
        }

        /// <summary>Familiarity after this much real chat time. It only grows, and slows near 100.</summary>
        public static float FamiliarityAfter(float familiarity, double seconds)
        {
            familiarity = ClampFamiliarity(familiarity);
            if (double.IsNaN(seconds) || seconds <= 0d)
                return familiarity;
            var gain = (MaxFamiliarity - familiarity) * (1d - Math.Exp(-seconds / FamiliaritySeconds));
            return ClampFamiliarity(familiarity + (float)gain);
        }

        /// <summary>
        /// Sends the change's mood nudges through the mood state, where her temperament scales
        /// them like any other push. True when the shown mood changed.
        /// </summary>
        public static bool PushMoods(CompanionMoodState state, RelationshipChange change, float intensity)
        {
            if (state == null || change.MoodPushes == null)
                return false;
            var changed = false;
            for (var i = 0; i < change.MoodPushes.Length; i++)
                changed |= state.Push(change.MoodPushes[i].Mood, CompanionMoodTable.Scaled(state, change.MoodPushes[i], intensity));
            return changed;
        }

        static CompanionMoodPush[] BlendPushes(CompanionMoodPush[] stranger, CompanionMoodPush[] close, float share)
        {
            var count = 0;
            var moods = new CompanionMood[stranger.Length + close.Length];
            var gains = new float[moods.Length];
            Add(stranger, 1f - share, moods, gains, ref count);
            Add(close, share, moods, gains, ref count);

            var kept = 0;
            for (var i = 0; i < count; i++)
            {
                if (gains[i] >= 0.005f)
                    kept++;
            }

            var result = new CompanionMoodPush[kept];
            var next = 0;
            for (var i = 0; i < count; i++)
            {
                if (gains[i] >= 0.005f)
                    result[next++] = new CompanionMoodPush(moods[i], gains[i]);
            }

            return result;
        }

        static void Add(CompanionMoodPush[] source, float weight, CompanionMood[] moods, float[] gains, ref int count)
        {
            for (var i = 0; i < source.Length; i++)
            {
                // Intimate is never pushed from here, whatever the tables say.
                if (source[i].Mood == CompanionMood.Intimate)
                    continue;
                var at = -1;
                for (var j = 0; j < count; j++)
                {
                    if (moods[j] == source[i].Mood)
                    {
                        at = j;
                        break;
                    }
                }

                if (at < 0)
                {
                    at = count++;
                    moods[at] = source[i].Mood;
                    gains[at] = 0f;
                }

                gains[at] += source[i].Gain * weight;
            }
        }

        public static float ClampAffection(float value)
        {
            if (float.IsNaN(value))
                return 0f;
            return Math.Max(MinAffection, Math.Min(MaxAffection, value));
        }

        public static float ClampFamiliarity(float value)
        {
            if (float.IsNaN(value))
                return 0f;
            return Math.Max(0f, Math.Min(MaxFamiliarity, value));
        }
    }
}
