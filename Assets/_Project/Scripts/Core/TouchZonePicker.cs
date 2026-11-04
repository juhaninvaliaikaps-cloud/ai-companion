using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>One screen-space touch target, projected from a humanoid bone.</summary>
    public struct TouchZoneProbe
    {
        public string Id;
        public Vector2 Center;
        public float RadiusPixels;

        public TouchZoneProbe(string id, Vector2 center, float radiusPixels)
        {
            Id = id;
            Center = center;
            RadiusPixels = radiusPixels;
        }
    }

    /// <summary>
    /// Picks the body zone under the pointer. A pure function of projected bone positions, so
    /// the behaviour is testable without a scene or a camera.
    /// </summary>
    public static class TouchZonePicker
    {
        /// <summary>
        /// The nearest zone whose radius contains the point. Ties go to the first zone, which
        /// keeps the order in the asset meaningful. Returns false when nothing is touched.
        /// </summary>
        public static bool TryPick(IReadOnlyList<TouchZoneProbe> zones, Vector2 point, out string id)
        {
            id = null;
            if (zones == null)
                return false;

            var best = float.MaxValue;
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone.RadiusPixels <= 0f || string.IsNullOrEmpty(zone.Id))
                    continue;

                var distance = Vector2.Distance(zone.Center, point);
                if (distance > zone.RadiusPixels || distance >= best)
                    continue;

                best = distance;
                id = zone.Id;
            }

            return id != null;
        }
    }
}
