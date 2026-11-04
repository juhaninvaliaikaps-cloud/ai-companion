using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Decides whether a dragged hand is being pulled sideways past her reach, which reads as
    /// "lead her by the hand". Only a lateral pull counts, so pulling up or towards her does not
    /// start a walk.
    /// </summary>
    public static class HandPull
    {
        public const float StretchFactor = 1.02f;
        public const float LateralShare = 0.6f;

        /// <summary>
        /// -1 to walk left, +1 to walk right, 0 when she is not being led. The stretch factor is
        /// a parameter so a walk can start at 1.02 and continue at a lower 0.85: as she walks
        /// towards the pointer the pull shortens, and without that hysteresis she would take one
        /// step and stop instead of following the hand.
        /// </summary>
        public static int WalkDirection(Vector3 requestedLocal, Vector3 shoulderLocal, float maxReach)
        {
            return WalkDirection(requestedLocal, shoulderLocal, maxReach, StretchFactor);
        }

        public static int WalkDirection(Vector3 requestedLocal, Vector3 shoulderLocal, float maxReach, float stretchFactor)
        {
            if (maxReach <= 0f)
                return 0;

            var offset = requestedLocal - shoulderLocal;
            var distance = offset.magnitude;
            if (distance < maxReach * Mathf.Max(0.1f, stretchFactor))
                return 0;

            if (Mathf.Abs(offset.x) < LateralShare * distance)
                return 0;

            return offset.x < 0f ? -1 : 1;
        }
    }
}
