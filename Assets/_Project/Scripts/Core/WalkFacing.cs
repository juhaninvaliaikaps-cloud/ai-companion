namespace AICompanion.Core
{
    /// <summary>
    /// How far she turns toward the walk direction. Kept apart from the avatar so the
    /// value is testable and the 2D avatar can mirror instead of rotate.
    /// </summary>
    public static class WalkFacing
    {
        public const float TurnDegrees = 65f;

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