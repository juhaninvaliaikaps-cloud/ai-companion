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

        /// <summary>
        /// Long enough that a reply cue usually arrives while she is still walking, so a
        /// gesture can play over the walk. A stop cue still ends it immediately.
        /// </summary>
        public const float WalkRequestSeconds = 12f;
        public const string WalkCue = "walk";
        public const string StopCue = "stop";

        IWalkRequest _walk;

        public const float GestureCooldownSeconds = 6f;

        readonly DirectorRulebook _rules = new DirectorRulebook(GestureCooldownSeconds);
        IAvatarLocomotion _locomotion;

        public bool ReactionsSuppressed => _suppressed;

        bool IsFalling()
        {
            if (_locomotion == null)
                _locomotion = GetComponent<IAvatarLocomotion>();
            return _locomotion != null && _locomotion.IsFalling;
        }

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
                // The cue instruction is always sent. It used to be dropped with an empty
                // gesture catalog, and then the model was never told to write a cue at all.
                var list = catalog == null ? string.Empty : catalog.BuildPromptList();
                var text = new System.Text.StringBuilder();
                if (list.Length > 0)
                {
                    text.Append("\n\nAnimations you can use. Name the gesture id in the cue tag.\n")
                        .Append(list)
                        .Append('\n');
                }
                else
                {
                    text.Append("\n\n");
                }

                var reactions = AvatarReactionCatalog.PromptLine();
                if (reactions.Length > 0)
                    text.Append(reactions).Append('\n');

                text.Append(AnimationCueParser.Instruction)
                    .Append('\n')
                    .Append(EmotionNames.CatalogLine);
                if (IntimateExpressions.Active)
                    text.Append('\n').Append(EmotionNames.IntimateCatalogLine);
                return text.ToString();
            }
        }

        void Awake()
        {
            Resolve();
        }

        /// <summary>
        /// The reaction named in the cue, or else an emotion word that is a reaction's name
        /// (a model may write emotion="pout"). Nothing happens without a reaction avatar.
        /// </summary>
        void TryRequestReaction(AnimationCue cue)
        {
            var name = ReactionName(cue);
            if (name.Length == 0)
                return;

            var target = GetComponent<IAvatarReactions>();
            if (target == null || !(target is Component))
            {
                Debug.Log("AnimationDirector: reaction " + name + " -> no reaction avatar (2D video mode is off)");
                return;
            }

            var result = target.RequestReaction(name);
            Debug.Log("AnimationDirector: reaction " + name + " -> " + result.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// The reaction to ask for: the cue's reaction attribute, else a gesture or an emotion
        /// word that names an offered reaction. Models write gesture="pout" and
        /// gesture="turn_around" although the prompt asks for reaction= (JUH-99 player log,
        /// 2026-11-12, Rocinante-X-12B): only the walk played.
        /// </summary>
        public static string ReactionName(AnimationCue cue)
        {
            if (!string.IsNullOrEmpty(cue.Reaction))
                return cue.Reaction;

            var gesture = cue.Gesture == null ? string.Empty : cue.Gesture.Trim().ToLowerInvariant();
            if (gesture.Length > 0 && gesture != WalkCue && gesture != StopCue)
            {
                // gesture="flustered" plays; with emotion="flustered" the face already asks for it.
                var fromGesture = AvatarReactionCatalog.Find(gesture);
                if (fromGesture.Length > 0 && !IsEmotion(fromGesture, cue.Emotion))
                    return fromGesture;
            }

            // A walk or stop cue never reaches the face: Play returns after the movement. So the
            // emotion's own clip is asked for here. It waits for the walk and plays after it.
            if ((gesture == WalkCue || gesture == StopCue) && cue.Emotion != Emotion.Neutral)
            {
                var fromEmotion = AvatarReactionCatalog.Find(cue.Emotion.ToString());
                if (fromEmotion.Length > 0)
                    return fromEmotion;
            }

            var word = AvatarReactionCatalog.Find(cue.EmotionField);
            return word.Length > 0 && !IsPlainEmotion(word) ? word : string.Empty;
        }

        // A plain emotion such as "happy" reaches the 2D avatar through the face, not here,
        // so it is not asked for twice.
        static bool IsPlainEmotion(string word)
        {
            Emotion emotion;
            return EmotionNames.TryParse(word, out emotion) && string.Equals(emotion.ToString(), word, System.StringComparison.OrdinalIgnoreCase);
        }

        static bool IsEmotion(string word, Emotion emotion)
        {
            return string.Equals(emotion.ToString(), word, System.StringComparison.OrdinalIgnoreCase);
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

            if (_animator is IExpressionHint hint)
                hint.ExpressionName = string.Empty;
            _animator.SetEmotion(Emotion.Neutral, 1f);
            _animator.SetTalking(false);
        }

        public void Play(AnimationCue cue)
        {
            if (!CompanionPause.ReactionsAllowed(_suppressed))
                return;

            // A body reaction (2D video avatar) goes to the avatar, then the cue carries on.
            // A walk or stop returns below before the face is set (as it always has), so for
            // those ReactionName asks for the emotion's own clip instead.
            TryRequestReaction(cue);

            // Walk and stop are not catalog clips. Handling them first keeps a walk row
            // with an Animator state from playing an on-the-spot loop instead of moving.
            if (TryHandleMovement(cue))
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

            if (decision.Action == DirectorAction.GestureState && IsFalling())
            {
                Debug.Log(AnimationDirectorLog.Skipped(cue, busy: false, falling: true));
                return;
            }

            var verdict = _rules.Allow(decision, Time.unscaledTime);
            if (verdict != RuleVerdict.Allow)
            {
                LastDecision = decision;
                Debug.Log(AnimationDirectorLog.Skipped(cue, verdict == RuleVerdict.Busy));
                return;
            }

            var played = Apply(decision);
            LastDecision = decision;
            Debug.Log(AnimationDirectorLog.Format(cue, decision, played));
        }

        /// <summary>
        /// walk and stop are requests to the desktop presence rather than Animator states, so
        /// she moves because the model asked, not because a wander timer fired.
        /// </summary>
        bool TryHandleMovement(AnimationCue cue)
        {
            var token = cue.Gesture == null ? string.Empty : cue.Gesture.Trim();
            var direction = cue.Direction;
            AnimationCueParser.NormalizeMovementCue(ref token, ref direction);
            if (token != WalkCue && token != StopCue)
                return false;

            var walk = ResolveWalk();
            if (walk == null)
            {
                Debug.Log("Walk: no IWalkRequest found");
                return true;
            }

            if (token == WalkCue)
            {
                if (direction == 0f)
                    direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                walk.RequestWalk(direction, WalkRequestSeconds);
                Debug.Log("AnimationDirector: cue walk -> requested");
            }
            else
            {
                walk.RequestStop();
                Debug.Log("AnimationDirector: cue stop -> requested");
            }

            return true;
        }

        /// <summary>
        /// The presence is added to the overlay host after the scene loads. In Main that
        /// host is this object. A split setup is found by the interface, and a miss is
        /// not cached, so a presence that appears later still receives the next cue.
        /// </summary>
        IWalkRequest ResolveWalk()
        {
            if (IsAlive(_walk))
                return _walk;

            _walk = null;
            var local = GetComponent<IWalkRequest>();
            if (IsAlive(local))
            {
                _walk = local;
                return _walk;
            }

            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var candidate = behaviours[i] as IWalkRequest;
                if (!IsAlive(candidate))
                    continue;

                _walk = candidate;
                return _walk;
            }

            return null;
        }

        public void PlayNeutral()
        {
            Play(AnimationCueParser.NeutralCue);
        }

        bool Apply(DirectorDecision decision)
        {
            if (IsAlive(_animator))
            {
                // The portrait name has to be in place before SetEmotion logs, so a
                // portrait row whose body emotion is neutral still reports the face.
                if (_animator is IExpressionHint hint)
                    hint.ExpressionName = decision.Portrait;
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
