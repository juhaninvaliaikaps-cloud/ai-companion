using System;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-190: discovery-gated ceilings. Emotional discoveries (her public likes/dislikes)
    /// and intimate discoveries (kinks and sensitive spots) raise the maximum affection; kinks
    /// and sensitive spots raise the maximum innocence/perversion. A persona with no relevant
    /// secrets is not permanently locked, so an empty list falls back to 100.
    /// </summary>
    public static class RelationshipCeiling
    {
        public const float AffectionBase = 80f;
        public const float AffectionTop = 100f;
        public const float InnocenceBase = 70f;
        public const float InnocenceTop = 100f;

        /// <summary>
        /// Affection ceiling from all emotional and intimate discoveries. Each discovery adds an
        /// equal share of the 80..100 range, so all of them are needed for 100.
        /// </summary>
        public static float Affection(int foundPublic, int totalPublic, int foundIntimate, int totalIntimate)
        {
            var total = Math.Max(0, totalPublic) + Math.Max(0, totalIntimate);
            if (total <= 0)
                return AffectionTop;
            var found = Math.Min(total, Math.Max(0, foundPublic) + Math.Max(0, foundIntimate));
            return AffectionBase + (AffectionTop - AffectionBase) * found / total;
        }

        /// <summary>
        /// Innocence/perversion ceiling from kinks and sensitive spots. Shy-to-do secrets do not
        /// count. Each discovery adds an equal share of the 70..100 range.
        /// </summary>
        public static float Innocence(int foundIntimate, int totalIntimate)
        {
            var total = Math.Max(0, totalIntimate);
            if (total <= 0)
                return InnocenceTop;
            var found = Math.Min(total, Math.Max(0, foundIntimate));
            return InnocenceBase + (InnocenceTop - InnocenceBase) * found / total;
        }

        /// <summary>
        /// Applies a ceiling to a positive change without clawing back an existing value above
        /// it. Negative changes (next lower than now) always pass through.
        /// </summary>
        public static float ClampAffection(float now, float next, float ceiling)
        {
            if (next <= now)
                return next;
            var cap = Math.Max(0f, ceiling);
            return next > cap ? Math.Max(now, cap) : next;
        }

        /// <summary>Clamps a positive innocence move to the ceiling; a lower existing value is kept.</summary>
        public static float ClampInnocence(float now, float next, float ceiling)
        {
            if (next <= now)
                return next;
            var cap = Math.Max(0f, ceiling);
            return next > cap ? Math.Max(now, cap) : next;
        }
    }
}
