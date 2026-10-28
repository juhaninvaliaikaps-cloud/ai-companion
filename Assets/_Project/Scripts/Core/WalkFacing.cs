namespace AICompanion.Core
{
    /// <summary>
    /// How far she turns toward the walk direction. Kept apart from the avatar so the
    /// value is testable and the 2D avatar can mirror instead of rotate.
    /// </summary>
    public static class WalkFacing
    {
        public const float TurnDegrees = 65f;

        /// <summary>
        /// The extra yaw to apply while walking, relative to whatever yaw the turntable
        /// saved. Walking overrides the saved yaw, so a character the user turned toward
        /// the camera still faces the way she travels; stopping returns to the saved yaw.
        /// </summary>
        public static float Offset(float direction, float baseYaw)
        {
            if (float.IsNaN(direction) || direction == 0f)
                return 0f;
            return Target(direction) - (float.IsNaN(baseYaw) ? 0f : baseYaw);
        }

        public static float Target(float direction)
        {
            if (float.IsNaN(direction))
                return 0f;
            if (direction < -1f)
                direction = -1f;
            if (direction > 1f)
                direction = 1f;
            return direction * TurnDegrees;
        }
    }
}