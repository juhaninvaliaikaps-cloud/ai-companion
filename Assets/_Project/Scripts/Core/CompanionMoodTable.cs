using System;

namespace AICompanion.Core
{
    /// <summary>One nudge to her mood: which mood, and how much at full intensity.</summary>
    public readonly struct CompanionMoodPush
    {
        public CompanionMood Mood { get; }
        public float Gain { get; }

        public CompanionMoodPush(CompanionMood mood, float gain)
        {
            Mood = mood;
            Gain = gain;
        }
    }

    /// <summary>
    /// What moves her mood, in one table that is easy to tune (JUH-103). The amount of a push
    /// is <c>Gain * (0.5 + intensity)</c>, so a strong cue (intensity 1) moves 1.5 times the gain
    /// and a weak one (0) half of it. Touches and being ignored have hooks only for now.
    /// </summary>
    public static class CompanionMoodTable
    {
        // ---- her own cue: the emotion word as the model wrote it, before aliasing ----------
        // Richer words come first, so "pouty" builds Annoyed rather than the Sad it aliases to.
        static readonly string[] CueWords =
        {
            "pout", "pouty", "angry", "annoyed", "tsundere", "grumpy", "smug",
            "loving", "kiss", "affectionate", "love",
            "laughing", "excited", "playful", "smug_wink",
            "crying", "hurt",
            "scared", "shocked", "worried", "nervous", "afraid", "fear", "shock", "worry", "embarrassed_worried",
            "sleepy", "tired", "bored",
            "seductive", "flirty",
        };

