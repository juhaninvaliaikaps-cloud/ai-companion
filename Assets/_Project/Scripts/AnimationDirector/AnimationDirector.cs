using System;
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
    public sealed class AnimationDirector : MonoBehaviour, IReactionSuppression
    {
        [SerializeField] AnimationClipCatalog catalog;
        [SerializeField, Range(0f, 1f)] float gestureCrossfadeSeconds = AvatarAnimatorRules.TransitionSeconds;

        AnimationDirectorModel _model;
        IAvatarAnimator _animator;
        IAnimationStatePlayer _states;
        bool _suppressed;

        public bool ReactionsSuppressed => _suppressed;

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
            var animator = GetComponent<IAvatarAnimator>();
            var states = GetComponent<IAnimationStatePlayer>();
            _animator = IsAlive(animator) ? animator : null;
            _states = IsAlive(states) ? states : null;
            _model = catalog == null ? null : new AnimationDirectorModel(catalog);
        }

        public void SetSuppressed(bool suppressed)
        {
            if (_animator == null && _states == null)
                Resolve();
            if (_suppressed == suppressed)
                return;

            _suppressed = suppressed;
            if (CompanionPause.ReactionsAllowed(_suppressed) || _animator == null)
                return;

            _animator.SetEmotion(Emotion.Neutral, 1f);
            _animator.SetTalking(false);
        }

        public void Play(AnimationCue cue)
        {
            if (!CompanionPause.ReactionsAllowed(_suppressed))
                return;

            if (_model == null || !IsAlive(_animator) || !IsAlive(_states))
            {
                Resolve();
            }

            if (_model == null)
            {
                Debug.Log(AnimationDirectorLog.MissingCatalog(cue));
                return;
            }

            var decision = _model.Decide(cue);
            var played = Apply(decision);
            LastDecision = decision;
            Debug.Log(AnimationDirectorLog.Format(cue, decision, played));
        }

        public void PlayNeutral()
        {
            Play(AnimationCueParser.NeutralCue);
        }

        bool Apply(DirectorDecision decision)
        {
            if (IsAlive(_animator))
            {
                _animator.SetEmotion(decision.Emotion, decision.Intensity);
            }

            if (decision.Action != DirectorAction.GestureState || !IsAlive(_states) || decision.StateName.Length == 0)
            {
                return false;
            }

            try
            {
                return _states.TryPlayState(decision.StateName, gestureCrossfadeSeconds);
            }
            catch (Exception)
            {
                // A missing or swapped Animator must not fail the reply that carried the cue.
                return false;
            }
        }

        static bool IsAlive(object target)
        {
            if (target == null)
            {
                return false;
            }

            var unityObject = target as UnityEngine.Object;
            if (ReferenceEquals(unityObject, null))
            {
                return true;
            }

            return unityObject != null;
        }
    }
}
