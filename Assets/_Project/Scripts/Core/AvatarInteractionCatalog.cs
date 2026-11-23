using System;
using System.Collections.Generic;

namespace AICompanion.Core
{
    /// <summary>
    /// One 2D touch interaction: what the menu says, what the avatar shows, and the event the
    /// model receives. Kept free of Unity so the menu and the cue text can be tested.
    /// </summary>
    public sealed class AvatarInteractionDefinition
    {
        public string Id = string.Empty;
        public string Label = string.Empty;
        public string Expression = string.Empty;
        public string Clip = string.Empty;
        public Emotion Emotion = Emotion.Happy;
        public float Intensity = 0.6f;
        public string Signal = string.Empty;
        public bool Hold;
        public bool Release;
        /// <summary>Requires the 18+ gate (Private mode and the adult portrait set).</summary>
        public bool Adult;
        /// <summary>Only offered while the back-view clip is showing.</summary>
        public bool BackOnly;
        /// <summary>Plays the third-window clip as a loop instead of once.</summary>
        public bool Loop;
    }

    /// <summary>
    /// The fixed 2D interaction list. These are menu actions, not body hit zones: the avatar
    /// decides whether a matching clip exists and falls back to a portrait expression.
    /// </summary>
    public static class AvatarInteractionCatalog
    {
        public const string PatHead = "pat_head";
        public const string StrokeHair = "stroke_hair";
        public const string CupCheek = "cup_cheek";
        public const string TouchFace = "touch_face";
        public const string Kiss = "kiss";
        public const string HoldHand = "hold_hand";
        public const string LetGo = "let_go";
        public const string TouchShoulder = "touch_shoulder";
        public const string TouchBreast = "touch_breast";
        public const string TouchBreastLoop = "touch_breast_loop";
        public const string TouchVagina = "touch_vagina";
        public const string TouchButt = "touch_butt";
        public const string TouchButtLoop = "touch_butt_loop";
        public const string FingerMouth = "finger_mouth";
        public const string MakeOut = "make_out";
        public const string LyingKiss = "lying_kiss";
        public const string LyingMakeOut = "lying_make_out";
        public const string LyingMakeOut2 = "lying_make_out_2";
        public const string BlowjobKneeling = "blowjob_kneeling";
        public const string BlowjobLying = "blowjob_lying";
        public const string BlowjobOnTop = "blowjob_ontop";
        public const string FingerSuckLoop = "finger_suck_loop";

