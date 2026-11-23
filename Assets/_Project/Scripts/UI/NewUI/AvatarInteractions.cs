using System.Collections.Generic;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using AICompanion.Settings;
using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-101: the 2D interaction menu's actions. Each action plays the avatar's reaction
    /// when one exists, changes the expression, and queues the event line for the model.
    /// There are no body zones or 3D touch dependencies here. The event line is not the
    /// user's speech. An adult line that is still queued when the 18+ gate closes is dropped.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarInteractions : MonoBehaviour
    {
        readonly struct PendingLine
        {
            public readonly string Text;
            public readonly bool Adult;

            public PendingLine(string text, bool adult)
            {
                Text = text;
                Adult = adult;
            }
        }

        readonly Queue<PendingLine> _signals = new Queue<PendingLine>(4);
        ChatPanel _chat;
        AnimationDirector.AnimationDirector _director;
        IExpressionHint _hint;
        IAvatarInteractionTarget _target;
        bool _resolved;
        bool _holding;
        string _heldExpression;
        float _heldUntil;

        void LateUpdate()
        {
            if (string.IsNullOrEmpty(_heldExpression) || Time.unscaledTime >= _heldUntil)
                return;
            if (!_resolved || _hint == null)
                Resolve();
            if (_hint != null)
            {
                // ExpressionHold clears a non-speaking portrait almost immediately. Keep the
                // explicit touch expression alive for the interaction's short beat.
                _hint.ExpressionName = _heldExpression;
            }
        }

        public bool IsHolding
        {
            get { return _holding; }
        }

        void Awake()
        {
            Resolve();
        }

        void Resolve()
        {
            if (_chat == null)
                _chat = GetComponent<ChatPanel>();
            if (_chat == null)
                _chat = FindFirstObjectByType<ChatPanel>();
            if (_director == null)
                _director = GetComponent<AnimationDirector.AnimationDirector>();
            if (_director == null)
                _director = FindFirstObjectByType<AnimationDirector.AnimationDirector>();
            if (_hint == null)
            {
                var allHints = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (var i = 0; i < allHints.Length; i++)
                {
                    var candidate = allHints[i] as IExpressionHint;
                    if (candidate != null)
                    {
                        _hint = candidate;
                        break;
                    }
                }
            }
            if (_target == null)
            {
                var behaviours = GetComponents<MonoBehaviour>();
                for (var i = 0; i < behaviours.Length; i++)
                {
                    var candidate = behaviours[i] as IAvatarInteractionTarget;
                    if (candidate != null)
                    {
                        _target = candidate;
                        break;
                    }
                }

                if (_target == null)
                {
                    var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    for (var i = 0; i < all.Length; i++)
                    {
                        var candidate = all[i] as IAvatarInteractionTarget;
                        if (candidate != null)
                        {
                            _target = candidate;
                            break;
                        }
                    }
                }
            }

            _resolved = true;
        }

        void Update()
        {
            if (_signals.Count == 0)
                return;
            if (!_resolved)
                Resolve();

            while (_signals.Count > 0)
            {
                var next = _signals.Peek();
                var ready = _chat != null && !_chat.TranscriptBusy;
                var choice = InteractionSignalDelivery.Decide(next.Adult, IntimateExpressions.Active, ready);
                if (choice == InteractionSignalDelivery.Choice.Hold)
                    return;
                if (choice == InteractionSignalDelivery.Choice.Drop)
                {
                    _signals.Dequeue();
                    Debug.Log("Interaction: dropped a queued adult line because 18+ is off");
                    continue;
                }

                if (_chat.SubmitInteractionLine(next.Text, next.Adult))
                    _signals.Dequeue();
                return;
            }
        }

        public bool CanRun(string id)
        {
            var item = AvatarInteractionCatalog.Find(id);
            if (item == null)
                return false;
            if (item.Adult && !IntimateExpressions.Active)
                return false;
            if (item.BackOnly && (_target == null || !_target.IsBackView))
                return false;
            if (item.Release)
                return _holding;
            if (item.Hold && _holding)
                return false;
            // Holding one hand does not freeze her. A kiss, a cheek touch or a shoulder
            // touch may happen while the hand stays held; only another hold is refused.
            return true;
        }

        public bool Run(string id)
        {
            AdultClipWindow.Ensure();
            if (!_resolved || _chat == null || _target == null || (_director == null && _hint == null))
            {
                _resolved = false;
                Resolve();
            }
            var item = AvatarInteractionCatalog.Find(id);
            if (item == null || !CanRun(id))
                return false;

            if (item.Hold)
                _holding = true;
            else if (item.Release)
                _holding = false;

            if (_director != null)
            {
                _director.Play(new AnimationCue(
                    item.Emotion,
                    item.Intensity,
                    string.Empty,
                    0f,
                    item.Expression));
            }
            else if (_hint != null)
            {
                // The portrait is driven by ExpressionName; without a director this keeps the
                // 2D face working even if the avatar scene is still starting.
                _hint.ExpressionName = item.Expression;
            }

            // The director may shade the cue by mood. A touch/kiss is an explicit local
            // interaction, so keep its portrait expression at least for this frame.
            if (_hint != null)
                _hint.ExpressionName = item.Expression;

            _heldExpression = item.Expression;
            _heldUntil = Time.unscaledTime + (item.Id == AvatarInteractionCatalog.FingerMouth ? 2.8f : 2.2f);

            Debug.Log("Interaction: " + item.Id + " expression=" + item.Expression
                + " director=" + (_director != null) + " hint=" + (_hint != null) + " target=" + (_target != null));

            if (_target != null)
                _target.TryPlayInteraction(item.Id);
            AdultClipWindow.Play(item.Id);

            if (!string.IsNullOrEmpty(item.Signal))
                _signals.Enqueue(new PendingLine(item.Signal, item.Adult));
            return true;
        }
    }
}