        static readonly CompanionMoodPush[][] CueWordPushes =
        {
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.3f) },        // pout
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.3f) },        // pouty
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.35f) },       // angry
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.3f) },        // annoyed
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.25f), new CompanionMoodPush(CompanionMood.Flustered, 0.1f) }, // tsundere
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.25f) },       // grumpy
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.2f) },          // smug
            new[] { new CompanionMoodPush(CompanionMood.Affectionate, 0.3f) },   // loving
            new[] { new CompanionMoodPush(CompanionMood.Affectionate, 0.35f) },  // kiss
            new[] { new CompanionMoodPush(CompanionMood.Affectionate, 0.3f) },   // affectionate
            new[] { new CompanionMoodPush(CompanionMood.Affectionate, 0.3f) },   // love
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.35f) },         // laughing
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.3f) },          // excited
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.25f) },         // playful
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.2f) },          // smug_wink
            new[] { new CompanionMoodPush(CompanionMood.Sad, 0.45f) },           // crying
            new[] { new CompanionMoodPush(CompanionMood.Sad, 0.35f) },           // hurt
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.35f) },       // scared
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.3f) },        // shocked
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.25f) },       // worried
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.25f) },       // nervous
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.35f) },       // afraid
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.35f) },       // fear
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.3f) },        // shock
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.25f) },       // worry
            new[] { new CompanionMoodPush(CompanionMood.Flustered, 0.2f), new CompanionMoodPush(CompanionMood.Anxious, 0.1f) }, // embarrassed_worried
            new[] { new CompanionMoodPush(CompanionMood.Sleepy, 0.25f) },        // sleepy
            new[] { new CompanionMoodPush(CompanionMood.Sleepy, 0.2f) },         // tired
            new[] { new CompanionMoodPush(CompanionMood.Sleepy, 0.15f) },        // bored
            new[] { new CompanionMoodPush(CompanionMood.Intimate, 0.3f), new CompanionMoodPush(CompanionMood.Affectionate, 0.1f) }, // seductive
            new[] { new CompanionMoodPush(CompanionMood.Intimate, 0.3f), new CompanionMoodPush(CompanionMood.Affectionate, 0.1f) }, // flirty
        };

        // ---- her own cue: the emotion, when the word is not in the list above ------------
        static readonly CompanionMoodPush[][] CueEmotionPushes =
        {
            new CompanionMoodPush[0],                                             // Neutral
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.25f) },          // Happy
            new[] { new CompanionMoodPush(CompanionMood.Sad, 0.3f) },             // Sad
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.1f) },         // Surprised
            new CompanionMoodPush[0],                                             // Thinking
            new[] { new CompanionMoodPush(CompanionMood.Flustered, 0.3f) },       // Embarrassed
            new[] { new CompanionMoodPush(CompanionMood.Flustered, 0.2f), new CompanionMoodPush(CompanionMood.Affectionate, 0.1f) }, // Shy
            new[] { new CompanionMoodPush(CompanionMood.Flustered, 0.35f) },      // Flustered
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.2f), new CompanionMoodPush(CompanionMood.Sad, 0.1f) }, // Concerned
        };

        // ---- the user's detected mood (JUH-21): she picks up the user's mood ----------------
        static readonly CompanionMoodPush[][] UserPushes =
        {
            new CompanionMoodPush[0],                                             // Neutral
            new[] { new CompanionMoodPush(CompanionMood.Happy, 0.2f) },           // Happy
            new[] { new CompanionMoodPush(CompanionMood.Sad, 0.15f) },            // Sad
            new[] { new CompanionMoodPush(CompanionMood.Annoyed, 0.15f) },        // Angry
            new[] { new CompanionMoodPush(CompanionMood.Anxious, 0.15f) },        // Anxious
            new CompanionMoodPush[0],                                             // Surprised
            new[] { new CompanionMoodPush(CompanionMood.Affectionate, 0.25f) },   // Affectionate
            new CompanionMoodPush[0],                                             // Confused
        };

        static readonly CompanionMoodPush[] None = new CompanionMoodPush[0];

        /// <summary>The pushes for her own reply cue.</summary>
        public static CompanionMoodPush[] ForCue(AnimationCue cue)
        {
            var word = cue.EmotionField == null ? string.Empty : cue.EmotionField.Trim();
            for (var i = 0; i < CueWords.Length; i++)
            {
                if (string.Equals(CueWords[i], word, StringComparison.OrdinalIgnoreCase))
                    return CueWordPushes[i];
            }

            var index = (int)cue.Emotion;
            return index >= 0 && index < CueEmotionPushes.Length ? CueEmotionPushes[index] : None;
        }

        /// <summary>The pushes for the user's detected mood.</summary>
        public static CompanionMoodPush[] ForUser(UserMood mood)
        {
            var index = (int)mood.Emotion;
            return index >= 0 && index < UserPushes.Length ? UserPushes[index] : None;
        }

        /// <summary><c>Gain * (0.5 + intensity)</c>, intensity clamped to 0..1.</summary>
        public static float Amount(float gain, float intensity)
        {
            if (float.IsNaN(intensity))
                intensity = 0.5f;
            intensity = Math.Max(0f, Math.Min(1f, intensity));
            return gain * (0.5f + intensity);
        }

        /// <summary>Applies her cue to the mood. Returns true when the shown mood changed.</summary>
        public static bool ApplyCue(CompanionMoodState state, AnimationCue cue)
        {
            var changed = false;
            var pushes = ForCue(cue);
            for (var i = 0; i < pushes.Length; i++)
                changed |= state.Push(pushes[i].Mood, Scaled(state, pushes[i], cue.Intensity));
            return changed;
        }

        /// <summary>Applies the user's detected mood. Returns true when the shown mood changed.</summary>
        public static bool ApplyUser(CompanionMoodState state, UserMood mood)
        {
            var changed = false;
            var pushes = ForUser(mood);
            for (var i = 0; i < pushes.Length; i++)
                changed |= state.Push(pushes[i].Mood, Scaled(state, pushes[i], mood.Intensity));
            return changed;
        }

        /// <summary>
        /// JUH-103 phase 3: the push scaled by the persona's temperament. The default gain is 1,
        /// and Intimate is never scaled unless it is allowed.
        /// </summary>
        public static float Scaled(CompanionMoodState state, CompanionMoodPush push, float intensity)
        {
            var amount = Amount(push.Gain, intensity);
            var temperament = state.Temperament;
            return temperament == null ? amount : amount * temperament.Gain(push.Mood, state.IntimateAllowed);
        }
    }

    /// <summary>Where a touch landed. A hook for JUH-61; it does not move the mood yet.</summary>
    public enum CompanionTouch
    {
        Pat = 0,
        Poke = 1,
        Other = 2,
    }

    /// <summary>
    /// Inputs for her mood from other parts of the app. The chat panel reports her reply's
    /// cue and the user's detected mood; touches and being ignored are hooks for later
    /// phases. The mood keeper listens. Without a listener a report does nothing.
    /// </summary>
    public static class CompanionMoodInputs
    {
        public static event Action<AnimationCue> CueApplied;
        public static event Action<UserMood> UserMoodDetected;

        /// <summary>Hook only (phase 1): reported, not yet used.</summary>
        public static event Action<CompanionTouch> Touched;

        /// <summary>Hook only (phase 1): seconds without the user, reported, not yet used.</summary>
        public static event Action<float> Ignored;

        public static void ReportCue(AnimationCue cue)
        {
            var handler = CueApplied;
            if (handler != null)
                handler(cue);
        }

        public static void ReportUserMood(UserMood mood)
        {
            var handler = UserMoodDetected;
            if (handler != null)
                handler(mood);
        }

        public static void ReportTouch(CompanionTouch touch)
        {
            var handler = Touched;
            if (handler != null)
                handler(touch);
        }

        public static void ReportIgnored(float seconds)
        {
            var handler = Ignored;
            if (handler != null)
                handler(seconds);
        }
    }
}
