namespace AICompanion.AnimationDirector
{
    public enum RuleVerdict
    {
        Allow = 0,
        Busy = 1,
        Cooldown = 2,
    }

    /// <summary>
    /// Keeps the director from repeating itself. A gesture that is still playing cannot be
    /// replaced by one with the same or lower priority, and the same catalog row cannot play
    /// again before its cooldown. Emotion-only decisions always pass. Pure logic, no scene.
    /// </summary>
    public sealed class DirectorRulebook
    {
        public const float DefaultCooldownSeconds = 6f;

        readonly float _cooldown;
        readonly System.Collections.Generic.Dictionary<string, float> _lastPlayed =
            new System.Collections.Generic.Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);

        string _activeState = string.Empty;
        float _activeUntil;
        int _activePriority;

        public DirectorRulebook(float cooldownSeconds = DefaultCooldownSeconds)
        {
            _cooldown = cooldownSeconds < 0f ? 0f : cooldownSeconds;
        }

        public RuleVerdict Allow(DirectorDecision decision, float now)
        {
            if (decision.Action != DirectorAction.GestureState)
                return RuleVerdict.Allow;

            if (_activeState.Length > 0 && now < _activeUntil && decision.Priority <= _activePriority)
                return RuleVerdict.Busy;

            if (decision.EntryId.Length > 0
                && _lastPlayed.TryGetValue(decision.EntryId, out var last)
                && now - last < _cooldown)
            {
                return RuleVerdict.Cooldown;
            }

            _activeState = decision.StateName;
            _activePriority = decision.Priority;
            _activeUntil = now + System.Math.Max(0.2f, decision.DurationSeconds);
            if (decision.EntryId.Length > 0)
                _lastPlayed[decision.EntryId] = now;
            return RuleVerdict.Allow;
        }

        public void Clear()
        {
            _lastPlayed.Clear();
            _activeState = string.Empty;
            _activeUntil = 0f;
            _activePriority = 0;
        }
    }
}
