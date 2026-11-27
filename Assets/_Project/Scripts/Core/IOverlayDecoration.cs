using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>Drawn artwork that should not count as part of the clickable avatar silhouette.</summary>
    public interface IOverlayDecoration
    {
        bool ContainsDecoration(Vector2 unityScreen);
    }
}
