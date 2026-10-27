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

        public DirectorDecision(DirectorAction action, string stateName, Emotion emotion, float intensity, string entryId)
        {
            Action = action;
            StateName = stateName ?? string.Empty;
            Emotion = emotion;
            Intensity = AnimationCue.ClampIntensity(intensity);
            EntryId = entryId ?? string.Empty;
        }
    }

    /// <summary>
    /// Turns a cue into a decision using the catalog. Pure logic, so tests do not need
    /// an Animator. A gesture that the catalog knows and that has an Animator state
    /// wins; a known gesture without a state falls back to its emotion; an unknown
    /// gesture falls back to neutral.
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
                if (_catalog != null && _catalog.TryGetByGesture(gesture, out entry))
                {
                    if (entry.HasAnimatorState)
                    {
                        return new DirectorDecision(DirectorAction.GestureState, entry.animatorState, entry.emotion, entry.intensity, entry.id);
                    }

                    return new DirectorDecision(DirectorAction.Emotion, string.Empty, entry.emotion, entry.intensity, entry.id);
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
    }
}
