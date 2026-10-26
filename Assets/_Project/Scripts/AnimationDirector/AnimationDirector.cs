using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// Applies a cue to the avatar through <see cref="IAvatarAnimator"/> and, when the
    /// catalog row names an Animator state, through <see cref="IAnimationStatePlayer"/>.
    /// The decision itself lives in <see cref="AnimationDirectorModel"/> so it can be
    /// tested without an Animator.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(900)]
    public sealed class AnimationDirector : MonoBehaviour
    {
        [SerializeField] AnimationClipCatalog catalog;
        [SerializeField, Range(0f, 1f)] float gestureCrossfadeSeconds = AvatarAnimatorRules.TransitionSeconds;

        AnimationDirectorModel _model;
        IAvatarAnimator _animator;
        IAnimationStatePlayer _states;

        public DirectorDecision LastDecision { get; private set; }

        public AnimationClipCatalog Catalog
        {
            get { return catalog; }
        }

        /// <summary>
        /// Appended to the chat system prompt: the catalog as prompt lines plus the
        /// cue format. Empty when no catalog is assigned.
        /// </summary>
        public string SystemPromptSuffix
        {
            get
            {
                if (catalog == null)
                {
                    return string.Empty;
                }

                var list = catalog.BuildPromptList();
                if (list.Length == 0)
                {
                    return string.Empty;
                }

                return "\n\nAnimations you can use. Name the gesture id in the cue tag.\n"
                    + list
                    + "\n"
                    + AnimationCueParser.Instruction;
            }
        }

        void Awake()
        {
            Resolve();
        }

        public void Resolve()
        {
            _animator = GetComponent<IAvatarAnimator>();
            _states = GetComponent<IAnimationStatePlayer>();
            _model = catalog == null ? null : new AnimationDirectorModel(catalog);
        }

        public void Play(AnimationCue cue)
        {
            if (_model == null)
            {
                Resolve();
            }

            if (_model == null)
            {
                return;
            }

            var decision = _model.Decide(cue);
            Apply(decision);
            LastDecision = decision;
        }

        public void PlayNeutral()
        {
            Play(AnimationCueParser.NeutralCue);
        }

        void Apply(DirectorDecision decision)
        {
            if (_animator != null)
            {
                _animator.SetEmotion(decision.Emotion, decision.Intensity);
            }

            if (decision.Action == DirectorAction.GestureState && _states != null && decision.StateName.Length > 0)
            {
                _states.TryPlayState(decision.StateName, gestureCrossfadeSeconds);
            }
        }
    }
}
