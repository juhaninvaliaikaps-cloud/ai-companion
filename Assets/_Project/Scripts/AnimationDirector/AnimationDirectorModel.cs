using AICompanion.Core;

namespace AICompanion.AnimationDirector
{
    public enum DirectorAction
    {
        Emotion = 0,
        GestureState = 1,
        Neutral = 2,
    }

    /// <summary>
    /// What the director decided to do with one cue: set an emotion on the avatar,
    /// play a named Animator state, or fall back to neutral.
    /// </summary>
    public readonly struct DirectorDecision
    {
        public DirectorAction Action { get; }
        public string StateName { get; }
        public Emotion Emotion { get; }
        public float Intensity { get; }
        public string EntryId { get; }

        /// <summary>
        /// True when the cue named a gesture row that has no Animator state yet.
        /// The row's emotion is still applied. Gesture clips are a later task.
        /// </summary>
        public bool GestureLacksState { get; }

        public DirectorDecision(DirectorAction action, string stateName, Emotion emotion, float intensity, string entryId, bool gestureLacksState = false)
        {
            Action = action;
            StateName = stateName ?? string.Empty;
            Emotion = emotion;
            Intensity = AnimationCue.ClampIntensity(intensity);
            EntryId = entryId ?? string.Empty;
            GestureLacksState = gestureLacksState;
        }
    }

    /// <summary>
    /// One console line per cue the director receives. The gesture token is a catalog
    /// id, never the reply text.
    /// </summary>
    public static class AnimationDirectorLog
    {
        public static string Format(AnimationCue cue, DirectorDecision decision, bool statePlayed)
        {
            var gesture = SafeGesture(cue.Gesture);
            if (gesture.Length > 0)
            {
                if (decision.Action == DirectorAction.Neutral)
                {
                    return "AnimationDirector: cue " + gesture + " -> unknown, neutral";
                }

                if (decision.Action == DirectorAction.GestureState && statePlayed)
                {
                    return "AnimationDirector: cue " + gesture + " -> played";
                }

                if (decision.Action == DirectorAction.GestureState || decision.GestureLacksState)
                {
                    return "AnimationDirector: cue " + gesture + " -> no animator state, skipped";
                }

                return "AnimationDirector: emotion " + EmotionName(decision.Emotion) + " -> applied";
            }

            if (UnsafeGesture(cue.Gesture))
            {
                return "AnimationDirector: cue invalid -> skipped";
            }

            if (decision.Action == DirectorAction.Emotion)
            {
                return "AnimationDirector: emotion " + EmotionName(decision.Emotion) + " -> applied";
            }

            return "AnimationDirector: no cue -> neutral";
        }

        public static string MissingCatalog(AnimationCue cue)
        {
            var gesture = SafeGesture(cue.Gesture);
            if (gesture.Length > 0)
            {
                return "AnimationDirector: cue " + gesture + " -> no catalog, skipped";
            }

            if (UnsafeGesture(cue.Gesture))
            {
                return "AnimationDirector: cue invalid -> skipped";
            }

            if (cue.Emotion != Emotion.Neutral)
            {
                return "AnimationDirector: emotion " + EmotionName(cue.Emotion) + " -> no catalog, skipped";
            }

            return "AnimationDirector: no cue -> no catalog, skipped";
        }

        static string EmotionName(Emotion emotion)
        {
            return emotion.ToString().ToLowerInvariant();
        }

        static string SafeGesture(string gesture)
        {
            if (!AnimationCueParser.IsCueToken(gesture))
            {
                return string.Empty;
            }

            return gesture.Trim().ToLowerInvariant();
        }

        static bool UnsafeGesture(string gesture)
        {
            return !string.IsNullOrWhiteSpace(gesture) && !AnimationCueParser.IsCueToken(gesture);
        }
    }

    /// <summary>
    /// Turns a cue into a decision using the catalog. Pure logic, so tests do not need
    /// an Animator. The gesture string is a catalog id or a gesture name. A gesture
    /// row with an Animator state wins; any other known row uses its emotion, so
    /// emotion states stay on the parameter path. An unknown gesture falls back to neutral.
    /// </summary>
    public sealed class AnimationDirectorModel
    {
        public const float NeutralIntensity = 0.4f;

        readonly AnimationClipCatalog _catalog;

        public AnimationDirectorModel(AnimationClipCatalog catalog)
        {
            _catalog = catalog;
        }

        public DirectorDecision Decide(AnimationCue cue)
        {
            var gesture = cue.Gesture == null ? string.Empty : cue.Gesture.Trim();

            if (gesture.Length > 0)
            {
                AnimationEntry entry;
                if (TryResolve(gesture, out entry))
                {
                    // Emotion rows name a base-layer state that AnyState already drives.
                    // Cross-fading those fights that transition. Only a gesture row with
                    // its own state takes the cross-fade path.
                    if (entry.HasGesture && entry.HasAnimatorState)
                    {
                        return new DirectorDecision(
                            DirectorAction.GestureState,
                            entry.animatorState.Trim(),
                            entry.emotion,
                            entry.intensity,
                            entry.id);
                    }

                    return new DirectorDecision(
                        DirectorAction.Emotion,
                        string.Empty,
                        entry.emotion,
                        entry.intensity,
                        entry.id,
                        entry.HasGesture);
                }

                return Neutral();
            }

            if (cue.Emotion == Emotion.Neutral)
            {
                return Neutral();
            }

            return new DirectorDecision(DirectorAction.Emotion, string.Empty, cue.Emotion, cue.Intensity, string.Empty);
        }

        public static DirectorDecision Neutral()
        {
            return new DirectorDecision(DirectorAction.Neutral, AvatarAnimatorStates.Idle, Emotion.Neutral, NeutralIntensity, string.Empty);
        }

        bool TryResolve(string gesture, out AnimationEntry entry)
        {
            entry = null;
            if (_catalog == null || gesture.Length == 0)
            {
                return false;
            }

            if (_catalog.TryGetById(gesture, out entry))
            {
                return true;
            }

            return _catalog.TryGetByGesture(gesture, out entry);
        }
    }
}
