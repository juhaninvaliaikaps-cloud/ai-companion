using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// World-space renderer bounds the overlay can grab. The avatar registers this
    /// while a model is showing. The capsule collider stays the fallback.
    /// </summary>
    public interface IOverlayHitBounds
    {
        bool RayHits(Ray ray);
    }

    public static class OverlayHitBoundsRegistry
    {
        public static IOverlayHitBounds Current { get; set; }
    }
}
