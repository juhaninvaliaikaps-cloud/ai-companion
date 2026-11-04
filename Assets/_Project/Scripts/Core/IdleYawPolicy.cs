namespace AICompanion.Core
{
    /// <summary>
    /// The single place that decides how far the idle yaw may turn. The desktop supports a
    /// full turntable, so there is no clamp: while she walks, the controller turns her toward
    /// the travel direction for the duration of the walk (JUH-85) and returns to this yaw
    /// when she stops. If an idle limit is ever wanted, this constant is the only value to
    /// change and every caller follows it.
    /// </summary>
    public static class IdleYawPolicy
    {
        /// <summary>360 means a free turntable. A smaller value would clamp the idle yaw.</summary>
        public const float MaxDegrees = 360f;

        public static bool AllowsFullTurn
        {
            get { return MaxDegrees >= 360f; }
        }

        /// <summary>
        /// Normalises the idle yaw into (-180, 180]. With <see cref="MaxDegrees"/> at 360 this
        /// is a wrap, not a clamp, so every direction stays reachable.
        /// </summary>
        public static float Clamp(float yawDegrees)
        {
            return AvatarFacing.WrapDegrees(yawDegrees);
        }
    }
}
