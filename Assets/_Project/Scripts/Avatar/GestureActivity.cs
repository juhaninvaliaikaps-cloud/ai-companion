namespace AICompanion.Avatar
{
    /// <summary>
    /// The Gestures override layer (Walk, Wave, Nod, Shrug, Laugh, Turn180) and the
    /// Walking bool. Procedural sway fades out while either is active so it does not
    /// fight those clips. A missing layer leaves the sway on.
    /// </summary>
    public static class GestureActivity
    {
        public const float FadeSeconds = 0.28f;

        public static readonly string[] StateNames =
        {
            "Wave",
            "Nod",
            "Shrug",
            "Laugh",
            "Walk",
            "Turn180",
            "Fall"
        };

        public static bool IsGestureState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
                return false;

            for (var i = 0; i < StateNames.Length; i++)
            {
                if (string.Equals(stateName, StateNames[i], System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool Suppress(bool hasGestureLayer, string currentState, bool inTransition, string nextState, bool walking)
        {
            if (walking)
                return true;
            if (!hasGestureLayer)
                return false;
            if (IsGestureState(currentState))
                return true;
            return inTransition && IsGestureState(nextState);
        }

        public static float FadeWeight(float current, bool suppress, float deltaTime, float seconds)
        {
            if (float.IsNaN(current))
                current = suppress ? 0f : 1f;
            var target = suppress ? 0f : 1f;
            if (seconds <= 0f || deltaTime >= seconds)
                return target;
            if (deltaTime <= 0f)
                return current < 0f ? 0f : current > 1f ? 1f : current;

            var step = deltaTime / seconds;
            var value = current + (target - current) * step;
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }
}
