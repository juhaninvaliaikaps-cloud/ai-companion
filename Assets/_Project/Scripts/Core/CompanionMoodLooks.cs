using System;

namespace AICompanion.Core
{
    /// <summary>What her mood shows while no reply expression is held (JUH-103 phase 2).</summary>
    public struct MoodLook
    {
        /// <summary>The mood this look is for, after the Intimate gate.</summary>
        public CompanionMood Mood;

        /// <summary>Portrait expression name, such as "concerned". Empty means the neutral portrait.</summary>
        public string Expression;

        /// <summary>The 3D face emotion at rest, and how strong.</summary>
        public Emotion Face;
        public float Intensity;

        /// <summary>A silent 2D reaction clip that may stand in for an idle variation. Empty for none.</summary>
        public string IdleClip;

        public bool HasExpression
        {
            get { return !string.IsNullOrEmpty(Expression); }
        }

        public bool IsNeutral
        {
            get { return !HasExpression && Face == Emotion.Neutral && string.IsNullOrEmpty(IdleClip); }
        }
    }

    /// <summary>
    /// The one mapping from her lasting mood to what she shows (JUH-103 phase 2):
    /// <list type="bullet">
    /// <item>the rest look, used while no reply expression is held and for neutral cues;</item>
    /// <item>softer variants of a reply cue's face, such as a smile for a happy cue while she is sad.</item>
    /// </list>
    /// Reply cues always win; the mood only shades them. Intimate counts only while
    /// <see cref="IntimateExpressions.Active"/>; otherwise it is treated as Content everywhere.
    /// </summary>
    public static class CompanionMoodLooks
    {
        // ---- rest look, indexed by CompanionMood ------------------------------------------
        static readonly string[] RestExpression =
        {
            string.Empty,           // Content
            "smile",                // Happy
            "smile",                // Affectionate
            "shy",                  // Flustered
            "pouty",                // Annoyed
            "concerned",            // Sad
            "embarrassed_worried",  // Anxious
            "sleepy",               // Sleepy
            "shy",                  // Intimate (gated; never a finger_ portrait)
        };

        static readonly Emotion[] RestFace =
        {
            Emotion.Neutral,    // Content
            Emotion.Happy,      // Happy
            Emotion.Happy,      // Affectionate
            Emotion.Shy,        // Flustered
            Emotion.Sad,        // Annoyed
            Emotion.Concerned,  // Sad
            Emotion.Concerned,  // Anxious
            Emotion.Neutral,    // Sleepy (the portrait shows it; the 3D face has no sleepy pose)
            Emotion.Shy,        // Intimate
        };

        static readonly float[] RestIntensity =
        {
            0f,     // Content
            0.35f,  // Happy
            0.25f,  // Affectionate
            0.3f,   // Flustered
            0.25f,  // Annoyed
            0.35f,  // Sad
            0.3f,   // Anxious
            0f,     // Sleepy
            0.3f,   // Intimate
        };

        // Only silent clips: a clip with sound would play its sound at rest.
        static readonly string[] RestIdleClip =
        {
            string.Empty,   // Content
            "happy",        // Happy
            string.Empty,   // Affectionate
            string.Empty,   // Flustered
            string.Empty,   // Annoyed (pout has sound)
            "concerned",    // Sad
            "concerned",    // Anxious
            string.Empty,   // Sleepy
            string.Empty,   // Intimate
        };

        // ---- softer variants of a reply cue's face: (mood, cue face) -> softer face ----------
        static readonly CompanionMood[] SoftenMood =
        {
            CompanionMood.Sad, CompanionMood.Sad, CompanionMood.Sad, CompanionMood.Sad,
            CompanionMood.Anxious, CompanionMood.Anxious, CompanionMood.Anxious,
            CompanionMood.Sleepy, CompanionMood.Sleepy, CompanionMood.Sleepy,
            CompanionMood.Annoyed, CompanionMood.Annoyed,
            CompanionMood.Happy,
            CompanionMood.Affectionate,
            CompanionMood.Flustered,
        };

        static readonly string[] SoftenFrom =
        {
            "happy", "excited", "laughing", "smug",
            "happy", "excited", "laughing",
            "happy", "excited", "laughing",
            "happy", "loving",
            "sad",
            "angry",
            "happy",
        };

        static readonly string[] SoftenTo =
        {
            "smile", "smile", "smile", "smile",
            "smile", "smile", "smile",
            "smile", "smile", "smile",
            "smug", "tsundere",
            "concerned",
            "pouty",
            "shy",
        };

        /// <summary>A softened face is this share of the cue's intensity.</summary>
        public const float SoftenedIntensity = 0.8f;

        /// <summary>The mood that counts: Intimate is Content while the gate is closed.</summary>
        public static CompanionMood Effective(CompanionMood mood, bool intimateActive)
        {
            var index = (int)mood;
            if (index < 0 || index >= CompanionMoodNames.Count)
                return CompanionMood.Content;
            if (mood == CompanionMood.Intimate && !intimateActive)
                return CompanionMood.Content;
            return mood;
        }

        /// <summary>
        /// The rest look for a shown mood. The face intensity grows with the mood's strength
        /// (0 to 1), from 60 to 100 percent of the table value.
        /// </summary>
        public static MoodLook Rest(CompanionMood mood, float strength, bool intimateActive)
        {
            var effective = Effective(mood, intimateActive);
            var index = (int)effective;
            if (float.IsNaN(strength))
                strength = 0f;
            strength = Math.Max(0f, Math.Min(1f, strength));
            return new MoodLook
            {
                Mood = effective,
                Expression = RestExpression[index],
                Face = RestFace[index],
                Intensity = RestIntensity[index] * (0.6f + 0.4f * strength),
                IdleClip = RestIdleClip[index],
            };
        }

        /// <summary>
        /// The softer face for a reply cue's face word in this mood, or empty when the cue
        /// stays as written. Intimate never softens anything.
        /// </summary>
        public static string Soften(CompanionMood mood, string cueFace, bool intimateActive)
        {
            if (string.IsNullOrWhiteSpace(cueFace))
                return string.Empty;
            var effective = Effective(mood, intimateActive);
            if (effective == CompanionMood.Content || effective == CompanionMood.Intimate)
                return string.Empty;

            var word = cueFace.Trim();
            for (var i = 0; i < SoftenFrom.Length; i++)
            {
                if (SoftenMood[i] == effective && string.Equals(SoftenFrom[i], word, StringComparison.OrdinalIgnoreCase))
                    return SoftenTo[i];
            }

            return string.Empty;
        }

        /// <summary>The log line when mood picks something: <c>Mood expression: sad → concerned (rest face)</c>.</summary>
        public static string LogLine(CompanionMood mood, string picked, string where)
        {
            var target = string.IsNullOrEmpty(picked) ? "neutral" : picked;
            var line = "Mood expression: " + CompanionMoodNames.Name(mood) + " → " + target;
            return string.IsNullOrEmpty(where) ? line : line + " (" + where + ")";
        }
    }
}
