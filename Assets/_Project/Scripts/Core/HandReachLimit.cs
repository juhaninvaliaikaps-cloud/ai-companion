using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Keeps a dragged hand inside the volume a human arm can actually reach: no stretched arm,
    /// nothing behind the shoulder plane, nothing above the head or below the hips. All values
    /// are in the avatar root's local space, so the limit follows the body however she is turned.
    /// </summary>
    public static class HandReachLimit
    {
        public const float DefaultReachFactor = 0.95f;

        public readonly struct Bounds
        {
            public readonly float MaxReach;
            public readonly float MinY;
            public readonly float MaxY;
            public readonly float MinZ;
            public readonly float MaxSide;

            public Bounds(float maxReach, float minY, float maxY, float minZ, float maxSide)
            {
                MaxReach = maxReach;
                MinY = minY;
                MaxY = maxY;
                MinZ = minZ;
                MaxSide = maxSide;
            }
        }

        public static Bounds ForShoulder(Vector3 shoulderLocal, Vector3 lowerArmLocal, Vector3 handLocal, Vector3 headLocal, Vector3 hipsLocal)
        {
            var reach = Vector3.Distance(shoulderLocal, lowerArmLocal) + Vector3.Distance(lowerArmLocal, handLocal);
            var maxReach = Mathf.Max(0.05f, reach * DefaultReachFactor);
            var minY = Mathf.Min(hipsLocal.y + 0.05f, shoulderLocal.y - 0.35f);
            var maxY = Mathf.Max(shoulderLocal.y + 0.25f, headLocal.y);
            var minZ = shoulderLocal.z - 0.02f;
            return new Bounds(maxReach, minY, maxY, minZ, 0.45f);
        }

        /// <summary>Clamps the requested hand position into the reachable volume.</summary>
        public static Vector3 Clamp(Vector3 handLocal, Vector3 shoulderLocal, Bounds bounds)
        {
            var offset = handLocal - shoulderLocal;
            if (offset.sqrMagnitude > bounds.MaxReach * bounds.MaxReach && offset.sqrMagnitude > 0f)
                offset = offset.normalized * bounds.MaxReach;

            var clamped = shoulderLocal + offset;
            clamped.x = Mathf.Clamp(clamped.x, shoulderLocal.x - bounds.MaxSide, shoulderLocal.x + bounds.MaxSide);
            clamped.y = Mathf.Clamp(clamped.y, bounds.MinY, bounds.MaxY);
            clamped.z = Mathf.Max(clamped.z, bounds.MinZ);

            // The second clamp keeps the reach limit true after the box moved the point back.
            var again = clamped - shoulderLocal;
            if (again.sqrMagnitude > bounds.MaxReach * bounds.MaxReach && again.sqrMagnitude > 0f)
                clamped = shoulderLocal + again.normalized * bounds.MaxReach;

            return clamped;
        }
    }
}