        public static readonly IReadOnlyList<AvatarInteractionDefinition> All = new[]
        {
            new AvatarInteractionDefinition
            {
                Id = PatHead,
                Label = "Pat head",
                Expression = "happy",
                Clip = "happy",
                Emotion = Emotion.Happy,
                Intensity = 0.6f,
                Signal = "*User gently pats your head.*",
            },
            new AvatarInteractionDefinition
            {
                Id = StrokeHair,
                Label = "Stroke hair",
                Expression = "smile",
                Clip = "happy",
                Emotion = Emotion.Happy,
                Intensity = 0.45f,
                Signal = "*User strokes your hair.*",
            },
            new AvatarInteractionDefinition
            {
                Id = CupCheek,
                Label = "Cup cheek",
                Expression = "flustered",
                Clip = "flustered",
                Emotion = Emotion.Flustered,
                Intensity = 0.7f,
                Signal = "*User cups your cheek.*",
            },
            new AvatarInteractionDefinition
            {
                Id = TouchFace,
                Label = "Touch face",
                Expression = "embarrassed",
                Clip = "embarrassed",
                Emotion = Emotion.Embarrassed,
                Intensity = 0.6f,
                Signal = "*User touches your face.*",
            },
            new AvatarInteractionDefinition
            {
                Id = Kiss,
                Label = "Kiss lips",
                Expression = "kiss",
                Clip = "kiss",
                Emotion = Emotion.Happy,
                Intensity = 0.8f,
                Signal = "*User kisses you.*",
            },
            new AvatarInteractionDefinition
            {
                Id = MakeOut,
                Label = "Make out",
                Expression = "flustered",
                Clip = "kiss_loop",
                Emotion = Emotion.Flustered,
                Intensity = 0.9f,
                Signal = "*User deepens the kiss.*",
                Adult = true,
                Loop = true,
            },
            new AvatarInteractionDefinition
            {
                Id = LyingKiss,
                Label = "Lying kiss",
                Expression = "kiss",
                Clip = "lying_kiss",
                Emotion = Emotion.Happy,
                Intensity = 0.8f,
                Signal = "*User kisses you while you lie together.*",
                Adult = true,
            },
            new AvatarInteractionDefinition
            {
                Id = LyingMakeOut,
                Label = "Lying make out",
                Expression = "flustered",
                Clip = "lying_make_out",
                Emotion = Emotion.Flustered,
                Intensity = 0.9f,
                Signal = "*User keeps kissing you while you lie together.*",
                Adult = true,
                Loop = true,
            },
            new AvatarInteractionDefinition
            {
                Id = LyingMakeOut2,
                Label = "Lying make out 2",
                Expression = "flustered",
                Clip = "lying_make_out_2",
                Emotion = Emotion.Flustered,
                Intensity = 0.9f,
                Signal = "*User keeps kissing you slowly while you lie together.*",
                Adult = true,
                Loop = true,
            },
            new AvatarInteractionDefinition
            {
                Id = HoldHand,
                Label = "Hold hand",
                Expression = "shy",
                Clip = "hold_hand",
                Emotion = Emotion.Shy,
                Intensity = 0.6f,
                Signal = "*User is holding your hand.*",
                Hold = true,
            },
            new AvatarInteractionDefinition
            {
                Id = LetGo,
                Label = "Let go",
                Expression = "smile",
                Clip = "happy",
                Emotion = Emotion.Happy,
                Intensity = 0.45f,
                Signal = "*User lets go of your hand.*",
                Release = true,
            },
            new AvatarInteractionDefinition
            {
                Id = TouchShoulder,
                Label = "Touch shoulder",
                Expression = "concerned",
                Clip = "concerned",
                Emotion = Emotion.Concerned,
                Intensity = 0.4f,
                Signal = "*User touches your shoulder.*",
            },
            new AvatarInteractionDefinition
            {
                Id = TouchBreast,
                Label = "Touch breast",
                Expression = "flustered",
                Clip = "touch_breast",
                Emotion = Emotion.Flustered,
                Intensity = 0.7f,
                Signal = "*User cups your breast.*",
                Adult = true,
            },
            new AvatarInteractionDefinition
            {
                Id = TouchBreastLoop,
                Label = "Touch breast +loop",
                Expression = "flustered",
                Clip = "touch_breast_loop",
                Emotion = Emotion.Flustered,
                Intensity = 0.75f,
                Signal = "*User keeps cupping your breast.*",
                Adult = true,
                Loop = true,
            },
            new AvatarInteractionDefinition
            {
                Id = TouchVagina,
                Label = "Touch vagina",
                Expression = "flustered",
                Clip = "touch_vagina",
                Emotion = Emotion.Flustered,
                Intensity = 0.8f,
                Signal = "*User touches your vagina.*",
                Adult = true,
            },
            new AvatarInteractionDefinition
            {
                Id = TouchButt,
                Label = "Touch butt",
                Expression = "embarrassed",
                Clip = "touch_butt",
                Emotion = Emotion.Embarrassed,
                Intensity = 0.7f,
                Signal = "*User touches your butt.*",
                Adult = true,
                BackOnly = false,
            },
            new AvatarInteractionDefinition
            {
                Id = TouchButtLoop,
                Label = "Touch butt +loop",
                Expression = "embarrassed",
                Clip = "touch_butt_loop",
                Emotion = Emotion.Embarrassed,
                Intensity = 0.75f,
                Signal = "*User keeps touching your butt.*",
                Adult = true,
                BackOnly = false,
                Loop = true,
            },
            new AvatarInteractionDefinition
            {
                Id = FingerMouth,
                Label = "Finger in mouth",
                Expression = "finger_suck",
                Clip = "finger_mouth",
                Emotion = Emotion.Flustered,
                Intensity = 0.7f,
                Signal = "*User slides a finger into your mouth.*",
                Adult = true,
            },
            new AvatarInteractionDefinition
            {
                Id = BlowjobKneeling,
                Label = "Blowjob, kneeling",
                Expression = "flustered",
                Clip = "blowjob_kneeling",
                Emotion = Emotion.Flustered,
                Intensity = 0.85f,
                Signal = "*User guides you to kneel and take him in your mouth.*",
                Adult = true,
                Loop = true,
            },
            new AvatarInteractionDefinition
            {
                Id = BlowjobLying,
                Label = "Blowjob, lying",
                Expression = "flustered",
                Clip = "blowjob_lying",
                Emotion = Emotion.Flustered,
                Intensity = 0.85f,
                Signal = "*User lies back and you take him in your mouth.*",
                Adult = true,
                Loop = true,
            },
            new AvatarInteractionDefinition
            {
                Id = BlowjobOnTop,
                Label = "Blowjob, on top",
                Expression = "flustered",
                Clip = "blowjob_ontop",
                Emotion = Emotion.Flustered,
                Intensity = 0.85f,
                Signal = "*User pulls you over him and you take him in your mouth.*",
                Adult = true,
                Loop = true,
            },
            new AvatarInteractionDefinition
            {
                Id = FingerSuckLoop,
                Label = "Finger suck +loop",
                Expression = "finger_suck",
                Clip = "finger_suck_loop",
                Emotion = Emotion.Flustered,
                Intensity = 0.75f,
                Signal = "*User keeps a finger in your mouth.*",
                Adult = true,
                Loop = true,
            },
        };

        public static AvatarInteractionDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            var token = id.Trim().ToLowerInvariant();
            for (var i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i].Id, token, StringComparison.Ordinal))
                    return All[i];
            }

            return null;
        }

        public static bool IsHold(string id)
        {
            var item = Find(id);
            return item != null && item.Hold;
        }

        public static bool IsRelease(string id)
        {
            var item = Find(id);
            return item != null && item.Release;
        }
    }

    /// <summary>
    /// What to do with one queued interaction line. An adult line is sent only while the
    /// 18+ gate is open, which keeps that turn on the local model. If the gate closes
    /// before the line is sent, it is dropped and never handed to a provider.
    /// </summary>
    public static class InteractionSignalDelivery
    {
        public enum Choice
        {
            Hold = 0,
            Send = 1,
            Drop = 2,
        }

        public static Choice Decide(bool adult, bool gateOpen, bool chatReady)
        {
            if (adult && !gateOpen)
                return Choice.Drop;
            if (!chatReady)
                return Choice.Hold;
            return Choice.Send;
        }

        /// <summary>
        /// An interaction line is an event for the model, not the user's speech.
        /// Scoring it would treat words such as "kisses" as the user's mood.
        /// </summary>
        public static bool ScoreAsUserMood(bool interactionLine)
        {
            return !interactionLine;
        }
    }
}
